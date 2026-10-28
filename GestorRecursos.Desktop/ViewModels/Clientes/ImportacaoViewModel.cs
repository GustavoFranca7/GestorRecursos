using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GestorRecursos.Core.Data;
using GestorRecursos.Core.Importacao;
using GestorRecursos.Core.Integracoes;
using GestorRecursos.Core.Models;
using GestorRecursos.Core.Validacao;
using GestorRecursos.Desktop.Servicos;

namespace GestorRecursos.Desktop.ViewModels;

/// <summary>Um documento lido, já como rascunho de cadastro que se pode corrigir.</summary>
/// <remarks>
/// Os campos são editáveis aqui, e não só depois de gravar, por dois motivos: a conferência
/// acontece com o documento à vista, e o cliente corrigido na hora não precisa ser
/// reencontrado numa lista de dezenas depois.
///
/// A situação se recalcula a cada tecla — preencher o que faltava muda o selo de "conferir"
/// para "pronto" sem precisar reler a pasta.
/// </remarks>
public partial class LinhaImportacaoViewModel : ObservableObject
{
    private readonly Action _aoMudar;

    public LinhaImportacaoViewModel(ProcuracaoLida lido, Action aoMudar)
    {
        Lido = lido;
        _aoMudar = aoMudar;

        if (lido.Cliente is { } c)
        {
            Nome = c.Nome;
            Cpf = Formato.Cpf(c.Cpf);
            Email = c.Email ?? string.Empty;
            Rg = c.Rg ?? string.Empty;
            RgOrgao = c.RgOrgao ?? string.Empty;
            CnhNumero = c.CnhNumero ?? string.Empty;
            Nacionalidade = c.Nacionalidade ?? string.Empty;
            EstadoCivil = c.EstadoCivil ?? string.Empty;
            Profissao = c.Profissao ?? string.Empty;

            if (c.Endereco is { } e)
            {
                Cep = Formato.Cep(e.Cep);
                Logradouro = e.Logradouro;
                Numero = e.Numero ?? string.Empty;
                Complemento = e.Complemento ?? string.Empty;
                Bairro = e.Bairro ?? string.Empty;
                Cidade = e.Cidade;
                Uf = e.Estado;
            }
        }

        Marcado = PodeMarcar;
    }

    public ProcuracaoLida Lido { get; }

    public string Arquivo => Lido.Arquivo;

    /// <summary>A linha de endereço como estava escrita, para conferir contra o digitado.</summary>
    public string? EnderecoNoDocumento => Lido.EnderecoNoDocumento;

    public bool TemEnderecoNoDocumento => !string.IsNullOrWhiteSpace(Lido.EnderecoNoDocumento);

    // ------------------------------------------------------- campos editáveis

    [ObservableProperty] public partial string Nome { get; set; } = string.Empty;
    [ObservableProperty] public partial string Cpf { get; set; } = string.Empty;

    /// <summary>O único campo do cadastro que procuração nenhuma traz.</summary>
    [ObservableProperty] public partial string Whatsapp { get; set; } = string.Empty;

    [ObservableProperty] public partial string Email { get; set; } = string.Empty;
    [ObservableProperty] public partial string Rg { get; set; } = string.Empty;
    [ObservableProperty] public partial string RgOrgao { get; set; } = string.Empty;
    [ObservableProperty] public partial string CnhNumero { get; set; } = string.Empty;
    [ObservableProperty] public partial string Nacionalidade { get; set; } = string.Empty;
    [ObservableProperty] public partial string EstadoCivil { get; set; } = string.Empty;
    [ObservableProperty] public partial string Profissao { get; set; } = string.Empty;

    [ObservableProperty] public partial string Cep { get; set; } = string.Empty;
    [ObservableProperty] public partial string Logradouro { get; set; } = string.Empty;
    [ObservableProperty] public partial string Numero { get; set; } = string.Empty;
    [ObservableProperty] public partial string Complemento { get; set; } = string.Empty;
    [ObservableProperty] public partial string Bairro { get; set; } = string.Empty;
    [ObservableProperty] public partial string Cidade { get; set; } = string.Empty;
    [ObservableProperty] public partial string Uf { get; set; } = string.Empty;

    // ---------------------------------------------------------------- estado

    /// <summary>Formulário aberto. Fechado por padrão: lista de dez não cabe aberta.</summary>
    [ObservableProperty] public partial bool Aberto { get; set; }

    [ObservableProperty] public partial bool Marcado { get; set; }

