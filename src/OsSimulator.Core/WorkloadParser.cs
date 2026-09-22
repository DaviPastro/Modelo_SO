using System.Text.Json;

namespace OsSimulator.Core;

public static class LeitorCargaTrabalho
{
    public static CargaTrabalho Ler(string caminho)
    {
        if (!File.Exists(caminho)) throw new ArgumentException($"Carga não encontrada: {caminho}");
        var entrada = JsonSerializer.Deserialize<EntradaCarga>(File.ReadAllText(caminho), OpcoesJson)
                    ?? throw new ArgumentException("Carga vazia ou inválida.");
        if (entrada.Processos is null || entrada.Processos.Count == 0) throw new ArgumentException("A carga deve conter processos.");

        var identificadoresProcessos = new HashSet<string>(StringComparer.Ordinal);
        var identificadoresThreads = new HashSet<string>(StringComparer.Ordinal);
        var processos = new List<BlocoControleProcesso>();
        foreach (var origem in entrada.Processos)
        {
            if (string.IsNullOrWhiteSpace(origem.Identificador) || !identificadoresProcessos.Add(origem.Identificador)) throw new ArgumentException("Identificador de processo ausente ou duplicado.");
            if (origem.TempoChegada < 0) throw new ArgumentException($"Chegada inválida para {origem.Identificador}.");
            if (origem.Threads is null || origem.Threads.Count == 0) throw new ArgumentException($"{origem.Identificador} deve conter ao menos uma thread.");
            var processo = new BlocoControleProcesso(origem.Identificador, origem.TempoChegada, origem.Prioridade);
            foreach (var origemThread in origem.Threads)
            {
                if (string.IsNullOrWhiteSpace(origemThread.Identificador) || !identificadoresThreads.Add(origemThread.Identificador)) throw new ArgumentException("Identificador de thread ausente ou duplicado.");
                if (origemThread.SurtosCpu is null || origemThread.SurtosCpu.Count == 0 || origemThread.SurtosCpu.Any(surto => surto <= 0))
                    throw new ArgumentException($"Surtos de CPU inválidos para {origemThread.Identificador}.");
                processo.Threads.Add(new BlocoControleThread(origemThread.Identificador, processo, origemThread.SurtosCpu));
            }
            processos.Add(processo);
        }
        return new CargaTrabalho(processos);
    }

    private static readonly JsonSerializerOptions OpcoesJson = new() { PropertyNameCaseInsensitive = true };
    private sealed class EntradaCarga { public List<EntradaProcesso>? Processos { get; set; } }
    private sealed class EntradaProcesso { public string Identificador { get; set; } = ""; public int TempoChegada { get; set; } public int Prioridade { get; set; } public List<EntradaThread>? Threads { get; set; } }
    private sealed class EntradaThread { public string Identificador { get; set; } = ""; public List<int>? SurtosCpu { get; set; } }
}
