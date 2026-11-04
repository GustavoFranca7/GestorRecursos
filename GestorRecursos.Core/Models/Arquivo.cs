namespace GestorRecursos.Core.Models;

// Um documento do cliente — a procuração assinada, a CNH, o comprovante de protocolo.
//
// Aqui estão os dados do anexo, não o anexo: o PDF em si fica na coluna CONTEUDO da
// tabela e só é lido quando alguém manda abrir. É o que permite listar os anexos da
// ficha sem trazer megabytes de documento pela rede.
public class Arquivo
{
    public int Id { get; set; }
    public int ClienteId { get; set; }

    /// <summary>A fase a que o documento pertence. Nulo quando é do cliente, não do recurso.</summary>
    /// <remarks>
    /// A procuração e a CNH são do cliente e servem a todos os recursos dele. Já o
    /// comprovante de protocolo é de uma fase específica — vale para a JARI, não para
    /// a defesa prévia que veio antes.
    /// </remarks>
    public int? InstanciaId { get; set; }

    public TipoArquivo Tipo { get; set; }

    /// <summary>O nome que o arquivo tinha quando foi escolhido. É o que aparece na tela.</summary>
    public string NomeArquivo { get; set; } = string.Empty;

    /// <summary>O tamanho do documento, em bytes.</summary>
    /// <remarks>
    /// Guardado em coluna própria, fora do BLOB: a lista mostra o tamanho sem abrir o
    /// documento, e o backup semanal do texto — que não leva BLOB — ainda registra o
    /// que ficou de fora.
    /// </remarks>
    public int Tamanho { get; set; }

    public DateTime DataUpload { get; set; }

    /// <summary>O tamanho em bytes, KB ou MB, para a tela.</summary>
    /// <remarks>
    /// Abaixo de 1 KB vai em bytes: arredondar para "0 KB" faria um documento existente
    /// parecer vazio.
    /// </remarks>
    public string TamanhoLegivel => Tamanho switch
    {
        < 1024 => $"{Tamanho} bytes",
        < 1024 * 1024 => $"{Tamanho / 1024.0:N0} KB",
        _ => $"{Tamanho / 1024.0 / 1024.0:N1} MB"
    };
}
