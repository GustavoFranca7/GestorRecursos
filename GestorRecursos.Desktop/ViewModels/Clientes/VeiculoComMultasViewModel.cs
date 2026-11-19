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

    /// <summary>"Nissan Versa · preta · 2019/2019". Vazio quando só se sabe a placa.</summary>
    /// <remarks>
    /// Ano junto, e não num campo à parte: são três pedaços da mesma frase, e separá-los
    /// era o que fazia o cabeçalho do carro virar uma fila de valores soltos.
    /// </remarks>
    public string Descricao { get; } = string.Join(" · ", new[]
        {
            dados.MarcaModelo,
            dados.Cor?.ToLowerInvariant(),
            dados.Ano
        }
        .Where(p => !string.IsNullOrWhiteSpace(p)));

    public string Renavam { get; } = string.IsNullOrWhiteSpace(dados.Renavam)
        ? ViewModelBase.SemValor
        : dados.Renavam;

    public bool TemRenavam { get; } = !string.IsNullOrWhiteSpace(dados.Renavam);

    public string Vinculo { get; } = Rotulos.Vinculo(dados.Vinculo);

    // O dono responde "de quem é o carro"; o condutor indicado fica em tom menor.
    public string FundoDoVinculo { get; } =
        dados.Vinculo == "PROPRIETARIO" ? "PrimariaClara" : "Fundo";

    public string CorDoVinculo { get; } =
        dados.Vinculo == "PROPRIETARIO" ? "PrimariaEscura" : "TextoFraco";

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

    /// <summary>O que foi a infração. É a primeira coisa que se lê na linha.</summary>
    public string Infracao { get; } = string.IsNullOrWhiteSpace(dados.Descricao)
        ? "Infração sem descrição"
        : dados.Descricao!;

    /// <summary>"art. 218 III · 28/08/2026 · Avenida Brasil, em frente ao número 1000".</summary>
    /// <remarks>
    /// Uma frase só, que corta no fim quando não cabe. Em três colunas estreitas, cada uma
    /// quebrava por conta própria e a linha da multa virava um bloco de seis alturas.
    /// </remarks>
    public string Detalhe { get; } = MontarDetalhe(dados);

    /// <summary>Gravidade como ponto colorido e texto, sem fundo: é dado do carro, não alarme.</summary>
    /// <remarks>A cor de fundo fica reservada ao prazo, que é o único que cobra ação.</remarks>
    public string TextoGravidade { get; } = Rotulos.Gravidade(dados.Gravidade, dados.Pontos);
    public string CorDaGravidade { get; } = Rotulos.CoresDaGravidade(dados.Gravidade).Texto;

    /// <summary>Prazo só aparece quando corre de verdade: julgado não tem prazo correndo.</summary>
    public bool TemPrazo { get; } =
        dados.PrazoQueVale is not null && !Rotulos.Encerrada(dados.Situacao);

    /// <summary>Multa registrada sem recurso contratado. É serviço em aberto, não falta de dado.</summary>
    public bool SemRecurso { get; } = dados.Situacao is null;

    public SeloViewModel? SeloGravidade { get; } = MontarGravidade(dados);
    public bool TemGravidade => SeloGravidade is not null;

    /// <summary>Na mesma régua de cor dos prazos do resto do sistema.</summary>
    public SeloViewModel SeloPrazo { get; } = MontarPrazo(dados);

    public SeloViewModel? SeloSituacao { get; } = MontarSituacao(dados);
    public bool TemSituacao => SeloSituacao is not null;

    /// <summary>Junta artigo, data e local numa frase, pulando o que o banco não tem.</summary>
    private static string MontarDetalhe(MultaDaFicha multa)
    {
        var partes = new List<string>();

        if (!string.IsNullOrWhiteSpace(multa.Enquadramento))
        {
            partes.Add($"art. {multa.Enquadramento}");
        }

        if (multa.DataHoraInfracao is { } quando)
        {
            partes.Add(quando.TimeOfDay == TimeSpan.Zero
                ? Formato.Data(quando)
                : $"{Formato.Data(quando)} às {quando:HH:mm}");
        }

        if (!string.IsNullOrWhiteSpace(multa.LocalInfracao))
        {
            partes.Add(multa.LocalInfracao!);
        }

        return partes.Count > 0 ? string.Join(" · ", partes) : "sem outros dados registrados";
    }

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

        // Com a palavra: ao lado de "na JARI", uma data solta não diz do que é.
        var dias = (int)(prazo.Date - DateTime.Today).TotalDays;
        return Prazos.Selo($"prazo {prazo:dd/MM}", dias);
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
        var texto = multa.Situacao switch
        {
            "AGUARDANDO_JULGAMENTO" or "PROTOCOLADO" => $"na {Rotulos.Fase(multa.Fase)}",

            // "aguardando protocolo no CETRAN" repete a fase e não cabe na faixa do estado.
            "AGUARDANDO_PROTOCOLO_JARI" or "AGUARDANDO_PROTOCOLO_CETRAN" =>
                $"a protocolar na {Rotulos.Fase(multa.Fase)}",

            _ => Rotulos.Situacao(multa.Situacao)
        };

        return new SeloViewModel(texto, "RoxoClaro", "Roxo");
    }
}
