namespace GestorRecursos.Core.Models;

/// <summary>Um processo do escritório, com o cliente junto, para a tela de Processos.</summary>
/// <remarks>
/// É o mesmo <see cref="ServicoDaFicha"/> que a aba Serviços usa, de propósito: um serviço
/// contratado com suas fases e etapas tem a mesma forma, seja visto a partir do cliente ou
/// do escritório inteiro. Assim a regra de "em que pé está esta fase" é uma só.
///
/// O que muda é a pergunta. Na ficha, ela é "o que este cliente contratou". Aqui é "o que
/// está correndo no escritório" — e por isso o cliente deixa de ser o contexto e passa a
/// ser mais uma coluna, que se busca e se lê na linha.
/// </remarks>
public class Processo
{
    public ServicoDaFicha Servico { get; set; } = new();

    public int ClienteId { get; set; }
    public string ClienteNome { get; set; } = string.Empty;
    public string ClienteCpf { get; set; } = string.Empty;
    public string ClienteWhatsapp { get; set; } = string.Empty;
}
