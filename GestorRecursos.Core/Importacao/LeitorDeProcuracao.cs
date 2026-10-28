using System.Text.RegularExpressions;
using GestorRecursos.Core.Documentos;
using GestorRecursos.Core.Integracoes;
using GestorRecursos.Core.Models;
using GestorRecursos.Core.Validacao;

namespace GestorRecursos.Core.Importacao;

/// <summary>Tira os dados do cliente de uma procuração já emitida.</summary>
/// <remarks>
/// As procurações antigas foram digitadas uma a uma no Word, e nenhuma é igual à outra:
/// o "nº" some, o CEP vem sem hífen, o órgão do RG às vezes é só a UF, a palavra "bairro"
/// aparece ou não. Por isso cada campo é procurado pelo formato, com folga na redação,
/// e não por posição fixa.
///
/// A parte delicada é outra. A procuração qualifica DUAS pessoas na mesma forma: o
/// cliente e a despachante que assina. Quem procurar "o CPF do documento" tem chance real
/// de levar o dela. A separação é estrutural — o texto é cortado em "OUTORGADO:" e só a
/// primeira metade é lida —, e o confronto com a tabela ESCRITORIO vem depois, como
/// alarme: se um identificador dela escapou para cá, o corte falhou e o documento é
/// recusado, em vez de entrar errado.
/// </remarks>
public static class LeitorDeProcuracao
{
    /// <summary>Lê um arquivo. Nunca lança: o que deu errado vira situação e aviso.</summary>
    public static async Task<ProcuracaoLida> LerAsync(
        string caminho,
        Escritorio escritorio,
        IConsultaCep? consultaCep = null,
        CancellationToken cancelamento = default)
    {
        var arquivo = Path.GetFileName(caminho);

        var lido = TextoDeDocumento.Ler(caminho);
        if (lido.Texto is not { } texto)
        {
            return ProcuracaoLida.Recusar(arquivo, lido.Motivo ?? "Não foi possível ler o arquivo.");
        }

        var inicio = Regex.Match(texto, @"OUTORGANTE\s*:");
        var corte = Regex.Match(texto, @"OUTORGADO\s*:");

        if (!inicio.Success)
        {
            return ProcuracaoLida.Recusar(arquivo,
                "Não é uma procuração no modelo do escritório: falta \"OUTORGANTE:\".");
        }

        if (!corte.Success || corte.Index <= inicio.Index)
        {
            return ProcuracaoLida.Recusar(arquivo,
                "Falta o marcador \"OUTORGADO:\". Sem ele não dá para separar os dados "
                + "do cliente dos do escritório, e a leitura não é confiável.");
        }

        // Daqui em diante, só o trecho do cliente.
        var parte = texto[inicio.Index..corte.Index];

        var avisos = new List<string>();
        var cliente = new Cliente();

        LerIdentificacao(parte, cliente);
        LerDocumentos(parte, cliente);

        if (string.IsNullOrWhiteSpace(cliente.Nome))
        {
            return ProcuracaoLida.Recusar(arquivo, "Não encontrei o nome do cliente.");
        }

        if (string.IsNullOrWhiteSpace(cliente.Cpf))
        {
            return ProcuracaoLida.Recusar(arquivo, "Não encontrei o CPF do cliente.");
        }

        // O alarme. Depois do corte, nada aqui deveria ser do escritório.
        if (Invadiu(cliente, escritorio) is { } invadido)
        {
            return ProcuracaoLida.Recusar(arquivo,
                $"O {invadido} lido é o do escritório. O documento foge do modelo e a "
                + "separação falhou — importar criaria um cadastro com dados trocados.");
        }

        if (!Normalizacao.CpfValido(cliente.Cpf))
        {
            avisos.Add("O CPF não passa na conta do dígito verificador. "
                       + "Confira no documento antes de importar.");
        }

        var enderecoBruto = TrechoDoEndereco(parte);
        cliente.Endereco = await MontarEnderecoAsync(enderecoBruto, consultaCep, cancelamento);

        if (cliente.Endereco is null)
        {
            avisos.Add(enderecoBruto is null
                ? "O documento não traz endereço."
                : "Não deu para montar o endereço com segurança. Ele fica registrado nas "
                  + "observações e pode ser preenchido na ficha.");
        }

        // Fica tudo guardado: a origem e a linha como estava escrita.
        cliente.Observacoes = Observacao(arquivo, enderecoBruto);

        foreach (var falta in Faltas(cliente)) avisos.Add(falta);

        return new ProcuracaoLida
        {
            Arquivo = arquivo,
            Cliente = cliente,
            EnderecoNoDocumento = enderecoBruto,
            Situacao = avisos.Count == 0 ? SituacaoLeitura.Pronto : SituacaoLeitura.Conferir,
            Avisos = avisos
        };
    }

