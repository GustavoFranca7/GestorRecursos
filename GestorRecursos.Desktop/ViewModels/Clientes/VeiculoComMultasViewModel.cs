using System;
using System.Collections.Generic;
using System.Linq;
using GestorRecursos.Core.Models;

namespace GestorRecursos.Desktop.ViewModels;

/// <summary>Um cartão da aba Veículos e multas: o veículo em cima, as multas dele embaixo.</summary>
public class VeiculoComMultasViewModel(VeiculoComMultas dados)
{
    /// <summary>O registro como veio do banco, para o formulário de edição reaproveitar.</summary>
    public VeiculoComMultas Dados { get; } = dados;

    public int Id { get; } = dados.Id;
    public string Placa { get; } = dados.Placa;

    /// <summary>"NISSAN VERSA 2019 · PRETA". Vazio quando só se sabe a placa.</summary>
    public string Descricao { get; } = string.Join(" · ", new[] { dados.MarcaModelo, dados.Cor }
        .Where(p => !string.IsNullOrWhiteSpace(p))
        .Select(p => p!.ToUpperInvariant()));

    public string? Ano { get; } = dados.Ano;
    public bool TemAno { get; } = !string.IsNullOrWhiteSpace(dados.Ano);

    public string Renavam { get; } = string.IsNullOrWhiteSpace(dados.Renavam)
        ? ViewModelBase.SemValor
        : dados.Renavam;

    public bool TemRenavam { get; } = !string.IsNullOrWhiteSpace(dados.Renavam);

    public string Vinculo { get; } = Rotulos.Vinculo(dados.Vinculo);

    public string? Observacao { get; } = dados.Observacao;
    public bool TemObservacao { get; } = !string.IsNullOrWhiteSpace(dados.Observacao);

    public IReadOnlyList<MultaViewModel> Multas { get; } =
        [.. dados.Multas.Select(m => new MultaViewModel(m))];

    public bool TemMultas => Multas.Count > 0;

    /// <summary>"2 em andamento", "1 multa", "nenhuma multa".</summary>
    public string ResumoMultas { get; } = MontarResumo(dados.Multas);

    /// <summary>Em andamento é a multa que virou recurso e ainda não foi julgada.</summary>
    public bool TemAndamento { get; } = dados.Multas.Any(EstaEmAndamento);

    private static string MontarResumo(List<MultaDaFicha> multas)
    {
        if (multas.Count == 0)
        {
            return "nenhuma multa";
        }

        var andamento = multas.Count(EstaEmAndamento);
        var total = multas.Count == 1 ? "1 multa" : $"{multas.Count} multas";

        return andamento > 0 ? $"{total} · {andamento} em andamento" : total;
    }

    private static bool EstaEmAndamento(MultaDaFicha multa) =>
        multa.Situacao is not null && !Rotulos.Encerrada(multa.Situacao);
}

/// <summary>Uma linha da tabela de multas, com tudo já escrito como aparece na tela.</summary>
public class MultaViewModel(MultaDaFicha dados)
{
    /// <summary>O registro como veio do banco, para o formulário de edição reaproveitar.</summary>
    public MultaDaFicha Dados { get; } = dados;

    public int Id { get; } = dados.Id;
    public string Ait { get; } = dados.Ait;

    public string Enquadramento { get; } = string.IsNullOrWhiteSpace(dados.Enquadramento)
        ? "—"
        : $"art. {dados.Enquadramento}";

    public string Descricao { get; } = dados.Descricao ?? string.Empty;
    public bool TemDescricao { get; } = !string.IsNullOrWhiteSpace(dados.Descricao);

    public string Local { get; } = dados.LocalInfracao ?? ViewModelBase.SemValor;

    /// <summary>"27/07/2026 às 09:22". Sem hora quando o banco só tem a data.</summary>
    public string DataHora { get; } = dados.DataHoraInfracao is { } quando
        ? quando.TimeOfDay == TimeSpan.Zero
            ? Formato.Data(quando)
            : $"{Formato.Data(quando)} às {quando:HH:mm}"
        : string.Empty;

    public string Valor { get; } = dados.Valor is { } valor ? Formato.Dinheiro(valor) : "—";

    public SeloViewModel? SeloGravidade { get; } = MontarGravidade(dados);
    public bool TemGravidade => SeloGravidade is not null;

    /// <summary>Na mesma régua de cor dos prazos do resto do sistema.</summary>
    public SeloViewModel SeloPrazo { get; } = MontarPrazo(dados);

    public SeloViewModel? SeloSituacao { get; } = MontarSituacao(dados);
    public bool TemSituacao => SeloSituacao is not null;

    private static SeloViewModel? MontarGravidade(MultaDaFicha multa)
    {
        var texto = Rotulos.Gravidade(multa.Gravidade, multa.Pontos);
        if (texto.Length == 0)
        {
            return null;
        }

        var (fundo, cor) = Rotulos.CoresDaGravidade(multa.Gravidade);
        return new SeloViewModel(texto, fundo, cor);
    }

    private static SeloViewModel MontarPrazo(MultaDaFicha multa)
    {
        // Recurso julgado não tem mais prazo correndo: pintar de vermelho a data de um
        // caso ganho seria alarme falso, e alarme falso estraga os alarmes de verdade.
        if (Rotulos.Encerrada(multa.Situacao))
        {
            return new SeloViewModel("—", "Fundo", "TextoApagado");
        }

        if (multa.PrazoQueVale is not { } prazo)
        {
            return new SeloViewModel("—", "Fundo", "TextoApagado");
        }

        var dias = (int)(prazo.Date - DateTime.Today).TotalDays;
        return Prazos.Selo(prazo.ToString("dd/MM"), dias);
    }

    /// <summary>Sem recurso contratado não há situação: a multa está só registrada.</summary>
    private static SeloViewModel? MontarSituacao(MultaDaFicha multa)
    {
        if (multa.Situacao is null)
        {
            return null;
        }

        // Quem está em julgamento é mostrado pela instância, que é a informação útil
        // para o escritório: "na JARI" diz mais que "aguardando julgamento".
        var texto = multa.Situacao is "AGUARDANDO_JULGAMENTO" or "PROTOCOLADO"
            ? $"na {Rotulos.Fase(multa.Fase)}"
            : Rotulos.Situacao(multa.Situacao);

        return new SeloViewModel(texto, "RoxoClaro", "Roxo");
    }
}
