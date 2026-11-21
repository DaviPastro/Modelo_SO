using static System.FormattableString;

namespace OsSimulator.Core;

/// <summary>Métricas de uma thread. Todos os tempos estão no clock lógico.</summary>
public sealed record MetricasThread(
    string Thread,
    string Processo,
    int Chegada,
    int PrimeiroDespacho,
    int Finalizacao,
    int TempoCpu,
    int Espera,
    int Bloqueado)
{
    /// <summary>Tempo de retorno: da chegada até a finalização.</summary>
    public int Retorno => Finalizacao - Chegada;

    /// <summary>Tempo de resposta: da chegada até a primeira vez que executou na CPU.</summary>
    public int Resposta => PrimeiroDespacho - Chegada;
}

/// <summary>Uso de um dispositivo de E/S.</summary>
public sealed record MetricasDispositivo(string Dispositivo, TipoDispositivo Tipo, int Atendidas, int TempoOcupado, double Utilizacao);

/// <summary>Tempo de retorno de um processo: da chegada até a finalização da sua última thread.</summary>
public sealed record MetricasProcesso(string Processo, int Chegada, int Finalizacao)
{
    public int Retorno => Finalizacao - Chegada;
}

/// <summary>Métricas gerais de uma execução, calculadas a partir da carga e do motor ao final da simulação.</summary>
public sealed record MetricasSimulacao(
    int TempoTotal,
    int TempoUtilCpu,
    int QuantidadeTrocas,
    int TempoTrocas,
    IReadOnlyList<MetricasThread> Threads,
    IReadOnlyList<MetricasProcesso> Processos,
    IReadOnlyList<MetricasDispositivo> Dispositivos)
{
    public int TempoOcioso => TempoTotal - TempoUtilCpu - TempoTrocas;

    public double UtilizacaoCpu => Proporcao(TempoUtilCpu, TempoTotal);
    public double ProporcaoSobrecarga => Proporcao(TempoTrocas, TempoTotal);
    public double ProporcaoOciosa => Proporcao(TempoOcioso, TempoTotal);

    /// <summary>Processos concluídos por unidade de tempo simulado.</summary>
    public double Throughput => Proporcao(Processos.Count, TempoTotal);

    public double RetornoMedio => Threads.Count == 0 ? 0 : Threads.Average(t => t.Retorno);
    public double EsperaMedia => Threads.Count == 0 ? 0 : Threads.Average(t => t.Espera);
    public double RespostaMedia => Threads.Count == 0 ? 0 : Threads.Average(t => t.Resposta);

    public static MetricasSimulacao Calcular(CargaTrabalho carga, MotorSimulacao motor)
    {
        var threads = carga.Processos
            .SelectMany(p => p.Threads)
            .Where(t => t.Estado == EstadoThread.Finalizado)
            .OrderBy(t => t.Identificador, StringComparer.Ordinal)
            .Select(t => new MetricasThread(
                t.Identificador,
                t.Processo.Identificador,
                t.TempoChegada,
                t.TempoPrimeiroDespacho!.Value,
                t.TempoFinalizacao!.Value,
                t.TempoCpu,
                t.TempoEspera,
                t.TempoBloqueado))
            .ToList();

        var processos = carga.Processos
            .Where(p => p.Estado == EstadoProcesso.Finalizado)
            .OrderBy(p => p.Identificador, StringComparer.Ordinal)
            .Select(p => new MetricasProcesso(p.Identificador, p.TempoChegada, p.TempoFinalizacao!.Value))
            .ToList();

        var dispositivos = motor.Dispositivos
            .OrderBy(d => d.Nome, StringComparer.Ordinal)
            .Select(d => new MetricasDispositivo(d.Nome, d.Tipo, d.RequisicoesAtendidas, d.TempoOcupado,
                Proporcao(d.TempoOcupado, motor.Relogio)))
            .ToList();

        return new MetricasSimulacao(
            motor.Relogio,
            motor.Cpu.TempoUtil,
            motor.Cpu.QuantidadeTrocasContexto,
            motor.Cpu.TempoTotalTrocasContexto,
            threads,
            processos,
            dispositivos);
    }

    /// <summary>Linhas de texto prontas para exibir no terminal (sempre com ponto decimal, para ser reproduzível).</summary>
    public IEnumerable<string> Formatar()
    {
        yield return "===== MÉTRICAS =====";
        yield return "Thread Proc   Cheg  1oDesp  Fim  CPU  Espera  Bloq  Resposta  Retorno";
        foreach (var t in Threads)
            yield return Invariant($"{t.Thread,-6} {t.Processo,-6} {t.Chegada,4}  {t.PrimeiroDespacho,6}  {t.Finalizacao,3}  {t.TempoCpu,3}  {t.Espera,6}  {t.Bloqueado,4}  {t.Resposta,8}  {t.Retorno,7}");

        yield return Invariant($"Médias -> espera: {EsperaMedia:F2} | resposta: {RespostaMedia:F2} | retorno: {RetornoMedio:F2}");
        yield return "Retorno por processo -> " + string.Join(" | ", Processos.Select(p => Invariant($"{p.Processo}: {p.Retorno}")));
        yield return Invariant($"CPU (tempo total {TempoTotal}) -> carga útil: {TempoUtilCpu} ({UtilizacaoCpu:P1}) | troca de contexto: {TempoTrocas} ({ProporcaoSobrecarga:P1}) | ociosa: {TempoOcioso} ({ProporcaoOciosa:P1})");
        foreach (var d in Dispositivos)
            yield return Invariant($"Dispositivo {d.Dispositivo} ({d.Tipo}) -> {d.Atendidas} pedido(s), ocupado {d.TempoOcupado} ({d.Utilizacao:P1})");
        yield return Invariant($"Trocas de contexto: {QuantidadeTrocas} (sobrecarga total {TempoTrocas})");
        yield return Invariant($"Throughput: {Processos.Count} processo(s) em {TempoTotal} = {Throughput:F3} por unidade de tempo");
    }

    private static double Proporcao(int parte, int total) => total == 0 ? 0 : (double)parte / total;
}
