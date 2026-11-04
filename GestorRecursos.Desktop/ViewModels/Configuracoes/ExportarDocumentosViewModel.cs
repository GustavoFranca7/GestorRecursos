using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GestorRecursos.Core.Exportacao;
using GestorRecursos.Desktop.Servicos;

namespace GestorRecursos.Desktop.ViewModels;

/// <summary>Grava em disco todos os documentos anexados.</summary>
/// <remarks>
/// É a única cópia dos documentos fora da instância Oracle: o backup semanal é de texto
/// e não os carrega. Por isso a tela diz, em palavras, o que está sendo levado e o que
/// isso significa — não é um botão de exportar qualquer.
///
/// Nada é apagado: nem do banco, nem da pasta escolhida. A exportação cria uma pasta
/// datada própria, então rodar de novo não desfaz a anterior.
/// </remarks>
public partial class ExportarDocumentosViewModel : ViewModelBase
{
    private readonly Action _aoFechar;
    private CancellationTokenSource? _cancelamento;
    private List<DocumentoParaExportar> _fila = [];

    public ExportarDocumentosViewModel(Action aoFechar)
    {
        _aoFechar = aoFechar;
        _ = Levantar();
    }

    [ObservableProperty] public partial bool Levantando { get; set; }
    [ObservableProperty] public partial bool Exportando { get; set; }
    [ObservableProperty] public partial string? Erro { get; set; }

    /// <summary>"37 documentos · 142,3 MB", antes de começar.</summary>
    [ObservableProperty] public partial string Resumo { get; set; } = string.Empty;

    [ObservableProperty] public partial int Total { get; set; }
    [ObservableProperty] public partial int Feitos { get; set; }
    [ObservableProperty] public partial string Atual { get; set; } = string.Empty;

    /// <summary>Preenchido quando termina. Enquanto for nulo, a exportação não acabou.</summary>
    [ObservableProperty] public partial string? Conclusao { get; set; }

    /// <summary>A pasta criada, para o botão de abrir.</summary>
    [ObservableProperty] public partial string? PastaGravada { get; set; }

    /// <summary>Interrompida no meio. Separa o aviso do fim bem-sucedido.</summary>
    [ObservableProperty] public partial bool Interrompida { get; set; }

    public ObservableCollection<string> Falhas { get; } = [];

    public bool TemDocumentos => Total > 0;
    public bool PodeComecar => !Levantando && !Exportando && Conclusao is null && TemDocumentos;
    public bool Terminou => Conclusao is not null;
    public bool TerminouInteira => Terminou && !Interrompida;
    public bool TemFalhas => Falhas.Count > 0;
    public bool TemErro => !string.IsNullOrWhiteSpace(Erro);

    private async Task Levantar()
    {
        Levantando = true;

        try
        {
            _fila = await ExportadorDeDocumentos.ListarAsync();
            Total = _fila.Count;

            var bytes = _fila.Sum(d => (long)d.Tamanho);
            var clientes = _fila.Select(d => d.ClienteId).Distinct().Count();

            Resumo = Total == 0
                ? "Nenhum documento anexado ainda."
                : $"{Total} documento(s) de {clientes} cliente(s) · {Tamanho(bytes)}";
        }
        catch (Exception excecao)
        {
            Erro = $"Não foi possível ler a lista de documentos: {excecao.Message}";
        }
        finally
        {
            Levantando = false;
            Avisar();
        }
    }

    [RelayCommand]
    private async Task Exportar()
    {
        // Menu escondido não é tranca: o comando confere de novo antes de ler o banco.
        if (!Sessao.EhAdministrador)
        {
            Erro = "Apenas um administrador pode exportar os documentos.";
            Avisar();
            return;
        }

        if (!PodeComecar) return;

        var destino = await Arquivos.EscolherPastaAsync("Onde gravar os documentos");
        if (destino is null) return;

        Exportando = true;
        Erro = null;
        Feitos = 0;
        Falhas.Clear();
        Avisar();

        _cancelamento = new CancellationTokenSource();

        var andamento = new Progress<AndamentoDaExportacao>(a =>
        {
            Feitos = a.Feitos;
            Atual = a.Atual;
        });

        try
        {
            var (pasta, resultado) = await ExportadorDeDocumentos.ExportarAsync(
                destino, _fila, andamento, _cancelamento.Token);

            PastaGravada = pasta;

            foreach (var falha in resultado.Falhas)
            {
                Falhas.Add(falha);
            }

            Interrompida = resultado.Cancelada;
            Conclusao = resultado.Cancelada
                ? $"Interrompida. {resultado.Gravados} de {Total} documento(s) foram gravados "
                  + "e estão inteiros na pasta."
                : $"{resultado.Gravados} documento(s) gravados · {Tamanho(resultado.Bytes)}";
        }
        catch (Exception excecao)
        {
            Erro = $"A exportação parou: {excecao.Message}";
        }
        finally
        {
            Exportando = false;
            Atual = string.Empty;
            _cancelamento?.Dispose();
            _cancelamento = null;
            Avisar();
        }
    }

    /// <summary>Para no documento seguinte. O que já saiu fica.</summary>
    [RelayCommand]
    private void Interromper() => _cancelamento?.Cancel();

    [RelayCommand]
    private void AbrirPasta()
    {
        if (PastaGravada is { } pasta) Arquivos.Abrir(pasta);
    }

    [RelayCommand]
    private void Fechar()
    {
        _cancelamento?.Cancel();
        _aoFechar();
    }

    private static string Tamanho(long bytes) => bytes switch
    {
        < 1024 => $"{bytes} bytes",
        < 1024 * 1024 => $"{bytes / 1024.0:N0} KB",
        _ => $"{bytes / 1024.0 / 1024.0:N1} MB"
    };

    private void Avisar()
    {
        OnPropertyChanged(nameof(TemDocumentos));
        OnPropertyChanged(nameof(PodeComecar));
        OnPropertyChanged(nameof(Terminou));
        OnPropertyChanged(nameof(TerminouInteira));
        OnPropertyChanged(nameof(TemFalhas));
        OnPropertyChanged(nameof(TemErro));
    }

    partial void OnTotalChanged(int valor) => Avisar();
    partial void OnConclusaoChanged(string? valor) => Avisar();
}
