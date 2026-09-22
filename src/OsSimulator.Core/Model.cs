namespace OsSimulator.Core;

public enum EstadoProcesso { Novo, Pronto, Executando, Finalizado }
public enum EstadoThread { Novo, Pronto, Executando, Finalizado }

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
    public EstadoProcesso Estado { get; internal set; } = EstadoProcesso.Novo;
    public int ContadorProgramaLogico { get; internal set; }
    public List<BlocoControleThread> Threads { get; } = [];
}

public sealed class BlocoControleThread
{
    public BlocoControleThread(string identificador, BlocoControleProcesso processo, IReadOnlyList<int> surtosCpu)
    {
        Identificador = identificador;
        Processo = processo;
        SurtosCpu = surtosCpu;
    }

    public string Identificador { get; }
    public BlocoControleProcesso Processo { get; }
    public IReadOnlyList<int> SurtosCpu { get; }
    public EstadoThread Estado { get; internal set; } = EstadoThread.Novo;
    public int ContadorProgramaLogico { get; internal set; }
}

public sealed record CargaTrabalho(IReadOnlyList<BlocoControleProcesso> Processos);
