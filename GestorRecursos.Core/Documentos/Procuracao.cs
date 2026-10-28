using System.Globalization;
using System.Text;
using GestorRecursos.Core.Models;
using GestorRecursos.Core.Validacao;

namespace GestorRecursos.Core.Documentos;

/// <summary>Em que parte do cadastro mora um dado que falta.</summary>
/// <remarks>
/// Serve para a tela levar direto ao grupo certo. É um conceito da Core — "onde este
/// dado é digitado" —, e não o nome da aba: quem exibe faz a tradução.
/// </remarks>
public enum ParteDoCadastro
{
    DadosPessoais,
    ContatoEndereco,
    Cnh,
    Escritorio
}

public sealed record CampoFaltante(string Nome, ParteDoCadastro Onde);

/// <summary>A procuração para defesa em auto de infração (RF-44).</summary>
/// <remarks>
/// O texto segue o modelo real do escritório, em
/// <c>Prints\PrintsEu\exemplos\Procuração-exemplo.pdf</c>. É documento jurídico: a
/// redação não se melhora por conta própria — só se trocam os dados.
///
/// A única licença tomada é de concordância. O modelo foi digitado para uma cliente e
/// mistura os gêneros no mesmo parágrafo ("inscrita no CPF... portador do RG"). Como o
/// cadastro não guarda sexo, as flexões levam "(a)" — forma confirmada com ela em 21/10,
/// e a usual em documento de modelo. As qualificações ("brasileira", "solteira") continuam
/// vindo do cadastro como foram digitadas.
/// </remarks>
public static class Procuracao
{
    private const string Titulo = "PROCURAÇÃO PARA DEFESA EM AUTO DE INFRAÇÃO";

    // Os poderes outorgados. Parágrafo fixo, copiado do modelo.
    private const string Poderes =
        "Através do presente instrumento particular de mandato, o OUTORGANTE nomeia e " +
        "constitui como seu procurador o OUTORGADO para realizar a defesa de seus " +
        "interesses junto ao órgão competente pelo auto de infração lavrado contra o " +
        "OUTORGANTE. Para tanto, concede ao OUTORGADO poderes para movimentar os autos e " +
        "realizar todas as atividades necessárias ao desenrolar do processo, como " +
        "apresentar defesas, interpor recursos e requerer vista, juntar e retirar " +
        "documentos além de efetuar depósitos de taxas, pagamento de impostos, e levantar " +
        "quantias, sendo-lhe permitido, ainda, assinar documentos e substabelecer.";

    /// <summary>O que falta preencher antes de gerar. Lista vazia significa pronto.</summary>
    /// <remarks>
    /// Documento com lacuna não serve: o cliente assina o que está escrito. Vale mais
    /// dizer o que falta do que entregar um papel com espaços em branco.
    /// </remarks>
    public static IReadOnlyList<CampoFaltante> Faltando(Cliente cliente, Escritorio? escritorio)
    {
        var faltas = new List<CampoFaltante>();

        void Exigir(string? valor, string nome, ParteDoCadastro onde)
        {
            if (Vazio(valor)) faltas.Add(new CampoFaltante(nome, onde));
        }

        Exigir(cliente.Nome, "nome", ParteDoCadastro.DadosPessoais);
        Exigir(cliente.Cpf, "CPF", ParteDoCadastro.DadosPessoais);
        Exigir(cliente.Nacionalidade, "nacionalidade", ParteDoCadastro.DadosPessoais);
        Exigir(cliente.EstadoCivil, "estado civil", ParteDoCadastro.DadosPessoais);
        Exigir(cliente.Profissao, "profissão", ParteDoCadastro.DadosPessoais);
        Exigir(cliente.Rg, "RG", ParteDoCadastro.DadosPessoais);
        Exigir(cliente.Email, "e-mail", ParteDoCadastro.ContatoEndereco);
        Exigir(cliente.CnhNumero, "nº de registro da CNH", ParteDoCadastro.Cnh);

        // RF-05: sem endereço não há procuração nem protocolo.
        if (cliente.Endereco is null || Vazio(cliente.Endereco.Logradouro))
        {
            faltas.Add(new CampoFaltante("endereço", ParteDoCadastro.ContatoEndereco));
        }

        if (escritorio is null)
        {
            faltas.Add(new CampoFaltante(
                "dados do escritório — nenhum registro no banco", ParteDoCadastro.Escritorio));
        }
        else if (Vazio(escritorio.Nome))
        {
            faltas.Add(new CampoFaltante("nome do escritório", ParteDoCadastro.Escritorio));
        }

        return faltas;
    }

