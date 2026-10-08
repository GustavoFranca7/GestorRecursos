using System;
using System.Collections.Generic;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using GestorRecursos.Core.Models;

namespace GestorRecursos.Desktop.ViewModels;

/// <summary>Um selo colorido da coluna Situação.</summary>
/// <remarks>
/// Guarda o nome do recurso de cor, e não a cor: trocar a paleta continua sendo
/// mexer só em Styles/Cores.axaml.
/// </remarks>
public record SeloViewModel(string Texto, string ChaveFundo, string ChaveTexto);

/// <summary>Uma linha da lista de clientes, já pronta para a tela.</summary>
/// <remarks>
/// Os selos e o "urgente" são calculados uma vez, na montagem. Fazer isso em conversor
/// significaria recalcular a cada repintura, com a tela rolando.
/// </remarks>
public partial class LinhaClienteViewModel : ObservableObject
{
    public LinhaClienteViewModel(ResumoCliente dados)
    {
        Id = dados.Id;
        Nome = dados.Nome;
        CpfFormatado = Formato.Cpf(dados.Cpf);
        WhatsappFormatado = Formato.Telefone(dados.Whatsapp);
        Placas = (dados.Placas ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();

        TemValorEmAberto = dados.EmAberto > 0;
        EmAbertoTexto = TemValorEmAberto ? Formato.Dinheiro(dados.EmAberto) : "—";

        var diasAteOPrazo = dados.PrazoMaisProximo is { } prazo
            ? (int)(prazo.Date - DateTime.Today).TotalDays
            : (int?)null;

        Urgente = Prazos.Urgente(diasAteOPrazo);
        SeloPrazo = MontarSeloPrazo(diasAteOPrazo);
        SeloFase = MontarSeloFase(dados.FaseDoPrazo);
    }

    public int Id { get; }

    /// <summary>De onde o resultado veio, quando a linha nasceu de uma busca.</summary>
    /// <remarks>
    /// "AIT TST000005 - placa TST0005". Sem isso, quem busca por uma placa vê o nome do
    /// dono e fica sem saber qual das placas dele casou.
    /// </remarks>
    public string? Contexto { get; set; }

    public string Nome { get; }
    public string CpfFormatado { get; }
    public string WhatsappFormatado { get; }
    public IReadOnlyList<string> Placas { get; }
    public string EmAbertoTexto { get; }
    public bool TemValorEmAberto { get; }

    /// <summary>Coluna Prazo: quanto falta, ou "tudo em dia". Sempre tem um.</summary>
    public SeloViewModel SeloPrazo { get; }

    /// <summary>Coluna Fase: autuação, JARI ou CETRAN. Nulo quando não há recurso andando.</summary>
    public SeloViewModel? SeloFase { get; }

    /// <summary>Prazo estourando: ganha a faixa vermelha na borda esquerda.</summary>
    public bool Urgente { get; }

    [ObservableProperty]
    public partial bool Selecionado { get; set; }

    /// <summary>Quanto falta para o prazo mais próximo, na cor da urgência.</summary>
    private static SeloViewModel MontarSeloPrazo(int? diasAteOPrazo) =>
        diasAteOPrazo is { } dias
            ? Prazos.Selo(Prazos.Texto(dias), dias)
            : Prazos.Selo("tudo em dia", null);

    /// <summary>Em que instância o recurso está. Coluna própria, para não disputar com o prazo.</summary>
    private static SeloViewModel? MontarSeloFase(string? fase)
    {
        var nome = fase switch
        {
            "AUTUACAO" => "autuação",
            "JARI" => "JARI",
            "CETRAN" => "CETRAN",
            _ => null
        };

        return nome is null ? null : new SeloViewModel(nome, "RoxoClaro", "Roxo");
    }
}
