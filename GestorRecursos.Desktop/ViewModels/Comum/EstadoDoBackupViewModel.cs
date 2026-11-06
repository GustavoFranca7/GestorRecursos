using System.Threading.Tasks;
using GestorRecursos.Core.Data;
using GestorRecursos.Core.Models;

namespace GestorRecursos.Desktop.ViewModels;

/// <summary>Como está a cópia de segurança: quando rodou e se já passou da hora.</summary>
/// <remarks>
/// O mesmo objeto serve ao painel, que avisa quando atrasa, e às Configurações, que
/// mostram o estado mesmo quando está em dia. Os dois textos ficam aqui juntos porque
/// é a mesma leitura: separá-los faria duas telas discordarem sobre a mesma data.
///
/// Nenhuma execução registrada NÃO é tratada como atraso. A tabela nasce vazia e só
/// recebe a primeira linha no domingo seguinte: avisar antes disso seria aviso falso no
/// dia da instalação, e aviso falso é o que ensina a ignorar o aviso de verdade.
/// </remarks>
public class EstadoDoBackupViewModel(ExecucaoDoBackup? ultimo)
{
    public ExecucaoDoBackup? Ultimo { get; } = ultimo;

    /// <summary>Há registro de alguma execução.</summary>
    public bool Conhecido => Ultimo is not null;

    /// <summary>Passou dos dias combinados sem rodar — é o que o painel mostra.</summary>
    public bool Atrasado => Ultimo is { } u && u.DiasAtras > BackupDAO.DiasParaAvisar;

    /// <summary>Rodou, mas deixou documento para trás.</summary>
    public bool Incompleto => Ultimo is { Falhas: > 0 };

    public string Titulo => Ultimo is { } u
        ? $"A cópia de segurança não roda há {u.DiasAtras} dias"
        : "Sem registro de cópia de segurança";

    /// <summary>O corpo do aviso do painel: o que está em risco, e desde quando.</summary>
    public string Texto => Ultimo is { } u
        ? $"O último backup registrado é de {Formato.Data(u.Quando)}. A cópia é semanal e o "
          + "banco não tem restauração: o que foi cadastrado desde essa data existe só na "
          + "nuvem, em uma cópia única. Verifique a tarefa agendada no computador que roda o backup."
        : "Nenhuma execução foi registrada ainda.";

    /// <summary>Uma linha, para o cartão das Configurações.</summary>
    public string Resumo => Ultimo is { } u
        ? $"{Formato.Data(u.Quando)} às {u.Quando:HH:mm}, em {u.Maquina} · "
          + $"{u.Linhas:N0} linhas · {HaQuantoTempo(u.DiasAtras)}"
        : "Nenhuma execução registrada ainda. A primeira entra na próxima vez que o backup rodar.";

    // Nomes da paleta, como nos selos da lista de acessos: a tela resolve o pincel.
    public string CorSelo => this switch
    {
        { Atrasado: true } => "Alerta",
        { Incompleto: true } => "Aviso",
        { Conhecido: true } => "Sucesso",
        _ => "TextoApagado"
    };

    // O cartão inteiro muda de cor quando atrasa: o selo sozinho, do tamanho que é,
    // passa batido em uma tela que já tem cartão demais.
    public string FundoCartao => Atrasado ? "AlertaClaro" : "Cartao";
    public string BordaCartao => Atrasado ? "AlertaBorda" : "Borda";

    public string FundoSelo => this switch
    {
        // Branco, e não vermelho claro: o cartão atrasado já é vermelho claro, e o selo
        // da mesma cor do fundo desaparece.
        { Atrasado: true } => "Cartao",
        { Incompleto: true } => "AvisoClaro",
        { Conhecido: true } => "SucessoClaro",
        _ => "Fundo"
    };

    public string Selo => this switch
    {
        { Atrasado: true } => "atrasado",
        { Incompleto: true } => "incompleto",
        { Conhecido: true } => "em dia",
        _ => "sem registro"
    };

    /// <summary>Lê o estado do banco. Devolve "sem registro" quando não dá para saber.</summary>
    /// <remarks>
    /// Erro aqui não é propagado: a tabela BACKUPS pode não existir ainda (o script
    /// 12-RegistroDoBackup.sql é rodado à mão), e o painel inteiro não pode ficar em
    /// branco por causa de um aviso acessório.
    /// </remarks>
    public static async Task<EstadoDoBackupViewModel> LerAsync()
    {
        try
        {
            return new EstadoDoBackupViewModel(await new BackupDAO().UltimoAsync());
        }
        catch
        {
            return new EstadoDoBackupViewModel(null);
        }
    }

    private static string HaQuantoTempo(int dias) => dias switch
    {
        0 => "hoje",
        1 => "ontem",
        _ => $"há {dias} dias"
    };
}
