using System.Collections.Generic;
using GestorRecursos.Core.Models;

namespace GestorRecursos.Desktop.ViewModels;

/// <summary>Uma linha da lista de prazos do painel.</summary>
/// <remarks>
/// Os selos nascem prontos, como na lista de clientes: calcular cor em conversor
/// significaria refazer a conta a cada repintura, com a tela rolando.
/// </remarks>
public class LinhaPrazoViewModel
{
    public LinhaPrazoViewModel(PrazoDoDia dados)
    {
        ClienteId = dados.ClienteId;
        Nome = dados.ClienteNome;
        WhatsappFormatado = Formato.Telefone(dados.ClienteWhatsapp);
        Dias = dados.Dias;

        DataTexto = Formato.DiaCurto(dados.Prazo);
        SeloPrazo = Prazos.Selo(Prazos.Texto(dados.Dias), dados.Dias);
        SeloFase = new SeloViewModel(Rotulos.Fase(dados.Fase), "RoxoClaro", "Roxo");
        Urgente = Prazos.Urgente(dados.Dias);

        Servico = dados.Servico;
        Orgao = string.IsNullOrWhiteSpace(dados.Orgao) ? "órgão não definido" : dados.Orgao;

        // AIT e placa, quando é multa; o número do processo, quando é CNH. Sem isso
        // a linha diz "recurso de multa grave" e não diz de qual.
        Referencias = new List<string>();
        if (!string.IsNullOrWhiteSpace(dados.Placa)) Referencias.Add(dados.Placa);
        if (!string.IsNullOrWhiteSpace(dados.Origem)) Referencias.Add(dados.Origem);
    }

    public int ClienteId { get; }
    public int Dias { get; }

    public string Nome { get; }
    public string WhatsappFormatado { get; }
    public string DataTexto { get; }
    public string Servico { get; }
    public string Orgao { get; }

    /// <summary>Placa e AIT, ou o número do processo. Cada uma é um selo copiável.</summary>
    public List<string> Referencias { get; }

    public SeloViewModel SeloPrazo { get; }
    public SeloViewModel SeloFase { get; }

    /// <summary>Prazo estourando: ganha a faixa vermelha na borda esquerda.</summary>
    public bool Urgente { get; }
}
