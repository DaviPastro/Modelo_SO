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
        configuracao.Validar();
        _escalonador = escalonador;
        _configuracao = configuracao;
    }

    public int Relogio { get; private set; }
    public Cpu Cpu { get; } = new();
    public IReadOnlyList<EntradaLog> Registro => _registro;

    // ---------------------------------------------------------------- laço principal

    public void Executar(CargaTrabalho carga)
    {
        Registrar("CONFIGURACAO", "SIM", _configuracao.Descrever());

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
            case TipoEvento.FimQuantum: TratarFimQuantum(evento.Thread!); break;
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
            ColocarNaFilaDeProntos(thread, "ADMISSAO", "thread entrou na fila de prontos");
    }

    private void IniciarTrocaDeContextoSeCpuLivre()
    {
        if (!Cpu.Livre) return;
        var proxima = _escalonador.SelecionarProxima();
        if (proxima is null) return;

        // A thread saiu da fila de prontos: fecha o intervalo de espera que estava aberto.
        proxima.TempoEspera += Relogio - proxima.InicioEsperaAtual!.Value;
        proxima.InicioEsperaAtual = null;

        var custo = _configuracao.CustoTrocaContexto;
        Cpu.IniciarTrocaDeContexto(proxima, custo);
        Registrar("INICIO_TROCA", proxima.Identificador, $"CPU carregando o contexto da thread (custo {custo})");
        _eventos.Enfileirar(new EventoSimulacao(Relogio + custo, TipoEvento.FimTrocaContexto, proxima.Processo, proxima));
    }

    private void TratarFimTrocaContexto(BlocoControleThread thread)
    {
        Cpu.ConcluirTrocaDeContexto();
        Registrar("FIM_TROCA", thread.Identificador,
            $"contexto restaurado da pilha lógica (PC={Cpu.Registradores!.ContadorPrograma}, restante={Cpu.Registradores.TempoRestante})");
        MudarEstado(thread, EstadoThread.Executando, "DESPACHO", "thread assumiu a CPU");
        thread.TempoPrimeiroDespacho ??= Relogio;   // só grava na primeira vez
        ExecutarOperacaoAtual(thread);
    }

    private void TratarFimSurtoCpu(BlocoControleThread thread)
    {
        if (Cpu.ThreadAtual != thread || Cpu.EmTrocaDeContexto)
            throw new InvalidOperationException($"Fim de surto para {thread.Identificador}, que não está executando.");

        var registradores = Cpu.Registradores!;
        Cpu.RegistrarTempoUtil(registradores.TempoRestante);
        thread.TempoCpu += registradores.TempoRestante;
        registradores.TempoRestante = 0;
        registradores.ContadorPrograma++;
        thread.Processo.ContadorProgramaLogico++;
        Registrar("FIM_SURTO_CPU", thread.Identificador, $"surto concluído (PC agora = {registradores.ContadorPrograma})");
        ExecutarOperacaoAtual(thread);
    }

    /// <summary>O quantum acabou mas o surto não: a thread usou uma fatia inteira e ainda resta trabalho.</summary>
    private void TratarFimQuantum(BlocoControleThread thread)
    {
        if (Cpu.ThreadAtual != thread || Cpu.EmTrocaDeContexto)
            throw new InvalidOperationException($"Fim de quantum para {thread.Identificador}, que não está executando.");

        var quantum = _escalonador.Quantum!.Value;
        var registradores = Cpu.Registradores!;
        Cpu.RegistrarTempoUtil(quantum);
        thread.TempoCpu += quantum;
        registradores.TempoRestante -= quantum;

        if (!_escalonador.PossuiThreadsProntas)
        {
            // Ninguém esperando: não faz sentido pagar uma troca de contexto. A thread ganha outro quantum.
            Registrar("NOVO_QUANTUM", thread.Identificador,
                $"quantum esgotado, mas a fila de prontos está vazia (restante={registradores.TempoRestante})");
            ExecutarOperacaoAtual(thread);
            return;
        }

        // Preempção: salva o contexto na pilha lógica e devolve a thread ao FIM da fila.
        Registrar("FIM_QUANTUM", thread.Identificador,
            $"quantum de {quantum} esgotado; contexto salvo na pilha lógica (PC={registradores.ContadorPrograma}, restante={registradores.TempoRestante})");
        Cpu.SalvarContextoELiberar();
        ColocarNaFilaDeProntos(thread, "PREEMPCAO", "quantum esgotado, thread volta ao fim da fila");
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
                AgendarFatiaDeCpu(thread, registradores);
                break;

            default:
                throw new InvalidOperationException($"Operação não suportada: {operacao}");
        }
    }

    /// <summary>
    /// Decide até quando a thread roda agora. Sem quantum (FCFS, prioridade) ou se o que resta cabe no quantum:
    /// agenda o fim do surto. Caso contrário: agenda o fim do quantum (preempção).
    /// </summary>
    private void AgendarFatiaDeCpu(BlocoControleThread thread, ContextoCpu registradores)
    {
        var quantum = _escalonador.Quantum;
        var restante = registradores.TempoRestante;

        if (quantum is null || restante <= quantum)
        {
            Registrar("EXECUCAO_CPU", thread.Identificador, $"executando surto de CPU por {restante} unidade(s)");
            _eventos.Enfileirar(new EventoSimulacao(Relogio + restante, TipoEvento.FimSurtoCpu, thread.Processo, thread));
        }
        else
        {
            Registrar("EXECUCAO_CPU", thread.Identificador, $"executando fatia de {quantum} de um surto que ainda precisa de {restante}");
            _eventos.Enfileirar(new EventoSimulacao(Relogio + quantum.Value, TipoEvento.FimQuantum, thread.Processo, thread));
        }
    }

    private void FinalizarThread(BlocoControleThread thread)
    {
        Cpu.Liberar();
        thread.TempoFinalizacao = Relogio;
        MudarEstado(thread, EstadoThread.Finalizado, "FINALIZACAO", "todas as operações foram concluídas");
    }

    // ---------------------------------------------------------------- utilitários

    /// <summary>
    /// Toda entrada na fila de prontos passa por aqui: muda o estado, abre o intervalo de espera
    /// e entrega a thread ao escalonador. Será reaproveitado na preempção (etapa 3) e no fim de E/S (etapa 4).
    /// </summary>
    private void ColocarNaFilaDeProntos(BlocoControleThread thread, string tipoEvento, string motivo)
    {
        MudarEstado(thread, EstadoThread.Pronto, tipoEvento, motivo);
        thread.InicioEsperaAtual = Relogio;
        _escalonador.Enfileirar(thread);
    }

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
            if (processo.Estado == EstadoProcesso.Finalizado)
                processo.TempoFinalizacao = Relogio;

            var tipo = processo.Estado == EstadoProcesso.Finalizado ? "FIM_PROCESSO" : "ESTADO_PROCESSO";
            Registrar(tipo, processo.Identificador,
                $"{estadoAnteriorProcesso} -> {processo.Estado}: causado por {tipoEvento} de {thread.Identificador}");
        }
    }

    private void Registrar(string tipo, string sujeito, string descricao) =>
        _registro.Add(new EntradaLog(Relogio, tipo, sujeito, descricao));
}
