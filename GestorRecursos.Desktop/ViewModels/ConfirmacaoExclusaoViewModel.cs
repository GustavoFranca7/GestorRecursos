using System;
using System.Collections.Generic;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GestorRecursos.Core.Data;
using GestorRecursos.Core.Models;

namespace GestorRecursos.Desktop.ViewModels;

// Janela que pergunta antes de excluir. Serve para um cliente (botao X) ou varios (lixeira).
public partial class ConfirmacaoExclusaoViewModel : ViewModelBase
{
    private readonly ClienteDAO _clienteDao;

    // Avisos para a tela principal: excluiu (com a quantidade) ou fechou.
    private readonly Action<int> _aoExcluir;
    private readonly Action _aoFechar;

    public ConfirmacaoExclusaoViewModel(ClienteDAO clienteDao, IReadOnlyList<Cliente> clientes,
                                        Action<int> aoExcluir, Action aoFechar)
    {
        _clienteDao = clienteDao;
        _aoExcluir = aoExcluir;
        _aoFechar = aoFechar;
        Clientes = clientes;
    }

    // Construtor vazio so para o preview da IDE conseguir desenhar a tela.
    public ConfirmacaoExclusaoViewModel() : this(new ClienteDAO(),
        new[] { new Cliente("Maria Souza", "12345678901", "", "47999990000") { Id = 1 } }, _ => { }, () => { })
    {
    }

    public IReadOnlyList<Cliente> Clientes { get; }

    private bool Varios => Clientes.Count > 1;

    public string Titulo => Varios ? $"EXCLUIR {Clientes.Count} CLIENTES" : "EXCLUIR CLIENTE";

    public string Pergunta => Varios
        ? "Tem certeza que deseja excluir os clientes abaixo?"
        : "Tem certeza que deseja excluir este cliente?";

    public string AvisoEndereco => Varios ? "Os enderecos tambem serao excluidos." : "O endereco tambem sera excluido.";

    public string TextoBotao => Varios ? $"EXCLUIR {Clientes.Count}" : "EXCLUIR";

    // Erro do banco, mostrado na propria janela. Nulo = sem erro.
    [ObservableProperty]
    public partial string? Erro { get; set; }

    [RelayCommand]
    private void Confirmar()
    {
        try
        {
            int excluidos = _clienteDao.ExcluirVariosComEndereco(Clientes.Select(c => c.Id));
            _aoExcluir(excluidos);
        }
        catch (Exception ex)
        {
            // Nada foi apagado (a transacao desfaz tudo); a janela continua aberta.
            Erro = $"ERRO AO EXCLUIR: {ex.Message}";
        }
    }

    [RelayCommand]
    private void Cancelar()
    {
        _aoFechar();
    }
}
