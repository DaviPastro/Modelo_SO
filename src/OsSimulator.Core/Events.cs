namespace OsSimulator.Core;

public enum TipoEvento { ChegadaProcesso, FimTrocaContexto, FimSurtoCpu }

/// <summary>Algo que vai acontecer em um instante do clock lógico.</summary>
public sealed record EventoSimulacao(
    int Tempo,
    TipoEvento Tipo,
    BlocoControleProcesso Processo,
    BlocoControleThread? Thread = null);

/// <summary>
/// Fila de eventos ordenada pelo tempo. Em empate, sai primeiro quem foi inserido primeiro
/// (número de sequência), o que garante o determinismo da simulação.
/// </summary>
public sealed class FilaEventos
{
    private long _sequencia;
    private readonly PriorityQueue<EventoSimulacao, (int Tempo, long Sequencia)> _eventos = new();

    public int Quantidade => _eventos.Count;

    public void Enfileirar(EventoSimulacao evento) => _eventos.Enqueue(evento, (evento.Tempo, _sequencia++));

    public EventoSimulacao Retirar() => _eventos.Dequeue();
}

/// <summary>Uma linha do log: clock, tipo do evento, objeto envolvido e descrição.</summary>
public sealed record EntradaLog(int Tempo, string Tipo, string Sujeito, string Descricao)
{
    public override string ToString() => $"[{Tempo:D4}] {Tipo,-16} {Sujeito,-6} {Descricao}";
}