    public static Documento Montar(Cliente cliente, Escritorio escritorio, DateTime quando)
    {
        var trechos = new List<TrechoDocumento>
        {
            new(Titulo, EstiloTrecho.Titulo),
            new(Outorgante(cliente), EstiloTrecho.Paragrafo),
            new(Outorgado(escritorio), EstiloTrecho.Paragrafo),
            new(Poderes, EstiloTrecho.Paragrafo),
            new(Rodape(escritorio, quando), EstiloTrecho.Centralizado),
            new(cliente.Nome.ToUpperInvariant(), EstiloTrecho.Assinatura)
        };

        return new Documento
        {
            Titulo = Titulo,
            NomeArquivo = $"Procuracao - {SemAcento(cliente.Nome)}",
            Trechos = trechos
        };
    }

    // "OUTORGANTE: FULANO, brasileira, solteira, entregadora, inscrita no CPF..."
    private static string Outorgante(Cliente cliente)
    {
        var texto = new StringBuilder("OUTORGANTE: ");
        texto.Append(cliente.Nome.ToUpperInvariant());
        Acrescentar(texto, cliente.Nacionalidade);
        Acrescentar(texto, cliente.EstadoCivil);
        Acrescentar(texto, cliente.Profissao);
        texto.Append($", inscrito(a) no CPF sob o nº {Cpf(cliente.Cpf)}");

        var rg = string.Join(" ", new[] { cliente.Rg, cliente.RgOrgao }
            .Where(p => !Vazio(p)));
        if (!Vazio(rg)) texto.Append($", portador(a) do RG nº {rg}");

        if (!Vazio(cliente.CnhNumero)) texto.Append($", CNH nº {cliente.CnhNumero}");
        if (!Vazio(cliente.Email)) texto.Append($", e-mail: {cliente.Email}");

        if (cliente.Endereco is { } endereco)
        {
            texto.Append($", residente e domiciliado(a) na {endereco.Resumo()}");
        }

        return texto.Append('.').ToString();
    }

    private static string Outorgado(Escritorio escritorio)
    {
        var texto = new StringBuilder("OUTORGADO: ");
        texto.Append(escritorio.Nome.ToUpperInvariant());
        Acrescentar(texto, escritorio.Nacionalidade);
        Acrescentar(texto, escritorio.EstadoCivil);
        Acrescentar(texto, escritorio.Profissao);

        if (!Vazio(escritorio.Rg)) texto.Append($", Carteira de Identidade nº {escritorio.Rg}");
        if (!Vazio(escritorio.Cpf)) texto.Append($", inscrito(a) no CPF nº {Cpf(escritorio.Cpf)}");
        if (!Vazio(escritorio.Email)) texto.Append($", endereço eletrônico {escritorio.Email}");

        if (!Vazio(escritorio.Logradouro))
        {
            texto.Append($", residente e domiciliado(a) na {escritorio.Logradouro}");
            if (!Vazio(escritorio.Numero)) texto.Append($", nº {escritorio.Numero}");
            if (!Vazio(escritorio.Bairro)) texto.Append($", bairro {escritorio.Bairro}");
            if (!Vazio(escritorio.Cep)) texto.Append($", Cep {Cep(escritorio.Cep)}");
            if (!Vazio(escritorio.Cidade)) texto.Append($", Cidade {escritorio.Cidade}");

            var estado = Estados.PorExtenso(escritorio.Uf);
            if (estado is not null) texto.Append($", no Estado de {estado}");
        }

        return texto.ToString();
    }

    private static string Rodape(Escritorio escritorio, DateTime quando)
    {
        var cidade = Vazio(escritorio.CidadeAssinatura) ? escritorio.Cidade : escritorio.CidadeAssinatura;
        var mes = new CultureInfo("pt-BR").DateTimeFormat.GetMonthName(quando.Month);
        return $"{cidade}, {quando.Day} de {char.ToUpperInvariant(mes[0])}{mes[1..]} de {quando.Year}.";
    }

    // As qualificações entram só quando preenchidas: vírgula seguida de vazio vira rasura.
    private static void Acrescentar(StringBuilder texto, string? parte)
    {
        if (!Vazio(parte)) texto.Append($", {parte}");
    }

    private static bool Vazio(string? texto) => string.IsNullOrWhiteSpace(texto);

    // O banco guarda só dígitos; o documento é lido por pessoa, e leva a pontuação.
    // Mora aqui, e não no Formato do Desktop, porque a Core não conhece a tela.
    private static string Cpf(string? cpf)
    {
        var digitos = Normalizacao.SomenteDigitos(cpf);
        return digitos.Length == 11
            ? $"{digitos[..3]}.{digitos[3..6]}.{digitos[6..9]}-{digitos[9..]}"
            : cpf ?? string.Empty;
    }

    private static string Cep(string? cep)
    {
        var digitos = Normalizacao.SomenteDigitos(cep);
        return digitos.Length == 8 ? $"{digitos[..5]}-{digitos[5..]}" : cep ?? string.Empty;
    }

    // Acento em nome de arquivo atravessa mal o Windows e o e-mail.
    private static string SemAcento(string texto)
    {
        var decomposto = texto.Normalize(NormalizationForm.FormD);
        var limpo = decomposto.Where(c =>
            CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark);
        return new string(limpo.ToArray()).Normalize(NormalizationForm.FormC);
    }
}
