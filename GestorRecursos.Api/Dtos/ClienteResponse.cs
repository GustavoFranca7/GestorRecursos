using GestorRecursos.Core.Models;

// Camada Api / Dtos: o formato dos dados que entram e saem pela rede.
namespace GestorRecursos.Api.Dtos
{
    // Existe separado do modelo Cliente para que mudancas internas no banco
    // nao quebrem quem ja consome a API.
    public class ClienteResponse
    {
        public int Id { get; set; }
        public string Nome { get; set; } = "";
        public string Cpf { get; set; } = "";
        public string? Email { get; set; }
        public string Whatsapp { get; set; } = "";
        public string? CnhNumero { get; set; }
        public string? CnhCategoria { get; set; }
        public DateTime? CnhValidade { get; set; }
        public bool Arquivado { get; set; }
        public DateTime DataCadastro { get; set; }
        public EnderecoResponse? Endereco { get; set; }
        public List<VeiculoResponse> Veiculos { get; set; } = [];

        // Traduz a entidade da Core para o formato de saida.
        public static ClienteResponse DoModelo(Cliente cliente) => new()
        {
            Id = cliente.Id,
            Nome = cliente.Nome,
            Cpf = cliente.Cpf,
            Email = cliente.Email,
            Whatsapp = cliente.Whatsapp,
            CnhNumero = cliente.CnhNumero,
            CnhCategoria = cliente.CnhCategoria,
            CnhValidade = cliente.CnhValidade,
            Arquivado = cliente.Arquivado,
            DataCadastro = cliente.DataCadastro,
            Endereco = cliente.Endereco is null ? null : EnderecoResponse.DoModelo(cliente.Endereco),
            Veiculos = cliente.Veiculos.Select(VeiculoResponse.DoModelo).ToList()
        };
    }

    public class EnderecoResponse
    {
        public int Id { get; set; }
        public string? Cep { get; set; }
        public string Logradouro { get; set; } = "";
        public string? Numero { get; set; }
        public string? Complemento { get; set; }
        public string? Bairro { get; set; }
        public string Cidade { get; set; } = "";
        public string Estado { get; set; } = "";

        public static EnderecoResponse DoModelo(Endereco endereco) => new()
        {
            Id = endereco.Id,
            Cep = endereco.Cep,
            Logradouro = endereco.Logradouro,
            Numero = endereco.Numero,
            Complemento = endereco.Complemento,
            Bairro = endereco.Bairro,
            Cidade = endereco.Cidade,
            Estado = endereco.Estado
        };
    }

    public class VeiculoResponse
    {
        public int Id { get; set; }
        public string Placa { get; set; } = "";
        public string? Renavam { get; set; }
        public string? MarcaModelo { get; set; }
        public string Vinculo { get; set; } = "";

        public static VeiculoResponse DoModelo(VeiculoDoCliente vinculo) => new()
        {
            Id = vinculo.Veiculo.Id,
            Placa = vinculo.Veiculo.Placa,
            Renavam = vinculo.Veiculo.Renavam,
            MarcaModelo = vinculo.Veiculo.MarcaModelo,
            Vinculo = vinculo.Vinculo.ToString()
        };
    }
}
