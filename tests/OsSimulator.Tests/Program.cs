using OsSimulator.Core;

var testes = new (string Nome, Action Executar)[]
{
    // Etapa 1 — núcleo
    ("Fila de eventos ordena por tempo e, no empate, por ordem de inserção", FilaEventosOrdenaPorTempoEInsercao),
    ("FCFS preserva a ordem de chegada", FcfsPreservaOrdemChegada),
    ("Thread passa por Novo, Pronto, Executando e Finalizado", ThreadSegueCicloDeVida),
    ("Processo finaliza somente após a última thread", ProcessoFinalizaAposUltimaThread),
    ("Troca de contexto é contada e consome tempo", TrocaDeContextoConsomeTempo),
    ("Operações de CPU consecutivas não liberam a CPU", OperacoesConsecutivasNaoLiberamCpu),
    ("Mesma carga e configuração geram o mesmo log", SimulacaoDeterministica),
    ("Leitor rejeita tipo de operação desconhecido", LeitorRejeitaOperacaoDesconhecida),
    // Etapa 2 — métricas e configuração
    ("Métricas de espera, resposta e retorno por thread", MetricasPorThread),
    ("Utilização da CPU separa carga útil, troca e ociosidade", UtilizacaoDaCpu),
    ("Log começa com a configuração usada", LogComecaComConfiguracao),
    ("Configuração inválida é rejeitada", ConfiguracaoInvalidaRejeitada),
    // Etapa 3 — Round Robin e prioridades
    ("Round Robin alterna as threads ao fim do quantum", RoundRobinAlternaThreads),
    ("Preempção salva o contexto e a thread retoma de onde parou", PreempcaoSalvaContexto),
    ("Round Robin sem concorrência não faz troca de contexto", RoundRobinSemConcorrencia),
    ("Prioridade escolhe o menor número primeiro", PrioridadeOrdenaFila),
    ("Prioridade empatada respeita a ordem de entrada na fila", PrioridadeEmpateFifo),
    ("Fábrica cria o escalonador pedido na configuração", FabricaCriaEscalonador),
    // Etapa 4 — entrada e saída
    ("E/S bloqueante bloqueia e desbloqueia a thread", EsBloqueanteBloqueiaEDesbloqueia),
    ("CPU atende outra thread enquanto uma espera E/S", CpuAtendeOutraDuranteEs),
    ("Fila do dispositivo atende em ordem de chegada", FilaDoDispositivoFcfs),
    ("E/S não bloqueante guarda o resultado no TCB sem bloquear", EsNaoBloqueante),
    ("Leitor rejeita dispositivo desconhecido", LeitorRejeitaDispositivoDesconhecido),
};

var falhas = 0;
foreach (var teste in testes)
{
    try { teste.Executar(); Console.WriteLine($"[OK] {teste.Nome}"); }
    catch (Exception erro) { falhas++; Console.Error.WriteLine($"[FALHOU] {teste.Nome}: {erro.Message}"); }
}
Console.WriteLine($"{testes.Length - falhas}/{testes.Length} testes passaram.");
return falhas == 0 ? 0 : 1;

// ------------------------------------------------------------------ testes da etapa 1

static void FilaEventosOrdenaPorTempoEInsercao()
{
    var processo = CriarProcesso("P1", 0, ("T1", 1));
    var fila = new FilaEventos();
    fila.Enfileirar(new EventoSimulacao(5, TipoEvento.ChegadaProcesso, processo));
    fila.Enfileirar(new EventoSimulacao(2, TipoEvento.FimSurtoCpu, processo));
    fila.Enfileirar(new EventoSimulacao(2, TipoEvento.FimTrocaContexto, processo));

    Assert(fila.Retirar().Tipo == TipoEvento.FimSurtoCpu, "O primeiro evento inserido no instante 2 deveria sair primeiro.");
    Assert(fila.Retirar().Tipo == TipoEvento.FimTrocaContexto, "O segundo evento do instante 2 deveria sair em seguida.");
    Assert(fila.Retirar().Tempo == 5, "O evento do instante 5 deveria sair por último.");
}

