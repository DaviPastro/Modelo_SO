namespace OsSimulator.Core;

public sealed class MotorSimulacao
{
    private readonly IEscalonador _escalonador;
    private readonly FilaEventos _eventos = new();
    private readonly List<string> _registro = [];
    private BlocoControleThread? _threadEmExecucao;

    public MotorSimulacao(IEscalonador escalonador) => _escalonador = escalonador;

    public int Relogio { get; private set; }
    public IReadOnlyList<string> Registro => _registro;

    public void Executar(CargaTrabalho carga)
    {
        foreach (var processo in carga.Processos.OrderBy(p => p.TempoChegada).ThenBy(p => p.Identificador, StringComparer.Ordinal))
            _eventos.Enfileirar(new EventoSimulacao(processo.TempoChegada, TipoEvento.ChegadaProcesso, processo));

        while (_eventos.Quantidade > 0)
        {
            var proximo = _eventos.Retirar();
            Relogio = proximo.Tempo;
            Processar(proximo);
            DespacharSeOciosa();
        }
    }

    private void Processar(EventoSimulacao evento)
    {
        switch (evento.Tipo)
        {
            case TipoEvento.ChegadaProcesso:
                TratarChegada(evento.Processo);
                break;
            case TipoEvento.SurtoCpuConcluido:
                TratarConclusaoCpu(evento.Thread!);
                break;
        }
    }

    private void TratarChegada(BlocoControleProcesso processo)
    {
        processo.Estado = EstadoProcesso.Pronto;
        Registrar("CHEGADA_PROCESSO", processo.Identificador, "processo admitido e pronto");
        foreach (var thread in processo.Threads.OrderBy(t => t.Identificador, StringComparer.Ordinal))
        {
            thread.Estado = EstadoThread.Pronto;
            _escalonador.Enfileirar(thread);
            Registrar("THREAD_PRONTA", thread.Identificador, "thread adicionada à fila FCFS");
        }
    }

    private void DespacharSeOciosa()
    {
        if (_threadEmExecucao is not null) return;
        var proxima = _escalonador.SelecionarProxima();
        if (proxima is null) return;

        _threadEmExecucao = proxima;
        proxima.Estado = EstadoThread.Executando;
        proxima.Processo.Estado = EstadoProcesso.Executando;
        var surto = proxima.SurtosCpu[proxima.ContadorProgramaLogico];
        Registrar("DESPACHO", proxima.Identificador, $"CPU executará surto de {surto}");
        _eventos.Enfileirar(new EventoSimulacao(Relogio + surto, TipoEvento.SurtoCpuConcluido, proxima.Processo, proxima));
    }

    private void TratarConclusaoCpu(BlocoControleThread thread)
    {
        if (_threadEmExecucao != thread)
            throw new InvalidOperationException("Conclusão de CPU para thread que não está em execução.");

        thread.ContadorProgramaLogico++;
        thread.Processo.ContadorProgramaLogico++;
        _threadEmExecucao = null;

        if (thread.ContadorProgramaLogico < thread.SurtosCpu.Count)
        {
            thread.Estado = EstadoThread.Pronto;
            thread.Processo.Estado = EstadoProcesso.Pronto;
            _escalonador.Enfileirar(thread);
            Registrar("THREAD_PRONTA", thread.Identificador, "próximo surto de CPU retornou à fila FCFS");
            return;
        }

        thread.Estado = EstadoThread.Finalizado;
        Registrar("THREAD_FINALIZADA", thread.Identificador, "todos os surtos de CPU foram concluídos");

        if (thread.Processo.Threads.All(t => t.Estado == EstadoThread.Finalizado))
        {
            thread.Processo.Estado = EstadoProcesso.Finalizado;
            Registrar("PROCESSO_FINALIZADO", thread.Processo.Identificador, "todas as threads foram finalizadas");
        }
    }

    private void Registrar(string nomeEvento, string sujeito, string descricao) =>
        _registro.Add($"[{Relogio:D4}] {nomeEvento,-20} {sujeito,-8} {descricao}");
}
