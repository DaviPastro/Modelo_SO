using OsSimulator.Core;

if (args.Length != 1)
{
    Console.Error.WriteLine("Uso: dotnet run --project src/OsSimulator.Cli -- <arquivo-de-carga.json>");
    return 1;
}

try
{
    var carga = LeitorCargaTrabalho.Ler(args[0]);
    var motor = new MotorSimulacao(new EscalonadorFcfs());
    motor.Executar(carga);
    Console.WriteLine("Simulador");
    Console.WriteLine("Política: FCFS | Clock lógico");
    foreach (var linha in motor.Registro) Console.WriteLine(linha);
    Console.WriteLine($"Relógio final: {motor.Relogio}");
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
