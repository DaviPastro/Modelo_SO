namespace OsSimulator.Core;

/// <summary>
/// Registradores simulados: o mínimo necessário para retomar uma thread de onde ela parou.
/// </summary>
public sealed class ContextoCpu
{
    /// <summary>Índice da operação atual da thread (contador de programa lógico).</summary>
    public int ContadorPrograma { get; internal set; }

    /// <summary>Quanto falta do surto de CPU atual. Zero significa "surto ainda não iniciado".</summary>
    public int TempoRestante { get; internal set; }
}

/// <summary>A CPU simulada (uma única CPU).</summary>
public sealed class Cpu
{
    public BlocoControleThread? ThreadAtual { get; private set; }
    public ContextoCpu? Registradores { get; private set; }
    public bool EmTrocaDeContexto { get; private set; }
    public bool Livre => ThreadAtual is null;

    public int QuantidadeTrocasContexto { get; private set; }
    public int TempoTotalTrocasContexto { get; private set; }

    /// <summary>A CPU fica reservada para a thread, mas ainda não executa nada dela.</summary>
    internal void IniciarTrocaDeContexto(BlocoControleThread thread, int custo)
    {
        if (!Livre) throw new InvalidOperationException("A CPU já está ocupada.");
        ThreadAtual = thread;
        EmTrocaDeContexto = true;
        QuantidadeTrocasContexto++;
        TempoTotalTrocasContexto += custo;
    }

    /// <summary>Restaura o contexto: desempilha os registradores da pilha lógica da thread.</summary>
    internal void ConcluirTrocaDeContexto()
    {
        Registradores = ThreadAtual!.PilhaLogica.Pop();
        EmTrocaDeContexto = false;
    }

    /// <summary>
    /// Salva o contexto: empilha os registradores na pilha lógica da thread e libera a CPU.
    /// Será usado quando a thread sair da CPU sem terminar (preempção na etapa 3, bloqueio na etapa 4).
    /// </summary>
    internal void SalvarContextoELiberar()
    {
        ThreadAtual!.PilhaLogica.Push(Registradores!);
        Liberar();
    }

    /// <summary>Libera a CPU sem salvar nada (usado quando a thread termina).</summary>
    internal void Liberar()
    {
        ThreadAtual = null;
        Registradores = null;
    }
}
