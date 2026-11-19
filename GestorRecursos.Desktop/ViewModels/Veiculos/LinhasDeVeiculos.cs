using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using GestorRecursos.Core.Models;

namespace GestorRecursos.Desktop.ViewModels;

/// <summary>Uma linha da lista de veículos.</summary>
public partial class VeiculoViewModel : ObservableObject
{
    public VeiculoViewModel(VeiculoDaLista dados)
    {
        Dados = dados;

        Busca = string.Join(
            ' ',
            dados.Placa,
            dados.Renavam,
            dados.MarcaModelo,
            dados.Cor,
            dados.Ano,
            dados.ProprietarioNome).ToLowerInvariant();
    }

    public VeiculoDaLista Dados { get; }

    public int Id => Dados.Id;
    public string Placa => Dados.Placa;
    public string Busca { get; }

    /// <summary>Aberta, a linha mostra o histórico do carro logo abaixo.</summary>
    [ObservableProperty]
    public partial bool Aberto { get; set; }

    /// <summary>O histórico só vai ao banco na primeira vez que a linha abre.</summary>
    [ObservableProperty]
    public partial bool CarregandoHistorico { get; set; }

    public bool JaCarregou { get; set; }

    public ObservableCollection<DonoViewModel> Donos { get; } = [];
    public ObservableCollection<MultaDoVeiculoViewModel> Multas { get; } = [];

    public bool SemDonos => JaCarregou && Donos.Count == 0;
    public bool SemMultas => JaCarregou && Multas.Count == 0;

    /// <summary>"GOL 1.6, prata, 2019/2019" — o que se sabe do carro, em uma linha.</summary>
    public string Descricao
    {
        get
        {
            var partes = new List<string>();

            if (!string.IsNullOrWhiteSpace(Dados.MarcaModelo))
            {
                partes.Add(Dados.MarcaModelo!);
            }

            if (!string.IsNullOrWhiteSpace(Dados.Cor))
            {
                partes.Add(Dados.Cor!.ToLowerInvariant());
            }

            if (!string.IsNullOrWhiteSpace(Dados.Ano))
            {
                partes.Add(Dados.Ano!);
            }

            return partes.Count > 0 ? string.Join(", ", partes) : "sem descrição";
        }
    }

    public bool TemDescricao => !string.IsNullOrWhiteSpace(Dados.MarcaModelo);

    public string? Renavam => Dados.Renavam;
    public bool TemRenavam => !string.IsNullOrWhiteSpace(Dados.Renavam);

    public int? ProprietarioId => Dados.ProprietarioId;

    /// <summary>De quem é a placa. É a pergunta que a seção existe para responder.</summary>
    /// <remarks>
    /// Três respostas, e não duas: o carro pode ter condutor indicado e nenhum dono. Sem
    /// dizer isso, a coluna ficava em branco e parecia defeito da tela.
    /// </remarks>
    public string DeQuemE => Dados.ProprietarioNome
        ?? (Dados.Condutores > 0 ? "proprietário não cadastrado" : "sem dono vinculado");

    public bool TemProprietario => Dados.ProprietarioId is not null;
    public bool SemVinculo => Dados.SemVinculo;

    /// <summary>Nome conhecido é texto comum; a ausência é aviso.</summary>
    public string CorDeQuemE => TemProprietario ? "Texto" : "Aviso";

    public string Condutores => Dados.Condutores switch
    {
        0 => string.Empty,
        1 => "+ 1 condutor indicado",
        _ => $"+ {Dados.Condutores} condutores indicados"
    };

    public bool TemCondutores => Dados.Condutores > 0;

    public string TextoMultas => Dados.Multas switch
    {
        0 => "nenhuma multa",
        1 => "1 multa",
        _ => $"{Dados.Multas} multas"
    };

    public string TextoRecursos => Dados.RecursosAbertos switch
    {
        0 => string.Empty,
        1 => "1 recurso em andamento",
        _ => $"{Dados.RecursosAbertos} recursos em andamento"
    };

    public bool TemRecursos => Dados.RecursosAbertos > 0;

    /// <summary>Falta dado que o texto do recurso usa (RF-52).</summary>
    public bool FaltaDado => Dados.FaltaDadoDoRecurso;

