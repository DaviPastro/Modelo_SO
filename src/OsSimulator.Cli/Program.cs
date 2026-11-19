using OsSimulator.Cli;
using OsSimulator.Core;

if (args.Length == 0 || args.Contains("--ajuda"))
{
    Console.WriteLine(LeitorArgumentos.Uso);
    return args.Length == 0 ? 1 : 0;
}

try
{
    // 1. Ler e validar parâmetros e carga ANTES de simular (erro aqui não gera resultado parcial).
    var configuracao = LeitorArgumentos.Ler(args);
    var carga = LeitorCargaTrabalho.Ler(configuracao.ArquivoCarga);

    // 2. Montar o simulador com a política escolhida e executar.
    var motor = new MotorSimulacao(FabricaEscalonador.Criar(configuracao), configuracao);
    motor.Executar(carga);

    // 3. Exibir log e métricas.
    foreach (var entrada in motor.Registro) Console.WriteLine(entrada);
    Console.WriteLine();
    foreach (var linha in MetricasSimulacao.Calcular(carga, motor).Formatar()) Console.WriteLine(linha);
    return 0;
}
catch (ArgumentException exception)
{
    Console.Error.WriteLine($"Erro de configuração: {exception.Message}");
    Console.Error.WriteLine("Use --ajuda para ver as opções.");
    return 2;
}
catch (Exception exception)
{
    Console.Error.WriteLine($"Erro de simulação: {exception.Message}");
    return 3;
}
