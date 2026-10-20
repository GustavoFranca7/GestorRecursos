namespace GestorRecursos.Desktop.ViewModels;

/// <summary>Como os valores fechados do banco aparecem escritos na tela.</summary>
/// <remarks>
/// O banco guarda AUTUACAO, GRAVISSIMA, AGUARDANDO_PROTOCOLO_JARI. A tela mostra isso em
/// português de gente. A tradução mora aqui porque estava escrita em três lugares, e a
/// mesma fase corria o risco de virar "autuação" numa tela e "Autuação" em outra —
/// a mesma armadilha que já tinha pegado a cor dos prazos (ver <see cref="Prazos"/>).
/// </remarks>
public static class Rotulos
{
    /// <summary>A instância do recurso: autuação, JARI ou CETRAN.</summary>
    public static string Fase(string? fase) => fase switch
    {
        "AUTUACAO" => "autuação",
        "JARI" => "JARI",
        "CETRAN" => "CETRAN",
        _ => fase ?? string.Empty
    };

    /// <summary>Em que pé está a instância.</summary>
    public static string Situacao(string? situacao) => situacao switch
    {
        "CONTRATADO" => "contratado",
        "AGUARDANDO_DOCUMENTO" => "aguardando documento",
        "AGUARDANDO_PROTOCOLO_JARI" => "aguardando protocolo na JARI",
        "AGUARDANDO_PROTOCOLO_CETRAN" => "aguardando protocolo no CETRAN",
        "PROTOCOLADO" => "protocolado",
        "AGUARDANDO_JULGAMENTO" => "aguardando julgamento",
        "DEFERIDO" => "deferido",
        "INDEFERIDO" => "indeferido",

        // O banco guarda CANCELADO, mas a ação na tela chama-se "Encerrar serviço".
        // Dois nomes para o mesmo estado confundem quem lê o selo depois.
        "CANCELADO" => "encerrado",

        "DESISTENCIA" => "desistência",
        "PRAZO_PERDIDO" => "prazo perdido",
        _ => (situacao ?? string.Empty).ToLowerInvariant().Replace('_', ' ')
    };

    /// <summary>Situação de quem não anda mais: ganhou, perdeu ou saiu do caminho.</summary>
    /// <remarks>
    /// Serve para não gritar prazo em caso resolvido: um recurso deferido com data de
    /// protocolo vencida não é urgência nenhuma, é trabalho concluído.
    /// </remarks>
    public static bool Encerrada(string? situacao) =>
        situacao is "DEFERIDO" or "INDEFERIDO" or "CANCELADO" or "DESISTENCIA";

    /// <summary>Como o dinheiro entrou, já com as parcelas quando elas importam.</summary>
    public static string Forma(Core.Models.FormaPagamento forma, int parcelas)
    {
        var nome = forma switch
        {
            Core.Models.FormaPagamento.Dinheiro => "dinheiro",
            Core.Models.FormaPagamento.Pix => "PIX",
            Core.Models.FormaPagamento.PixParcelado => "PIX parcelado",
            Core.Models.FormaPagamento.Debito => "débito",
            Core.Models.FormaPagamento.Credito => "crédito",
            _ => forma.ToString().ToLowerInvariant()
        };

        // Uma parcela não é parcelamento: dizer "em 1x" só polui a linha.
        return parcelas > 1 ? $"{nome} em {parcelas}x" : nome;
    }

    /// <summary>O papel do cliente no veículo.</summary>
    public static string Vinculo(string? vinculo) => vinculo switch
    {
        "PROPRIETARIO" => "proprietário",
        "CONDUTOR" => "condutor indicado",
        _ => (vinculo ?? string.Empty).ToLowerInvariant()
    };

    /// <summary>Gravidade da infração, com os pontos que ela custa na carteira.</summary>
    public static string Gravidade(string? gravidade, int? pontos)
    {
        var nome = gravidade switch
        {
            "LEVE" => "leve",
            "MEDIA" => "média",
            "GRAVE" => "grave",
            "GRAVISSIMA" => "gravíssima",
            _ => (gravidade ?? string.Empty).ToLowerInvariant()
        };

        if (nome.Length == 0)
        {
            return string.Empty;
        }

        return pontos is > 0 ? $"{nome} · {pontos} pts" : nome;
    }

    /// <summary>Cores do selo de gravidade: a escala vai do verde ao vermelho.</summary>
    public static (string Fundo, string Texto) CoresDaGravidade(string? gravidade) => gravidade switch
    {
        "LEVE" => ("SucessoClaro", "Sucesso"),
        "MEDIA" => ("AvisoClaro", "Aviso"),
        "GRAVE" => ("AlertaClaro", "Alerta"),
        "GRAVISSIMA" => ("AlertaClaro", "AlertaEscuro"),
        _ => ("Fundo", "TextoFraco")
    };
}