    // ------------------------------------------------------------- leitura

    /// <summary>Nome e as qualificações que vêm logo depois dele.</summary>
    private static void LerIdentificacao(string parte, Cliente cliente)
    {
        var nome = Regex.Match(parte, @"OUTORGANTE\s*:\s*([^,]+)");
        if (nome.Success) cliente.Nome = Limpar(nome.Groups[1].Value);

        // Entre o nome e o "inscrito no CPF" ficam nacionalidade, estado civil e
        // profissão. A quantidade varia, então o que sobra vai todo para a profissão.
        var meio = Regex.Match(parte,
            @"OUTORGANTE\s*:\s*[^,]+,(.*?),\s*inscrit[oa]\b", RegexOptions.Singleline);

        if (!meio.Success) return;

        var qualificacoes = meio.Groups[1].Value
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(q => q.Length > 0)
            .ToList();

        if (qualificacoes.Count > 0) cliente.Nacionalidade = Limpar(qualificacoes[0]);
        if (qualificacoes.Count > 1) cliente.EstadoCivil = Limpar(qualificacoes[1]);
        if (qualificacoes.Count > 2) cliente.Profissao = Limpar(string.Join(", ", qualificacoes.Skip(2)));
    }

    /// <summary>CPF, RG com órgão, número da CNH e e-mail.</summary>
    private static void LerDocumentos(string parte, Cliente cliente)
    {
        var cpf = Regex.Match(parte, @"CPF[^0-9]{0,30}(\d{3}\D?\d{3}\D?\d{3}\D?\d{2})");
        if (cpf.Success) cliente.Cpf = Normalizacao.SomenteDigitos(cpf.Groups[1].Value);

        // O órgão vem solto depois do número e muda de documento para documento:
        // "SSP RS", "SESP SC", "ssp rs" ou só "AL".
        var rg = Regex.Match(parte, @"RG[^0-9]{0,20}([\d.\-]{5,15}?)\s*([A-Za-z/ ]{1,15})?(?=,|\.|$)");
        if (rg.Success)
        {
            cliente.Rg = rg.Groups[1].Value.Trim(' ', '.', '-');
            var orgao = Limpar(rg.Groups[2].Value);
            if (orgao.Length > 0) cliente.RgOrgao = orgao.ToUpperInvariant();
        }

        // Ora "CNH nº 123", ora "CNH 123"; e há procuração sem CNH alguma.
        var cnh = Regex.Match(parte, @"CNH[^0-9]{0,15}(\d{9,11})");
        if (cnh.Success) cliente.CnhNumero = cnh.Groups[1].Value;

        var email = Regex.Match(parte, @"([A-Za-z0-9._%+\-]+@[A-Za-z0-9.\-]+\.[A-Za-z]{2,})");
        if (email.Success) cliente.Email = email.Groups[1].Value.ToLowerInvariant();
    }

    /// <summary>A linha de endereço, do "residente e domiciliado" até o fim do trecho.</summary>
    private static string? TrechoDoEndereco(string parte)
    {
        var achado = Regex.Match(parte, @"domiciliad[oa]\s*,?\s*(.+)$",
            RegexOptions.Singleline | RegexOptions.IgnoreCase);

        if (!achado.Success) return null;

        var linha = Limpar(achado.Groups[1].Value).TrimEnd('.', ',', ';');

        // "na", "no" e "em" são preposição, não parte do logradouro.
        linha = Regex.Replace(linha, @"^(na|no|em)\s+", string.Empty, RegexOptions.IgnoreCase);

        return linha.Length > 0 ? linha : null;
    }

    // ------------------------------------------------------------ endereço

