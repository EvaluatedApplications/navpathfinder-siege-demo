using NavPathfinder.Demo.Data;

namespace NavPathfinder.Demo.Rendering;

/// <summary>
/// Faction-specific glyph arrays and density ramps for agent rendering.
/// Shape identifies faction; colour (from ColorPalette) identifies AI state.
/// </summary>
public static class GlyphSets
{
    // Invaders: sword/arrow theme      ·  ×  †  ‡  ⁂
    public static readonly char[] InvGlyphs = [' ', '·', '×', '†', '‡', '⁂', '⁂', '⁂', '⁂'];

    // Defenders: shield theme          ·  +  ‡  #  ⌂
    public static readonly char[] DefGlyphs = [' ', '·', '+', '‡', '#', '⌂', '⌂', '⌂', '⌂'];

    // Civilians: dot theme             ·  ∘  ○  ●
    public static readonly char[] CivGlyphs = [' ', '·', '∘', '○', '●', '●', '●', '●', '●'];

    // Sparkline bars for FPS history
    public static readonly char[] Spark = ['▁', '▂', '▃', '▄', '▅', '▆', '▇', '█'];

    /// <summary>Gets the density glyph for the dominant faction in a cell.</summary>
    public static char AgentGlyph(int inv, int def, int civ)
    {
        bool invDom = inv >= def && inv >= civ;
        bool defDom = def > inv  && def >= civ;

        if (invDom)
            return InvGlyphs[Math.Clamp(inv, 0, 8)];
        if (defDom)
            return DefGlyphs[Math.Clamp(def, 0, 8)];
        return CivGlyphs[Math.Clamp(civ, 0, 8)];
    }

    /// <summary>Wall box-drawing character based on layer and connectivity.</summary>
    public static char WallChar(CastleLayer layer, bool hasHoriz, bool hasVert) => layer switch
    {
        CastleLayer.OuterCurtain => (hasHoriz, hasVert) switch
        {
            (true, true)   => '╬',
            (true, false)  => '═',
            (false, true)  => '║',
            _              => '█'
        },
        CastleLayer.InnerBailey => (hasHoriz, hasVert) switch
        {
            (true, true)   => '┼',
            (true, false)  => '─',
            (false, true)  => '│',
            _              => '▪'
        },
        CastleLayer.Keep => (hasHoriz, hasVert) switch
        {
            (true, true)   => '╋',
            (true, false)  => '━',
            (false, true)  => '┃',
            _              => '◆'
        },
        _ => '█'
    };

    /// <summary>Gate status label for HUD display.</summary>
    public static string GateStatusLabel(GateStatus status) => status switch
    {
        GateStatus.Open        => "Open",
        GateStatus.Locked      => "Lck",
        GateStatus.UnderAttack => "ATK!",
        GateStatus.Breached    => "BRCH",
        _ => "?"
    };
}
