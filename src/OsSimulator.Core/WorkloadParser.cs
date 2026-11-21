using System.Text.Json;

namespace OsSimulator.Core;

/// <summary>Lê o arquivo JSON da carga de trabalho, valida e monta os PCBs e TCBs.</summary>
public static class LeitorCargaTrabalho
{
    private static readonly JsonSerializerOptions OpcoesJson = new() { PropertyNameCaseInsensitive = true };

    public static CargaTrabalho Ler(string caminho)
    {
        if (!File.Exists(caminho))
            throw new ArgumentException($"Carga não encontrada: {caminho}");

        EntradaCarga? entrada;
        try
        {
            entrada = JsonSerializer.Deserialize<EntradaCarga>(File.ReadAllText(caminho), OpcoesJson);
        }
        catch (JsonException erro)
        {
            throw new ArgumentException($"JSON inválido: {erro.Message}");
        }

        if (entrada?.Processos is null || entrada.Processos.Count == 0)
            throw new ArgumentException("A carga deve conter ao menos um processo.");

        var idsProcessos = new HashSet<string>(StringComparer.Ordinal);
        var idsThreads = new HashSet<string>(StringComparer.Ordinal);
        var processos = new List<BlocoControleProcesso>();

        foreach (var origem in entrada.Processos)
        {
            if (string.IsNullOrWhiteSpace(origem.Identificador) || !idsProcessos.Add(origem.Identificador))
                throw new ArgumentException("Identificador de processo ausente ou duplicado.");
            if (origem.TempoChegada < 0)
                throw new ArgumentException($"Tempo de chegada inválido em {origem.Identificador}.");
            if (origem.Threads is null || origem.Threads.Count == 0)
                throw new ArgumentException($"{origem.Identificador} deve conter ao menos uma thread.");

            var processo = new BlocoControleProcesso(origem.Identificador, origem.TempoChegada, origem.Prioridade);
            foreach (var origemThread in origem.Threads)
                processo.Threads.Add(CriarThread(origemThread, processo, idsThreads));
            processos.Add(processo);
        }

        return new CargaTrabalho(processos);
    }

    private static BlocoControleThread CriarThread(EntradaThread origem, BlocoControleProcesso processo, HashSet<string> idsThreads)
    {
        if (string.IsNullOrWhiteSpace(origem.Identificador) || !idsThreads.Add(origem.Identificador))
            throw new ArgumentException($"Identificador de thread ausente ou duplicado em {processo.Identificador}.");
        if (origem.Operacoes is null || origem.Operacoes.Count == 0)
            throw new ArgumentException($"{origem.Identificador} deve conter ao menos uma operação.");

        var operacoes = origem.Operacoes.Select(op => CriarOperacao(op, origem.Identificador)).ToList();
        return new BlocoControleThread(origem.Identificador, processo, operacoes);
    }

    /// <summary>Converte uma operação do JSON no objeto correspondente. Novos tipos entram aqui.</summary>
    private static Operacao CriarOperacao(EntradaOperacao entrada, string idThread)
    {
        switch (entrada.Tipo?.ToLowerInvariant())
        {
            case "cpu":
                if (entrada.Duracao <= 0)
                    throw new ArgumentException($"Operação de CPU com duração inválida em {idThread}.");
                return new OperacaoCpu(entrada.Duracao);

            case "es":
                var dispositivo = entrada.Dispositivo?.ToLowerInvariant();
                if (dispositivo is null || !Dispositivo.NomesConhecidos.Contains(dispositivo))
                    throw new ArgumentException(
                        $"Dispositivo inválido '{entrada.Dispositivo}' em {idThread}. Use: {string.Join(", ", Dispositivo.NomesConhecidos)}.");
                return new OperacaoEs(dispositivo, entrada.Bloqueante ?? true);

            default:
                throw new ArgumentException($"Tipo de operação desconhecido '{entrada.Tipo}' em {idThread}.");
        }
    }

    // Classes que espelham o formato do JSON (usadas só durante a leitura).
    private sealed class EntradaCarga
    {
        public List<EntradaProcesso>? Processos { get; set; }
    }

    private sealed class EntradaProcesso
    {
        public string Identificador { get; set; } = "";
        public int TempoChegada { get; set; }
        public int Prioridade { get; set; }
        public List<EntradaThread>? Threads { get; set; }
    }

    private sealed class EntradaThread
    {
        public string Identificador { get; set; } = "";
        public List<EntradaOperacao>? Operacoes { get; set; }
    }

    private sealed class EntradaOperacao
    {
        public string? Tipo { get; set; }
        public int Duracao { get; set; }
        public string? Dispositivo { get; set; }
        public bool? Bloqueante { get; set; }   // se omitido, a E/S é bloqueante
    }
}
