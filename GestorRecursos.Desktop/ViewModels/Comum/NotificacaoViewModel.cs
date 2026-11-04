using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using GestorRecursos.Desktop.Servicos;

namespace GestorRecursos.Desktop.ViewModels;

/// <summary>Um aviso flutuante no canto, que se apaga sozinho.</summary>
/// <remarks>
/// Ele mesmo controla o próprio tempo: aparece, espera, desaparece e avisa a janela que
/// pode ser removido. Deixar isso com quem cria a notificação faria cada tela repetir o
/// mesmo cronômetro — e divergir no primeiro ajuste.
/// </remarks>
public partial class NotificacaoViewModel : ObservableObject
{
    /// <summary>Quanto tempo o aviso fica legível, antes de começar a sumir.</summary>
    public static readonly TimeSpan Duracao = TimeSpan.FromSeconds(3);

    /// <summary>O esmaecer. Tem de bater com a transição declarada no XAML.</summary>
    private static readonly TimeSpan Esmaecimento = TimeSpan.FromMilliseconds(400);

    public NotificacaoViewModel(string texto, TomDaNotificacao tom)
    {
        Texto = texto;
        Tom = tom;
    }

    public string Texto { get; }
    public TomDaNotificacao Tom { get; }

    /// <summary>Visível liga a opacidade; desligar dispara o esmaecimento do XAML.</summary>
    /// <remarks>
    /// Começa falso de propósito: o aviso nasce transparente e só então acende, e é isso
    /// que faz a transição animar a entrada em vez de ele aparecer de um quadro para o
    /// outro.
    /// </remarks>
    [ObservableProperty]
    public partial bool Visivel { get; set; }

    /// <summary>O que a tela liga à opacidade. A transição do XAML faz o resto.</summary>
    public double Opacidade => Visivel ? 1 : 0;

    partial void OnVisivelChanged(bool valor) => OnPropertyChanged(nameof(Opacidade));

    public string Fundo => Tom switch
    {
        TomDaNotificacao.Sucesso => "Sucesso",
        TomDaNotificacao.Problema => "Alerta",
        _ => "MenuFundo"
    };

    /// <summary>Roda o ciclo todo e devolve quando o aviso pode sair da lista.</summary>
    public async Task ViverAsync()
    {
        // Um quadro de espera antes de acender: sem isto o controle já nasce opaco e
        // não há do que a transição partir.
        await Task.Delay(30);
        Visivel = true;

        await Task.Delay(Duracao);
        Visivel = false;

        await Task.Delay(Esmaecimento);
    }
}
