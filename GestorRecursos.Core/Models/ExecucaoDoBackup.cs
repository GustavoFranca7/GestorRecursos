namespace GestorRecursos.Core.Models;

/// <summary>Uma execução do backup semanal, como ficou registrada no banco.</summary>
/// <remarks>
/// O backup grava a linha ao terminar; a tela lê a mais recente para saber se a cópia
/// de segurança continua acontecendo. Ver "Documentacao/1 Projeto/Rotina de backup.md".
/// </remarks>
public class ExecucaoDoBackup
{
    public int Id { get; init; }

    /// <summary>Quando terminou, no relógio da máquina que rodou.</summary>
    public DateTime Quando { get; init; }

    public string Maquina { get; init; } = string.Empty;
    public int Tabelas { get; init; }
    public int Linhas { get; init; }
    public long Bytes { get; init; }
    public int DocumentosNovos { get; init; }

    /// <summary>Documentos que não foram copiados. Maior que zero é acervo incompleto.</summary>
    public int Falhas { get; init; }

    /// <summary>Dias cheios desde a execução. Zero quando foi hoje.</summary>
    public int DiasAtras => (DateTime.Today - Quando.Date).Days;
}
