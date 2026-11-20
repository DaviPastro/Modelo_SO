using static System.FormattableString;

namespace OsSimulator.Core;

/// <summary>
/// Todos os parâmetros de uma execução. Alguns só passam a ter efeito nas próximas etapas
/// (quantum na 3, dispositivos na 4, memória na 5), mas já são lidos, validados e registrados no log.
/// </summary>
public sealed record ConfiguracaoSimulacao
{
    private static readonly string[] AlgoritmosValidos = ["fcfs", "rr", "prioridade"];

    public string ArquivoCarga { get; init; } = "";
    public string Algoritmo { get; init; } = "fcfs";
    public int Quantum { get; init; } = 4;
    public int CustoTrocaContexto { get; init; } = 1;
    public int TamanhoPagina { get; init; } = 4;
    public int NumeroMolduras { get; init; } = 4;
    public int TempoFaltaPagina { get; init; } = 5;
    public int TempoDisco { get; init; } = 6;
    public int TempoTerminal { get; init; } = 3;
    public int Semente { get; init; } = 42;

    /// <summary>Lança ArgumentException com mensagem objetiva se algum valor for inválido.</summary>
    public void Validar()
    {
        if (!AlgoritmosValidos.Contains(Algoritmo))
            throw new ArgumentException($"Algoritmo inválido: '{Algoritmo}'. Use fcfs, rr ou prioridade.");

        ExigirPositivo(Quantum, "quantum");
        if (CustoTrocaContexto < 0)
            throw new ArgumentException("O custo da troca de contexto (--troca) não pode ser negativo.");
        ExigirPositivo(TamanhoPagina, "pagina");
        ExigirPositivo(NumeroMolduras, "molduras");
        ExigirPositivo(TempoFaltaPagina, "falta-pagina");
        ExigirPositivo(TempoDisco, "disco");
        ExigirPositivo(TempoTerminal, "terminal");
    }

    /// <summary>Texto de uma linha com toda a configuração, registrado no início do log.</summary>
    public string Descrever() => Invariant($"carga={ArquivoCarga} algoritmo={Algoritmo} quantum={Quantum} troca={CustoTrocaContexto} pagina={TamanhoPagina} molduras={NumeroMolduras} falta-pagina={TempoFaltaPagina} disco={TempoDisco} terminal={TempoTerminal} semente={Semente}");

    private static void ExigirPositivo(int valor, string opcao)
    {
        if (valor <= 0)
            throw new ArgumentException($"O valor de --{opcao} deve ser maior que zero (recebido: {valor}).");
    }
}