    /// <summary>O que aconteceu na gravação. Nulo enquanto não foi gravada.</summary>
    [ObservableProperty] public partial string? Resultado { get; set; }

    [ObservableProperty] public partial bool DeuCerto { get; set; }

    /// <summary>Recusado, repetido e já gravado não entram: a caixa de marcar fica travada.</summary>
    /// <remarks>
    /// Quem já está no sistema não se edita por aqui: corrigir o cadastro existente é
    /// trabalho da ficha dele, e oferecer o formulário aqui sugeriria que esta tela o
    /// altera — ela só cria.
    ///
    /// Quem tentou gravar e não conseguiu continua liberado: o erro costuma ser de um campo,
    /// e obrigar a reler a pasta inteira para corrigir uma linha seria trabalho à toa.
    /// </remarks>
    public bool PodeMarcar =>
        Lido.Situacao != SituacaoLeitura.Recusado
        && Lido.JaCadastrado is null
        && !DeuCerto;

    public bool PodeEditar => PodeMarcar;

    /// <summary>Em que bloco a linha fica na lista: pendente, resolvida, recusada.</summary>
    /// <remarks>
    /// Quem abre a tela tem uma pergunta só — o que falta fazer. O que já entrou e o que o
    /// sistema não leu não exigem ação, e descem.
    /// </remarks>
    public int Ordem => Lido.Situacao == SituacaoLeitura.Recusado ? 2 : PodeMarcar ? 0 : 1;

    public string TextoAbrir => Aberto ? "fechar" : "conferir dados";

    // ------------------------------------------------------------ conferência

    public bool CpfValido => Normalizacao.CpfValido(Cpf);

    public bool WhatsappValido => Normalizacao.TelefoneValido(Normalizacao.Telefone(Whatsapp));

    public bool EnderecoCompleto =>
        !string.IsNullOrWhiteSpace(Logradouro)
        && !string.IsNullOrWhiteSpace(Cidade)
        && Normalizacao.UfValida(Normalizacao.Uf(Uf));

    /// <summary>O que ainda impede esta linha de entrar sem ressalva.</summary>
    /// <remarks>
    /// Recalculado a cada edição. O primeiro bloco impede a gravação; o segundo é o que a
    /// procuração exige e que, faltando, só vai aparecer na hora de emitir o documento.
    /// </remarks>
    public IReadOnlyList<string> Pendencias
    {
        get
        {
            // Quem já está no sistema não entra: listar o que mais falta seria pedir
            // correção de um cadastro que esta tela não vai gravar.
            if (Lido.JaCadastrado is { } quem) return [$"Este CPF já está no sistema, em {quem}."];

            var faltas = new List<string>();

            if (string.IsNullOrWhiteSpace(Nome)) faltas.Add("Falta o nome.");
            if (!CpfValido) faltas.Add("O CPF não passa na conta do dígito verificador.");
            if (!WhatsappValido) faltas.Add("Falta o WhatsApp, que o documento não traz.");
            if (!EnderecoCompleto) faltas.Add("O endereço precisa de logradouro, cidade e UF.");

            if (string.IsNullOrWhiteSpace(Rg)) faltas.Add("Sem RG — a procuração usa.");
            if (string.IsNullOrWhiteSpace(Email)) faltas.Add("Sem e-mail — a procuração usa.");
            if (string.IsNullOrWhiteSpace(CnhNumero)) faltas.Add("Sem nº de registro da CNH — a procuração usa.");
            if (string.IsNullOrWhiteSpace(Profissao)) faltas.Add("Sem profissão — a procuração usa.");

            return faltas;
        }
    }

    /// <summary>Falta o que impede gravar, e não apenas o que o documento deixará incompleto.</summary>
    public bool PodeGravar =>
        PodeMarcar
        && Lido.JaCadastrado is null
        && !string.IsNullOrWhiteSpace(Nome)
        && CpfValido
        && WhatsappValido
        && EnderecoCompleto;

    /// <summary>O aviso em amarelo. Vazio quando não há o que resolver.</summary>
    /// <remarks>
    /// Cadastro que já entrou não leva aviso: o que faltava na procuração foi preenchido
    /// aqui, e cobrar de novo um cadastro gravado é ruído. O recusado mantém a explicação,
    /// que é a única pista de por que o arquivo não serviu.
    /// </remarks>
    public string Observacoes => Lido.Situacao switch
    {
        SituacaoLeitura.Recusado => string.Join("  ", Lido.Avisos),
        _ when DeuCerto => string.Empty,
        _ => string.Join("  ", Pendencias)
    };

