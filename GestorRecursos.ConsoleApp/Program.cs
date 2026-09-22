using GestorRecursos.ConsoleApp.Views; // Telas do terminal
using System;
using System.Collections.Generic;
using System.Text;

// Ponto de entrada da aplicacao de console.
namespace GestorRecursos.ConsoleApp
{
    class Program
    {
        private static ClienteView _clienteView = new ClienteView();

        static void Main(string[] args)
        {
            bool executar = true;

            while (executar)
            {
                Console.Clear();
                Console.WriteLine("=========================================");
                Console.WriteLine("        GERENCIADOR DE CLIENTES          ");
                Console.WriteLine("=========================================");
                Console.WriteLine("1 - Listar Clientes");
                Console.WriteLine("2 - Cadastrar Novo Cliente");
                Console.WriteLine("3 - Atualizar Cliente/Endereço");
                Console.WriteLine("4 - Excluir Cliente");
                Console.WriteLine("0 - Sair");
                Console.WriteLine("=========================================");
                Console.Write("Escolha uma opção: ");

                string opcao = Console.ReadLine()?.Trim();

                switch (opcao)
                {
                    case "1":
                        _clienteView.ExibirListaClientes(); // Chamada de exibição de clientes com pergunta para exibir endereço.
                        break;
                    case "2":
                        _clienteView.ExecutarCadastrarCliente(); // Chamada de cadastro de clientes com endereços.
                        break;
                    case "3":
                        _clienteView.MenuEditar(); // Chamada do menu de edição.
                        break;
                    case "4":
                        _clienteView.ExecutarExcluirCliente(); // Chamada de exclusão.
                        break;
                    case "0":
                        executar = false;
                        Console.WriteLine("\nSaindo do sistema...");
                        break;
                    default:
                        Console.WriteLine("\nOpção inválida! Pressione qualquer tecla para continuar...");
                        Console.ReadKey();
                        break;
                }
            }
        }
    }
}
