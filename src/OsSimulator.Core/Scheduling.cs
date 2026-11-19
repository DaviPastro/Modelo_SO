namespace OsSimulator.Core;

/// <summary>Política de escalonamento: decide qual thread pronta usa a CPU em seguida.</summary>
public interface IEscalonador
{
    void Enfileirar(BlocoControleThread thread);
    BlocoControleThread? SelecionarProxima();
    bool PossuiThreadsProntas { get; }
}

/// <summary>FCFS: a primeira thread que entrou na fila de prontos é a primeira a sair.</summary>
public sealed class EscalonadorFcfs : IEscalonador
{
    private readonly Queue<BlocoControleThread> _filaProntos = new();

    public bool PossuiThreadsProntas => _filaProntos.Count > 0;

    public void Enfileirar(BlocoControleThread thread) => _filaProntos.Enqueue(thread);

    public BlocoControleThread? SelecionarProxima() => _filaProntos.Count == 0 ? null : _filaProntos.Dequeue();
}

/// <summary>Cria o escalonador a partir do nome do algoritmo informado na configuração.</summary>
public static class FabricaEscalonador
{
    public static IEscalonador Criar(ConfiguracaoSimulacao configuracao) => configuracao.Algoritmo switch
    {
        "fcfs" => new EscalonadorFcfs(),
        _ => throw new ArgumentException($"O algoritmo '{configuracao.Algoritmo}' ainda não foi implementado."),
    };
}