static void FcfsPreservaOrdemChegada()
{
    var motor = CriarMotor(custoTroca: 0);
    motor.Executar(new CargaTrabalho([CriarProcesso("P1", 0, ("T1", 3)), CriarProcesso("P2", 1, ("T2", 2))]));

    var despachos = motor.Registro.Where(e => e.Tipo == "DESPACHO").Select(e => e.Sujeito).ToList();
    Assert(despachos.SequenceEqual(new[] { "T1", "T2" }), $"Ordem de despacho incorreta: {string.Join(", ", despachos)}");
}

static void ThreadSegueCicloDeVida()
{
    var motor = CriarMotor(custoTroca: 0);
    motor.Executar(new CargaTrabalho([CriarProcesso("P1", 0, ("T1", 2))]));

    var transicoes = motor.Registro
        .Where(e => e.Sujeito == "T1" && e.Descricao.Contains("->"))
        .Select(e => e.Descricao.Split(':')[0])
        .ToList();
    var esperado = new[] { "Novo -> Pronto", "Pronto -> Executando", "Executando -> Finalizado" };
    Assert(transicoes.SequenceEqual(esperado), $"Transições inesperadas: {string.Join(" | ", transicoes)}");
}

static void ProcessoFinalizaAposUltimaThread()
{
    var processo = CriarProcesso("P1", 0, ("T1", 1), ("T2", 1));
    var motor = CriarMotor(custoTroca: 0);
    motor.Executar(new CargaTrabalho([processo]));

    Assert(processo.Estado == EstadoProcesso.Finalizado, "O processo deveria estar finalizado.");
    Assert(processo.Threads.All(t => t.Estado == EstadoThread.Finalizado), "Todas as threads deveriam estar finalizadas.");

    var fimProcesso = motor.Registro.Single(e => e.Tipo == "FIM_PROCESSO");
    var fimT2 = motor.Registro.Single(e => e.Tipo == "FINALIZACAO" && e.Sujeito == "T2");
    Assert(fimProcesso.Tempo == fimT2.Tempo, "O processo deveria finalizar no mesmo instante da última thread.");
}

static void TrocaDeContextoConsomeTempo()
{
    var motor = CriarMotor(custoTroca: 1);
    motor.Executar(new CargaTrabalho([CriarProcesso("P1", 0, ("T1", 3)), CriarProcesso("P2", 1, ("T2", 2))]));

    // T1: troca 0->1, executa 1->4 | T2: troca 4->5, executa 5->7
    Assert(motor.Relogio == 7, $"Relógio final deveria ser 7, mas foi {motor.Relogio}.");
    Assert(motor.Cpu.QuantidadeTrocasContexto == 2, "Deveriam ocorrer 2 trocas de contexto.");
    Assert(motor.Cpu.TempoTotalTrocasContexto == 2, "A sobrecarga total deveria ser 2.");
}

static void OperacoesConsecutivasNaoLiberamCpu()
{
    var processo = new BlocoControleProcesso("P1", 0, 1);
    processo.Threads.Add(new BlocoControleThread("T1", processo, [new OperacaoCpu(2), new OperacaoCpu(3)]));
    var motor = CriarMotor(custoTroca: 1);
    motor.Executar(new CargaTrabalho([processo]));

    Assert(motor.Registro.Count(e => e.Tipo == "DESPACHO") == 1, "A thread deveria ser despachada uma única vez.");
    Assert(motor.Relogio == 6, $"Relógio final deveria ser 6 (1 de troca + 2 + 3), mas foi {motor.Relogio}.");
}

static void SimulacaoDeterministica()
{
    IReadOnlyList<EntradaLog> Rodar()
    {
        var motor = CriarMotor(custoTroca: 1);
        motor.Executar(new CargaTrabalho([CriarProcesso("P1", 0, ("T1", 3), ("T2", 2)), CriarProcesso("P2", 0, ("T3", 1))]));
        return motor.Registro;
    }

    Assert(Rodar().SequenceEqual(Rodar()), "Duas execuções iguais produziram logs diferentes.");
}

static void LeitorRejeitaOperacaoDesconhecida()
{
    var caminho = Path.GetTempFileName();
    File.WriteAllText(caminho, """{ "processos": [ { "identificador": "P1", "tempoChegada": 0, "prioridade": 1, "threads": [ { "identificador": "T1", "operacoes": [ { "tipo": "voar", "duracao": 1 } ] } ] } ] }""");
    try
    {
        LeitorCargaTrabalho.Ler(caminho);
        throw new InvalidOperationException("O leitor deveria ter rejeitado a operação 'voar'.");
    }
    catch (ArgumentException)
    {
        // esperado: a carga é inválida
    }
    finally
    {
        File.Delete(caminho);
    }
}

