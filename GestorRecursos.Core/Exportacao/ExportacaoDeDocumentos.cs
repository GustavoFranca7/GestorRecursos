using GestorRecursos.Core.Models;

namespace GestorRecursos.Core.Exportacao;

/// <summary>Um anexo na fila da exportação, sem o documento em si.</summary>
public sealed record DocumentoParaExportar(
    int Id,
    int ClienteId,
    string ClienteNome,
    TipoArquivo Tipo,
    string NomeArquivo,
    int Tamanho);

/// <summary>Em que ponto a exportação está, para a barra de progresso.</summary>
public sealed record AndamentoDaExportacao(int Feitos, int Total, string Atual);

/// <summary>O que a exportação gravou e o que não conseguiu.</summary>
public sealed record ResultadoDaExportacao(
    int Gravados,
    long Bytes,
    List<string> Falhas,
    bool Cancelada);

/// <summary>O que a atualização semanal do espelho fez.</summary>
/// <remarks>
/// <paramref name="Sumiram"/> não é falha: é o anexo que foi apagado da ficha entre a
/// leitura da lista e a gravação. O documento continua no espelho, da semana passada.
/// </remarks>
public sealed record ResultadoDoEspelho(
    int Novos,
    int JaEstavam,
    int Sumiram,
    long Bytes,
    List<string> Falhas);