    public bool TemObservacoes => Observacoes.Length > 0;

    /// <summary>Verde quando entrou, vermelho quando a gravação falhou.</summary>
    public string CorResultado => DeuCerto ? "Sucesso" : "Alerta";

    public string Situacao => Lido.Situacao switch
    {
        SituacaoLeitura.Recusado => "recusado",
        _ when DeuCerto => "importado",
        _ when Lido.JaCadastrado is not null => "já cadastrado",
        _ when Resultado is not null => "não gravou",
        _ when Pendencias.Count == 0 => "pronto",
        _ when PodeGravar => "incompleto",
        _ => "conferir"
    };

    public string FundoSelo => Situacao switch
    {
        "pronto" or "importado" => "SucessoClaro",
        "recusado" or "não gravou" => "AlertaClaro",
        "já cadastrado" => "Fundo",
        _ => "AvisoClaro"
    };

    public string CorSelo => Situacao switch
    {
        "pronto" or "importado" => "Sucesso",
        "recusado" or "não gravou" => "Alerta",
        "já cadastrado" => "TextoApagado",
        _ => "Aviso"
    };

    [RelayCommand]
    private void Alternar()
    {
        Aberto = !Aberto;
        OnPropertyChanged(nameof(TextoAbrir));
    }

    /// <summary>O cadastro como está depois das correções.</summary>
    public Cliente Montar()
    {
        var cliente = Lido.Cliente ?? new Cliente();

        cliente.Nome = Nome.Trim();
        cliente.Cpf = Normalizacao.SomenteDigitos(Cpf);
        cliente.Whatsapp = Normalizacao.Telefone(Whatsapp);
        cliente.Email = Vazio(Email);
        cliente.Rg = Vazio(Rg);
        cliente.RgOrgao = Vazio(RgOrgao);
        cliente.CnhNumero = Vazio(CnhNumero);
        cliente.Nacionalidade = Vazio(Nacionalidade);
        cliente.EstadoCivil = Vazio(EstadoCivil);
        cliente.Profissao = Vazio(Profissao);

        cliente.Endereco = EnderecoCompleto
            ? new Endereco
            {
                Cep = Vazio(Normalizacao.SomenteDigitos(Cep)),
                Logradouro = Logradouro.Trim(),
                Numero = Vazio(Numero),
                Complemento = Vazio(Complemento),
                Bairro = Vazio(Bairro),
                Cidade = Cidade.Trim(),
                Estado = Normalizacao.Uf(Uf)
            }
            : null;

        return cliente;
    }

    private static string? Vazio(string? texto) =>
        string.IsNullOrWhiteSpace(texto) ? null : texto.Trim();

    /// <summary>Qualquer edição mexe na situação, no resumo e na contagem da tela.</summary>
    protected override void OnPropertyChanged(System.ComponentModel.PropertyChangedEventArgs evento)
    {
        base.OnPropertyChanged(evento);

        // Só os campos: avisar de novo sobre os derivados entraria em laço.
        if (evento.PropertyName is nameof(Situacao) or nameof(Observacoes)
            or nameof(FundoSelo) or nameof(CorSelo) or nameof(TemObservacoes)
            or nameof(CorResultado) or nameof(PodeEditar) or nameof(Ordem)
            or nameof(PodeGravar) or nameof(PodeMarcar) or nameof(Pendencias))
        {
            return;
        }

        OnPropertyChanged(nameof(Situacao));
        OnPropertyChanged(nameof(Observacoes));
        OnPropertyChanged(nameof(TemObservacoes));
        OnPropertyChanged(nameof(FundoSelo));
        OnPropertyChanged(nameof(CorSelo));
        OnPropertyChanged(nameof(CorResultado));
        OnPropertyChanged(nameof(PodeGravar));
        OnPropertyChanged(nameof(PodeMarcar));
        OnPropertyChanged(nameof(PodeEditar));

        _aoMudar();
    }
}

