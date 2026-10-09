using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GestorRecursos.Core.Data;
using GestorRecursos.Core.Models;
using GestorRecursos.Core.Validacao;

namespace GestorRecursos.Desktop.ViewModels;

/// <summary>Registro e edição de uma multa, sempre presa a um veículo.</summary>
/// <remarks>
/// Escolher a gravidade preenche pontos e valor pela tabela do CTB, como sugestão: na
/// maior parte dos autos é esse o número, e quem tiver o papel na mão corrige.
/// </remarks>
public partial class MultaFormularioViewModel : ViewModelBase
{
    private readonly MultaDAO _multas = new();
    private readonly Action? _aoCancelar;
    private readonly Action<int>? _aoSalvar;
    private readonly int _veiculoId;
    private readonly int _idEditando;

    /// <summary>Verdadeiro enquanto o construtor enche os campos com o que veio do banco.</summary>
    private readonly bool _carregando;

    public MultaFormularioViewModel(
        int veiculoId,
        string placa,
        Action? aoCancelar = null,
        Action<int>? aoSalvar = null,
        MultaDaFicha? paraEditar = null)
    {
        _veiculoId = veiculoId;
        Placa = placa;
        _aoCancelar = aoCancelar;
        _aoSalvar = aoSalvar;

        if (paraEditar is null)
        {
            return;
        }

        _carregando = true;
        _idEditando = paraEditar.Id;
        Ait = paraEditar.Ait;
        Enquadramento = paraEditar.Enquadramento ?? string.Empty;
        Descricao = paraEditar.Descricao ?? string.Empty;
        Local = paraEditar.LocalInfracao ?? string.Empty;
        Data = paraEditar.DataHoraInfracao is { } quando ? Formato.Data(quando) : string.Empty;
        Hora = paraEditar.DataHoraInfracao is { } h && h.TimeOfDay != TimeSpan.Zero
            ? h.ToString("HHmm")
            : string.Empty;
        Gravidade = TextoDaGravidade(paraEditar.Gravidade);
        Pontos = paraEditar.Pontos?.ToString() ?? string.Empty;
        Valor = paraEditar.Valor?.ToString("0.00", Brasil) ?? string.Empty;
        PrazoDefesa = paraEditar.PrazoDefesa is { } prazo ? Formato.Data(prazo) : string.Empty;
        _carregando = false;
    }

    private static readonly CultureInfo Brasil = new("pt-BR");

    public IReadOnlyList<string> Gravidades { get; } = ["Leve", "Média", "Grave", "Gravíssima"];

    /// <summary>A placa do veículo dono da multa. Só leitura: a multa nasce no carro.</summary>
    public string Placa { get; }

    [ObservableProperty] public partial string Ait { get; set; } = string.Empty;
    [ObservableProperty] public partial string Enquadramento { get; set; } = string.Empty;
    [ObservableProperty] public partial string Descricao { get; set; } = string.Empty;
    [ObservableProperty] public partial string Local { get; set; } = string.Empty;
    [ObservableProperty] public partial string Data { get; set; } = string.Empty;
    [ObservableProperty] public partial string Hora { get; set; } = string.Empty;
    [ObservableProperty] public partial string? Gravidade { get; set; }
    [ObservableProperty] public partial string Pontos { get; set; } = string.Empty;
    [ObservableProperty] public partial string Valor { get; set; } = string.Empty;
    [ObservableProperty] public partial string PrazoDefesa { get; set; } = string.Empty;

    [ObservableProperty] public partial bool Salvando { get; set; }
    [ObservableProperty] public partial string? Erro { get; set; }
    [ObservableProperty] public partial string? ErroAit { get; set; }
    [ObservableProperty] public partial string? ErroData { get; set; }

    public bool Editando => _idEditando > 0;
    public string Titulo => Editando ? "Editar multa" : "Registrar multa";
    public string TextoSalvar => Editando ? "Salvar alterações" : "Registrar multa";
    public bool TemErro => Erro is not null;

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
            var multa = Montar();

            if (Editando)
            {
                await _multas.AtualizarAsync(multa);
            }
            else
            {
                multa.Id = await _multas.CadastrarAsync(multa);
            }

