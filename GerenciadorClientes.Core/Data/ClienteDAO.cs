using GerenciadorClientes.Core.Models; // Entidades Cliente e Endereco
using Oracle.ManagedDataAccess.Client;
using System;
using System.Collections.Generic;
using System.Text;


// Camada Core / Data: tudo que conversa diretamente com o Oracle.
namespace GerenciadorClientes.Core.Data
{
    // Classe responsável por realizar operações de persistência de dados (CRUD) para a entidade Cliente utilizando o Oracle.
    public class ClienteDAO
    {
        // Insere um novo cliente no banco de dados. O ID é gerado
        // pela sequência SEQ_CLIENTES.NEXTVAL definida no Oracle.
        public bool Cadastrar(Cliente cliente)
        {
            // Query serve para inserir um novo registro na tabela CLIENTES, utilizando parâmetros nomeados  
            string query = @"INSERT INTO CLIENTES (ID, NOME, CPF, EMAIL, TELEFONE) 
                            VALUES (SEQ_CLIENTES.NEXTVAL, :nome, :cpf, :email, :telefone)";
            
            // Abre uma conexão com o banco utilizando a fábrica Database.GetConnection()
            using (OracleConnection conexao = Database.GetConnection())
            {
                conexao.Open();

                // Prepara o comando com parâmetros nomeados para evitar SQL injection
                using (OracleCommand cmd = new OracleCommand(query, conexao))
                {
                    cmd.Parameters.Add(new OracleParameter("nome", cliente.Nome));
                    cmd.Parameters.Add(new OracleParameter("cpf", cliente.Cpf));
                    cmd.Parameters.Add(new OracleParameter("email", TextoOuNulo(cliente.Email)));
                    cmd.Parameters.Add(new OracleParameter("telefone", cliente.Telefone));

                    // Executa a inserção e retorna true se pelo menos uma linha foi afetada
                    int linhasAfetadas = cmd.ExecuteNonQuery();
                    return linhasAfetadas > 0;
                }
            }
        }

        //Recupera todos os clientes do banco, mapeando cada registro para
        // uma instância de Cliente e retornando-os em uma lista ordenada por ID.
        public List<Cliente> ListarTodos()
        {
            List<Cliente> lista = new List<Cliente>();
            string query = "SELECT ID, NOME, CPF, EMAIL, TELEFONE, DATA_CADASTRO FROM CLIENTES ORDER BY ID";

            // Abre conexão com o banco
            using (OracleConnection conexao = Database.GetConnection())
            {
                conexao.Open();

                using (OracleCommand cmd = new OracleCommand(query, conexao))
                {
                    // Executa o reader para percorrer os resultados
                    using (OracleDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            // Mapeia as colunas do SELECT para as propriedades do objeto Cliente
                            Cliente c = new Cliente
                            {
                                Id = reader.GetInt32(0),
                                Nome = reader.GetString(1),
                                Cpf = reader.GetString(2),
                                // E-mail e opcional: nulo no banco vira texto vazio
                                Email = reader.IsDBNull(3) ? string.Empty : reader.GetString(3),
                                // Trata valores nulos na coluna TELEFONE
                                Telefone = reader.IsDBNull(4) ? "Não informado" : reader.GetString(4),
                                DataCadastro = reader.GetDateTime(5)
                            };

                            lista.Add(c);
                        }
                    }
                }
            }

