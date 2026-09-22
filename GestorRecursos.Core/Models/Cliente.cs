using System;
using System.Collections.Generic;
using System.Text;

// Camada Core / Models: entidades que representam as tabelas do banco.
namespace GestorRecursos.Core.Models
{
    public class Cliente
    {
        // Classe que representa um cliente no sistema, com propriedades correspondentes aos campos do banco de dados.
        // Get e Set funcionam como métodos de acesso para leitura e escrita das propriedades.
        public int Id { get; set; }
        public string Nome { get; set; }
        public string Cpf { get; set; }
        public string Email { get; set; }
        public string Telefone { get; set; }
        public DateTime DataCadastro { get; set; }

        // Propriedade de navegação para o Endereço
        public Endereco Endereco { get; set; }

        // Construtor padrão
        public Cliente() { }

        // Construtor auxiliar para novos cadastros
        // Este construtor é usado para criar um cliente com os dados fornecidos pelo usuário.
        public Cliente(string nome, string cpf, string email, string telefone)
        {
            Nome = nome;
            Cpf = cpf;
            Email = email;
            Telefone = telefone;

        }
    }
}