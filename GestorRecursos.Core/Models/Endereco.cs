using System;
using System.Collections.Generic;
using System.Text;

// Camada Core / Models: entidades que representam as tabelas do banco.
namespace GestorRecursos.Core.Models
{
    public class Endereco
    {
        public int Id { get; set; }
        public int ClienteId { get; set; }
        public string Logradouro { get; set; }
        public string Numero { get; set; }
        public string Cidade { get; set; }
        public string Estado { get; set; }
        public string Cep { get; set; }

        public Endereco() { }

        public Endereco(string logradouro, string numero, string cidade, string estado, string cep)
        {
            Logradouro = logradouro;
            Numero = numero;
            Cidade = cidade;
            Estado = estado;
            Cep = cep;
        }
    }
}