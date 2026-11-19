namespace OsSimulator.Core;

/// <summary>
/// Parâmetros da simulação. Nas próximas etapas ganhará quantum, molduras, tempos dos dispositivos etc.
/// </summary>
public sealed record ConfiguracaoSimulacao(int CustoTrocaContexto = 1);
