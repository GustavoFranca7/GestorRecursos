using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GestorRecursos.Core.Data;
using GestorRecursos.Core.Models;
using GestorRecursos.Core.Validacao;

namespace GestorRecursos.Desktop.ViewModels;

/// <summary>Os grupos de campos do cadastro, na ordem do índice.</summary>
/// <remarks>
/// A tela é montada a partir desta lista: acrescentar um grupo aqui é acrescentar uma
/// linha no índice. É o que torna barato mudar a regra do que entra no cadastro.
/// </remarks>
public enum GrupoCadastro
{
    DadosPessoais,
    ContatoEndereco,
    Cnh
}

/// <summary>Uma linha do índice da esquerda.</summary>
public partial class GrupoCadastroViewModel(string titulo, GrupoCadastro grupo, string numero, bool obrigatorio)
    : ObservableObject
{
    public string Titulo { get; } = titulo;
    public GrupoCadastro Grupo { get; } = grupo;

    /// <summary>O que aparece na bolinha enquanto o grupo não está completo.</summary>
    public string Numero { get; } = numero;

    /// <summary>Grupo que tem campo obrigatório: não dá para salvar sem passar por ele.</summary>
    public bool Obrigatorio { get; } = obrigatorio;

    [ObservableProperty]
    public partial bool Escolhido { get; set; }

    /// <summary>Preenchido o bastante para a bolinha virar visto.</summary>
    [ObservableProperty]
    public partial bool Completo { get; set; }

    /// <summary>Linha de baixo: o que já foi digitado, ou o que o grupo espera.</summary>
    [ObservableProperty]
    public partial string Resumo { get; set; } = string.Empty;
}

/// <summary>Cadastro e edição de cliente: janela central com índice de grupos.</summary>
/// <remarks>
/// Só entram aqui os três grupos da aba Dados da ficha. Veículos, multas, contratações e
/// documentos são do dia a dia depois do cadastro, e entram pela ficha.
///
/// Cadastrar e editar usam a mesma tela de propósito: quem edita o CNH entra direto no
/// grupo do CNH, mas continua podendo pular para os outros sem salvar e reabrir.
/// </remarks>
public partial class NovoClienteViewModel : ViewModelBase
{
    private readonly ClienteDAO _clientes = new();
    private readonly Action? _aoCancelar;
    private readonly Action<int>? _aoSalvar;

    /// <summary>O cliente que está sendo editado. Nulo quando é cadastro novo.</summary>
    private readonly Cliente? _original;

    public ObservableCollection<GrupoCadastroViewModel> Grupos { get; } =
    [
        new("Dados pessoais",     GrupoCadastro.DadosPessoais,   "1", obrigatorio: true),
        new("Contato e endereço", GrupoCadastro.ContatoEndereco, "2", obrigatorio: true),
        new("CNH",                GrupoCadastro.Cnh,             "3", obrigatorio: false)
    ];

    // Listas abertas: um cadastro antigo pode trazer valor fora delas, e perder o dado
    // da pessoa por causa da nossa lista seria o pior desfecho possível.
    public ObservableCollection<string> EstadosCivis { get; } =
        ["Solteiro(a)", "Casado(a)", "Divorciado(a)", "Viúvo(a)", "União estável"];

    public ObservableCollection<string> Categorias { get; } =
        ["A", "B", "AB", "C", "D", "E", "AC", "AD", "AE"];

    public IReadOnlyList<string> Ufs { get; } =
    [
        "AC", "AL", "AP", "AM", "BA", "CE", "DF", "ES", "GO", "MA", "MT", "MS", "MG", "PA",
        "PB", "PR", "PE", "PI", "RJ", "RN", "RS", "RO", "RR", "SC", "SP", "SE", "TO"
    ];

    // ---------- dados pessoais ----------
    [ObservableProperty] public partial string Nome { get; set; } = string.Empty;
    [ObservableProperty] public partial string Cpf { get; set; } = string.Empty;
    [ObservableProperty] public partial string Rg { get; set; } = string.Empty;
    // O órgão emissor é guardado como um texto só ("SESP/PR"), mas digitado em dois campos:
    // siglas variam de 2 a 6 letras, então não dá para adivinhar onde a UF começa.
    [ObservableProperty] public partial string RgOrgaoSigla { get; set; } = string.Empty;
    [ObservableProperty] public partial string? RgOrgaoUf { get; set; }
    [ObservableProperty] public partial string Nacionalidade { get; set; } = "Brasileira";
    [ObservableProperty] public partial string? EstadoCivil { get; set; }
    [ObservableProperty] public partial string Profissao { get; set; } = string.Empty;

