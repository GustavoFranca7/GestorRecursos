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
        new("1.3.0", "17/11/2026",
            "Três seções novas no menu: Processos, Financeiro e Agenda.",
            [
                "Processos: todos os recursos de todos os clientes numa lista só, com o nome de quem é cada um. Busca por nome, CPF, AIT ou placa, e filtros por situação, fase, tipo de serviço, órgão e data, numa barra que abre no ícone de funil.",
                "Financeiro: o caixa do mês mostra quanto entrou, quanto saiu em despesas, o líquido e por qual forma o dinheiro veio. O título do mês abre um calendário para escolher mês e ano.",
                "Financeiro: a aba Cobrança lista o que está em aberto, de quem e há quanto tempo. O pagamento pode ser lançado ali mesmo, sem abrir a ficha.",
                "Despesas: correio, taxa de DETRAN, autenticação. Entram no caixa do mês e descontam do resultado da contratação em que foram lançadas.",
                "Saldo do cliente: valor devolvido fica registrado, aparece no resumo da ficha e pode ser usado para pagar — tanto ao contratar um serviço novo quanto num pagamento avulso. Quando sobra troco, o resto continua como saldo.",
                "Agenda: o mês inteiro em calendário, com os prazos de protocolo e os compromissos marcados. O dia com prazo vencendo ganha uma faixa vermelha.",
                "Agenda: o dia escolhido abre ao lado, com o plano do dia — quantos recursos do dia já foram protocolados —, os compromissos e o diário.",
                "Agenda: a mesma informação também em lista, agrupada por dia, com busca e filtro por tipo.",
                "Agenda: compromisso que se repete toda semana, a cada quinze dias ou todo mês é marcado de uma vez, até a data escolhida. Excluir pergunta se é só o do dia ou daí em diante.",
                "Agenda: o recurso já julgado continua no calendário, riscado, dizendo como terminou — deferido, indeferido ou encerrado."
            ]),

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
