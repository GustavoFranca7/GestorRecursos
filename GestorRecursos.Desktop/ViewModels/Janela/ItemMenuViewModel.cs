using CommunityToolkit.Mvvm.ComponentModel;

namespace GestorRecursos.Desktop.ViewModels;

/// <summary>Um item do menu lateral.</summary>
/// <remarks>
/// A chave do ícone é texto porque a geometria vive no dicionário de recursos
/// (Assets/Icones.axaml): o ViewModel não conhece desenho, só o nome dele.
/// </remarks>
public partial class ItemMenuViewModel(string titulo, string chaveIcone, SecaoApp secao) : ObservableObject
{
    public string Titulo { get; } = titulo;

    public string ChaveIcone { get; } = chaveIcone;

    public SecaoApp Secao { get; } = secao;

    [ObservableProperty]
    public partial bool Ativo { get; set; }
}

/// <summary>As telas que o menu alcança.</summary>
public enum SecaoApp
{
    Dashboard,
    Clientes,
    Processos,
    Financeiro,
    Agenda,
    Veiculos,
    Servicos,
    Configuracoes,
    Arquivados
}
