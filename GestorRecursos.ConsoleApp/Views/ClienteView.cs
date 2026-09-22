using GestorRecursos.Core.Data;   // ClienteDAO e EnderecoDAO
using GestorRecursos.Core.Models; // Entidades Cliente e Endereco
using System;
using System.Collections.Generic;
using System.Text;

// Camada de apresentacao: telas do terminal.
namespace GestorRecursos.ConsoleApp.Views
{
    // Classe responsável pela interface de console para operações relacionadas a Cliente.
    // Contém métodos para exibir a lista de clientes e cadastrar um novo cliente,
    // delegando a persistência para ClienteDAO.
    public class ClienteView
    {   
        private ClienteDAO _clienteDao = new ClienteDAO();// Instância do DAO que comunica com o banco (Oracle) para CRUD de clientes.
        private EnderecoView _enderecoView = new EnderecoView(); // Instância da View de Endereço
        private EnderecoDAO _enderecoDao = new EnderecoDAO(); // Instância do DAO de Endereço

        // Método: ExibirListaClientes
        // Descrição: Exibe no console todos os clientes cadastrados.
        public void ExibirListaClientes()
        {
            Console.Clear();
            Console.WriteLine("=========================================================================================================");
            Console.WriteLine("                                      LISTA DE CLIENTES CADASTRADOS                                      ");
            Console.WriteLine("=========================================================================================================");

            // Cabeçalho ajustado
            Console.WriteLine($"| {"ID",-4} | {"NOME",-15} | {"CPF",-14} | {"EMAIL",-28} | {"TELEFONE",-15} | {"CADASTRO",-10} |");
            Console.WriteLine("---------------------------------------------------------------------------------------------------------");

            List<Cliente> clientes = _clienteDao.ListarTodos();

            if (clientes.Count == 0)
            {
                Console.WriteLine("| Nenhum cliente encontrado no banco de dados.                                                           |");
            }
            else
            {
                foreach (var c in clientes)
                {
                    string nome = c.Nome.Length > 15 ? c.Nome.Substring(0, 12) + "..." : c.Nome;
                    string email = c.Email.Length > 28 ? c.Email.Substring(0, 25) + "..." : c.Email;
                    string dataFormatada = c.DataCadastro.ToString("dd/MM/yyyy");

                    // Correção: o ,-10 fica DENTRO das chaves da dataFormatada
                    Console.WriteLine($"| {c.Id,-4} | {nome,-15} | {c.Cpf,-14} | {email,-28} | {c.Telefone,-15} | {dataFormatada,-10} |");
                }
            }

            Console.WriteLine("=========================================================================================================");

            Console.Write("Deseja verificar o endereço de algum cliente? (S/N): ");
            string resposta = Console.ReadLine()?.Trim().ToUpper();

            if (resposta == "S")
            {
                bool pesquisarNovamente = true;

                while (pesquisarNovamente)
                {
                    Console.Write("\nDigite o ID do cliente: ");

                    // Validação de entrada para garantir que o ID seja um número inteiro
                    if (int.TryParse(Console.ReadLine(), out int idCliente))
                    {
                        Endereco endereco = _enderecoDao.BuscarPorClienteId(idCliente);

                        if (endereco != null)
                        {
                            _enderecoView.ExibirEnderecoDoCliente(idCliente);
                        }
                        else
                        {
                            Console.WriteLine($"\n[AVISO] Nenhum endereço localizado para o ID {idCliente}.");
                        }
                    }
                    else
                    {
                        Console.WriteLine("\n[ERRO] Por favor, digite um número de ID válido.");
                    }

                    // Pergunta centralizada no final de cada tentativa
                    Console.Write("\nDeseja pesquisar outro ID? (S/N): ");
                    string opcao = Console.ReadLine()?.Trim().ToUpper();

                    if (opcao != "S")
                    {
                        pesquisarNovamente = false;
                    }
                }
            }

            PausarERetornar();
        }

