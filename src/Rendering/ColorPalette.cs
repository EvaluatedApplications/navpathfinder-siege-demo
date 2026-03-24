using NavPathfinder.Demo.Data;
using NavPathfinder.Demo.Simulation;

namespace NavPathfinder.Demo.Rendering;

/// <summary>
/// Castle siege fantasy palette — vibrant, saturated colours on terrain backgrounds.
/// Uses 256-colour ANSI escape codes for terminal rendering.
/// </summary>
public static class ColorPalette
{
    // ── ANSI escape sequences ──────────────────────────────────────────────
    public const string Reset      = "\u001b[0m";
    public const string Eol        = "\u001b[K";       // erase to end of line
    public const string DimGray    = "\u001b[38;5;246m";   // lighter grey for readability
    public const string BoldWhite  = "\u001b[1;97m";
    public const string White      = "\u001b[97m";
    public const string BoldRed    = "\u001b[1;38;5;196m";
    public const string BoldGreen  = "\u001b[1;38;5;46m";
    public const string BoldYellow = "\u001b[1;38;5;220m";
    public const string BoldCyan   = "\u001b[1;38;5;51m";
    public const string BoldMagenta= "\u001b[1;38;5;199m";
    public const string Green      = "\u001b[38;5;40m";
    public const string Cyan       = "\u001b[38;5;45m";
    public const string Yellow     = "\u001b[38;5;220m";
    public const string Red        = "\u001b[38;5;196m";
    public const string Magenta    = "\u001b[38;5;199m";

    // ── Terrain background colours ─────────────────────────────────────────
    public const string BgOuterField  = "\u001b[48;5;22m";   // dark green grass
    public const string BgOuterYard   = "\u001b[48;5;236m";  // dark stone courtyard
    public const string BgInnerBailey = "\u001b[48;5;237m";  // polished stone
    public const string BgKeep        = "\u001b[48;5;238m";  // throne room stone
    public const string BgInvZone     = "\u001b[48;5;52m";   // dark red — invader-owned zone
    public const string BgContest     = "\u001b[48;5;58m";   // olive — contested zone

    // ── 256-colour wall palettes per castle layer (vibrant) ────────────────
    public const string OuterWallFg = "\u001b[38;5;178m";  // warm gold sandstone
    public const string InnerWallFg = "\u001b[38;5;255m";  // bright silver
    public const string KeepWallFg  = "\u001b[38;5;226m";  // bright gold

    // ── 256-colour for specific elements ───────────────────────────────────
    public const string BreachPathFg = "\u001b[38;5;199m";  // hot pink corridors

    // ── Invader faction — crimson/gold theme ───────────────────────────────
    public const string InvColor = "\u001b[38;5;196m";  // bright crimson

    // ── Defender faction — emerald/teal theme ──────────────────────────────
    public const string DefColor = "\u001b[38;5;40m";   // bright emerald

    // ── Civilian faction — warm amber theme ────────────────────────────────
    public const string CivColor = "\u001b[38;5;214m";  // warm amber

    /// <summary>Maps each BehaviourState to a distinct ANSI 256-colour foreground.</summary>
    public static string StateColor(BehaviourState state) => state switch
    {
        // Invader states — crimson/gold spectrum
        BehaviourState.Mustering => "\u001b[38;5;167m",  // warm rose — gathering
        BehaviourState.Advancing => "\u001b[38;5;196m",  // bright crimson — marching
        BehaviourState.Sieging   => "\u001b[38;5;214m",  // bright gold-orange — at gate
        BehaviourState.Breaching => "\u001b[38;5;199m",  // hot pink — breaking through
        BehaviourState.Claiming  => "\u001b[38;5;220m",  // bright gold — triumphant
        BehaviourState.Pushing   => "\u001b[38;5;160m",  // deep crimson — advancing

        // Defender states — emerald/teal spectrum
        BehaviourState.Holding      => "\u001b[38;5;40m",   // bright emerald — steady
        BehaviourState.Reinforcing  => "\u001b[38;5;51m",   // bright cyan — rushing
        BehaviourState.Fighting     => "\u001b[38;5;118m",  // bright lime — combat
        BehaviourState.FallingBack  => "\u001b[38;5;75m",   // bright periwinkle — retreat
        BehaviourState.LastStand    => "\u001b[1;38;5;231m", // bold white — final stand

        // Civilian states — warm amber spectrum
        BehaviourState.Sheltering => "\u001b[38;5;229m",  // light cream yellow
        BehaviourState.Fleeing    => "\u001b[38;5;220m",  // bright gold
        BehaviourState.Hidden     => "\u001b[38;5;136m",  // warm brown

        _ => "\u001b[38;5;250m"  // light gray fallback
    };

