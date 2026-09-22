using System.Collections.Generic;
using System.Linq;

// Camada Core / Validacao: regras de correcao e conferencia dos dados digitados.
// Ficam na Core para servirem tanto ao Desktop quanto ao ConsoleApp.
namespace GestorRecursos.Core.Validacao
{
    public static class Normalizacao
    {
        // As 27 siglas validas de estado.
        private static readonly HashSet<string> Ufs = new HashSet<string>
        {
            "AC", "AL", "AP", "AM", "BA", "CE", "DF", "ES", "GO", "MA", "MT", "MS", "MG", "PA",
            "PB", "PR", "PE", "PI", "RJ", "RN", "RS", "RO", "RR", "SC", "SP", "SE", "TO"
        };

        // Remove tudo que nao for numero: pontos, tracos, espacos, parenteses.
        public static string SomenteDigitos(string? texto)
        {
            return new string((texto ?? string.Empty).Where(c => c >= '0' && c <= '9').ToArray());
        }

        // Deixa o telefone no padrao DDD + 9 + numero, com 11 digitos.
        public static string Telefone(string? texto)
        {
            // Tira o zero da frente, como em "047..."
            string digitos = SomenteDigitos(texto).TrimStart('0');

            // Tira o codigo do pais (55) quando ele veio junto
            if (digitos.StartsWith("55") && digitos.Length >= 12)
            {
                digitos = digitos.Substring(2);
            }

            // Faltou o 9 depois do DDD
            if (digitos.Length == 10)
            {
                digitos = digitos.Insert(2, "9");
            }

            return digitos;
        }

        public static bool TelefoneValido(string telefone)
        {
            return telefone.Length == 11 && telefone[2] == '9';
        }

        // So letras, em maiusculo: "sc" vira "SC".
        public static string Uf(string? texto)
        {
            return new string((texto ?? string.Empty).Where(char.IsAsciiLetter).ToArray()).ToUpperInvariant();
        }

        public static bool UfValida(string uf)
        {
            return Ufs.Contains(uf);
        }
    }
}
