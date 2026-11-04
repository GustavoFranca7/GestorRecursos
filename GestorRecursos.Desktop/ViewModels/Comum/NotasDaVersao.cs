using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace GestorRecursos.Desktop.ViewModels;

/// <summary>O que mudou numa versão, como ela lê.</summary>
public sealed record NotaDaVersao(string Versao, string Data, string Resumo, string[] Mudancas);

/// <summary>O histórico de versões que a tela de Configurações mostra.</summary>
/// <remarks>
/// Escrito à mão e no código, de propósito. Não sai do Git nem do banco: o texto do commit
/// é para quem programa, e o que ela precisa saber é outra coisa — "o que mudou na minha
/// tela". Ficar no código garante que a nota chega junto com a versão que ela descreve.
///
/// <b>Ao lançar uma versão:</b> acrescente a entrada nova no topo, em português corrente,
/// dizendo o que mudou para quem usa — não o que mudou no código. O registro completo, com
/// decisões e horas, continua na documentação.
/// </remarks>
public static class NotasDaVersao
{
    public static IReadOnlyList<NotaDaVersao> Todas { get; } =
    [
        new("1.2.0", "04/11/2026",
            "Documentos do cliente guardados no sistema, e backup automático toda semana.",
            [
                "A aba Documentos da ficha passa a guardar arquivos: procuração assinada, CNH, CRLV, comprovantes. Aceita PDF e Word, até 10 MB por arquivo.",
                "Cada documento pode ser baixado para o computador ou aberto direto no leitor.",
                "O sistema passou a fazer cópia de segurança sozinho, todo domingo, com os documentos junto.",
                "Em Configurações, um botão para gravar todos os documentos em disco, uma pasta por cliente.",
                "Avisos de confirmação agora aparecem no canto da tela e somem sozinhos.",
                "Correção: no painel, a aba \"Até 7 dias\" mostrava junto os prazos já vencidos."
            ]),

        new("1.1.1", "28/10/2026",
            "Importação de cadastros pelas procurações antigas, e atualização sem fechar o programa.",
            [
                "Cadastro de cliente a partir das procurações já emitidas: o sistema lê o documento e preenche a ficha.",
                "Resultado \"Anulado\" no andamento de um recurso, que abre a fase seguinte.",
                "O instalador passou a atualizar no lugar, mesmo com o programa aberto.",
                "No cadastro, um campo de CNH a menos, e o que ficou diz o que é."
            ])
    ];

    /// <summary>A versão que está rodando, como "1.2.0".</summary>
    /// <remarks>
    /// Lida do próprio executável, e não escrita aqui: duas fontes para o mesmo número
    /// divergem na primeira vez que alguém esquecer de mexer numa delas.
    /// </remarks>
    public static string VersaoAtual
    {
        get
        {
            var versao = Assembly.GetEntryAssembly()?.GetName().Version;

            return versao is null
                ? Todas.First().Versao
                : $"{versao.Major}.{versao.Minor}.{versao.Build}";
        }
    }
}