/// <summary>Importa cadastros de uma pasta de procurações já emitidas (RF-58).</summary>
/// <remarks>
/// Existe por causa dos clientes anteriores ao sistema: a procuração deles já traz quase
/// tudo que o cadastro pede, e redigitar cliente a cliente seria o maior trabalho da
/// implantação.
///
/// Nada entra sozinho. A tela lê, mostra o que entendeu, deixa corrigir e espera aprovação
/// linha a linha — documento digitado à mão não é fonte confiável o bastante para gravar
/// direto.
/// </remarks>
public partial class ImportacaoViewModel : ViewModelBase
{
    private readonly ClienteDAO _clientes = new();
    private readonly EscritorioDAO _escritorios = new();
    private readonly IConsultaCep _cep = new CepComCache(new ViaCepConsulta());
    private readonly Action _aoFechar;
    private readonly Func<int, Task> _aoImportar;

    public ImportacaoViewModel(Action aoFechar, Func<int, Task> aoImportar)
    {
        _aoFechar = aoFechar;
        _aoImportar = aoImportar;
    }

    public ObservableCollection<LinhaImportacaoViewModel> Linhas { get; } = [];

    [ObservableProperty] public partial string? Pasta { get; set; }
    [ObservableProperty] public partial bool Lendo { get; set; }
    [ObservableProperty] public partial bool Importando { get; set; }
    [ObservableProperty] public partial string? Erro { get; set; }
    [ObservableProperty] public partial string? Aviso { get; set; }

    public bool TemLinhas => Linhas.Count > 0;
    public bool Ocupado => Lendo || Importando;

    public int Marcadas => Linhas.Count(l => l.Marcado && l.PodeMarcar);

    public string Contagem
    {
        get
        {
            if (Linhas.Count == 0) return string.Empty;

            var prontos = Linhas.Count(l => l.Situacao == "pronto");
            var conferir = Linhas.Count(l => l.Situacao is "conferir" or "incompleto");
            var recusados = Linhas.Count(l => l.Situacao == "recusado");
            var repetidos = Linhas.Count(l => l.Situacao == "já cadastrado");

            var partes = new List<string> { $"{Linhas.Count} documento(s)" };
            if (prontos > 0) partes.Add($"{prontos} pronto(s)");
            if (conferir > 0) partes.Add($"{conferir} a conferir");
            if (repetidos > 0) partes.Add($"{repetidos} já cadastrado(s)");
            if (recusados > 0) partes.Add($"{recusados} recusado(s)");

            return string.Join(" · ", partes);
        }
    }

    [RelayCommand]
    private async Task EscolherPasta()
    {
        var pasta = await Arquivos.EscolherPastaAsync("Pasta com as procurações");
        if (pasta is null) return;

        Pasta = pasta;
        await Ler(pasta);
    }

    private async Task Ler(string pasta)
    {
        Lendo = true;
        Erro = null;
        Aviso = null;
        Linhas.Clear();
        Avisar();

        try
        {
            // Sem os dados do escritório não há com que comparar a leitura, e é
            // justamente essa comparação que impede um cadastro com dados trocados.
            var escritorio = await _escritorios.BuscarAsync();
            if (escritorio is null)
            {
                Erro = "Os dados do escritório ainda não estão no sistema. "
                       + "Sem eles não é possível conferir a leitura dos documentos.";
                return;
            }

            var arquivos = Directory.EnumerateFiles(pasta)
                .Where(a => TextoDeDocumento.ExtensoesAceitas
                    .Contains(Path.GetExtension(a).ToLowerInvariant()))
                .OrderBy(a => Path.GetFileName(a), StringComparer.CurrentCultureIgnoreCase)
                .ToList();

            if (arquivos.Count == 0)
            {
                Aviso = "Nenhum arquivo .docx ou .pdf nesta pasta.";
                return;
            }

            foreach (var arquivo in arquivos)
            {
                var lido = await LeitorDeProcuracao.LerAsync(arquivo, escritorio, _cep);

                // Reimportar a mesma pasta não pode duplicar ninguém.
                if (lido.Cliente is { } cliente && !string.IsNullOrWhiteSpace(cliente.Cpf))
                {
                    var existente = await _clientes.BuscarPorCpfAsync(cliente.Cpf);
                    if (existente is not null) lido.JaCadastrado = existente.Nome;
                }

                Linhas.Add(new LinhaImportacaoViewModel(lido, Avisar));
            }

            Ordenar();
        }
        catch (Exception excecao)
        {
            Erro = $"Não foi possível ler a pasta: {excecao.Message}";
        }
        finally
        {
            Lendo = false;
            Avisar();
        }
    }