// ------------------------------------------------------------------ testes da etapa 2

static void MetricasPorThread()
{
    var carga = new CargaTrabalho([CriarProcesso("P1", 0, ("T1", 3)), CriarProcesso("P2", 1, ("T2", 2))]);
    var motor = CriarMotor(custoTroca: 1);
    motor.Executar(carga);
    var metricas = MetricasSimulacao.Calcular(carga, motor);

    // T1: chega 0, sai da fila em 0 (espera 0), executa a partir de 1 (resposta 1), termina em 4 (retorno 4)
    var t1 = metricas.Threads.Single(t => t.Thread == "T1");
    Assert(t1.Espera == 0 && t1.Resposta == 1 && t1.Retorno == 4, $"T1 incorreta: {t1}");

    // T2: chega 1, sai da fila em 4 (espera 3), executa a partir de 5 (resposta 4), termina em 7 (retorno 6)
    var t2 = metricas.Threads.Single(t => t.Thread == "T2");
    Assert(t2.Espera == 3 && t2.Resposta == 4 && t2.Retorno == 6, $"T2 incorreta: {t2}");

    Assert(metricas.EsperaMedia == 1.5, $"Espera média deveria ser 1.5, mas foi {metricas.EsperaMedia}.");
    Assert(metricas.Processos.Single(p => p.Processo == "P2").Retorno == 6, "Retorno de P2 deveria ser 6.");
}

static void UtilizacaoDaCpu()
{
    // Único processo chega em 5: CPU fica ociosa de 0 a 5, troca de 5 a 6, executa de 6 a 8.
    var carga = new CargaTrabalho([CriarProcesso("P1", 5, ("T1", 2))]);
    var motor = CriarMotor(custoTroca: 1);
    motor.Executar(carga);
    var metricas = MetricasSimulacao.Calcular(carga, motor);

    Assert(metricas.TempoTotal == 8, $"Tempo total deveria ser 8, mas foi {metricas.TempoTotal}.");
    Assert(metricas.TempoUtilCpu == 2, $"Carga útil deveria ser 2, mas foi {metricas.TempoUtilCpu}.");
    Assert(metricas.TempoTrocas == 1, $"Sobrecarga deveria ser 1, mas foi {metricas.TempoTrocas}.");
    Assert(metricas.TempoOcioso == 5, $"Tempo ocioso deveria ser 5, mas foi {metricas.TempoOcioso}.");
    Assert(metricas.UtilizacaoCpu == 0.25, $"Utilização deveria ser 0.25, mas foi {metricas.UtilizacaoCpu}.");
    Assert(metricas.Throughput == 1.0 / 8, $"Throughput deveria ser 1/8, mas foi {metricas.Throughput}.");
}

static void LogComecaComConfiguracao()
{
    var motor = CriarMotor(custoTroca: 2);
    motor.Executar(new CargaTrabalho([CriarProcesso("P1", 0, ("T1", 1))]));

    var primeira = motor.Registro[0];
    Assert(primeira.Tipo == "CONFIGURACAO", "A primeira linha do log deveria ser a configuração.");
    Assert(primeira.Descricao.Contains("troca=2"), $"A configuração registrada está incompleta: {primeira.Descricao}");
}

static void ConfiguracaoInvalidaRejeitada()
{
    ExigirErro(() => new ConfiguracaoSimulacao { Quantum = 0 }.Validar(), "quantum zero");
    ExigirErro(() => new ConfiguracaoSimulacao { CustoTrocaContexto = -1 }.Validar(), "troca negativa");
    ExigirErro(() => new ConfiguracaoSimulacao { Algoritmo = "sjf" }.Validar(), "algoritmo desconhecido");
    ExigirErro(() => new ConfiguracaoSimulacao { NumeroMolduras = 0 }.Validar(), "zero molduras");
}

// ------------------------------------------------------------------ testes da etapa 3