    // ---------- contato e endereço ----------
    [ObservableProperty] public partial string Whatsapp { get; set; } = string.Empty;
    [ObservableProperty] public partial string Email { get; set; } = string.Empty;
    [ObservableProperty] public partial string Cep { get; set; } = string.Empty;
    [ObservableProperty] public partial string Cidade { get; set; } = string.Empty;
    [ObservableProperty] public partial string Logradouro { get; set; } = string.Empty;
    [ObservableProperty] public partial string Bairro { get; set; } = string.Empty;
    [ObservableProperty] public partial string Numero { get; set; } = string.Empty;
    [ObservableProperty] public partial string Complemento { get; set; } = string.Empty;
    [ObservableProperty] public partial string? Uf { get; set; } = "SC";

    // ---------- CNH ----------
    [ObservableProperty] public partial string CnhNumero { get; set; } = string.Empty;
    [ObservableProperty] public partial string? CnhCategoria { get; set; }
    [ObservableProperty] public partial string CnhValidade { get; set; } = string.Empty;
    [ObservableProperty] public partial string CnhRegistro { get; set; } = string.Empty;

    // ---------- estado da tela ----------
    [ObservableProperty] public partial GrupoCadastro GrupoAtual { get; set; } = GrupoCadastro.DadosPessoais;
    [ObservableProperty] public partial bool Salvando { get; set; }
    [ObservableProperty] public partial string? Erro { get; set; }

    [ObservableProperty] public partial string? ErroNome { get; set; }
    [ObservableProperty] public partial string? ErroCpf { get; set; }
    [ObservableProperty] public partial string? ErroWhatsapp { get; set; }
    [ObservableProperty] public partial string? ErroValidade { get; set; }

    public NovoClienteViewModel(
        Action? aoCancelar = null,
        Action<int>? aoSalvar = null,
        Cliente? paraEditar = null,
        GrupoCadastro grupoInicial = GrupoCadastro.DadosPessoais)
    {
        _aoCancelar = aoCancelar;
        _aoSalvar = aoSalvar;
        _original = paraEditar;

        if (paraEditar is not null)
        {
            Preencher(paraEditar);
        }

        IrParaGrupo(grupoInicial);
        AtualizarIndice();
    }

    public bool Editando => _original is not null;
    public string Titulo => Editando ? "Editar cliente" : "Novo cliente";
    public string TextoSalvar => Editando ? "Salvar alterações" : "Salvar cliente";

    /// <summary>Em edição o índice vira navegação: todos os grupos já têm conteúdo.</summary>
    public string Instrucao => Editando
        ? "Troque de grupo à vontade: tudo é salvo de uma vez, no fim."
        : "Apenas nome, CPF e WhatsApp são obrigatórios. Outras informações podem ser "
          + "inseridas posteriormente na ficha utilizando o botão de editar.";

    public bool NosDadosPessoais => GrupoAtual == GrupoCadastro.DadosPessoais;
    public bool NoContatoEndereco => GrupoAtual == GrupoCadastro.ContatoEndereco;
    public bool NaCnh => GrupoAtual == GrupoCadastro.Cnh;

    public bool TemErro => Erro is not null;

    /// <summary>Texto do botão que leva ao próximo grupo. Vazio no último.</summary>
    public string ProximoGrupo => GrupoAtual switch
    {
        GrupoCadastro.DadosPessoais => "Contato e endereço →",
        GrupoCadastro.ContatoEndereco => "CNH →",
        _ => string.Empty
    };

    public bool TemProximoGrupo => GrupoAtual != GrupoCadastro.Cnh;

    [RelayCommand]
    private void IrParaGrupo(GrupoCadastro grupo)
    {
        GrupoAtual = grupo;

        foreach (var item in Grupos)
        {
            item.Escolhido = item.Grupo == grupo;
        }
    }

    [RelayCommand]
    private void Avancar()
    {
        if (GrupoAtual == GrupoCadastro.DadosPessoais) IrParaGrupo(GrupoCadastro.ContatoEndereco);
        else if (GrupoAtual == GrupoCadastro.ContatoEndereco) IrParaGrupo(GrupoCadastro.Cnh);
    }

    [RelayCommand]
    private void Cancelar() => _aoCancelar?.Invoke();

    [RelayCommand]
    private async Task Salvar()
    {
        if (Salvando || !await Conferir())
        {
            return;
        }

        Salvando = true;
        Erro = null;

        try
        {
            var cliente = Montar();

            if (Editando)
            {
                await _clientes.AtualizarAsync(cliente);
            }
            else
            {
                cliente.Id = await _clientes.CadastrarAsync(cliente);
            }

            _aoSalvar?.Invoke(cliente.Id);
        }
        catch (Exception excecao)
        {
            Erro = $"Não foi possível salvar: {excecao.Message}";
        }
        finally
        {
            Salvando = false;
        }
    }