            _aoSalvar?.Invoke(multa.Id);
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

    private async Task<bool> Conferir()
    {
        ErroAit = ErroData = Erro = null;

        var ait = Ait.Trim();
        if (ait.Length == 0)
        {
            ErroAit = "O AIT é obrigatório.";
        }
        else if (await _multas.AitJaCadastradoAsync(ait, _idEditando))
        {
            ErroAit = "Já existe uma multa com este AIT.";
        }

        if (Data.Trim().Length > 0 && LerData(Data) is null)
        {
            ErroData = "Data inválida. Use dd/mm/aaaa.";
        }

        var certo = ErroAit is null && ErroData is null;
        if (!certo)
        {
            Erro = "Confira os campos marcados.";
        }

        return certo;
    }

    private Multa Montar() => new()
    {
        Id = _idEditando,
        VeiculoId = _veiculoId,
        Ait = Ait.Trim().ToUpperInvariant(),
        Enquadramento = Limpar(Enquadramento),
        Descricao = Limpar(Descricao),
        LocalInfracao = Limpar(Local),
        DataHoraInfracao = MomentoDaInfracao(),
        Gravidade = GravidadeEscolhida(),
        Pontos = int.TryParse(Normalizacao.SomenteDigitos(Pontos), out var p) ? p : null,
        Valor = LerDinheiro(Valor),
        PrazoDefesa = LerData(PrazoDefesa)
    };

    /// <summary>Junta data e hora: o auto traz as duas, e a hora sustenta o recurso.</summary>
    private DateTime? MomentoDaInfracao()
    {
        if (LerData(Data) is not { } dia)
        {
            return null;
        }

        var digitos = Normalizacao.SomenteDigitos(Hora);
        if (digitos.Length == 4
            && int.TryParse(digitos[..2], out var h) && h is >= 0 and < 24
            && int.TryParse(digitos[2..], out var m) && m is >= 0 and < 60)
        {
            return dia.AddHours(h).AddMinutes(m);
        }

        return dia;
    }

    private GravidadeMulta? GravidadeEscolhida() => Gravidade switch
    {
        "Leve" => GravidadeMulta.Leve,
        "Média" => GravidadeMulta.Media,
        "Grave" => GravidadeMulta.Grave,
        "Gravíssima" => GravidadeMulta.Gravissima,
        _ => null
    };

    private static string? TextoDaGravidade(string? doBanco) => doBanco switch
    {
        "LEVE" => "Leve",
        "MEDIA" => "Média",
        "GRAVE" => "Grave",
        "GRAVISSIMA" => "Gravíssima",
        _ => null
    };

    /// <summary>Trocar a gravidade refaz pontos e valor pela tabela do CTB.</summary>
    /// <remarks>
    /// Sempre reescreve, e não só quando está em branco: quem corrige a gravidade está
    /// dizendo que errou a infração, e os dois números vêm dela. Os campos seguem
    /// editáveis — o auto manda mais que a tabela.
    ///
    /// Fica calado enquanto a tela carrega uma multa já gravada, senão a sugestão
    /// apagaria o que está no banco.
    /// </remarks>
    partial void OnGravidadeChanged(string? valor)
    {
        if (_carregando || GravidadeEscolhida() is not { } gravidade)
        {
            return;
        }

        Pontos = InfracaoCtb.Pontos(gravidade).ToString();
        Valor = InfracaoCtb.Valor(gravidade).ToString("0.00", Brasil);
    }

    private static decimal? LerDinheiro(string texto)
    {
        var limpo = texto.Trim().Replace("R$", string.Empty).Trim();
        return decimal.TryParse(limpo, NumberStyles.Currency, Brasil, out var valor) ? valor : null;
    }

    private static DateTime? LerData(string texto)
    {
        var digitos = Normalizacao.SomenteDigitos(texto);
        return digitos.Length == 8
            && DateTime.TryParseExact(digitos, "ddMMyyyy", null, DateTimeStyles.None, out var data)
                ? data
                : null;
    }

    private static string? Limpar(string? texto) =>
        string.IsNullOrWhiteSpace(texto) ? null : texto.Trim();

    partial void OnErroChanged(string? valor) => OnPropertyChanged(nameof(TemErro));
}
