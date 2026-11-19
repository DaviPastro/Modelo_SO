using OsSimulator.Core;

namespace OsSimulator.Cli;

/// <summary>
/// Converte os argumentos da linha de comando ("--opcao valor") em uma ConfiguracaoSimulacao.
/// Só cuida da sintaxe; as regras de valores válidos ficam em ConfiguracaoSimulacao.Validar().
/// </summary>
internal static class LeitorArgumentos
{
    public const string Uso = """
        Uso: dotnet run --project src/OsSimulator.Cli -- --carga <arquivo.json> [opções]

        Opções (todas recebem um valor):
          --carga <arquivo>       arquivo JSON da carga de trabalho (obrigatório)
          --algoritmo <nome>      fcfs | rr | prioridade          (padrão: fcfs)
          --quantum <n>           quantum do Round Robin           (padrão: 4)
          --troca <n>             custo da troca de contexto       (padrão: 1)
          --pagina <n>            tamanho da página                (padrão: 4)
          --molduras <n>          número de molduras na memória    (padrão: 4)
          --falta-pagina <n>      tempo de atendimento de falta    (padrão: 5)
          --disco <n>             tempo de serviço do disco        (padrão: 6)
          --terminal <n>          tempo de serviço do terminal     (padrão: 3)
          --semente <n>           semente de aleatoriedade         (padrão: 42)

        Exemplo:
          dotnet run --project src/OsSimulator.Cli -- --carga workloads/fcfs-basico.json --troca 1
        """;

    public static ConfiguracaoSimulacao Ler(string[] args)
    {
        if (args.Length % 2 != 0)
            throw new ArgumentException("Cada opção precisa de exatamente um valor (ex.: --quantum 4).");

        var configuracao = new ConfiguracaoSimulacao();
        var opcoesUsadas = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (var i = 0; i < args.Length; i += 2)
        {
            var opcao = args[i];
            var valor = args[i + 1];

            if (!opcao.StartsWith("--"))
                throw new ArgumentException($"Esperava uma opção começando com '--', mas recebeu '{opcao}'.");
            if (!opcoesUsadas.Add(opcao))
                throw new ArgumentException($"A opção {opcao} foi informada mais de uma vez.");

            // "with" cria uma cópia da configuração alterando só o campo indicado.
            configuracao = opcao.ToLowerInvariant() switch
            {
                "--carga" => configuracao with { ArquivoCarga = valor },
                "--algoritmo" => configuracao with { Algoritmo = valor.ToLowerInvariant() },
                "--quantum" => configuracao with { Quantum = Inteiro(opcao, valor) },
                "--troca" => configuracao with { CustoTrocaContexto = Inteiro(opcao, valor) },
                "--pagina" => configuracao with { TamanhoPagina = Inteiro(opcao, valor) },
                "--molduras" => configuracao with { NumeroMolduras = Inteiro(opcao, valor) },
                "--falta-pagina" => configuracao with { TempoFaltaPagina = Inteiro(opcao, valor) },
                "--disco" => configuracao with { TempoDisco = Inteiro(opcao, valor) },
                "--terminal" => configuracao with { TempoTerminal = Inteiro(opcao, valor) },
                "--semente" => configuracao with { Semente = Inteiro(opcao, valor) },
                _ => throw new ArgumentException($"Opção desconhecida: {opcao}"),
            };
        }

        if (string.IsNullOrWhiteSpace(configuracao.ArquivoCarga))
            throw new ArgumentException("Informe o arquivo de carga com --carga <arquivo.json>.");

        configuracao.Validar();
        return configuracao;
    }

    private static int Inteiro(string opcao, string valor) =>
        int.TryParse(valor, out var numero)
            ? numero
            : throw new ArgumentException($"Valor inválido para {opcao}: '{valor}' (esperado um número inteiro).");
}
