namespace GestorRecursos.Core.Models;

/// <summary>Uma linha da seção Veículos: o carro e o que já se sabe dele.</summary>
/// <remarks>
/// A seção existe para responder "de quem é esta placa" (RF-13), que é a pergunta que
/// abriu o projeto: <i>"às vezes volta a placa, mas eu não sei quem é o dono da placa"</i>.
///
/// A busca global já acha o cliente por placa, mas só quando se sabe a placa inteira. O
/// que ela não faz é mostrar o <b>carro</b> como coisa própria: um veículo tem um dono e
/// pode ter vários condutores indicados, e acumula multas de épocas diferentes, ligadas a
/// clientes diferentes. A ficha de cada cliente mostra só o pedaço dele.
/// </remarks>
public class VeiculoDaLista
{
    public int Id { get; set; }
    public string Placa { get; set; } = string.Empty;
    public string? Renavam { get; set; }
    public string? MarcaModelo { get; set; }
    public string? Cor { get; set; }
    public string? Ano { get; set; }
    public string? Observacao { get; set; }

    /// <summary>O dono. Nulo quando o carro não é de nenhum cliente do escritório.</summary>
    public int? ProprietarioId { get; set; }
    public string? ProprietarioNome { get; set; }

    /// <summary>Quantos clientes aparecem como condutor indicado deste carro.</summary>
    public int Condutores { get; set; }

    public int Multas { get; set; }

    /// <summary>Recursos deste carro que ainda andam.</summary>
    public int RecursosAbertos { get; set; }

    /// <summary>Ninguém ligado ao carro, nem dono nem condutor.</summary>
    /// <remarks>
    /// Acontece pelo cadastro rápido de dentro da tela de multa (RF-16b): a placa entra
    /// junto com o auto e o vínculo fica para depois — e depois ninguém lembra. Sem esta
    /// lista, o carro só reaparece quando a multa dele volta do DETRAN.
    /// </remarks>
    public bool SemVinculo => ProprietarioId is null && Condutores == 0;

    /// <summary>Falta algum dado que o texto do recurso usa (RF-52).</summary>
    /// <remarks>
    /// Marca/modelo, cor, ano e RENAVAM não fazem falta para cadastrar a multa, mas o
    /// recurso descreve o veículo com eles. Hoje a falta só aparece na hora de escrever a
    /// peça, que é tarde: aqui ela aparece antes, quando ainda dá para pedir ao cliente.
    /// </remarks>
    public bool FaltaDadoDoRecurso =>
        string.IsNullOrWhiteSpace(MarcaModelo)
        || string.IsNullOrWhiteSpace(Cor)
        || string.IsNullOrWhiteSpace(Ano)
        || string.IsNullOrWhiteSpace(Renavam);
}

/// <summary>Um cliente ligado ao veículo, e em que papel.</summary>
public class DonoDoVeiculo
{
    /// <summary>ID do vínculo, e não do cliente: é por ele que se desfaz a ligação.</summary>
    public int VinculoId { get; set; }

    public int ClienteId { get; set; }
    public string Nome { get; set; } = string.Empty;
    public string Cpf { get; set; } = string.Empty;
    public string? Whatsapp { get; set; }

    /// <summary>PROPRIETARIO ou CONDUTOR.</summary>
    public string Vinculo { get; set; } = string.Empty;

    public bool EhProprietario => Vinculo == "PROPRIETARIO";
}

/// <summary>Uma multa do carro, com o cliente que contratou o recurso, se houver.</summary>
public class MultaDoVeiculo
{
    public int Id { get; set; }
    public string Ait { get; set; } = string.Empty;
    public string? Descricao { get; set; }
    public string? Enquadramento { get; set; }
    public DateTime? DataHoraInfracao { get; set; }
    public string? Gravidade { get; set; }
    public int? Pontos { get; set; }
    public decimal? Valor { get; set; }
    public DateTime? PrazoDefesa { get; set; }

    /// <summary>Quem contratou o recurso desta multa. Nulo quando nunca virou serviço.</summary>
    public int? ClienteId { get; set; }
    public string? ClienteNome { get; set; }

    // Da instância mais recente: o recurso anda de AUTUACAO para JARI e para CETRAN.
    public string? Fase { get; set; }
    public string? Situacao { get; set; }
    public DateTime? PrazoProtocolo { get; set; }

    /// <summary>Sem recurso, o prazo que vale é o da defesa; com recurso, o do protocolo.</summary>
    public DateTime? PrazoQueVale => PrazoProtocolo ?? PrazoDefesa;

    public bool TemRecurso => Fase is not null;
}

/// <summary>O histórico do carro: quem está ligado a ele e o que ele já tomou.</summary>
public class HistoricoDoVeiculo
{
    public IReadOnlyList<DonoDoVeiculo> Donos { get; set; } = [];
    public IReadOnlyList<MultaDoVeiculo> Multas { get; set; } = [];
}