    /// <summary>FPS display colour based on target 15fps.</summary>
    public static string FpsColor(double fps) =>
        fps >= 13 ? Green : fps >= 8 ? Yellow : Red;

    /// <summary>Background tint for agent density (layered on terrain bg).</summary>
    public static string DensityBackground(int totalAgents) => totalAgents switch
    {
        >= 10 => "\u001b[48;5;243m",
        >= 6  => "\u001b[48;5;240m",
        >= 3  => "\u001b[48;5;238m",
        _     => ""
    };

    /// <summary>Terrain background colour for a floor cell based on castle layer.</summary>
    public static string TerrainBackground(int col, int row)
    {
        int oT = SiegeWorld.WallTop, oB = SiegeWorld.WallBot;
        int oL = SiegeWorld.WallLeft, oR = SiegeWorld.WallRight;
        int iT = SiegeWorld.InnerTop, iB = SiegeWorld.InnerBot;
        int iL = SiegeWorld.InnerLeft, iR = SiegeWorld.InnerRight;
        int kT = SiegeWorld.KeepTop, kB = SiegeWorld.KeepBot;
        int kL = SiegeWorld.KeepLeft, kR = SiegeWorld.KeepRight;

        // Keep interior
        if (col > kL && col < kR && row > kT && row < kB)
            return BgKeep;

        // Inner bailey
        if (col > iL && col < iR && row > iT && row < iB)
            return BgInnerBailey;

        // Outer courtyard (inside outer walls, outside inner walls)
        if (col > oL && col < oR && row > oT && row < oB)
            return BgOuterYard;

        // Outer field (outside castle walls)
        return BgOuterField;
    }

    /// <summary>Gate status display colour.</summary>
    public static string GateStatusColor(GateStatus status) => status switch
    {
        GateStatus.Open        => BoldCyan,
        GateStatus.Locked      => BoldGreen,
        GateStatus.UnderAttack => BoldRed,
        GateStatus.Breached    => BoldMagenta,
        _ => Reset
    };

    /// <summary>Wall foreground colour by castle layer.</summary>
    public static string WallColor(CastleLayer layer) => layer switch
    {
        CastleLayer.OuterCurtain => OuterWallFg,
        CastleLayer.InnerBailey  => InnerWallFg,
        CastleLayer.Keep         => KeepWallFg,
        _                        => DimGray
    };

    /// <summary>Wall background colour by castle layer (subtle stone tint).</summary>
    public static string WallBackground(CastleLayer layer) => layer switch
    {
        CastleLayer.OuterCurtain => "\u001b[48;5;235m",  // dark stone
        CastleLayer.InnerBailey  => "\u001b[48;5;236m",  // medium stone
        CastleLayer.Keep         => "\u001b[48;5;58m",   // warm dark gold tint
        _                        => ""
    };

    // ── HUD panel background ───────────────────────────────────────────────
    public const string BgHud = "\u001b[48;5;233m"; // very dark charcoal for HUD
    public const string HudSeparator = "\u001b[38;5;240m"; // dim separator lines
}