    /// <summary>Confere o que é obrigatório e leva ao grupo do primeiro problema.</summary>
    private async Task<bool> Conferir()
    {
        ErroNome = ErroCpf = ErroWhatsapp = ErroValidade = null;
        Erro = null;

        if (string.IsNullOrWhiteSpace(Nome))
        {
            ErroNome = "O nome é obrigatório.";
        }

        var cpf = Normalizacao.SomenteDigitos(Cpf);
        if (cpf.Length == 0)
        {
            ErroCpf = "O CPF é obrigatório.";
        }
        else if (cpf.Length != 11)
        {
            ErroCpf = "O CPF precisa ter 11 números.";
        }
        else if (await _clientes.CpfJaCadastradoAsync(cpf, _original?.Id ?? 0))
        {
            ErroCpf = "Já existe um cliente com este CPF.";
        }

        var zap = Normalizacao.Telefone(Whatsapp);
        if (zap.Length == 0)
        {
            ErroWhatsapp = "O WhatsApp é obrigatório.";
        }
        else if (!Normalizacao.TelefoneValido(zap))
        {
            ErroWhatsapp = "Informe DDD e número, com 11 dígitos.";
        }

        if (!string.IsNullOrWhiteSpace(CnhValidade) && LerData(CnhValidade) is null)
        {
            ErroValidade = "Data inválida. Use dd/mm/aaaa.";
        }

        // Leva ao grupo onde está o primeiro problema: erro em aba fechada não se resolve.
        if (ErroNome is not null || ErroCpf is not null)
        {
            IrParaGrupo(GrupoCadastro.DadosPessoais);
        }
        else if (ErroWhatsapp is not null)
        {
            IrParaGrupo(GrupoCadastro.ContatoEndereco);
        }
        else if (ErroValidade is not null)
        {
            IrParaGrupo(GrupoCadastro.Cnh);
        }

        var certo = ErroNome is null && ErroCpf is null && ErroWhatsapp is null && ErroValidade is null;
        if (!certo)
        {
            Erro = "Confira os campos marcados.";
        }

        AtualizarIndice();
        return certo;
    }

    private Cliente Montar()
    {
        var cliente = new Cliente
        {
            // Em edição, o que esta tela não mostra tem de voltar como estava: observações
            // e o estado de arquivado entram no mesmo UPDATE e seriam apagados em silêncio.
            Id = _original?.Id ?? 0,
            Observacoes = _original?.Observacoes,
            Arquivado = _original?.Arquivado ?? false,
            Nome = Nome.Trim(),
            Cpf = Normalizacao.SomenteDigitos(Cpf),
            Whatsapp = Normalizacao.Telefone(Whatsapp),
            Email = Limpar(Email),
            Rg = Normalizacao.SomenteDigitos(Rg),
            RgOrgao = JuntarOrgao(),
            Nacionalidade = Limpar(Nacionalidade),
            EstadoCivil = Limpar(EstadoCivil),
            Profissao = Limpar(Profissao),
            CnhNumero = Limpar(CnhNumero),
            CnhCategoria = Limpar(CnhCategoria),
            CnhValidade = LerData(CnhValidade),
            CnhRegistro = Limpar(CnhRegistro)
        };

        // Endereço só existe se houver o mínimo para ele significar alguma coisa.
        if (!string.IsNullOrWhiteSpace(Logradouro) && !string.IsNullOrWhiteSpace(Cidade))
        {
            cliente.Endereco = new Endereco
            {
                Cep = Limpar(Normalizacao.SomenteDigitos(Cep)),
                Logradouro = Logradouro.Trim(),
                Numero = Limpar(Numero),
                Complemento = Limpar(Complemento),
                Bairro = Limpar(Bairro),
                Cidade = Cidade.Trim(),
                Estado = Normalizacao.Uf(Uf)
            };
        }

        return cliente;
    }

    /// <summary>Joga o cliente do banco nos campos, já com a pontuação que a tela usa.</summary>
    private void Preencher(Cliente cliente)
    {
        Nome = cliente.Nome ?? string.Empty;
        Cpf = Formato.Cpf(cliente.Cpf);
        Rg = Formato.Rg(cliente.Rg);
        Nacionalidade = cliente.Nacionalidade ?? string.Empty;
        EstadoCivil = Acolher(EstadosCivis, cliente.EstadoCivil);
        Profissao = cliente.Profissao ?? string.Empty;

        SepararOrgao(cliente.RgOrgao);

        Whatsapp = Formato.Telefone(cliente.Whatsapp);
        Email = cliente.Email ?? string.Empty;

        CnhNumero = cliente.CnhNumero ?? string.Empty;
        CnhCategoria = Acolher(Categorias, cliente.CnhCategoria);
        CnhValidade = Formato.Data(cliente.CnhValidade);
        CnhRegistro = cliente.CnhRegistro ?? string.Empty;

        if (cliente.Endereco is { } endereco)
        {
            Cep = Formato.Cep(endereco.Cep);
            Cidade = endereco.Cidade ?? string.Empty;
            Logradouro = endereco.Logradouro ?? string.Empty;
            Bairro = endereco.Bairro ?? string.Empty;
            Numero = endereco.Numero ?? string.Empty;
            Complemento = endereco.Complemento ?? string.Empty;
            var estado = Normalizacao.Uf(endereco.Estado);
            Uf = Ufs.Contains(estado) ? estado : null;
        }
    }

