namespace OsSimulator.Core;

// Estados do ciclo de vida (Tanenbaum, seção 2.1.5).
// "Bloqueado" passa a ser usado na etapa de E/S (thread esperando um dispositivo ou uma página).
public enum EstadoProcesso { Novo, Pronto, Executando, Bloqueado, Finalizado }
public enum EstadoThread { Novo, Pronto, Executando, Bloqueado, Finalizado }

/// <summary>PCB: o que o "SO" guarda sobre cada processo.</summary>
public sealed class BlocoControleProcesso
{
    public BlocoControleProcesso(string identificador, int tempoChegada, int prioridade)
    {
        Identificador = identificador;
        TempoChegada = tempoChegada;
        Prioridade = prioridade;
    }

    public string Identificador { get; }
    public int TempoChegada { get; }
    public int Prioridade { get; }

    /// <summary>Instante lógico em que o processo foi admitido. Nulo enquanto ainda não chegou.</summary>
    public int? TempoCriacao { get; internal set; }

    public EstadoProcesso Estado { get; private set; } = EstadoProcesso.Novo;

    /// <summary>Contador de programa lógico do processo: total de operações já concluídas por suas threads.</summary>
    public int ContadorProgramaLogico { get; internal set; }

    public List<BlocoControleThread> Threads { get; } = [];

    /// <summary>
    /// O estado do processo é derivado do estado das suas threads.
    /// Retorna true se o estado mudou, para que o motor registre a mudança no log.
    /// </summary>
    internal bool AtualizarEstado()
    {
        var anterior = Estado;

        if (Threads.All(t => t.Estado == EstadoThread.Finalizado)) Estado = EstadoProcesso.Finalizado;
        else if (Threads.Any(t => t.Estado == EstadoThread.Executando)) Estado = EstadoProcesso.Executando;
        else if (Threads.Any(t => t.Estado == EstadoThread.Pronto)) Estado = EstadoProcesso.Pronto;
        else if (Threads.Any(t => t.Estado == EstadoThread.Bloqueado)) Estado = EstadoProcesso.Bloqueado;
        else Estado = EstadoProcesso.Novo;

        return Estado != anterior;
    }
}

/// <summary>TCB: o que o "SO" guarda sobre cada thread (a unidade que disputa a CPU).</summary>
public sealed class BlocoControleThread
{
    public BlocoControleThread(string identificador, BlocoControleProcesso processo, IReadOnlyList<Operacao> operacoes)
    {
        Identificador = identificador;
        Processo = processo;
        Operacoes = operacoes;
        PilhaLogica.Push(new ContextoCpu()); // contexto inicial: a thread começa na operação 0
    }

    public string Identificador { get; }
    public BlocoControleProcesso Processo { get; }
    public IReadOnlyList<Operacao> Operacoes { get; }
    public EstadoThread Estado { get; internal set; } = EstadoThread.Novo;

    /// <summary>
    /// Pilha lógica da thread. Quando a thread sai da CPU, seus registradores são empilhados aqui;
    /// quando volta, são desempilhados (Tanenbaum, seção 2.1.6).
    /// </summary>
    public Stack<ContextoCpu> PilhaLogica { get; } = new();
}

public sealed record CargaTrabalho(IReadOnlyList<BlocoControleProcesso> Processos);
