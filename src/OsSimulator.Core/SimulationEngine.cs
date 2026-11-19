namespace OsSimulator.Core;

/// <summary>
/// Núcleo da simulação: controla o clock lógico e a fila de eventos, e coordena CPU e escalonador.
/// </summary>
public sealed class MotorSimulacao
{
    private readonly IEscalonador _escalonador;
    private readonly ConfiguracaoSimulacao _configuracao;
    private readonly FilaEventos _eventos = new();
    private readonly List<EntradaLog> _registro = [];

    public MotorSimulacao(IEscalonador escalonador, ConfiguracaoSimulacao configuracao)
    {
        if (configuracao.CustoTrocaContexto < 0)
            throw new ArgumentException("O custo da troca de contexto não pode ser negativo.");
        _escalonador = escalonador;
        _configuracao = configuracao;
    }

    public int Relogio { get; private set; }
    public Cpu Cpu { get; } = new();
    public IReadOnlyList<EntradaLog> Registro => _registro;

    // ---------------------------------------------------------------- laço principal

    public void Executar(CargaTrabalho carga)
    {
        var processosOrdenados = carga.Processos
            .OrderBy(p => p.TempoChegada)
            .ThenBy(p => p.Identificador, StringComparer.Ordinal);

        foreach (var processo in processosOrdenados)
            _eventos.Enfileirar(new EventoSimulacao(processo.TempoChegada, TipoEvento.ChegadaProcesso, processo));

        while (_eventos.Quantidade > 0)
        {
            var evento = _eventos.Retirar();
            Relogio = evento.Tempo;          // o clock "pula" direto para o próximo evento
            Processar(evento);
            IniciarTrocaDeContextoSeCpuLivre();
        }
    }

    private void Processar(EventoSimulacao evento)
    {
        switch (evento.Tipo)
        {
            case TipoEvento.ChegadaProcesso: TratarChegada(evento.Processo); break;
            case TipoEvento.FimTrocaContexto: TratarFimTrocaContexto(evento.Thread!); break;
            case TipoEvento.FimSurtoCpu: TratarFimSurtoCpu(evento.Thread!); break;
            default: throw new InvalidOperationException($"Evento desconhecido: {evento.Tipo}");
        }
    }

    // ---------------------------------------------------------------- tratadores de evento

    private void TratarChegada(BlocoControleProcesso processo)
    {
        processo.TempoCriacao = Relogio;
        Registrar("CHEGADA", processo.Identificador,
            $"processo chegou (prioridade {processo.Prioridade}, {processo.Threads.Count} thread(s))");

        foreach (var thread in processo.Threads.OrderBy(t => t.Identificador, StringComparer.Ordinal))
        {
            MudarEstado(thread, EstadoThread.Pronto, "ADMISSAO", "thread entrou na fila de prontos");
            _escalonador.Enfileirar(thread);
        }
    }

    private void IniciarTrocaDeContextoSeCpuLivre()
    {
        if (!Cpu.Livre) return;
        var proxima = _escalonador.SelecionarProxima();
        if (proxima is null) return;

        var custo = _configuracao.CustoTrocaContexto;
        Cpu.IniciarTrocaDeContexto(proxima, custo);
        Registrar("INICIO_TROCA", proxima.Identificador, $"CPU carregando o contexto da thread (custo {custo})");
        _eventos.Enfileirar(new EventoSimulacao(Relogio + custo, TipoEvento.FimTrocaContexto, proxima.Processo, proxima));
    }

    private void TratarFimTrocaContexto(BlocoControleThread thread)
    {
        Cpu.ConcluirTrocaDeContexto();
        Registrar("FIM_TROCA", thread.Identificador,
            $"contexto restaurado da pilha lógica (PC={Cpu.Registradores!.ContadorPrograma})");
        MudarEstado(thread, EstadoThread.Executando, "DESPACHO", "thread assumiu a CPU");
        ExecutarOperacaoAtual(thread);
    }

    private void TratarFimSurtoCpu(BlocoControleThread thread)
    {
        if (Cpu.ThreadAtual != thread || Cpu.EmTrocaDeContexto)
            throw new InvalidOperationException($"Fim de surto para {thread.Identificador}, que não está executando.");

        var registradores = Cpu.Registradores!;
        registradores.TempoRestante = 0;
        registradores.ContadorPrograma++;
        thread.Processo.ContadorProgramaLogico++;
        Registrar("FIM_SURTO_CPU", thread.Identificador, $"surto concluído (PC agora = {registradores.ContadorPrograma})");
        ExecutarOperacaoAtual(thread);
    }

    // ---------------------------------------------------------------- execução das operações

    /// <summary>Olha a operação apontada pelo PC e decide o que fazer com ela.</summary>
    private void ExecutarOperacaoAtual(BlocoControleThread thread)
    {
        var registradores = Cpu.Registradores!;
        if (registradores.ContadorPrograma >= thread.Operacoes.Count)
        {
            FinalizarThread(thread);
            return;
        }

        var operacao = thread.Operacoes[registradores.ContadorPrograma];
        switch (operacao)
        {
            case OperacaoCpu operacaoCpu:
                if (registradores.TempoRestante == 0)
                    registradores.TempoRestante = operacaoCpu.Duracao;
                Registrar("EXECUCAO_CPU", thread.Identificador,
                    $"executando surto de CPU por {registradores.TempoRestante} unidade(s)");
                _eventos.Enfileirar(new EventoSimulacao(
                    Relogio + registradores.TempoRestante, TipoEvento.FimSurtoCpu, thread.Processo, thread));
                break;

            default:
                throw new InvalidOperationException($"Operação não suportada: {operacao}");
        }
    }

    private void FinalizarThread(BlocoControleThread thread)
    {
        Cpu.Liberar();
        MudarEstado(thread, EstadoThread.Finalizado, "FINALIZACAO", "todas as operações foram concluídas");
    }

    // ---------------------------------------------------------------- utilitários

    /// <summary>
    /// Único lugar onde o estado de uma thread muda. Registra a transição no log e,
    /// se o estado do processo mudar em consequência, registra isso também.
    /// </summary>
    private void MudarEstado(BlocoControleThread thread, EstadoThread novoEstado, string tipoEvento, string motivo)
    {
        var estadoAnterior = thread.Estado;
        thread.Estado = novoEstado;
        Registrar(tipoEvento, thread.Identificador, $"{estadoAnterior} -> {novoEstado}: {motivo}");

        var processo = thread.Processo;
        var estadoAnteriorProcesso = processo.Estado;
        if (processo.AtualizarEstado())
        {
            var tipo = processo.Estado == EstadoProcesso.Finalizado ? "FIM_PROCESSO" : "ESTADO_PROCESSO";
            Registrar(tipo, processo.Identificador,
                $"{estadoAnteriorProcesso} -> {processo.Estado}: causado por {tipoEvento} de {thread.Identificador}");
        }
    }

    private void Registrar(string tipo, string sujeito, string descricao) =>
        _registro.Add(new EntradaLog(Relogio, tipo, sujeito, descricao));
}
