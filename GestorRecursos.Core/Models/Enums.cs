namespace GestorRecursos.Core.Models;

// Camada Core / Models: os domínios fechados do sistema.
//
// Cada um espelha um CHECK do banco. Existem como enum, e não como string,
// para que um valor errado seja erro de compilação e não erro de execução.
// O texto que vai para o banco está no atributo, porque nem todo valor cabe
// num nome de membro C# (AGUARDANDO_PROTOCOLO_JARI, por exemplo).

[AttributeUsage(AttributeTargets.Field)]
public sealed class ValorBancoAttribute(string valor) : Attribute
{
    public string Valor { get; } = valor;
}

public enum VinculoVeiculo
{
    [ValorBanco("PROPRIETARIO")] Proprietario,
    [ValorBanco("CONDUTOR")] Condutor
}

public enum GravidadeMulta
{
    [ValorBanco("LEVE")] Leve,
    [ValorBanco("MEDIA")] Media,
    [ValorBanco("GRAVE")] Grave,
    [ValorBanco("GRAVISSIMA")] Gravissima
}

public enum TipoProcessoCnh
{
    [ValorBanco("SUSPENSAO")] Suspensao,
    [ValorBanco("CASSACAO")] Cassacao
}

public enum SituacaoRegistro
{
    [ValorBanco("ANDAMENTO")] Andamento,
    [ValorBanco("CONCLUIDO")] Concluido,
    [ValorBanco("CANCELADO")] Cancelado
}

public enum FaseInstancia
{
    [ValorBanco("AUTUACAO")] Autuacao,
    [ValorBanco("JARI")] Jari,
    [ValorBanco("CETRAN")] Cetran
}

public enum SituacaoInstancia
{
    [ValorBanco("CONTRATADO")] Contratado,
    [ValorBanco("AGUARDANDO_DOCUMENTO")] AguardandoDocumento,
    [ValorBanco("AGUARDANDO_PROTOCOLO_JARI")] AguardandoProtocoloJari,
    [ValorBanco("AGUARDANDO_PROTOCOLO_CETRAN")] AguardandoProtocoloCetran,
    [ValorBanco("PROTOCOLADO")] Protocolado,
    [ValorBanco("AGUARDANDO_JULGAMENTO")] AguardandoJulgamento,
    [ValorBanco("DEFERIDO")] Deferido,
    [ValorBanco("INDEFERIDO")] Indeferido,
    [ValorBanco("CANCELADO")] Cancelado,
    [ValorBanco("DESISTENCIA")] Desistencia,
    [ValorBanco("PRAZO_PERDIDO")] PrazoPerdido
}

public enum ResultadoInstancia
{
    [ValorBanco("DEFERIDO")] Deferido,
    [ValorBanco("INDEFERIDO")] Indeferido
}

public enum FormaPagamento
{
    [ValorBanco("DINHEIRO")] Dinheiro,
    [ValorBanco("PIX")] Pix,
    [ValorBanco("PIX_PARCELADO")] PixParcelado,
    [ValorBanco("DEBITO")] Debito,
    [ValorBanco("CREDITO")] Credito
}

public enum TipoCompromisso
{
    [ValorBanco("PRAZO")] Prazo,
    [ValorBanco("ATENDIMENTO")] Atendimento,
    [ValorBanco("PROVA")] Prova,
    [ValorBanco("VISTORIA")] Vistoria,
    [ValorBanco("DETRAN")] Detran,
    [ValorBanco("TAREFA")] Tarefa,
    [ValorBanco("OUTRO")] Outro
}

public enum Recorrencia
{
    [ValorBanco("NENHUMA")] Nenhuma,
    [ValorBanco("SEMANAL")] Semanal,
    [ValorBanco("QUINZENAL")] Quinzenal,
    [ValorBanco("MENSAL")] Mensal
}

/// <summary>Traduz enum para o texto que o banco guarda, e de volta.</summary>
public static class ValorBanco
{
    // O reflection roda uma vez por valor e fica em cache: o custo some depois
    // da primeira leitura, e o mapa continua declarado junto do enum.
    private static readonly Dictionary<Enum, string> ParaTexto = [];
    private static readonly Dictionary<Type, Dictionary<string, Enum>> ParaEnum = [];
    private static readonly Lock Trava = new();

    public static string Texto(this Enum valor)
    {
        lock (Trava)
        {
            if (ParaTexto.TryGetValue(valor, out var existente))
            {
                return existente;
            }

            var campo = valor.GetType().GetField(valor.ToString())!;
            var atributo = (ValorBancoAttribute?)Attribute.GetCustomAttribute(campo, typeof(ValorBancoAttribute));
            var texto = atributo?.Valor ?? valor.ToString().ToUpperInvariant();

            ParaTexto[valor] = texto;
            return texto;
        }
    }

    public static T De<T>(string? texto) where T : struct, Enum
    {
        var valor = DeOuNulo<T>(texto);
        return valor ?? throw new ArgumentException(
            $"O valor '{texto}' não corresponde a nenhum {typeof(T).Name}. " +
            "Ou o banco tem um dado fora do domínio, ou falta um membro no enum.");
    }

    public static T? DeOuNulo<T>(string? texto) where T : struct, Enum
    {
        if (string.IsNullOrWhiteSpace(texto))
        {
            return null;
        }

        lock (Trava)
        {
            if (!ParaEnum.TryGetValue(typeof(T), out var mapa))
            {
                mapa = [];
                foreach (T item in Enum.GetValues<T>())
                {
                    mapa[Texto(item)] = item;
                }

                ParaEnum[typeof(T)] = mapa;
            }

            return mapa.TryGetValue(texto.Trim().ToUpperInvariant(), out var encontrado) ? (T)encontrado : null;
        }
    }
}
