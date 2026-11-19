using OsSimulator.Core;

var testes = new (string Nome, Action Executar)[]
{
    ("Fila de eventos ordena por tempo e, no empate, por ordem de inserção", FilaEventosOrdenaPorTempoEInsercao),
    ("FCFS preserva a ordem de chegada", FcfsPreservaOrdemChegada),
    ("Thread passa por Novo, Pronto, Executando e Finalizado", ThreadSegueCicloDeVida),
    ("Processo finaliza somente após a última thread", ProcessoFinalizaAposUltimaThread),
    ("Troca de contexto é contada e consome tempo", TrocaDeContextoConsomeTempo),
    ("Operações de CPU consecutivas não liberam a CPU", OperacoesConsecutivasNaoLiberamCpu),
    ("Mesma carga e configuração geram o mesmo log", SimulacaoDeterministica),
    ("Leitor rejeita tipo de operação desconhecido", LeitorRejeitaOperacaoDesconhecida),
};

var falhas = 0;
foreach (var teste in testes)
{
    try { teste.Executar(); Console.WriteLine($"[OK] {teste.Nome}"); }
    catch (Exception erro) { falhas++; Console.Error.WriteLine($"[FALHOU] {teste.Nome}: {erro.Message}"); }
}
Console.WriteLine($"{testes.Length - falhas}/{testes.Length} testes passaram.");
return falhas == 0 ? 0 : 1;

// ------------------------------------------------------------------ testes

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

// ------------------------------------------------------------------ auxiliares

static MotorSimulacao CriarMotor(int custoTroca) =>
    new(new EscalonadorFcfs(), new ConfiguracaoSimulacao(CustoTrocaContexto: custoTroca));

static BlocoControleProcesso CriarProcesso(string identificador, int tempoChegada, params (string Identificador, int DuracaoCpu)[] threads)
{
    var processo = new BlocoControleProcesso(identificador, tempoChegada, prioridade: 1);
    foreach (var thread in threads)
        processo.Threads.Add(new BlocoControleThread(thread.Identificador, processo, [new OperacaoCpu(thread.DuracaoCpu)]));
    return processo;
}

static void Assert(bool condicao, string mensagem)
{
    if (!condicao) throw new InvalidOperationException(mensagem);
}