    /// <summary>O que exatamente falta, para o aviso dizer o que pedir ao cliente.</summary>
    public string QueFalta
    {
        get
        {
            var faltando = new List<string>();

            if (string.IsNullOrWhiteSpace(Dados.MarcaModelo)) faltando.Add("marca/modelo");
            if (string.IsNullOrWhiteSpace(Dados.Cor)) faltando.Add("cor");
            if (string.IsNullOrWhiteSpace(Dados.Ano)) faltando.Add("ano");
            if (string.IsNullOrWhiteSpace(Dados.Renavam)) faltando.Add("RENAVAM");

            return faltando.Count == 0 ? string.Empty : $"falta {string.Join(", ", faltando)}";
        }
    }

    public void Preencher(HistoricoDoVeiculo historico)
    {
        Donos.Clear();
        foreach (var dono in historico.Donos)
        {
            Donos.Add(new DonoViewModel(dono));
        }

        Multas.Clear();
        foreach (var multa in historico.Multas)
        {
            Multas.Add(new MultaDoVeiculoViewModel(multa));
        }

        JaCarregou = true;

        OnPropertyChanged(nameof(SemDonos));
        OnPropertyChanged(nameof(SemMultas));
    }
}

/// <summary>Um cliente ligado ao carro, e em que papel.</summary>
public class DonoViewModel(DonoDoVeiculo dados)
{
    public int ClienteId { get; } = dados.ClienteId;
    public string Nome { get; } = dados.Nome;
    public string Cpf { get; } = Formato.Cpf(dados.Cpf);
    public string Whatsapp { get; } = Formato.Telefone(dados.Whatsapp);
    public bool TemWhatsapp { get; } = !string.IsNullOrWhiteSpace(dados.Whatsapp);

    public string Papel { get; } = dados.EhProprietario ? "proprietário" : "condutor indicado";

    // O dono é o que responde "de quem é esta placa"; o condutor fica em tom menor.
    public string CorDoPapel { get; } = dados.EhProprietario ? "PrimariaEscura" : "TextoFraco";
    public string FundoDoPapel { get; } = dados.EhProprietario ? "PrimariaClara" : "Fundo";
}

/// <summary>Uma multa do carro, com quem contratou o recurso dela, se alguém contratou.</summary>
public class MultaDoVeiculoViewModel(MultaDoVeiculo dados)
{
    public MultaDoVeiculo Dados { get; } = dados;

    public int? ClienteId { get; } = dados.ClienteId;
    public string Ait { get; } = dados.Ait;

    public string Descricao { get; } =
        string.IsNullOrWhiteSpace(dados.Descricao) ? "sem descrição" : dados.Descricao!;

    public string Quando { get; } = dados.DataHoraInfracao is { } data
        ? Formato.Data(data)
        : "sem data";

    public string Valor { get; } = dados.Valor is { } valor ? Formato.Dinheiro(valor) : string.Empty;
    public bool TemValor { get; } = dados.Valor is not null;

    public string Gravidade { get; } = Rotulos.Gravidade(dados.Gravidade, dados.Pontos);
    public bool TemGravidade { get; } = !string.IsNullOrWhiteSpace(dados.Gravidade);

    public string FundoDaGravidade { get; } = Rotulos.CoresDaGravidade(dados.Gravidade).Fundo;
    public string CorDaGravidade { get; } = Rotulos.CoresDaGravidade(dados.Gravidade).Texto;

    /// <summary>De quem foi o recurso. Multa sem serviço contratado não tem cliente.</summary>
    public string DeQuem { get; } = dados.ClienteNome ?? "sem recurso contratado";
    public bool TemCliente { get; } = dados.ClienteId is not null;

    /// <summary>Em que pé está o recurso, quando existe um.</summary>
    public string Andamento { get; } = dados.TemRecurso
        ? $"{Rotulos.Fase(dados.Fase)} · {Rotulos.Situacao(dados.Situacao)}"
        : string.Empty;

    public bool TemRecurso { get; } = dados.TemRecurso;

    public string Prazo { get; } = dados.PrazoQueVale is { } prazo ? Formato.Data(prazo) : string.Empty;
    public bool TemPrazo { get; } = dados.PrazoQueVale is not null;

    /// <summary>Prazo vencendo hoje ou antes, e a fase ainda não terminou.</summary>
    public bool Urgente { get; } =
        dados.PrazoQueVale is { } quando
        && quando.Date <= DateTime.Today
        && !Rotulos.Encerrada(dados.Situacao);
}
