namespace OsSimulator.Core;

/// <summary>
/// Dispositivo de bloco: guarda dados em blocos de tamanho fixo, endereçáveis (ex.: disco).
/// Dispositivo de caractere: envia/recebe um fluxo de caracteres, sem endereçamento (ex.: terminal).
/// (Tanenbaum, seção 5.1.1)
/// </summary>
public enum TipoDispositivo { Bloco, Caractere }

/// <summary>Um pedido de E/S esperando ou sendo atendido por um dispositivo.</summary>
public sealed record RequisicaoEs(BlocoControleThread Thread, bool Bloqueante, int TempoSolicitacao);

/// <summary>Resultado de uma E/S não bloqueante, guardado no TCB quando o dispositivo termina.</summary>
public sealed record ResultadoEs(string Dispositivo, int TempoSolicitacao, int TempoConclusao);

/// <summary>
/// Dispositivo simulado: atende um pedido por vez, em ordem de chegada (fila FCFS),
/// gastando sempre o mesmo tempo de serviço. Ao terminar, gera uma interrupção (evento).
/// </summary>
public sealed class Dispositivo
{
    /// <summary>Nomes aceitos no campo "dispositivo" da carga de trabalho.</summary>
    public static readonly string[] NomesConhecidos = ["disco", "terminal"];

    private readonly Queue<RequisicaoEs> _fila = new();

    public Dispositivo(string nome, TipoDispositivo tipo, int tempoServico)
    {
        Nome = nome;
        Tipo = tipo;
        TempoServico = tempoServico;
    }

    public string Nome { get; }
    public TipoDispositivo Tipo { get; }
    public int TempoServico { get; }

    /// <summary>Pedido sendo atendido agora (nulo = dispositivo livre).</summary>
    public RequisicaoEs? EmAtendimento { get; private set; }
    public bool Ocupado => EmAtendimento is not null;
    public int TamanhoFila => _fila.Count;

    // Métricas
    public int TempoOcupado { get; private set; }
    public int RequisicoesAtendidas { get; private set; }

    /// <summary>Coloca o pedido na fila. Retorna true se o dispositivo estava livre e já começou a atendê-lo.</summary>
    internal bool Enfileirar(RequisicaoEs requisicao)
    {
        _fila.Enqueue(requisicao);
        return !Ocupado && IniciarProximo();
    }

    /// <summary>Tira o próximo pedido da fila e começa a atendê-lo. Retorna false se a fila está vazia.</summary>
    internal bool IniciarProximo()
    {
        if (_fila.Count == 0) return false;
        EmAtendimento = _fila.Dequeue();
        return true;
    }

    /// <summary>Encerra o atendimento atual e devolve o pedido concluído.</summary>
    internal RequisicaoEs Concluir()
    {
        var concluida = EmAtendimento ?? throw new InvalidOperationException($"{Nome} não está atendendo nenhum pedido.");
        EmAtendimento = null;
        TempoOcupado += TempoServico;
        RequisicoesAtendidas++;
        return concluida;
    }
}