static void RoundRobinAlternaThreads()
{
    // quantum 2, sem custo de troca. T1 precisa de 5, T2 de 3, ambas chegam em 0.
    // Linha do tempo: T1 0-2 | T2 2-4 | T1 4-6 | T2 6-7 (fim) | T1 7-8 (fim)
    var motor = CriarMotorCom(new EscalonadorRoundRobin(2), custoTroca: 0);
    motor.Executar(new CargaTrabalho([CriarProcesso("P1", 0, ("T1", 5)), CriarProcesso("P2", 0, ("T2", 3))]));

    var despachos = motor.Registro.Where(e => e.Tipo == "DESPACHO").Select(e => e.Sujeito).ToList();
    Assert(despachos.SequenceEqual(new[] { "T1", "T2", "T1", "T2", "T1" }), $"Ordem incorreta: {string.Join(", ", despachos)}");
    Assert(motor.Registro.Count(e => e.Tipo == "PREEMPCAO") == 3, "Deveriam ocorrer 3 preempções.");
    Assert(motor.Relogio == 8, $"Relógio final deveria ser 8, mas foi {motor.Relogio}.");
}

static void PreempcaoSalvaContexto()
{
    // T1 (5) é preemptada em 2 com 3 unidades restantes; ao voltar, o contexto restaurado deve mostrar isso.
    var motor = CriarMotorCom(new EscalonadorRoundRobin(2), custoTroca: 0);
    motor.Executar(new CargaTrabalho([CriarProcesso("P1", 0, ("T1", 5)), CriarProcesso("P2", 0, ("T2", 1))]));

    var restauracoesT1 = motor.Registro.Where(e => e.Tipo == "FIM_TROCA" && e.Sujeito == "T1").ToList();
    Assert(restauracoesT1.Count == 2, $"T1 deveria ser restaurada 2 vezes, mas foi {restauracoesT1.Count}.");
    Assert(restauracoesT1[1].Descricao.Contains("restante=3"), $"Contexto restaurado incorreto: {restauracoesT1[1].Descricao}");
}

static void RoundRobinSemConcorrencia()
{
    // Thread sozinha com surto de 5 e quantum 2: ganha novos quanta sem sair da CPU.
    var motor = CriarMotorCom(new EscalonadorRoundRobin(2), custoTroca: 1);
    motor.Executar(new CargaTrabalho([CriarProcesso("P1", 0, ("T1", 5))]));

    Assert(motor.Cpu.QuantidadeTrocasContexto == 1, "Deveria haver só a troca de contexto inicial.");
    Assert(motor.Registro.Count(e => e.Tipo == "NOVO_QUANTUM") == 2, "Deveriam ser concedidos 2 novos quanta.");
    Assert(motor.Relogio == 6, $"Relógio final deveria ser 6 (1 de troca + 5), mas foi {motor.Relogio}.");
}

static void PrioridadeOrdenaFila()
{
    // T1 ocupa a CPU de 0 a 3. Enquanto isso chegam T2 (prioridade 3) e T3 (prioridade 2): T3 deve ir antes.
    var motor = CriarMotorCom(new EscalonadorPrioridade(), custoTroca: 0);
    motor.Executar(new CargaTrabalho([
        CriarProcessoComPrioridade("P1", 0, 1, "T1", 3),
        CriarProcessoComPrioridade("P2", 1, 3, "T2", 1),
        CriarProcessoComPrioridade("P3", 2, 2, "T3", 1)]));

    var despachos = motor.Registro.Where(e => e.Tipo == "DESPACHO").Select(e => e.Sujeito).ToList();
    Assert(despachos.SequenceEqual(new[] { "T1", "T3", "T2" }), $"Ordem incorreta: {string.Join(", ", despachos)}");
}

static void PrioridadeEmpateFifo()
{
    // TA e TB têm a mesma prioridade; TA entrou na fila antes, então sai antes.
    var motor = CriarMotorCom(new EscalonadorPrioridade(), custoTroca: 0);
    motor.Executar(new CargaTrabalho([
        CriarProcessoComPrioridade("P0", 0, 1, "T0", 3),
        CriarProcessoComPrioridade("PB", 2, 2, "TB", 1),
        CriarProcessoComPrioridade("PA", 1, 2, "TA", 1)]));

    var despachos = motor.Registro.Where(e => e.Tipo == "DESPACHO").Select(e => e.Sujeito).ToList();
    Assert(despachos.SequenceEqual(new[] { "T0", "TA", "TB" }), $"Ordem incorreta: {string.Join(", ", despachos)}");
}