            return lista;
        }
        public bool CadastrarComEndereco(Cliente cliente)
        {
            string sqlCliente = @"INSERT INTO CLIENTES (ID, NOME, CPF, EMAIL, TELEFONE) 
                                 VALUES (SEQ_CLIENTES.NEXTVAL, :nome, :cpf, :email, :telefone) 
                                 RETURNING ID INTO :id";

            string sqlEndereco = @"INSERT INTO ENDERECOS (ID, CLIENTE_ID, LOGRADOURO, NUMERO, CIDADE, ESTADO, CEP) 
                                  VALUES (SEQ_ENDERECOS.NEXTVAL, :clienteId, :logradouro, :numero, :cidade, :estado, :cep)";

            using (OracleConnection conexao = Database.GetConnection())
            {
                conexao.Open();
                // Inicia uma transação no banco
                using (OracleTransaction transacao = conexao.BeginTransaction())
                {
                    try
                    {
                        int idClienteGerado;

                        // 1. Inserir Cliente e resgatar o ID gerado
                        using (OracleCommand cmdCliente = new OracleCommand(sqlCliente, conexao))
                        {
                            cmdCliente.Transaction = transacao;
                            cmdCliente.Parameters.Add(new OracleParameter("nome", cliente.Nome));
                            cmdCliente.Parameters.Add(new OracleParameter("cpf", cliente.Cpf));
                            cmdCliente.Parameters.Add(new OracleParameter("email", TextoOuNulo(cliente.Email)));
                            cmdCliente.Parameters.Add(new OracleParameter("telefone", cliente.Telefone));

                            // Configura parâmetro de SAÍDA (OUT) para capturar o ID
                            OracleParameter pId = new OracleParameter("id", OracleDbType.Int32)
                            {
                                Direction = System.Data.ParameterDirection.Output
                            };
                            cmdCliente.Parameters.Add(pId);

                            cmdCliente.ExecuteNonQuery();

                            // Recupera o valor que o Oracle gerou na Sequence
                            idClienteGerado = Convert.ToInt32(pId.Value.ToString());
                        }

                        // 2. Inserir Endereço usando o idClienteGerado
                        using (OracleCommand cmdEndereco = new OracleCommand(sqlEndereco, conexao))
                        {
                   
                            cmdEndereco.Transaction = transacao;
                            cmdEndereco.Parameters.Add(new OracleParameter("clienteId", idClienteGerado));
                            cmdEndereco.Parameters.Add(new OracleParameter("logradouro", cliente.Endereco.Logradouro));
                            cmdEndereco.Parameters.Add(new OracleParameter("numero", cliente.Endereco.Numero));
                            cmdEndereco.Parameters.Add(new OracleParameter("cidade", cliente.Endereco.Cidade));
                            cmdEndereco.Parameters.Add(new OracleParameter("estado", cliente.Endereco.Estado));
                            cmdEndereco.Parameters.Add(new OracleParameter("cep", cliente.Endereco.Cep));

                            cmdEndereco.ExecuteNonQuery();
                        }

                        // Confirma ambas as gravações no banco de dados
                        transacao.Commit();
                        return true;
                    }
                    catch (Exception)
                    {
                        // Desfaz tudo se houver qualquer erro no meio do caminho
                        transacao.Rollback();
                        throw;
                    }
                }
            }
        }
        public bool Atualizar(Cliente cliente)
        {
            string query = @"UPDATE CLIENTES 
                    SET NOME = :nome, 
                        CPF = :cpf,
                        EMAIL = :email, 
                        TELEFONE = :telefone 
                    WHERE ID = :id";

            using (OracleConnection conexao = Database.GetConnection())
            {
                conexao.Open();
                using (OracleCommand cmd = new OracleCommand(query, conexao))
                {
                    cmd.Parameters.Add(new OracleParameter("nome", cliente.Nome));
                    cmd.Parameters.Add(new OracleParameter("cpf", cliente.Cpf));
                    cmd.Parameters.Add(new OracleParameter("email", TextoOuNulo(cliente.Email)));
                    cmd.Parameters.Add(new OracleParameter("telefone", cliente.Telefone));
                    cmd.Parameters.Add(new OracleParameter("id", cliente.Id));

                    int linhasAfetadas = cmd.ExecuteNonQuery();
                    return linhasAfetadas > 0;
                }
            }
        }
        public Cliente BuscarPorId(int id)
        {
            string query = @"SELECT ID, NOME, CPF, EMAIL, TELEFONE, DATA_CADASTRO 
                    FROM CLIENTES 
                    WHERE ID = :id";

            using (OracleConnection conexao = Database.GetConnection())
            {
                conexao.Open();
                using (OracleCommand cmd = new OracleCommand(query, conexao))
                {
                    cmd.Parameters.Add(new OracleParameter("id", id));

                    using (OracleDataReader reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            return new Cliente
                            {
                                Id = reader.GetInt32(0),
                                Nome = reader.GetString(1),
                                Cpf = reader.GetString(2),
                                // E-mail e opcional: nulo no banco vira texto vazio
                                Email = reader.IsDBNull(3) ? string.Empty : reader.GetString(3),
                                Telefone = reader.IsDBNull(4) ? "Não informado" : reader.GetString(4),
                                DataCadastro = reader.GetDateTime(5)
                            };
                        }
                    }
                }
            }

