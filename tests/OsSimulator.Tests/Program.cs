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

// ------------------------------------------------------------------ auxiliares

static MotorSimulacao CriarMotor(int custoTroca) =>
    new(new EscalonadorFcfs(), new ConfiguracaoSimulacao { CustoTrocaContexto = custoTroca });

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