static void FabricaCriaEscalonador()
{
    Assert(FabricaEscalonador.Criar(new ConfiguracaoSimulacao { Algoritmo = "fcfs" }) is EscalonadorFcfs, "fcfs deveria criar EscalonadorFcfs.");
    Assert(FabricaEscalonador.Criar(new ConfiguracaoSimulacao { Algoritmo = "prioridade" }) is EscalonadorPrioridade, "prioridade deveria criar EscalonadorPrioridade.");

    var rr = FabricaEscalonador.Criar(new ConfiguracaoSimulacao { Algoritmo = "rr", Quantum = 3 });
    Assert(rr is EscalonadorRoundRobin && rr.Quantum == 3, "rr deveria criar EscalonadorRoundRobin com quantum 3.");
}

// ------------------------------------------------------------------ testes da etapa 4

static void EsBloqueanteBloqueiaEDesbloqueia()
{
    // T1: CPU 0-2 | pede disco em 2, bloqueia | disco 2-7 | desbloqueia em 7 | CPU 7-8
    var processo = ProcessoComOperacoes("P1", 0, "T1",
        new OperacaoCpu(2), new OperacaoEs("disco", Bloqueante: true), new OperacaoCpu(1));
    var carga = new CargaTrabalho([processo]);
    var motor = CriarMotorCom(new EscalonadorFcfs(), custoTroca: 0, tempoDisco: 5);
    motor.Executar(carga);

    var transicoes = motor.Registro
        .Where(e => e.Sujeito == "T1" && e.Descricao.Contains("->"))
        .Select(e => e.Descricao.Split(':')[0])
        .ToList();
    var esperado = new[]
    {
        "Novo -> Pronto", "Pronto -> Executando", "Executando -> Bloqueado",
        "Bloqueado -> Pronto", "Pronto -> Executando", "Executando -> Finalizado",
    };
    Assert(transicoes.SequenceEqual(esperado), $"Transições inesperadas: {string.Join(" | ", transicoes)}");
    Assert(motor.Relogio == 8, $"Relógio final deveria ser 8, mas foi {motor.Relogio}.");
    Assert(processo.Threads[0].TempoBloqueado == 5, $"Tempo bloqueado deveria ser 5, mas foi {processo.Threads[0].TempoBloqueado}.");

    var disco = MetricasSimulacao.Calcular(carga, motor).Dispositivos.Single(d => d.Dispositivo == "disco");
    Assert(disco.Atendidas == 1 && disco.TempoOcupado == 5, $"Métrica do disco incorreta: {disco}");
}

static void CpuAtendeOutraDuranteEs()
{
    // T1: CPU 0-1, bloqueia no disco (1-6) | T2 usa a CPU de 1 a 4 | CPU ociosa 4-6 | T1 volta: CPU 6-7
    var carga = new CargaTrabalho([
        ProcessoComOperacoes("P1", 0, "T1", new OperacaoCpu(1), new OperacaoEs("disco", true), new OperacaoCpu(1)),
        ProcessoComOperacoes("P2", 0, "T2", new OperacaoCpu(3))]);
    var motor = CriarMotorCom(new EscalonadorFcfs(), custoTroca: 0, tempoDisco: 5);
    motor.Executar(carga);

    var despachoT2 = motor.Registro.First(e => e.Tipo == "DESPACHO" && e.Sujeito == "T2");
    Assert(despachoT2.Tempo == 1, $"T2 deveria assumir a CPU em 1, mas foi em {despachoT2.Tempo}.");
    Assert(motor.Relogio == 7, $"Relógio final deveria ser 7, mas foi {motor.Relogio}.");

    var metricas = MetricasSimulacao.Calcular(carga, motor);
    Assert(metricas.TempoOcioso == 2, $"CPU deveria ficar ociosa por 2, mas foi {metricas.TempoOcioso}.");
}

