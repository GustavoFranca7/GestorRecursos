using GestorRecursos.Core.Data;   // EnderecoDAO
using GestorRecursos.Core.Models; // Entidade Endereco
using System;

// Camada de apresentacao: telas do terminal.
namespace GestorRecursos.ConsoleApp.Views
{
    public class EnderecoView
    {
        private EnderecoDAO _enderecoDao = new EnderecoDAO();

        public void ExibirEnderecoDoCliente(int clienteId)
        {
            Endereco endereco = _enderecoDao.BuscarPorClienteId(clienteId);

            Console.WriteLine("\n--- ENDEREÇO DO CLIENTE ---");
            if (endereco != null)
            {
                Console.WriteLine($"Logradouro: {endereco.Logradouro}, Nº {endereco.Numero}");
                Console.WriteLine($"Cidade/UF:  {endereco.Cidade}/{endereco.Estado}");
                Console.WriteLine($"CEP:        {endereco.Cep}");
            }
            else
            {
                Console.WriteLine("[AVISO] Nenhum endereço localizado para este ID de cliente.");
            }
        }

        public void EditarEnderecoDoCliente(int clienteId)
        {
            Console.Clear();
            Console.WriteLine($"--- EDITANDO ENDEREÇO DO CLIENTE ID: {clienteId} ---");

            // Busca o endereço atual usando a instância do DAO que já deve existir na sua View
            Endereco enderecoAtual = _enderecoDao.BuscarPorClienteId(clienteId);

            if (enderecoAtual == null)
            {
                Console.WriteLine("\n[AVISO] Nenhum endereço encontrado para este cliente.");
                Console.WriteLine("Pressione qualquer tecla para voltar...");
                Console.ReadKey();
                return;
            }

            Console.WriteLine("Dica: Pressione ENTER sem digitar nada para manter o valor atual.\n");

            // 1. Logradouro
            Console.WriteLine($"Logradouro Atual: {enderecoAtual.Logradouro}");
            Console.Write("Novo Logradouro: ");
            string novoLogradouro = Console.ReadLine()?.Trim();
            if (!string.IsNullOrEmpty(novoLogradouro))
            {
                enderecoAtual.Logradouro = novoLogradouro;
            }

            // 2. Número
            Console.WriteLine($"\nNúmero Atual: {enderecoAtual.Numero}");
            Console.Write("Novo Número: ");
            string novoNumero = Console.ReadLine()?.Trim();
            if (!string.IsNullOrEmpty(novoNumero))
            {
                enderecoAtual.Numero = novoNumero;
            }

            // 3. Cidade
            Console.WriteLine($"\nCidade Atual: {enderecoAtual.Cidade}");
            Console.Write("Nova Cidade: ");
            string novaCidade = Console.ReadLine()?.Trim();
            if (!string.IsNullOrEmpty(novaCidade))
            {
                enderecoAtual.Cidade = novaCidade;
            }

            // 4. Estado
            Console.WriteLine($"\nEstado Atual: {enderecoAtual.Estado}");
            Console.Write("Novo Estado: ");
            string novoEstado = Console.ReadLine()?.Trim();
            if (!string.IsNullOrEmpty(novoEstado))
            {
                enderecoAtual.Estado = novoEstado;
            }

            // 5. CEP
            Console.WriteLine($"\nCEP Atual: {enderecoAtual.Cep}");
            Console.Write("Novo CEP: ");
            string novoCep = Console.ReadLine()?.Trim();
            if (!string.IsNullOrEmpty(novoCep))
            {
                enderecoAtual.Cep = novoCep;
            }

            // Persiste no banco de dados via DAO
            bool sucesso = _enderecoDao.Atualizar(enderecoAtual);

            if (sucesso)
            {
                Console.WriteLine("\n[SUCESSO] Endereço atualizado com sucesso!");
            }
            else
            {
                Console.WriteLine("\n[ERRO] Não foi possível atualizar o endereço.");
            }

            Console.WriteLine("Pressione qualquer tecla para voltar...");
            Console.ReadKey();
        }
    }
}