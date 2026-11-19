namespace OsSimulator.Core;

/// <summary>
/// Uma instrução do "programa" de uma thread. Cada thread executa uma lista de operações em ordem.
/// Nas próximas etapas surgirão novos tipos: E/S (etapa 4), memória (etapa 5) e arquivo (etapa 6).
/// </summary>
public abstract record Operacao { }

/// <summary>Surto de CPU: a thread usa a CPU por <paramref name="Duracao"/> unidades de tempo lógico.</summary>
public sealed record OperacaoCpu(int Duracao) : Operacao;
