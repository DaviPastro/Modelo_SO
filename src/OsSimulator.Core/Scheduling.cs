namespace OsSimulator.Core;

public interface IEscalonador
{
    void Enfileirar(BlocoControleThread thread);
    BlocoControleThread? SelecionarProxima();
    bool PossuiThreadsProntas { get; }
}

public sealed class EscalonadorFcfs : IEscalonador
{
    private readonly Queue<BlocoControleThread> _filaProntos = new();

    public bool PossuiThreadsProntas => _filaProntos.Count > 0;

    public void Enfileirar(BlocoControleThread thread) => _filaProntos.Enqueue(thread);

    public BlocoControleThread? SelecionarProxima() => _filaProntos.Count == 0 ? null : _filaProntos.Dequeue();
}
