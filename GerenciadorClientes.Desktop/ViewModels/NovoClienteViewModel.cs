using System;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GerenciadorClientes.Core.Data;
using GerenciadorClientes.Core.Models;
using GerenciadorClientes.Core.Validacao;

namespace GerenciadorClientes.Desktop.ViewModels;

// Formulario de novo cliente, aberto por cima da lista.
// Cliente e endereco sao validados e gravados juntos.
public partial class NovoClienteViewModel : ViewModelBase
{
    private readonly ClienteDAO _clienteDao;

    // Avisos para a tela principal: salvou (com o cliente criado) ou fechou.
    private readonly Action<Cliente> _aoSalvar;
    private readonly Action _aoFechar;

    public NovoClienteViewModel(ClienteDAO clienteDao, Action<Cliente> aoSalvar, Action aoFechar)
    {
        _clienteDao = clienteDao;
        _aoSalvar = aoSalvar;
        _aoFechar = aoFechar;
    }

    // Construtor vazio so para o preview da IDE conseguir desenhar a tela.
    public NovoClienteViewModel() : this(new ClienteDAO(), _ => { }, () => { })
    {
    }

    // ===================== Campos =====================

    [ObservableProperty] public partial string Nome { get; set; } = string.Empty;
    [ObservableProperty] public partial string Cpf { get; set; } = string.Empty;
    [ObservableProperty] public partial string Telefone { get; set; } = string.Empty;
    [ObservableProperty] public partial string Email { get; set; } = string.Empty;
    [ObservableProperty] public partial string Logradouro { get; set; } = string.Empty;
    [ObservableProperty] public partial string Numero { get; set; } = string.Empty;
    [ObservableProperty] public partial string Cidade { get; set; } = string.Empty;
    [ObservableProperty] public partial string Uf { get; set; } = string.Empty;
    [ObservableProperty] public partial string Cep { get; set; } = string.Empty;

    // ===================== Erros (nulo = campo ok) =====================

    [ObservableProperty] public partial string? ErroNome { get; set; }
    [ObservableProperty] public partial string? ErroCpf { get; set; }
    [ObservableProperty] public partial string? ErroTelefone { get; set; }
    [ObservableProperty] public partial string? ErroEmail { get; set; }
    [ObservableProperty] public partial string? ErroLogradouro { get; set; }
    [ObservableProperty] public partial string? ErroNumero { get; set; }
    [ObservableProperty] public partial string? ErroCidade { get; set; }
    [ObservableProperty] public partial string? ErroUf { get; set; }
    [ObservableProperty] public partial string? ErroCep { get; set; }

    // Faixa vinho do topo, explicando por que nao salvou.
    [ObservableProperty] public partial string? Aviso { get; set; }

    // Texto a direita do titulo ENDERECO.
    [ObservableProperty] public partial string AvisoEndereco { get; set; } = "obrigatorio para cadastrar";

    // ===================== Correcao automatica =====================
    // CPF, telefone, UF e CEP so atualizam ao sair do campo (LostFocus no XAML),
    // entao a correcao nao atrapalha enquanto a pessoa ainda esta digitando.

    partial void OnCpfChanged(string value)
    {
        Cpf = Normalizacao.SomenteDigitos(value);
        ErroCpf = null;
    }

    partial void OnTelefoneChanged(string value)
    {
        Telefone = Normalizacao.Telefone(value);
        ErroTelefone = null;
    }

    partial void OnUfChanged(string value)
    {
        Uf = Normalizacao.Uf(value);
        ErroUf = null;
    }

    partial void OnCepChanged(string value)
    {
        Cep = Normalizacao.SomenteDigitos(value);
        ErroCep = null;
    }

    // Nos demais campos, basta limpar o erro quando a pessoa mexe neles.
    partial void OnNomeChanged(string value) => ErroNome = null;
    partial void OnEmailChanged(string value) => ErroEmail = null;
    partial void OnLogradouroChanged(string value) => ErroLogradouro = null;
    partial void OnNumeroChanged(string value) => ErroNumero = null;
    partial void OnCidadeChanged(string value) => ErroCidade = null;

    // ===================== Acoes =====================

    [RelayCommand]
    private void Salvar()
    {
        try
        {
            Nome = Nome.Trim();
            Email = Email.Trim();
            Logradouro = Logradouro.Trim();
            Numero = Numero.Trim();
            Cidade = Cidade.Trim();

            if (!Validar())
            {
                return;
            }

            Cliente novoCliente = new Cliente(Nome, Cpf, Email, Telefone)
            {
                Endereco = new Endereco(Logradouro, Numero, Cidade, Uf, Cep)
            };

            _clienteDao.CadastrarComEndereco(novoCliente);
            _aoSalvar(novoCliente);
        }
        catch (Exception ex)
        {
            // Banco fora do ar ou gravacao recusada: o formulario continua aberto.
            Aviso = $"ERRO AO SALVAR: {ex.Message}";
        }
    }

    [RelayCommand]
    private void Cancelar()
    {
        _aoFechar();
    }

    // Preenche as mensagens de erro e diz se da para gravar.
    private bool Validar()
    {
        ErroNome = Obrigatorio(Nome, 100);
        ErroCpf = string.IsNullOrEmpty(Cpf) ? "OBRIGATORIO"
                : Cpf.Length != 11 ? "PRECISA TER 11 NUMEROS" : null;
        ErroTelefone = string.IsNullOrEmpty(Telefone) ? "OBRIGATORIO"
                     : !Normalizacao.TelefoneValido(Telefone) ? "USE DDD + 9 + NUMERO" : null;
        ErroEmail = Email.Length == 0 ? null
                  : Email.Length > 100 || !Email.Contains('@') ? "E-MAIL INVALIDO" : null;

        ErroLogradouro = Obrigatorio(Logradouro, 150);
        ErroNumero = Obrigatorio(Numero, 20);
        ErroCidade = Obrigatorio(Cidade, 50);
        ErroUf = string.IsNullOrEmpty(Uf) ? "OBRIGATORIO"
               : !Normalizacao.UfValida(Uf) ? "UF INVALIDA" : null;
        ErroCep = string.IsNullOrEmpty(Cep) ? "OBRIGATORIO"
                : Cep.Length != 8 ? "PRECISA TER 8 NUMEROS" : null;

        // O banco so e consultado se o CPF ja estiver no formato certo.
        if (ErroCpf is null && _clienteDao.CpfJaCadastrado(Cpf))
        {
            ErroCpf = "CPF JA CADASTRADO";
        }

        int errosEndereco = new[] { ErroLogradouro, ErroNumero, ErroCidade, ErroUf, ErroCep }.Count(e => e is not null);
        bool errosCliente = new[] { ErroNome, ErroCpf, ErroTelefone, ErroEmail }.Any(e => e is not null);

        AvisoEndereco = errosEndereco == 0 ? "obrigatorio para cadastrar" : $"{errosEndereco} campo(s) com problema";

        if (errosEndereco > 0)
        {
            Aviso = "NAO FOI POSSIVEL SALVAR. Um cliente so pode ser cadastrado junto com o endereco completo.";
        }
        else if (errosCliente)
        {
            Aviso = "NAO FOI POSSIVEL SALVAR. Corrija os campos marcados.";
        }
        else
        {
            Aviso = null;
        }

        return Aviso is null;
    }

    private static string? Obrigatorio(string valor, int maximo)
    {
        if (string.IsNullOrWhiteSpace(valor))
        {
            return "OBRIGATORIO";
        }

        return valor.Length > maximo ? $"MAXIMO {maximo} CARACTERES" : null;
    }
}
