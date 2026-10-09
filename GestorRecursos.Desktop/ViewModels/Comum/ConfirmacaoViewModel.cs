using System;
using CommunityToolkit.Mvvm.Input;

namespace GestorRecursos.Desktop.ViewModels;

/// <summary>Uma pergunta de sim ou não, por cima da tela.</summary>
/// <remarks>
/// Uma só para o sistema: arquivar cliente, desvincular veículo e apagar multa fazem a
/// mesma pergunta de formas diferentes, e três caixas parecidas acabam divergindo no
/// texto do botão ou na cor do perigo.
///
/// Sem <paramref name="botao"/> vira recado: só o "Entendi", para quando a resposta é
/// que a ação não pode ser feita e o motivo precisa ser lido.
/// </remarks>
public partial class ConfirmacaoViewModel(
    string titulo,
    string texto,
    string? botao,
    Action? aoConfirmar,
    Action? aoCancelar) : ViewModelBase
{
    public string Titulo { get; } = titulo;
    public string Texto { get; } = texto;
    public string Botao { get; } = botao ?? string.Empty;

    /// <summary>Falso quando é só um recado, sem nada a confirmar.</summary>
    public bool TemAcao { get; } = botao is not null;

    public string TextoCancelar { get; } = botao is null ? "Entendi" : "Cancelar";

    [RelayCommand]
    private void Confirmar() => aoConfirmar?.Invoke();

    [RelayCommand]
    private void Cancelar() => aoCancelar?.Invoke();
}
