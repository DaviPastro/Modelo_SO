using OsSimulator.Core;

var tests = new (string Name, Action Execute)[]
{
    ("FCFS preserva a ordem de chegada", FcfsPreservaOrdemChegada),
    ("Transições finalizam processo após a última thread", ProcessoFinalizaAposUltimaThread),
    ("Fila de eventos ordena eventos no mesmo clock", FilaEventosEDeterministica),
};

var failures = 0;
foreach (var test in tests)
{
    try { test.Execute(); Console.WriteLine($"[OK] {test.Name}"); }
    catch (Exception error) { failures++; Console.Error.WriteLine($"[FALHOU] {test.Name}: {error.Message}"); }
}
return failures == 0 ? 0 : 1;

static void FcfsPreservaOrdemChegada()
{
    var primeiro = CriarProcesso("P1", 0, ("T1", 3));
    var segundo = CriarProcesso("P2", 1, ("T2", 2));
    var motor = new MotorSimulacao(new EscalonadorFcfs());
    motor.Executar(new CargaTrabalho([primeiro, segundo]));
    Assert(motor.Registro.Any(linha => linha.Contains("DESPACHO") && linha.Contains("T1")), "T1 não foi despachada.");
    var despachos = motor.Registro.Where(linha => linha.Contains("DESPACHO")).ToList();
    Assert(despachos[0].Contains("T1") && despachos[1].Contains("T2"), "A ordem FCFS está incorreta.");
}

static void ProcessoFinalizaAposUltimaThread()
{
    var processo = CriarProcesso("P1", 0, ("T1", 1), ("T2", 1));
    var motor = new MotorSimulacao(new EscalonadorFcfs());
    motor.Executar(new CargaTrabalho([processo]));
    Assert(processo.Estado == EstadoProcesso.Finalizado, "O processo deveria estar finalizado.");
    Assert(processo.Threads.All(thread => thread.Estado == EstadoThread.Finalizado), "Todas as threads deveriam estar finalizadas.");
}

static void FilaEventosEDeterministica()
{
    var processo = CriarProcesso("P1", 0, ("T1", 1));
    var fila = new FilaEventos();
    fila.Enfileirar(new EventoSimulacao(5, TipoEvento.ChegadaProcesso, processo));
    fila.Enfileirar(new EventoSimulacao(2, TipoEvento.ChegadaProcesso, processo));
    Assert(fila.Retirar().Tempo == 2, "O evento mais cedo deveria sair primeiro.");
}

static BlocoControleProcesso CriarProcesso(string identificador, int tempoChegada, params (string Identificador, int Surto)[] threads)
{
    var processo = new BlocoControleProcesso(identificador, tempoChegada, 0);
    foreach (var thread in threads) processo.Threads.Add(new BlocoControleThread(thread.Identificador, processo, [thread.Surto]));
    return processo;
}

static void Assert(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}