    [RelayCommand]
    private async Task Importar()
    {
        var escolhidas = Linhas.Where(l => l.Marcado && l.PodeMarcar).ToList();
        if (escolhidas.Count == 0)
        {
            Erro = "Nenhum cadastro marcado para importar.";
            return;
        }

        var incompletos = escolhidas.Where(l => !l.PodeGravar).ToList();
        if (incompletos.Count > 0)
        {
            // Abre quem está impedindo, para a correção ser o próximo clique.
            foreach (var linha in incompletos) linha.Aberto = true;

            Erro = $"{incompletos.Count} cadastro(s) marcado(s) ainda não podem entrar. "
                   + "Os formulários foram abertos com o que falta em cada um.";
            return;
        }

        Importando = true;
        Erro = null;
        Aviso = null;
        Avisar();

        var gravados = 0;

        try
        {
            foreach (var linha in escolhidas)
            {
                try
                {
                    var cliente = linha.Montar();

                    // O CPF pode ter sido corrigido depois da leitura da pasta.
                    if (await _clientes.CpfJaCadastradoAsync(cliente.Cpf))
                    {
                        linha.DeuCerto = false;
                        linha.Resultado = "não gravou: este CPF já está cadastrado.";
                        continue;
                    }

                    await _clientes.CadastrarAsync(cliente);

                    linha.DeuCerto = true;
                    linha.Resultado = "importado";
                    linha.Marcado = false;
                    linha.Aberto = false;
                    gravados++;
                }
                catch (Exception excecao)
                {
                    // Um documento ruim não derruba a importação inteira.
                    linha.DeuCerto = false;
                    linha.Resultado = $"não gravou: {excecao.Message}";
                }
            }

            Aviso = gravados == escolhidas.Count
                ? $"{gravados} cadastro(s) importado(s)."
                : $"{gravados} de {escolhidas.Count} cadastro(s) importado(s). "
                  + "Veja nas linhas o que não entrou.";

            if (gravados > 0) await _aoImportar(gravados);
        }
        finally
        {
            Importando = false;
            Ordenar();
            Avisar();
        }
    }

    /// <summary>Põe o que falta importar em cima, o resolvido no meio, o recusado embaixo.</summary>
    /// <remarks>
    /// Chamada depois de ler a pasta e depois de gravar — é na gravação que a lista muda de
    /// cara, e as linhas que ainda pedem atenção sobem sozinhas em vez de ficarem perdidas
    /// entre as que já entraram. Dentro de cada bloco, a ordem continua a dos arquivos.
    /// </remarks>
    private void Ordenar()
    {
        var ordenadas = Linhas
            .OrderBy(l => l.Ordem)
            .ThenBy(l => l.Arquivo, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        Linhas.Clear();
        foreach (var linha in ordenadas) Linhas.Add(linha);
    }

    /// <summary>Marca ou desmarca tudo que pode entrar.</summary>
    [RelayCommand]
    private void InverterMarcacao()
    {
        var marcar = Marcadas == 0;
        foreach (var linha in Linhas.Where(l => l.PodeMarcar)) linha.Marcado = marcar;
        Avisar();
    }

    [RelayCommand]
    private void Fechar() => _aoFechar();

    private void Avisar()
    {
        OnPropertyChanged(nameof(TemLinhas));
        OnPropertyChanged(nameof(Ocupado));
        OnPropertyChanged(nameof(Marcadas));
        OnPropertyChanged(nameof(Contagem));
    }

    partial void OnLendoChanged(bool valor) => Avisar();
    partial void OnImportandoChanged(bool valor) => Avisar();

    /// <summary>Pergunta cada CEP uma vez só.</summary>
    /// <remarks>
    /// Numa pasta de clientes do mesmo bairro, o mesmo CEP se repete dezenas de vezes.
    /// Sem isto seriam dezenas de chamadas iguais a um serviço gratuito de terceiro.
    /// </remarks>
    private sealed class CepComCache(IConsultaCep interno) : IConsultaCep
    {
        private readonly ConcurrentDictionary<string, ResultadoCep> _lembrados = new();

        public async Task<ResultadoCep> BuscarAsync(string? cep, CancellationToken cancelamento = default)
        {
            var chave = Normalizacao.SomenteDigitos(cep);
            if (_lembrados.TryGetValue(chave, out var lembrado)) return lembrado;

            var resultado = await interno.BuscarAsync(cep, cancelamento);

            // Indisponível é estado do momento, não resposta: guardar atrapalharia a
            // próxima tentativa, que pode já ter rede.
            if (resultado.Situacao != SituacaoCep.Indisponivel) _lembrados[chave] = resultado;

            return resultado;
        }
    }
}