    /// <summary>Monta o endereço do texto, completando pelo CEP quando dá.</summary>
    /// <remarks>
    /// Logradouro, cidade e estado são obrigatórios no banco. Sem os três, o cliente entra
    /// sem endereço e a linha original fica nas observações — melhor um campo vazio, que a
    /// tela cobra, do que um endereço inventado que ninguém mais questiona.
    /// </remarks>
    private static async Task<Endereco?> MontarEnderecoAsync(
        string? linha, IConsultaCep? consultaCep, CancellationToken cancelamento)
    {
        if (linha is null) return null;

        var cep = Regex.Match(linha, @"CEP\s*:?\s*(\d{5}\s*-?\s*\d{3})", RegexOptions.IgnoreCase);
        var endereco = new Endereco
        {
            Cep = cep.Success ? Normalizacao.SomenteDigitos(cep.Groups[1].Value) : null
        };

        // Tudo que vem antes da primeira vírgula, ou antes do "nº", é o logradouro.
        var cabeca = Regex.Match(linha, @"^(.*?)(?=,|\bn[ºo°]\b|$)", RegexOptions.IgnoreCase);
        var logradouro = Limpar(cabeca.Groups[1].Value);

        // "rua RUA 430" e "rua Rua 454": o tipo da via aparece duas vezes.
        logradouro = Regex.Replace(logradouro,
            @"^(rua|avenida|av\.?|travessa|rodovia|estrada)\s+(?=(rua|avenida|av\.?|travessa|rodovia|estrada)\b)",
            string.Empty, RegexOptions.IgnoreCase);

        endereco.Logradouro = Maiuscula(logradouro);

        var numero = Regex.Match(linha, @"\bn[ºo°]\s*([\dA-Za-z\-]{1,10})", RegexOptions.IgnoreCase);
        if (numero.Success)
        {
            endereco.Numero = numero.Groups[1].Value.Trim();
        }
        else
        {
            // Sem "nº": o número costuma abrir o trecho seguinte ao logradouro.
            var solto = Regex.Match(linha, @",\s*(\d{1,6})\b");
            if (solto.Success) endereco.Numero = solto.Groups[1].Value;
        }

        // Nem sempre há vírgula separando o bairro do CEP: "bairro morretes CEP 88220-000".
        var bairro = Regex.Match(linha, @"\bbairro\s+(.+?)(?=\s*(?:,|\.|\bCEP\b|$))", RegexOptions.IgnoreCase);
        if (bairro.Success) endereco.Bairro = Maiuscula(bairro.Groups[1].Value);

        LerCidadeEstado(linha, endereco);

        // O CEP sabe mais que o texto digitado à mão: o que ele trouxer prevalece.
        if (consultaCep is not null && endereco.Cep?.Length == 8)
        {
            var consulta = await consultaCep.BuscarAsync(endereco.Cep, cancelamento);
            if (consulta is { Situacao: SituacaoCep.Encontrado, Endereco: { } achado })
            {
                // CEP de cidade inteira ("...-000") volta sem rua nem bairro: aí o
                // que foi lido do documento continua valendo.
                if (!string.IsNullOrWhiteSpace(achado.Logradouro)) endereco.Logradouro = achado.Logradouro;
                if (!string.IsNullOrWhiteSpace(achado.Bairro)) endereco.Bairro = achado.Bairro;
                if (!string.IsNullOrWhiteSpace(achado.Cidade)) endereco.Cidade = achado.Cidade;
                if (!string.IsNullOrWhiteSpace(achado.Uf)) endereco.Estado = achado.Uf;
            }
        }

        var completo = !string.IsNullOrWhiteSpace(endereco.Logradouro)
                       && !string.IsNullOrWhiteSpace(endereco.Cidade)
                       && Normalizacao.UfValida(endereco.Estado);

        return completo ? endereco : null;
    }

