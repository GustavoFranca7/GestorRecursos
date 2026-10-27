using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Configuration;

namespace GestorRecursos.Core.Integracoes;

public enum SituacaoEnvio
{
    Enviado,

    /// <summary>Falta a seção Email na configuração: ninguém configurou o remetente.</summary>
    SemConfiguracao,

    /// <summary>O servidor recusou ou não respondeu.</summary>
    Falhou
}

public sealed record ResultadoEnvio(SituacaoEnvio Situacao, string? Detalhe = null);

/// <summary>Manda o código de recuperação para o e-mail do usuário.</summary>
/// <remarks>
/// Usa <see cref="SmtpClient"/> do próprio .NET: uma mensagem curta por mês não justifica
/// dependência nova.
///
/// A conta remetente e a senha vêm da configuração, nunca do código — mesma regra da
/// connection string. No Gmail, a senha é uma "senha de app", e não a da conta.
/// </remarks>
public class EnvioDeEmail
{
    private static IConfiguration Configuracao => Configuracoes.Atual;

    public async Task<ResultadoEnvio> EnviarCodigoAsync(
        string destino,
        string codigo,
        int minutosDeValidade,
        CancellationToken cancelamento = default)
    {
        var servidor = Configuracao["Email:Servidor"];
        var remetente = Configuracao["Email:Usuario"];
        var senha = Configuracao["Email:Senha"];

        if (string.IsNullOrWhiteSpace(servidor)
            || string.IsNullOrWhiteSpace(remetente)
            || string.IsNullOrWhiteSpace(senha))
        {
            return new ResultadoEnvio(SituacaoEnvio.SemConfiguracao);
        }

        var porta = int.TryParse(Configuracao["Email:Porta"], out var p) ? p : 587;
        var nomeExibido = Configuracao["Email:Nome"] ?? "Gestor de Recursos";

        try
        {
            using var cliente = new SmtpClient(servidor, porta)
            {
                EnableSsl = true,
                Credentials = new NetworkCredential(remetente, senha)
            };

            using var mensagem = new MailMessage
            {
                From = new MailAddress(remetente, nomeExibido),
                Subject = "Código para trocar a senha",
                Body = Corpo(codigo, minutosDeValidade),
                IsBodyHtml = false
            };
            mensagem.To.Add(destino);

            await cliente.SendMailAsync(mensagem, cancelamento);
            return new ResultadoEnvio(SituacaoEnvio.Enviado);
        }
        catch (Exception excecao)
        {
            return new ResultadoEnvio(SituacaoEnvio.Falhou, Motivo(excecao));
        }
    }

    // O SmtpException diz só "Failure sending mail". O que explica — recusa do servidor,
    // credencial errada, rede fora — está na exceção de dentro.
    private static string Motivo(Exception excecao)
    {
        var partes = new List<string>();

        for (var atual = excecao; atual is not null; atual = atual.InnerException)
        {
            partes.Add(atual.Message);
        }

        return string.Join(" — ", partes.Distinct());
    }

    // Texto puro, curto e sem link: o código é digitado no próprio programa, então não
    // há nada para clicar — e e-mail de recuperação sem link nenhum é mais difícil de
    // imitar numa tentativa de golpe.
    private static string Corpo(string codigo, int minutos) =>
        $"""
         Seu código para trocar a senha do Gestor de Recursos:

             {codigo}

         Ele vale por {minutos} minutos e serve uma vez só.
         Digite-o na própria tela do programa, em "Esqueci a senha".

         Se não foi você que pediu, ignore este e-mail: a senha atual continua valendo.
         """;
}
