using OsSimulator.Core;

if (args.Length != 1)
{
    Console.Error.WriteLine("Uso: dotnet run --project src/OsSimulator.Cli -- <arquivo-de-carga.json>");
    return 1;
}

try
{
    // Na etapa 2 todos estes valores passarão a vir da linha de comando.
    var configuracao = new ConfiguracaoSimulacao(CustoTrocaContexto: 1);
    var carga = LeitorCargaTrabalho.Ler(args[0]);
    var motor = new MotorSimulacao(new EscalonadorFcfs(), configuracao);
    motor.Executar(carga);

    Console.WriteLine($"Configuração: carga={args[0]}, política=FCFS, custoTrocaContexto={configuracao.CustoTrocaContexto}");
    Console.WriteLine();
    foreach (var entrada in motor.Registro) Console.WriteLine(entrada);
    Console.WriteLine();
    Console.WriteLine($"Relógio final: {motor.Relogio}");
    Console.WriteLine($"Trocas de contexto: {motor.Cpu.QuantidadeTrocasContexto} (sobrecarga total: {motor.Cpu.TempoTotalTrocasContexto})");
    return 0;
}
catch (ArgumentException exception)
{
    Console.Error.WriteLine($"Erro de configuração: {exception.Message}");
    return 2;
}
catch (Exception exception)
{
    Console.Error.WriteLine($"Erro de simulação: {exception.Message}");
    return 3;
}