            return null; // Retorna null se o ID não existir no banco
        }

        // Quantidade total de clientes, usada para calcular o numero de paginas.
        public int ContarTodos()
        {
            using (OracleConnection conexao = Database.GetConnection())
            {
                conexao.Open();
                using (OracleCommand cmd = new OracleCommand("SELECT COUNT(*) FROM CLIENTES", conexao))
                {
                    return Convert.ToInt32(cmd.ExecuteScalar());
                }
            }
        }

        // Busca so os clientes de uma pagina, ja com o endereco de cada um.
        // LEFT JOIN traz o cliente mesmo sem endereco; ROW_NUMBER garante um endereco por cliente.
        // OFFSET pula os clientes das paginas anteriores e FETCH NEXT limita a quantidade.
        public List<Cliente> ListarPagina(int pagina, int tamanhoPagina)
        {
            string query = @"SELECT C.ID, C.NOME, C.CPF, C.EMAIL, C.TELEFONE, C.DATA_CADASTRO,
                                    E.ID, E.LOGRADOURO, E.NUMERO, E.CIDADE, E.ESTADO, E.CEP
                             FROM CLIENTES C
                             LEFT JOIN (SELECT EN.*, ROW_NUMBER() OVER (PARTITION BY EN.CLIENTE_ID ORDER BY EN.ID) AS ORDEM
                                        FROM ENDERECOS EN) E
                                    ON E.CLIENTE_ID = C.ID AND E.ORDEM = 1
                             ORDER BY C.ID
                             OFFSET :pular ROWS FETCH NEXT :quantidade ROWS ONLY";

            List<Cliente> lista = new List<Cliente>();

            using (OracleConnection conexao = Database.GetConnection())
            {
                conexao.Open();
                using (OracleCommand cmd = new OracleCommand(query, conexao))
                {
                    // Liga os parametros pelo nome, e nao pela ordem em que foram adicionados.
                    cmd.BindByName = true;
                    cmd.Parameters.Add(new OracleParameter("pular", (pagina - 1) * tamanhoPagina));
                    cmd.Parameters.Add(new OracleParameter("quantidade", tamanhoPagina));

                    using (OracleDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            Cliente cliente = new Cliente
                            {
                                Id = reader.GetInt32(0),
                                Nome = reader.GetString(1),
                                Cpf = reader.GetString(2),
                                Email = reader.IsDBNull(3) ? string.Empty : reader.GetString(3),
                                Telefone = reader.IsDBNull(4) ? string.Empty : reader.GetString(4),
                                DataCadastro = reader.GetDateTime(5)
                            };

                            // Cliente sem endereco: as colunas do LEFT JOIN vem nulas.
                            if (!reader.IsDBNull(6))
                            {
                                cliente.Endereco = new Endereco
                                {
                                    Id = reader.GetInt32(6),
                                    ClienteId = cliente.Id,
                                    Logradouro = reader.GetString(7),
                                    Numero = reader.GetString(8),
                                    Cidade = reader.GetString(9),
                                    Estado = reader.GetString(10),
                                    Cep = reader.GetString(11)
                                };
                            }

                            lista.Add(cliente);
                        }
                    }
                }
            }

            return lista;
        }

        // Converte texto vazio em NULL para o banco (o e-mail e opcional).
        private static object TextoOuNulo(string? valor)
        {
            return string.IsNullOrWhiteSpace(valor) ? DBNull.Value : valor.Trim();
        }

        // Confere se o CPF ja existe antes de gravar (a coluna CPF e UNIQUE).
        public bool CpfJaCadastrado(string cpf)
        {
            string query = "SELECT COUNT(*) FROM CLIENTES WHERE CPF = :cpf";

            using (OracleConnection conexao = Database.GetConnection())
            {
                conexao.Open();
                using (OracleCommand cmd = new OracleCommand(query, conexao))
                {
                    cmd.Parameters.Add(new OracleParameter("cpf", cpf));
                    return Convert.ToInt32(cmd.ExecuteScalar()) > 0;
                }
            }
        }

        // Exclui um cliente e o endereço vinculado a ele de forma atômica.
        // A ordem das exclusões importa: o endereço (tabela filha) precisa sair
        // ANTES do cliente (tabela pai), senão a constraint FK_ENDERECO_CLIENTE
        // bloqueia a operação com o erro ORA-02292.
        public bool ExcluirComEndereco(int id)
        {
            // Apaga todos os endereços que pertencem ao cliente informado
            string sqlEndereco = "DELETE FROM ENDERECOS WHERE CLIENTE_ID = :clienteId";

            // Apaga o cliente propriamente dito
            string sqlCliente = "DELETE FROM CLIENTES WHERE ID = :id";

            using (OracleConnection conexao = Database.GetConnection())
            {
                conexao.Open();

                // Abre a transação: nada é gravado no banco até o Commit
                using (OracleTransaction transacao = conexao.BeginTransaction())
                {
                    try
                    {
                        // 1. Excluir o(s) endereço(s) do cliente
                        using (OracleCommand cmdEndereco = new OracleCommand(sqlEndereco, conexao))
                        {
                            // Amarra o comando à transação aberta acima
                            cmdEndereco.Transaction = transacao;
                            cmdEndereco.Parameters.Add(new OracleParameter("clienteId", id));

                            // Não validamos o retorno aqui: um cliente sem endereço
                            // cadastrado é uma situação válida (zero linhas afetadas)
                            cmdEndereco.ExecuteNonQuery();
                        }

                        // 2. Excluir o cliente
                        int linhasAfetadas;
                        using (OracleCommand cmdCliente = new OracleCommand(sqlCliente, conexao))
                        {
                            cmdCliente.Transaction = transacao;
                            cmdCliente.Parameters.Add(new OracleParameter("id", id));

                            // Aqui sim o retorno importa: indica se o cliente existia
                            linhasAfetadas = cmdCliente.ExecuteNonQuery();
                        }

                        // Se nenhum cliente foi removido, o ID não existia no banco.
                        // Desfaz a exclusão dos endereços para não deixar lixo no banco.
                        if (linhasAfetadas == 0)
                        {
                            transacao.Rollback();
                            return false;
                        }

                        // Confirma as duas exclusões de uma só vez
                        transacao.Commit();
                        return true;
                    }
                    catch (Exception)
                    {
                        // Qualquer falha no meio do caminho desfaz TUDO,
                        // devolvendo o banco ao estado anterior ao BeginTransaction
                        transacao.Rollback();
                        throw;
                    }
                }
            }
        }
    }
}
