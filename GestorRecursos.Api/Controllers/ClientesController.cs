using GestorRecursos.Api.Dtos;
using GestorRecursos.Core.Data;
using Microsoft.AspNetCore.Mvc;

// Camada Api / Controllers: traduz HTTP para chamadas da Core. Sem regra de negocio aqui.
namespace GestorRecursos.Api.Controllers
{
    [ApiController]
    [Route("api/clientes")]
    public class ClientesController : ControllerBase
    {
        private readonly ClienteDAO _clienteDao;
        private readonly BuscaDAO _buscaDao;

        // Os DAOs chegam prontos pelo construtor (injecao de dependencia), e nao com
        // "new" aqui dentro: e isso que permite trocar a dependencia nos testes.
        public ClientesController(ClienteDAO clienteDao, BuscaDAO buscaDao)
        {
            _clienteDao = clienteDao;
            _buscaDao = buscaDao;
        }

        /// <summary>Lista os clientes de forma paginada.</summary>
        /// <param name="pagina">Numero da pagina, comecando em 1.</param>
        /// <param name="tamanho">Quantidade de registros por pagina (maximo 100).</param>
        /// <param name="incluirArquivados">Inclui na lista quem foi arquivado.</param>
        [HttpGet]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<PaginaResponse<ClienteResponse>>> Listar(
            [FromQuery] int pagina = 1,
            [FromQuery] int tamanho = 20,
            [FromQuery] bool incluirArquivados = false,
            CancellationToken cancelamento = default)
        {
            // Limite no tamanho para uma chamada nao conseguir pedir a tabela inteira.
            if (pagina < 1) return BadRequest(new { erro = "A pagina deve ser maior que zero." });
            if (tamanho is < 1 or > 100) return BadRequest(new { erro = "O tamanho deve estar entre 1 e 100." });

            var resultado = await _clienteDao.ListarPaginaAsync(pagina, tamanho, incluirArquivados, cancelamento);

            return Ok(new PaginaResponse<ClienteResponse>
            {
                Pagina = resultado.NumeroPagina,
                TamanhoPagina = resultado.TamanhoPagina,
                TotalRegistros = resultado.TotalRegistros,
                TotalPaginas = resultado.TotalPaginas,
                Itens = resultado.Itens.Select(ClienteResponse.DoModelo).ToList()
            });
        }

        /// <summary>Busca um cliente pelo seu id, com endereco e veiculos.</summary>
        [HttpGet("{id:int}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<ClienteResponse>> BuscarPorId(int id, CancellationToken cancelamento = default)
        {
            var cliente = await _clienteDao.BuscarPorIdAsync(id, cancelamento);

            // Id inexistente nao e erro do servidor: e 404, e quem chamou trata.
            if (cliente is null) return NotFound(new { erro = $"Cliente {id} nao encontrado." });

            return Ok(ClienteResponse.DoModelo(cliente));
        }

        /// <summary>Procura por nome, CPF, placa, AIT, numero de processo ou protocolo.</summary>
        /// <remarks>
        /// Resolve a maior dor do escritorio: chega uma atualizacao com a placa ou o
        /// numero do processo, e e preciso descobrir de qual cliente se trata.
        /// </remarks>
        [HttpGet("busca")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<List<ResultadoBuscaResponse>>> Buscar(
            [FromQuery] string termo,
            CancellationToken cancelamento = default)
        {
            if (string.IsNullOrWhiteSpace(termo) || termo.Trim().Length < 3)
            {
                return BadRequest(new { erro = "Informe ao menos 3 caracteres para a busca." });
            }

            var resultados = await _buscaDao.ProcurarAsync(termo, cancelamento: cancelamento);
            return Ok(resultados.Select(ResultadoBuscaResponse.DoModelo).ToList());
        }
    }
}
