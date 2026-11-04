using System;

namespace GestorRecursos.Desktop.Servicos;

/// <summary>O tom da notificação. Decide a cor, e só.</summary>
public enum TomDaNotificacao
{
    /// <summary>Deu certo: salvou, copiou, importou.</summary>
    Sucesso,

    /// <summary>Aconteceu, e convém saber. Nada quebrou.</summary>
    Informacao,

    /// <summary>Não deu certo, mas não é erro de tela inteira.</summary>
    Problema
}

/// <summary>Avisa a janela de que algo aconteceu, para ela mostrar o aviso flutuante.</summary>
/// <remarks>
/// Mesmo arranjo da <see cref="AreaDeTransferencia"/>, e pelo mesmo motivo: o aviso
/// pertence à janela, e o ViewModel não conhece janela nenhuma. A janela se inscreve ao
/// abrir e desenha o que chegar, venha de qual tela vier.
///
/// <b>Quando usar.</b> Confirmação de ação concluída que não exige nada de quem leu:
/// "Copiado", "Documento salvo", "3 cadastros importados". O aviso some em 3 segundos.
///
/// <b>Quando não usar.</b> Texto que precisa ser lido com calma, copiado ou seguido —
/// senha provisória, código de recuperação, lista do que falta corrigir. Isso mora na
/// tela, onde fica até a pessoa resolver.
/// </remarks>
public static class Notificacoes
{
    public static event Action<string, TomDaNotificacao>? Chegou;

    public static void Sucesso(string texto) => Mostrar(texto, TomDaNotificacao.Sucesso);

    public static void Informar(string texto) => Mostrar(texto, TomDaNotificacao.Informacao);

    public static void Problema(string texto) => Mostrar(texto, TomDaNotificacao.Problema);

    private static void Mostrar(string texto, TomDaNotificacao tom)
    {
        if (!string.IsNullOrWhiteSpace(texto))
        {
            Chegou?.Invoke(texto, tom);
        }
    }
}
