using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace GestorRecursos.Core.Documentos;

/// <summary>Põe um <see cref="Documento"/> no papel.</summary>
/// <remarks>
/// Só desenha: o texto vem montado de quem sabe a regra (<see cref="Procuracao"/>). A
/// separação existe para o documento ser conferido na tela antes de virar arquivo, sem
/// gerar PDF a cada vez.
/// </remarks>
public static class DocumentoPdf
{
    // Fonte com serifa, como nos modelos do escritório. Se não existir na máquina, o
    // QuestPDF cai na fonte padrão em vez de falhar.
    private const string Fonte = "Times New Roman";

    static DocumentoPdf()
    {
        // Licença Community do QuestPDF: gratuita para empresa com faturamento abaixo
        // de US$ 1 milhão. Tem de ser declarada antes do primeiro documento.
        QuestPDF.Settings.License = LicenseType.Community;

        // Desde a versão 2026.9 o QuestPDF ignora as fontes do sistema e aborta quando a
        // família não existe. Sem isto, só a Lato embutida está disponível.
        QuestPDF.Settings.UseSystemFonts = true;
    }

    public static byte[] Gerar(Documento documento) => Montar(documento).GeneratePdf();

    /// <summary>A primeira página como PNG, para a prévia na tela.</summary>
    /// <remarks>
    /// 110 DPI: legível na tela sem pesar. A impressão não passa por aqui — sai do PDF,
    /// que é vetorial.
    /// </remarks>
    public static byte[] GerarImagem(Documento documento) =>
        Montar(documento).GenerateImages(new ImageGenerationSettings
        {
            ImageFormat = ImageFormat.Png,
            RasterDpi = 110
        }).First();

    public static void Gravar(Documento documento, string caminho) =>
        Montar(documento).GeneratePdf(caminho);

    private static IDocument Montar(Documento documento) => Document.Create(container =>
    {
        container.Page(pagina =>
        {
            pagina.Size(PageSizes.A4);
            pagina.Margin(2.5f, Unit.Centimetre);
            pagina.DefaultTextStyle(estilo => estilo.FontFamily(Fonte).FontSize(12));

            pagina.Content().Column(coluna =>
            {
                coluna.Spacing(14);

                foreach (var trecho in documento.Trechos)
                {
                    Desenhar(coluna, trecho);
                }
            });
        });
    });

    private static void Desenhar(ColumnDescriptor coluna, TrechoDocumento trecho)
    {
        switch (trecho.Estilo)
        {
            case EstiloTrecho.Titulo:
                coluna.Item().PaddingBottom(10).Text(trecho.Texto)
                      .FontSize(14).Bold().AlignCenter();
                break;

            case EstiloTrecho.Centralizado:
                coluna.Item().PaddingTop(20).Text(trecho.Texto).AlignCenter();
                break;

            case EstiloTrecho.Assinatura:
                // A linha e o nome andam juntos: assinar sobre o nada não identifica ninguém.
                coluna.Item().PaddingTop(36).AlignCenter().Width(320).Column(assinatura =>
                {
                    assinatura.Item().LineHorizontal(0.8f);
                    assinatura.Item().PaddingTop(4).Text(trecho.Texto)
                              .FontSize(11).AlignCenter();
                });
                break;

            default:
                // Justificado com entrelinha solta: é como os modelos dela são escritos,
                // e sobra espaço para rubricar à margem.
                coluna.Item().Text(texto =>
                {
                    texto.Justify();
                    texto.DefaultTextStyle(estilo => estilo.LineHeight(1.5f));
                    texto.Span(trecho.Texto);
                });
                break;
        }
    }
}
