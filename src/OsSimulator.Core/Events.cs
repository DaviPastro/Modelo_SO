namespace OsSimulator.Core;

public enum TipoEvento { ChegadaProcesso, SurtoCpuConcluido }

public sealed record EventoSimulacao(
    int Tempo,
    TipoEvento Tipo,
    BlocoControleProcesso Processo,
    BlocoControleThread? Thread = null);

public sealed class FilaEventos
{
    private long _sequencia;
    private readonly PriorityQueue<EventoSimulacao, (int Tempo, long Sequencia, string Identificador)> _eventos = new();

    public int Quantidade => _eventos.Count;

    public void Enfileirar(EventoSimulacao evento)
    {
        var identificador = evento.Thread?.Identificador ?? evento.Processo.Identificador;
        _eventos.Enqueue(evento, (evento.Tempo, _sequencia++, identificador));
    }

    public EventoSimulacao Retirar() => _eventos.Dequeue();
}