    /// <summary>Cidade e estado do texto: ora "Itapema - SC", ora "Itapema, Santa Catarina".</summary>
    private static void LerCidadeEstado(string linha, Endereco endereco)
    {
        var pedacos = linha.Split([',', '-'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(p => Regex.Replace(p, @"CEP\s*:?\s*[\d\s-]*", string.Empty, RegexOptions.IgnoreCase).Trim())
            .Where(p => p.Length > 0)
            .ToList();

        for (var i = pedacos.Count - 1; i >= 0; i--)
        {
            var pedaco = pedacos[i];

            // Por extenso ("Santa Catarina") ou pela sigla ("SC").
            var sigla = Estados.Sigla(pedaco);
            if (sigla is null && pedaco.Length == 2 && Normalizacao.UfValida(Normalizacao.Uf(pedaco)))
            {
                sigla = Normalizacao.Uf(pedaco);
            }

            if (sigla is null) continue;

            endereco.Estado = sigla;

            // A cidade é o pedaço logo antes do estado.
            if (i > 0 && !Regex.IsMatch(pedacos[i - 1], @"^\d"))
            {
                endereco.Cidade = Maiuscula(pedacos[i - 1]);
            }

            return;
        }
    }

    // -------------------------------------------------------------- alarme

    /// <summary>Qual identificador do escritório vazou para o trecho do cliente.</summary>
    /// <remarks>
    /// Só identificadores únicos entram aqui. Rua, bairro, CEP, cidade, nacionalidade e
    /// estado civil ficam de fora de propósito: cliente pode legitimamente dividir todos
    /// esses com o escritório — há cliente morando na mesma rua —, e descartar por
    /// coincidência apagaria dado verdadeiro sem ninguém notar.
    /// </remarks>
    private static string? Invadiu(Cliente cliente, Escritorio escritorio)
    {
        if (Digitos(cliente.Cpf) is { Length: 11 } cpf && cpf == Digitos(escritorio.Cpf)) return "CPF";
        if (Digitos(cliente.Rg) is { Length: > 3 } rg && rg == Digitos(escritorio.Rg)) return "RG";

        if (!string.IsNullOrWhiteSpace(cliente.Email)
            && string.Equals(cliente.Email, escritorio.Email?.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            return "e-mail";
        }

        if (!string.IsNullOrWhiteSpace(escritorio.Nome)
            && string.Equals(cliente.Nome.Trim(), escritorio.Nome.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            return "nome";
        }

        return null;
    }

    // --------------------------------------------------------------- apoio

    /// <summary>O que a procuração exige e não veio no documento.</summary>
    private static IEnumerable<string> Faltas(Cliente cliente)
    {
        if (string.IsNullOrWhiteSpace(cliente.Rg)) yield return "Sem RG no documento.";
        if (string.IsNullOrWhiteSpace(cliente.Email)) yield return "Sem e-mail no documento.";
        if (string.IsNullOrWhiteSpace(cliente.CnhNumero)) yield return "Sem nº de registro da CNH no documento.";
        if (string.IsNullOrWhiteSpace(cliente.Profissao)) yield return "Sem profissão no documento.";
    }

    private static string Observacao(string arquivo, string? enderecoBruto)
    {
        var texto = $"Cadastro importado da procuração \"{arquivo}\" em {DateTime.Today:dd/MM/yyyy}.";

        if (enderecoBruto is not null)
        {
            texto += $" Endereço como estava no documento: {enderecoBruto}.";
        }

        // O WhatsApp é o único campo do cadastro que procuração nenhuma traz.
        return texto + " Falta o WhatsApp, que não consta em procuração.";
    }

    private static string Digitos(string? texto) => Normalizacao.SomenteDigitos(texto);

    private static string Limpar(string? texto) =>
        Regex.Replace(texto ?? string.Empty, @"\s+", " ").Trim();

    /// <summary>"BAIRRO MORRETES" e "morretes" viram "Morretes".</summary>
    /// <remarks>
    /// O documento mistura caixa alta, baixa e title case no mesmo endereço. Uniformizar
    /// na entrada evita que a mesma rua apareça de três jeitos na busca.
    /// </remarks>
    private static string Maiuscula(string? texto)
    {
        var limpo = Limpar(texto);
        if (limpo.Length == 0) return string.Empty;

        // "BAIRRO MORRETES" carrega o rótulo junto; ele não é nome de bairro.
        limpo = Regex.Replace(limpo, @"^bairro\s+", string.Empty, RegexOptions.IgnoreCase);

        var minusculas = new[] { "de", "da", "do", "das", "dos", "e" };

        var palavras = limpo.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select((palavra, posicao) =>
            {
                var baixa = palavra.ToLowerInvariant();

                if (posicao > 0 && minusculas.Contains(baixa)) return baixa;

                // Deixa em paz o que não começa com letra, como "0622-A".
                return char.IsLetter(baixa[0])
                    ? char.ToUpperInvariant(baixa[0]) + baixa[1..]
                    : palavra;
            });

        return string.Join(' ', palavras);
    }
}
