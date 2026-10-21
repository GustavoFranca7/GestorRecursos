using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GestorRecursos.Core.Documentos;
using GestorRecursos.Core.Models;
using GestorRecursos.Desktop.Servicos;

namespace GestorRecursos.Desktop.ViewModels;

/// <summary>A procuração aberta por cima da ficha, para conferir antes de imprimir.</summary>
/// <remarks>
/// Janela flutuante, e não um painel na aba: a aba Documentos vai ganhar os anexos, e um
/// documento de página inteira tampava o resto.
///
/// O arquivo não é guardado. O PDF se refaz do cadastro a qualquer momento, e salvar uma
/// cópia criaria duas versões do mesmo papel — a gravada e a que o cadastro diria hoje.
/// </remarks>
public partial class ProcuracaoViewModel : ViewModelBase
{
    private readonly Cliente _cliente;
    private readonly Escritorio? _escritorio;
    private readonly Action _aoFechar;
    private readonly Action<ParteDoCadastro> _aoCorrigir;

    /// <summary>Onde o primeiro dado que falta é digitado. Guia o botão de corrigir.</summary>
    private ParteDoCadastro _ondeCorrigir = ParteDoCadastro.DadosPessoais;

    public ProcuracaoViewModel(
        Cliente cliente,
        Escritorio? escritorio,
        Action aoFechar,
        Action<ParteDoCadastro> aoCorrigir)
    {
        _cliente = cliente;
        _escritorio = escritorio;
        _aoFechar = aoFechar;
        _aoCorrigir = aoCorrigir;
    }

    public string Titulo => "Procuração para defesa em auto de infração";

    public string Subtitulo =>
        $"Preenchida com os dados de {_cliente.Nome} e do escritório. "
        + "A data é a de hoje, e o documento se refaz a qualquer momento.";

    /// <summary>O que impede a procuração de sair. Vazia significa pronta.</summary>
    public ObservableCollection<string> Faltam { get; } = [];

    /// <summary>A primeira página do PDF, exatamente como vai sair impressa.</summary>
    [ObservableProperty]
    public partial Bitmap? Previa { get; set; }

    [ObservableProperty]
    public partial bool Montando { get; set; }

    [ObservableProperty]
    public partial string? Erro { get; set; }

    public bool Pronta => Previa is not null;
    public bool Incompleta => Faltam.Count > 0;

    /// <summary>Confere o cadastro e desenha a página. Chamado uma vez, ao abrir.</summary>
    public async Task PrepararAsync()
    {
        Montando = true;

        try
        {
            Faltam.Clear();

            foreach (var falta in Procuracao.Faltando(_cliente, _escritorio))
            {
                Faltam.Add(falta.Nome);
                if (Faltam.Count == 1) _ondeCorrigir = falta.Onde;
            }

            if (Faltam.Count == 0 && _escritorio is not null)
            {
                // Desenhar a página é trabalho de processador: fora da thread da
                // interface, senão a janela abre travada.
                var documento = Montar();
                var imagem = await Task.Run(() => DocumentoPdf.GerarImagem(documento));

                using var memoria = new MemoryStream(imagem);
                Previa = new Bitmap(memoria);
            }
        }
        catch (Exception excecao)
        {
            Erro = $"Não foi possível montar o documento: {excecao.Message}";
        }
        finally
        {
            Montando = false;
            OnPropertyChanged(nameof(Pronta));
            OnPropertyChanged(nameof(Incompleta));
        }
    }

    /// <summary>Pergunta onde salvar, grava o PDF e abre no leitor do sistema.</summary>
    [RelayCommand]
    private async Task SalvarPdf()
    {
        if (_escritorio is null)
        {
            return;
        }

        try
        {
            var documento = Montar();
            var caminho = await Arquivos.OndeSalvarPdfAsync(documento.NomeArquivo);

            if (caminho is null)
            {
                return;
            }

            DocumentoPdf.Gravar(documento, caminho);
            Arquivos.Abrir(caminho);
        }
        catch (Exception excecao)
        {
            Erro = $"Não foi possível gerar o PDF: {excecao.Message}";
        }
    }

    /// <summary>Leva ao cadastro, no grupo onde o dado errado é digitado.</summary>
    [RelayCommand]
    private void Corrigir() => _aoCorrigir(_ondeCorrigir);

    [RelayCommand]
    private void Fechar() => _aoFechar();

    // A data é sempre a de hoje: a procuração vale a partir de quando for assinada.
    private Documento Montar() => Procuracao.Montar(_cliente, _escritorio!, DateTime.Today);
}
