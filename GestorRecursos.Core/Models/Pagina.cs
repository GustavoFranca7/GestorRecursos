namespace GestorRecursos.Core.Models;

/// <summary>Uma fatia de resultados, com o total para montar a navegação.</summary>
/// <remarks>
/// O total viaja junto porque quem exibe precisa dele para saber quantas páginas
/// existem, e buscá-lo numa segunda chamada custaria outra ida ao banco.
/// </remarks>
public class Pagina<T>
{
    public int NumeroPagina { get; init; } = 1;
    public int TamanhoPagina { get; init; } = 20;
    public int TotalRegistros { get; init; }
    public List<T> Itens { get; init; } = [];

    public int TotalPaginas =>
        TamanhoPagina <= 0 ? 0 : (int)Math.Ceiling(TotalRegistros / (double)TamanhoPagina);

    public bool TemAnterior => NumeroPagina > 1;
    public bool TemProxima => NumeroPagina < TotalPaginas;

    public static Pagina<T> Vazia(int numeroPagina = 1, int tamanhoPagina = 20) =>
        new() { NumeroPagina = numeroPagina, TamanhoPagina = tamanhoPagina };
}
