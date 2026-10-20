using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using GestorRecursos.Core.Validacao;

namespace GestorRecursos.Core.Integracoes;

/// <summary>O endereço que um CEP devolve. Número e complemento são de quem digita.</summary>
public sealed record EnderecoDoCep(string Logradouro, string Bairro, string Cidade, string Uf);

/// <summary>Em que pé terminou a consulta.</summary>
public enum SituacaoCep
{
    Encontrado,

    /// <summary>O serviço respondeu que não conhece este CEP.</summary>
    NaoEncontrado,

    /// <summary>Não deu para perguntar: sem internet, serviço fora do ar ou demorou demais.</summary>
    Indisponivel
}

public sealed record ResultadoCep(SituacaoCep Situacao, EnderecoDoCep? Endereco = null);

/// <summary>Busca o endereço de um CEP.</summary>
/// <remarks>
/// É interface porque a tela não deve depender de quem responde: trocar de provedor, ou pôr
/// um dublê no lugar para testar sem internet, não muda uma linha do formulário.
/// </remarks>
public interface IConsultaCep
{
    Task<ResultadoCep> BuscarAsync(string? cep, CancellationToken cancelamento = default);
}

/// <summary>Consulta pelo ViaCEP, que é gratuito e não pede credencial.</summary>
public sealed class ViaCepConsulta : IConsultaCep
{
    // Um HttpClient só para a aplicação toda: criar um por consulta esgota as portas do
    // sistema operacional, porque a conexão fica reservada depois do Dispose.
    private static readonly HttpClient Cliente = new()
    {
        BaseAddress = new Uri("https://viacep.com.br/ws/"),

        // Preencher endereço é conforto, não obrigação: se demorar mais que isto, a pessoa
        // digita na mão em menos tempo do que esperaria.
        Timeout = TimeSpan.FromSeconds(6)
    };

    public async Task<ResultadoCep> BuscarAsync(string? cep, CancellationToken cancelamento = default)
    {
        var digitos = Normalizacao.SomenteDigitos(cep);

        // CEP incompleto não chega a virar chamada: o ViaCEP responderia 400 de qualquer jeito.
        if (digitos.Length != 8)
        {
            return new ResultadoCep(SituacaoCep.NaoEncontrado);
        }

        try
        {
            var resposta = await Cliente.GetAsync($"{digitos}/json/", cancelamento);

            if (!resposta.IsSuccessStatusCode)
            {
                // 400 é o que o ViaCEP devolve para CEP mal formado; o resto é problema dele.
                return new ResultadoCep(resposta.StatusCode == System.Net.HttpStatusCode.BadRequest
                    ? SituacaoCep.NaoEncontrado
                    : SituacaoCep.Indisponivel);
            }

            var corpo = await resposta.Content.ReadFromJsonAsync<RespostaViaCep>(cancelamento);

            if (corpo is null || corpo.NaoExiste || string.IsNullOrWhiteSpace(corpo.Localidade))
            {
                return new ResultadoCep(SituacaoCep.NaoEncontrado);
            }

            var endereco = new EnderecoDoCep(
                corpo.Logradouro?.Trim() ?? string.Empty,
                corpo.Bairro?.Trim() ?? string.Empty,
                corpo.Localidade.Trim(),
                Normalizacao.Uf(corpo.Uf));

            return new ResultadoCep(SituacaoCep.Encontrado, endereco);
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException or JsonException)
        {
            // Sem internet, serviço fora do ar ou resposta estranha: o cadastro segue na mão.
            // Cancelamento pedido por quem chamou também cai aqui, e o efeito é o mesmo.
            return new ResultadoCep(SituacaoCep.Indisponivel);
        }
    }

    /// <summary>O JSON do ViaCEP, só com os campos que o cadastro usa.</summary>
    private sealed class RespostaViaCep
    {
        [JsonPropertyName("logradouro")] public string? Logradouro { get; set; }
        [JsonPropertyName("bairro")] public string? Bairro { get; set; }
        [JsonPropertyName("localidade")] public string? Localidade { get; set; }
        [JsonPropertyName("uf")] public string? Uf { get; set; }

        // CEP inexistente volta como {"erro": ...}, e o ViaCEP já mandou isso como texto
        // ("true") e como booleano em versões diferentes. JsonElement aceita os dois.
        [JsonPropertyName("erro")] public JsonElement Erro { get; set; }

        public bool NaoExiste => Erro.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.String => bool.TryParse(Erro.GetString(), out var erro) && erro,
            _ => false
        };
    }
}
