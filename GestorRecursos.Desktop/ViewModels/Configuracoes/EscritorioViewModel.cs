using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GestorRecursos.Core.Data;
using GestorRecursos.Core.Models;
using GestorRecursos.Core.Validacao;
using GestorRecursos.Desktop.Servicos;

namespace GestorRecursos.Desktop.ViewModels;

/// <summary>Os dados da despachante que assina a procuração e os recursos (RF-51).</summary>
/// <remarks>
/// Até aqui só mudavam por script, o que fazia qualquer correção de endereço depender de
/// quem programa. A linha é uma só na tabela, travada em ID = 1 — não há "escritórios",
/// há o escritório.
///
/// Tudo o que está aqui sai impresso na procuração. É por isso que o aviso embaixo do
/// formulário fala em conferir antes de salvar: documento com dado errado volta do órgão.
/// </remarks>
public partial class EscritorioViewModel : ViewModelBase
{
    private readonly EscritorioDAO _escritorios = new();
    private readonly Action _aoFechar;
    private readonly Func<Task> _aoSalvar;

    public EscritorioViewModel(Escritorio? atual, Action aoFechar, Func<Task> aoSalvar)
    {
        _aoFechar = aoFechar;
        _aoSalvar = aoSalvar;

        if (atual is null)
        {
            return;
        }

        Nome = atual.Nome;
        Nacionalidade = atual.Nacionalidade ?? string.Empty;
        EstadoCivil = atual.EstadoCivil ?? string.Empty;
        Profissao = atual.Profissao ?? string.Empty;
        Rg = atual.Rg ?? string.Empty;
        RgOrgao = atual.RgOrgao ?? string.Empty;
        Cpf = Formato.Cpf(atual.Cpf);
        RegistroProfissional = atual.RegistroProfissional ?? string.Empty;
        Email = atual.Email ?? string.Empty;
        Logradouro = atual.Logradouro ?? string.Empty;
        Numero = atual.Numero ?? string.Empty;
        Complemento = atual.Complemento ?? string.Empty;
        Bairro = atual.Bairro ?? string.Empty;
        Cep = Formato.Cep(atual.Cep);
        Cidade = atual.Cidade ?? string.Empty;
        Uf = atual.Uf ?? string.Empty;
        CidadeAssinatura = atual.CidadeAssinatura ?? string.Empty;
    }

    [ObservableProperty] public partial string Nome { get; set; } = string.Empty;
    [ObservableProperty] public partial string Nacionalidade { get; set; } = string.Empty;
    [ObservableProperty] public partial string EstadoCivil { get; set; } = string.Empty;
    [ObservableProperty] public partial string Profissao { get; set; } = string.Empty;
    [ObservableProperty] public partial string Rg { get; set; } = string.Empty;
    [ObservableProperty] public partial string RgOrgao { get; set; } = string.Empty;
    [ObservableProperty] public partial string Cpf { get; set; } = string.Empty;
    [ObservableProperty] public partial string RegistroProfissional { get; set; } = string.Empty;
    [ObservableProperty] public partial string Email { get; set; } = string.Empty;

    [ObservableProperty] public partial string Logradouro { get; set; } = string.Empty;
    [ObservableProperty] public partial string Numero { get; set; } = string.Empty;
    [ObservableProperty] public partial string Complemento { get; set; } = string.Empty;
    [ObservableProperty] public partial string Bairro { get; set; } = string.Empty;
    [ObservableProperty] public partial string Cep { get; set; } = string.Empty;
    [ObservableProperty] public partial string Cidade { get; set; } = string.Empty;
    [ObservableProperty] public partial string Uf { get; set; } = string.Empty;

    [ObservableProperty] public partial string CidadeAssinatura { get; set; } = string.Empty;

    [ObservableProperty] public partial string? Erro { get; set; }
    [ObservableProperty] public partial bool Salvando { get; set; }

    public bool TemErro => !string.IsNullOrWhiteSpace(Erro);

    [RelayCommand]
    private async Task Confirmar()
    {
        if (!Sessao.EhAdministrador)
        {
            Avisar("Apenas um administrador pode alterar os dados do escritório.");
            return;
        }

        if (Nome.Trim().Length < 3)
        {
            Avisar("O nome é obrigatório: é ele que assina a procuração.");
            return;
        }

        var cpf = Normalizacao.SomenteDigitos(Cpf);

        // CPF em branco passa: o cadastro pode estar sendo preenchido aos poucos. Errado,
        // nao - a procuracao sai com ele impresso.
        if (cpf.Length > 0 && !Normalizacao.CpfValido(cpf))
        {
            Avisar("O CPF informado não é válido.");
            return;
        }

        Salvando = true;
        Erro = null;
        OnPropertyChanged(nameof(TemErro));

        try
        {
            await _escritorios.AtualizarAsync(new Escritorio
            {
                Nome = Nome,
                Nacionalidade = Nacionalidade,
                EstadoCivil = EstadoCivil,
                Profissao = Profissao,
                Rg = Rg,
                RgOrgao = RgOrgao,
                Cpf = cpf,
                RegistroProfissional = RegistroProfissional,
                Email = Email,
                Logradouro = Logradouro,
                Numero = Numero,
                Complemento = Complemento,
                Bairro = Bairro,
                Cep = Normalizacao.SomenteDigitos(Cep),
                Cidade = Cidade,
                Uf = Normalizacao.Uf(Uf),
                CidadeAssinatura = CidadeAssinatura
            });

            Notificacoes.Sucesso("Dados do escritório salvos.");
            await _aoSalvar();
        }
        catch (Exception excecao)
        {
            Avisar($"Não foi possível salvar: {excecao.Message}");
        }
        finally
        {
            Salvando = false;
        }
    }

    [RelayCommand]
    private void Cancelar() => _aoFechar();

    private void Avisar(string texto)
    {
        Erro = texto;
        OnPropertyChanged(nameof(TemErro));
    }
}
