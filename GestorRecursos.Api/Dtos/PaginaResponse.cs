using GestorRecursos.Core.Models;

// Camada Api / Dtos: envelope de paginacao e resultado de busca.
namespace GestorRecursos.Api.Dtos
{
    // Quem chama precisa saber o total para montar a navegacao, entao a lista
    // viaja acompanhada dos numeros da pagina.
    public class PaginaResponse<T>
    {
        public int Pagina { get; set; }
        public int TamanhoPagina { get; set; }
        public int TotalRegistros { get; set; }
        public int TotalPaginas { get; set; }
        public List<T> Itens { get; set; } = [];
    }

    public class ResultadoBuscaResponse
    {
        /// <summary>Onde o termo foi encontrado: CLIENTE, VEICULO, MULTA, PROCESSO ou PROTOCOLO.</summary>
        public string Origem { get; set; } = "";

        public string Encontrado { get; set; } = "";
        public int ClienteId { get; set; }
        public string ClienteNome { get; set; } = "";
        public string ClienteCpf { get; set; } = "";
        public string? ClienteWhatsapp { get; set; }
        public string? Detalhe { get; set; }
        public bool ClienteArquivado { get; set; }

        public static ResultadoBuscaResponse DoModelo(ResultadoBusca resultado) => new()
        {
            Origem = resultado.Origem,
            Encontrado = resultado.Encontrado,
            ClienteId = resultado.ClienteId,
            ClienteNome = resultado.ClienteNome,
            ClienteCpf = resultado.ClienteCpf,
            ClienteWhatsapp = resultado.ClienteWhatsapp,
            Detalhe = resultado.Detalhe,
            ClienteArquivado = resultado.ClienteArquivado
        };
    }
}
