namespace OsSimulator.Core;

/// <summary>
/// Política de escalonamento: decide QUAL thread pronta usa a CPU em seguida e POR QUANTO TEMPO.
/// O motor (mecanismo) apenas obedece: não sabe qual política está em uso (Tanenbaum, seção 3.6.6).
/// </summary>
public interface IEscalonador
{
    void Enfileirar(BlocoControleThread thread);
    BlocoControleThread? SelecionarProxima();
    bool PossuiThreadsProntas { get; }

    /// <summary>Fatia máxima de CPU por despacho. Nulo = política não preemptiva (roda até bloquear ou terminar).</summary>
    int? Quantum { get; }
}

/// <summary>FCFS (seção 2.4.4): a primeira thread que entrou na fila é a primeira a sair. Não preemptivo.</summary>
public sealed class EscalonadorFcfs : IEscalonador
{
    private readonly Queue<BlocoControleThread> _filaProntos = new();

    public bool PossuiThreadsProntas => _filaProntos.Count > 0;
    public int? Quantum => null;

    public void Enfileirar(BlocoControleThread thread) => _filaProntos.Enqueue(thread);

    public BlocoControleThread? SelecionarProxima() => _filaProntos.Count == 0 ? null : _filaProntos.Dequeue();
}

/// <summary>
/// Round Robin (seção 2.4.5): fila circular. Cada thread usa a CPU por no máximo um quantum;
/// se não terminou, volta para o FIM da fila.
/// </summary>
public sealed class EscalonadorRoundRobin : IEscalonador
{
    private readonly Queue<BlocoControleThread> _filaProntos = new();

    public EscalonadorRoundRobin(int quantum)
    {
        if (quantum <= 0) throw new ArgumentException("O quantum deve ser maior que zero.");
        Quantum = quantum;
    }

    public bool PossuiThreadsProntas => _filaProntos.Count > 0;
    public int? Quantum { get; }

    public void Enfileirar(BlocoControleThread thread) => _filaProntos.Enqueue(thread);

    public BlocoControleThread? SelecionarProxima() => _filaProntos.Count == 0 ? null : _filaProntos.Dequeue();
}

/// <summary>
/// Prioridades não preemptivo (seção 2.4.5): sai primeiro a thread de MENOR número de prioridade
/// (a prioridade vem do processo). Empate: quem entrou primeiro na fila (FIFO).
/// </summary>
public sealed class EscalonadorPrioridade : IEscalonador
{
    private long _sequencia;
    private readonly PriorityQueue<BlocoControleThread, (int Prioridade, long Sequencia)> _filaProntos = new();

    public bool PossuiThreadsProntas => _filaProntos.Count > 0;
    public int? Quantum => null;

    public void Enfileirar(BlocoControleThread thread) =>
        _filaProntos.Enqueue(thread, (thread.Processo.Prioridade, _sequencia++));

    public BlocoControleThread? SelecionarProxima() => _filaProntos.Count == 0 ? null : _filaProntos.Dequeue();
}

/// <summary>Cria o escalonador a partir do nome do algoritmo informado na configuração.</summary>
public static class FabricaEscalonador
{
    public static IEscalonador Criar(ConfiguracaoSimulacao configuracao) => configuracao.Algoritmo switch
    {
        "fcfs" => new EscalonadorFcfs(),
        "rr" => new EscalonadorRoundRobin(configuracao.Quantum),
        "prioridade" => new EscalonadorPrioridade(),
        _ => throw new ArgumentException($"Algoritmo desconhecido: '{configuracao.Algoritmo}'."),
    };
}