static void FilaDoDispositivoFcfs()
{
    // T1 e T2 pedem o disco no instante 0. T1 é atendida de 0 a 4; T2 espera na fila e é atendida de 4 a 8.
    var motor = CriarMotorCom(new EscalonadorFcfs(), custoTroca: 0, tempoDisco: 4);
    motor.Executar(new CargaTrabalho([
        ProcessoComOperacoes("P1", 0, "T1", new OperacaoEs("disco", true)),
        ProcessoComOperacoes("P2", 0, "T2", new OperacaoEs("disco", true))]));

    var interrupcoes = motor.Registro.Where(e => e.Tipo == "INTERRUPCAO").ToList();
    Assert(interrupcoes.Select(e => e.Tempo).SequenceEqual(new[] { 4, 8 }), "As interrupções deveriam ocorrer em 4 e 8.");
    Assert(interrupcoes[0].Descricao.Contains("T1") && interrupcoes[1].Descricao.Contains("T2"), "O disco deveria atender T1 antes de T2.");
}

static void EsNaoBloqueante()
{
    // T1 pede o terminal sem bloquear em 0 e continua: CPU 0-2, termina em 2. O terminal conclui em 3.
    var processo = ProcessoComOperacoes("P1", 0, "T1", new OperacaoEs("terminal", Bloqueante: false), new OperacaoCpu(2));
    var motor = CriarMotorCom(new EscalonadorFcfs(), custoTroca: 0, tempoTerminal: 3);
    motor.Executar(new CargaTrabalho([processo]));

    var thread = processo.Threads[0];
    Assert(!motor.Registro.Any(e => e.Tipo == "BLOQUEIO"), "A thread não deveria bloquear.");
    Assert(motor.Registro.Single(e => e.Tipo == "FINALIZACAO").Tempo == 2, "A thread deveria terminar em 2, sem esperar o terminal.");
    Assert(thread.ResultadosEs.Count == 1 && thread.ResultadosEs[0].TempoConclusao == 3, "O resultado deveria estar no TCB, concluído em 3.");
}

static void LeitorRejeitaDispositivoDesconhecido()
{
    var caminho = Path.GetTempFileName();
    File.WriteAllText(caminho, """{ "processos": [ { "identificador": "P1", "tempoChegada": 0, "prioridade": 1, "threads": [ { "identificador": "T1", "operacoes": [ { "tipo": "es", "dispositivo": "impressora" } ] } ] } ] }""");
    try
    {
        LeitorCargaTrabalho.Ler(caminho);
        throw new InvalidOperationException("O leitor deveria ter rejeitado o dispositivo 'impressora'.");
    }
    catch (ArgumentException)
    {
        // esperado
    }
    finally
    {
        File.Delete(caminho);
    }
}

// ------------------------------------------------------------------ auxiliares

static MotorSimulacao CriarMotor(int custoTroca) => CriarMotorCom(new EscalonadorFcfs(), custoTroca);

static MotorSimulacao CriarMotorCom(IEscalonador escalonador, int custoTroca, int tempoDisco = 6, int tempoTerminal = 3) =>
    new(escalonador, new ConfiguracaoSimulacao { CustoTrocaContexto = custoTroca, TempoDisco = tempoDisco, TempoTerminal = tempoTerminal });

static BlocoControleProcesso ProcessoComOperacoes(string identificador, int tempoChegada, string idThread, params Operacao[] operacoes)
{
    var processo = new BlocoControleProcesso(identificador, tempoChegada, prioridade: 1);
    processo.Threads.Add(new BlocoControleThread(idThread, processo, operacoes));
    return processo;
}

static BlocoControleProcesso CriarProcessoComPrioridade(string identificador, int tempoChegada, int prioridade, string idThread, int duracaoCpu)
{
    var processo = new BlocoControleProcesso(identificador, tempoChegada, prioridade);
    processo.Threads.Add(new BlocoControleThread(idThread, processo, [new OperacaoCpu(duracaoCpu)]));
    return processo;
}

static BlocoControleProcesso CriarProcesso(string identificador, int tempoChegada, params (string Identificador, int DuracaoCpu)[] threads)
{
    var processo = new BlocoControleProcesso(identificador, tempoChegada, prioridade: 1);
    foreach (var thread in threads)
        processo.Threads.Add(new BlocoControleThread(thread.Identificador, processo, [new OperacaoCpu(thread.DuracaoCpu)]));
    return processo;
}

static void ExigirErro(Action acao, string caso)
{
    try { acao(); }
    catch (ArgumentException) { return; }
    throw new InvalidOperationException($"Deveria ter rejeitado: {caso}.");
}

static void Assert(bool condicao, string mensagem)
{
    if (!condicao) throw new InvalidOperationException(mensagem);
}