    /// <summary>Põe na lista o valor que veio do banco e não estava previsto.</summary>
    /// <remarks>
    /// "Casado" gravado antes não é "Casado(a)" da nossa lista. Sem isto, abrir a edição
    /// deixaria o campo vazio e salvar apagaria o que a pessoa tinha informado.
    /// </remarks>
    private static string? Acolher(ObservableCollection<string> lista, string? valor)
    {
        if (string.IsNullOrWhiteSpace(valor))
        {
            return null;
        }

        var limpo = valor.Trim();
        if (!lista.Contains(limpo))
        {
            lista.Add(limpo);
        }

        return limpo;
    }

    /// <summary>Desfaz o "SESP/PR" nos dois campos da tela.</summary>
    private void SepararOrgao(string? orgao)
    {
        if (string.IsNullOrWhiteSpace(orgao))
        {
            return;
        }

        var barra = orgao.IndexOf('/');
        if (barra < 0)
        {
            RgOrgaoSigla = orgao.Trim().ToUpperInvariant();
            return;
        }

        RgOrgaoSigla = orgao[..barra].Trim().ToUpperInvariant();

        var uf = orgao[(barra + 1)..].Trim().ToUpperInvariant();
        RgOrgaoUf = Ufs.Contains(uf) ? uf : null;
    }

    /// <summary>Mantém o índice contando o que já foi preenchido.</summary>
    private void AtualizarIndice()
    {
        var pessoais = Grupos[0];
        pessoais.Completo = !string.IsNullOrWhiteSpace(Nome) && !string.IsNullOrWhiteSpace(Cpf);
        pessoais.Resumo = string.IsNullOrWhiteSpace(Nome) ? "Nome e CPF" : Nome.Trim();

        var contato = Grupos[1];
        contato.Completo = !string.IsNullOrWhiteSpace(Whatsapp);
        contato.Resumo = string.IsNullOrWhiteSpace(Whatsapp)
            ? "WhatsApp e endereço"
            : Formato.Telefone(Normalizacao.Telefone(Whatsapp));

        var cnh = Grupos[2];
        cnh.Completo = !string.IsNullOrWhiteSpace(CnhNumero);
        cnh.Resumo = string.IsNullOrWhiteSpace(CnhNumero) ? "Opcional" : CnhNumero.Trim();
    }

    partial void OnGrupoAtualChanged(GrupoCadastro valor)
    {
        OnPropertyChanged(nameof(NosDadosPessoais));
        OnPropertyChanged(nameof(NoContatoEndereco));
        OnPropertyChanged(nameof(NaCnh));
        OnPropertyChanged(nameof(ProximoGrupo));
        OnPropertyChanged(nameof(TemProximoGrupo));
    }

    partial void OnErroChanged(string? valor) => OnPropertyChanged(nameof(TemErro));

    partial void OnNomeChanged(string valor) => AtualizarIndice();
    partial void OnCpfChanged(string valor) => AtualizarIndice();
    partial void OnWhatsappChanged(string valor) => AtualizarIndice();
    partial void OnCnhNumeroChanged(string valor) => AtualizarIndice();

    private static string? Limpar(string? texto) =>
        string.IsNullOrWhiteSpace(texto) ? null : texto.Trim();

    /// <summary>Os dois campos do órgão viram "SESP/PR", que é como a ficha mostra.</summary>
    private string? JuntarOrgao()
    {
        var sigla = RgOrgaoSigla.Trim();

        if (sigla.Length == 0)
        {
            return null;
        }

        // A UF sozinha não diz nada, então ela só entra acompanhando a sigla.
        return string.IsNullOrWhiteSpace(RgOrgaoUf) ? sigla : $"{sigla}/{RgOrgaoUf}";
    }

    /// <summary>Aceita dd/mm/aaaa, com ou sem barras.</summary>
    private static DateTime? LerData(string texto)
    {
        if (string.IsNullOrWhiteSpace(texto))
        {
            return null;
        }

        var digitos = Normalizacao.SomenteDigitos(texto);
        if (digitos.Length == 8
            && DateTime.TryParseExact(digitos, "ddMMyyyy", null,
                   System.Globalization.DateTimeStyles.None, out var data))
        {
            return data;
        }

        return null;
    }
}