        // Método: ExecutarCadastrarCliente
        // Descrição: Executa o fluxo de cadastro de um novo cliente via console.
        public void ExecutarCadastrarCliente()
        {
            Console.Clear();
            Console.WriteLine("--- ADICIONAR NOVO CLIENTE E ENDEREÇO ---\n");

            // DADOS DO CLIENTE
            Console.Write("Nome: ");
            string nome = Console.ReadLine();

            Console.Write("CPF (apenas números): ");
            string cpf = Console.ReadLine();

            Console.Write("E-mail: ");
            string email = Console.ReadLine();

            Console.Write("Telefone: ");
            string telefone = Console.ReadLine();

            // DADOS DO ENDEREÇO
            Console.WriteLine("\n--- DADOS DE ENDEREÇO ---");
            Console.Write("Logradouro (Rua/Av): ");
            string logradouro = Console.ReadLine();

            Console.Write("Número: ");
            string numero = Console.ReadLine();

            Console.Write("Cidade: ");
            string cidade = Console.ReadLine();

            Console.Write("Estado (UF - ex: SC): ");
            string estado = Console.ReadLine();

            Console.Write("CEP: ");
            string cep = Console.ReadLine();

            // Montagem dos Objetos
            Cliente novoCliente = new Cliente(nome, cpf, email, telefone);
            novoCliente.Endereco = new Endereco(logradouro, numero, cidade, estado, cep);

            try
            {
                bool cadastrado = _clienteDao.CadastrarComEndereco(novoCliente);

                if (cadastrado)
                {
                    Console.WriteLine("\n[SUCESSO] Cliente e Endereço cadastrados juntos no Oracle!");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\n[ERRO] Falha ao cadastrar: {ex.Message}");
            }

            PausarERetornar();
        }
        public void EditarDadosCliente(Cliente clienteAtual)
        {
            Console.Clear();
            Console.WriteLine($"--- EDITANDO CLIENTE ID: {clienteAtual.Id} ---");
            Console.WriteLine("Dica: Pressione ENTER sem digitar nada para manter o valor atual.\n");

            // 1. Nome
            Console.WriteLine($"Nome Atual: {clienteAtual.Nome}");
            Console.Write("Novo Nome: ");
            string novoNome = Console.ReadLine()?.Trim();
            if (!string.IsNullOrEmpty(novoNome))
            {
                clienteAtual.Nome = novoNome;
            }

            // 2. CPF (Com opção de manter)
            Console.WriteLine($"\nCPF Atual: {clienteAtual.Cpf}");
            Console.Write("Novo CPF: ");
            string novoCpf = Console.ReadLine()?.Trim();
            if (!string.IsNullOrEmpty(novoCpf))
            {
                clienteAtual.Cpf = novoCpf;
            }

            // 3. E-mail
            Console.WriteLine($"\nE-mail Atual: {clienteAtual.Email}");
            Console.Write("Novo E-mail: ");
            string novoEmail = Console.ReadLine()?.Trim();
            if (!string.IsNullOrEmpty(novoEmail))
            {
                clienteAtual.Email = novoEmail;
            }

            // 4. Telefone
            Console.WriteLine($"\nTelefone Atual: {clienteAtual.Telefone}");
            Console.Write("Novo Telefone: ");
            string novoTelefone = Console.ReadLine()?.Trim();
            if (!string.IsNullOrEmpty(novoTelefone))
            {
                clienteAtual.Telefone = novoTelefone;
            }

            // Persiste no banco de dados via DAO
            bool sucesso = _clienteDao.Atualizar(clienteAtual);

            if (sucesso)
            {
                Console.WriteLine("\n[SUCESSO] Dados do cliente atualizados com sucesso!");
            }
            else
            {
                Console.WriteLine("\n[ERRO] Não foi possível atualizar os dados do cliente.");
            }
        }
        public void MenuEditar()
        {
            Console.Clear();
            Console.WriteLine("--- ATUALIZAÇÃO DE REGISTROS ---\n");
            Console.Write("Digite o ID do cliente que deseja editar: ");

            if (int.TryParse(Console.ReadLine(), out int idCliente))
            {
                Cliente cliente = _clienteDao.BuscarPorId(idCliente);

                if (cliente == null)
                {
                    Console.WriteLine($"\n[AVISO] Cliente com ID {idCliente} não foi encontrado.");
                    PausarERetornar();
                    return;
                }

                Console.WriteLine($"\nCliente selecionado: {cliente.Nome}");
                Console.WriteLine("1 - Editar Dados Pessoais (Nome, CPF, Email, Telefone)");
                Console.WriteLine("2 - Editar Endereço");
                Console.WriteLine("0 - Voltar");
                Console.Write("Escolha uma opção: ");

                string opcao = Console.ReadLine()?.Trim();

                switch (opcao)
                {
                    case "1":
                        EditarDadosCliente(cliente);
                        break;
                    case "2":
                        // Chamaremos o fluxo de edição da EnderecoView
                        _enderecoView.EditarEnderecoDoCliente(idCliente);
                        break;
                    case "0":
                        return;
                    default:
                        Console.WriteLine("\n[ERRO] Opção inválida!");
                        break;
                }
            }
            else
            {
                Console.WriteLine("\n[ERRO] Digite um ID numérico válido.");
            }

            PausarERetornar();
        }

        // Método: ExecutarExcluirCliente
        // Descrição: Executa o fluxo de exclusão de um cliente via console.
        // Antes de apagar, exibe os dados encontrados e pede confirmação explícita,
        // já que a exclusão é uma operação irreversível.
        public void ExecutarExcluirCliente()
        {
            Console.Clear();
            Console.WriteLine("--- EXCLUSÃO DE CLIENTE ---\n");
            Console.Write("Digite o ID do cliente que deseja excluir: ");

            // Validação de entrada: o ID precisa ser um número inteiro
            if (!int.TryParse(Console.ReadLine(), out int idCliente))
            {
                Console.WriteLine("\n[ERRO] Digite um ID numérico válido.");
                PausarERetornar();
                return;
            }

            // Busca o cliente para confirmar que ele existe antes de qualquer exclusão
            Cliente cliente = _clienteDao.BuscarPorId(idCliente);

            if (cliente == null)
            {
                Console.WriteLine($"\n[AVISO] Cliente com ID {idCliente} não foi encontrado.");
                PausarERetornar();
                return;
            }

            // Exibe os dados do cliente para o usuário conferir o que será apagado
            Console.WriteLine("\n--- DADOS QUE SERÃO EXCLUÍDOS ---");
            Console.WriteLine($"ID:       {cliente.Id}");
            Console.WriteLine($"Nome:     {cliente.Nome}");
            Console.WriteLine($"CPF:      {cliente.Cpf}");
            Console.WriteLine($"E-mail:   {cliente.Email}");
            Console.WriteLine($"Telefone: {cliente.Telefone}");

            // Busca o endereço para avisar que ele também será removido junto
            Endereco endereco = _enderecoDao.BuscarPorClienteId(idCliente);

            if (endereco != null)
            {
                Console.WriteLine($"Endereço: {endereco.Logradouro}, {endereco.Numero} - " +
                                  $"{endereco.Cidade}/{endereco.Estado} - CEP: {endereco.Cep}");
                Console.WriteLine("\n[ATENÇÃO] O endereço acima também será excluído.");
            }
            else
            {
                Console.WriteLine("Endereço: (nenhum endereço cadastrado)");
            }

            // Confirmação obrigatória: qualquer resposta diferente de "S" cancela
            Console.Write("\nConfirma a exclusão? Esta ação NÃO pode ser desfeita. (S/N): ");
            string confirmacao = Console.ReadLine()?.Trim().ToUpper();

            if (confirmacao != "S")
            {
                Console.WriteLine("\n[CANCELADO] Nenhum registro foi excluído.");
                PausarERetornar();
                return;
            }

            try
            {
                // Delega a exclusão ao DAO, que executa tudo dentro de uma transação
                bool excluido = _clienteDao.ExcluirComEndereco(idCliente);

                if (excluido)
                {
                    Console.WriteLine($"\n[SUCESSO] Cliente ID {idCliente} excluído do Oracle!");
                }
                else
                {
                    Console.WriteLine("\n[ERRO] Não foi possível excluir o cliente.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\n[ERRO] Falha ao excluir: {ex.Message}");
            }

            PausarERetornar();
        }

        // Método auxiliar: PausarERetornar
        // Descrição: Exibe uma mensagem e aguarda o usuário pressionar uma tecla.
        private void PausarERetornar()
        {
            Console.WriteLine("\nPressione qualquer tecla para retornar ao menu principal...");
            Console.ReadKey();
        }
    }
}
