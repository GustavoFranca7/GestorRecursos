using CommunityToolkit.Mvvm.ComponentModel;
using GestorRecursos.Core.Data;

namespace GestorRecursos.Desktop.ViewModels;

/// <summary>A seção Clientes do menu: ora a listagem, ora a ficha de um cliente.</summary>
/// <remarks>
/// Existe para que a lista não precise saber navegar e a ficha não precise saber voltar.
/// As duas avisam esta casca, que decide o que fica na tela.
/// </remarks>
public partial class ClientesViewModel : ViewModelBase
{
    [ObservableProperty]
    public partial ViewModelBase Conteudo { get; set; }

    /// <summary>Cadastro aberto por cima da lista. Nulo quando não há nenhum.</summary>
    [ObservableProperty]
    public partial NovoClienteViewModel? Cadastro { get; set; }

    public ClientesViewModel()
    {
        Conteudo = NovaLista();
    }

    /// <summary>Abre o cadastro sobre o que estiver na tela — listagem ou ficha.</summary>
    public void AbrirCadastro() =>
        Cadastro = new NovoClienteViewModel(
            aoCancelar: () => Cadastro = null,
            aoSalvar: AbrirFichaDoNovo);

    /// <summary>Abre a mesma tela do cadastro, já preenchida, no grupo que foi clicado.</summary>
    private async void AbrirEdicao(int clienteId, GrupoCadastro grupo)
    {
        var cliente = await new ClienteDAO().BuscarPorIdAsync(clienteId);
        if (cliente is null)
        {
            return;
        }

        Cadastro = new NovoClienteViewModel(
            aoCancelar: () => Cadastro = null,
            aoSalvar: AbrirFichaDoNovo,
            paraEditar: cliente,
            grupoInicial: grupo);
    }

    private void AbrirFichaDoNovo(int clienteId, bool criarServico)
    {
        Cadastro = null;
        AbrirFicha(clienteId, criarServico);
    }

    private ListaClientesViewModel NovaLista() =>
        new(aoAbrirFicha: id => AbrirFicha(id), aoNovoCliente: AbrirCadastro);

    private void AbrirFicha(int clienteId, bool criarServico = false) =>
        Conteudo = new FichaClienteViewModel(
            clienteId,
            aoVoltar: VoltarParaLista,
            aoNovoCliente: AbrirCadastro,
            aoEditar: grupo => AbrirEdicao(clienteId, grupo),
            abrirNovoServico: criarServico);

    private void VoltarParaLista() => Conteudo = NovaLista();
}
