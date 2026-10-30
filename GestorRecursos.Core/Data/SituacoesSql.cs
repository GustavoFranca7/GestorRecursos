namespace GestorRecursos.Core.Data;

// Camada Core / Data: pedaços de SQL que mais de um DAO precisa escrever igual.
//
// Existe por causa de um erro real: quando o resultado "anulado" entrou, a lista de
// situações encerradas estava copiada em dois DAOs e nenhuma das duas foi atualizada —
// uma fase anulada continuava cobrando prazo na tela. Lista copiada é lista que diverge.
internal static class SituacoesSql
{
    /// <summary>Instância que não anda mais: ganhou, perdeu, caiu ou saiu do caminho.</summary>
    /// <remarks>
    /// Serve para não gritar prazo em caso resolvido. O espelho desta regra na interface
    /// é <c>Rotulos.Encerrada</c>; as duas precisam listar o mesmo.
    /// </remarks>
    public const string InstanciaEncerrada =
        "('DEFERIDO','INDEFERIDO','ANULADO','CANCELADO','DESISTENCIA','PRAZO_PERDIDO')";
}
