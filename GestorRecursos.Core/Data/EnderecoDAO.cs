using GestorRecursos.Core.Models; // Entidade Endereco
using Oracle.ManagedDataAccess.Client;
using System;
using System.Collections.Generic;
using System.Text;

// Camada Core / Data: tudo que conversa diretamente com o Oracle.
namespace GestorRecursos.Core.Data
{
    public class EnderecoDAO
    {
        public Endereco BuscarPorClienteId(int clienteId)
        {
            string query = @"SELECT ID, CLIENTE_ID, LOGRADOURO, NUMERO, CIDADE, ESTADO, CEP 
                            FROM ENDERECOS 
                            WHERE CLIENTE_ID = :clienteId";

            using (OracleConnection conexao = Database.GetConnection())
            {
                conexao.Open();
                using (OracleCommand cmd = new OracleCommand(query, conexao))
                {
                    cmd.Parameters.Add(new OracleParameter("clienteId", clienteId));

                    using (OracleDataReader reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            return new Endereco
                            {
                                Id = reader.GetInt32(0),
                                ClienteId = reader.GetInt32(1),
                                Logradouro = reader.GetString(2),
                                Numero = reader.GetString(3),
                                Cidade = reader.GetString(4),
                                Estado = reader.GetString(5),
                                Cep = reader.GetString(6)
                            };
                        }
                    }
                }
            }

            return null; // Retorna null caso o cliente não tenha endereço cadastrado
        }

        public bool Atualizar(Endereco endereco)
        {
            string query = @"UPDATE ENDERECOS 
                    SET LOGRADOURO = :logradouro, 
                        NUMERO = :numero,                       
                        CIDADE = :cidade, 
                        ESTADO = :estado, 
                        CEP = :cep 
                    WHERE ID = :id";

            using (OracleConnection conexao = Database.GetConnection())
            {
                conexao.Open();
                using (OracleCommand cmd = new OracleCommand(query, conexao))
                {
                    cmd.Parameters.Add(new OracleParameter("logradouro", endereco.Logradouro));
                    cmd.Parameters.Add(new OracleParameter("numero", endereco.Numero));
                    cmd.Parameters.Add(new OracleParameter("cidade", endereco.Cidade));
                    cmd.Parameters.Add(new OracleParameter("estado", endereco.Estado));
                    cmd.Parameters.Add(new OracleParameter("cep", endereco.Cep));
                    cmd.Parameters.Add(new OracleParameter("id", endereco.Id));

                    int linhasAfetadas = cmd.ExecuteNonQuery();
                    return linhasAfetadas > 0;
                }
            }
        }
    }

}