namespace NavPathfinder.Demo.Rendering;

/// <summary>
/// Per-cell visual state in the render grid.
/// Steps write char/fg/bg; SerializeFrame reads them to produce ANSI output.
/// </summary>
public record struct RenderCell(char Char, byte Fg, byte Bg);

/// <summary>
/// Mutable frame buffer shared across rendering steps via SiegeTickData.
/// Each step appends ANSI output to the shared StringBuilder.
/// Reference semantics — the record field stays immutable, contents are mutable.
/// </summary>
public sealed class RenderBuffer
{
    public int Rows { get; }
    public int Cols { get; }

    // Agent density caches — rebuilt each frame
    public int[,] InvCount { get; }
    public int[,] DefCount { get; }
    public int[,] CivCount { get; }

    // Per-faction dominant state — colour comes from the dominant FACTION's state
    public Data.BehaviourState[,] InvDomState { get; }
    public Data.BehaviourState[,] DefDomState { get; }
    public Data.BehaviourState[,] CivDomState { get; }

    // FPS sparkline history — persists across frames
    public float[] FpsHistory { get; }
    public int FpsHistoryIndex { get; set; }

    // Leader positions — highest-level agent per faction (row, col) or (-1,-1) if none
    public (int Row, int Col) InvLeaderCell { get; set; } = (-1, -1);
    public (int Row, int Col) DefLeaderCell { get; set; } = (-1, -1);

    // Accumulated ANSI output — each step appends its section
    public System.Text.StringBuilder StringBuilder { get; } = new(1 << 17);

    public RenderBuffer(int rows, int cols)
    {
        Rows = rows;
        Cols = cols;
        InvCount = new int[rows, cols];
        DefCount = new int[rows, cols];
        CivCount = new int[rows, cols];
        InvDomState = new Data.BehaviourState[rows, cols];
        DefDomState = new Data.BehaviourState[rows, cols];
        CivDomState = new Data.BehaviourState[rows, cols];
        FpsHistory = new float[30];
    }

    /// <summary>Clears agent data and StringBuilder for a new frame. FPS history is preserved.</summary>
    public void ClearFrame()
    {
        Array.Clear(InvCount);
        Array.Clear(DefCount);
        Array.Clear(CivCount);
        Array.Clear(InvDomState);
        Array.Clear(DefDomState);
        Array.Clear(CivDomState);
        InvLeaderCell = (-1, -1);
        DefLeaderCell = (-1, -1);
        StringBuilder.Clear();
    }
}
