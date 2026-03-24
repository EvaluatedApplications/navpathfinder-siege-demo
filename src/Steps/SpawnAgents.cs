using System.Collections.Immutable;
using System.Numerics;
using EvalApp.Consumer;

using NavPathfinder.Demo.AI;
using NavPathfinder.Demo.Data;
using NavPathfinder.Demo.Data.Combat;
using NavPathfinder.Demo.Simulation;
using NavPathfinder.Sdk.Models;

namespace NavPathfinder.Demo.Steps;

/// <summary>
/// Pure step: accumulates per-faction spawn credits and emits new SimAgents when
/// credits cross 1.0, up to pool and on-screen limits.
///
/// Anti-doomstack: uses a coarse density grid to prevent spawning into packed areas
/// and adds goal jitter so agents don't all converge to the same pixel.
/// </summary>
public sealed class SpawnAgents : PureStep<SiegeTickData>
{
    private const float PressureScale = 0.6f;
    private const float ComebackBoost = 3.0f;

    // Density grid: cell size and max agents per cell before we refuse to spawn there.
    private const int   CellSize       = 4;
    private const int   MaxPerCell     = 6;
    private const float GoalJitter     = 3f;

    private static readonly Random _rng = new(42);

    // Reusable density grid — safe because step runs sequentially.
    private static int[]? _densityGrid;
    private static int _gridW, _gridH;

    public override SiegeTickData Execute(SiegeTickData data)
    {
        var cfg = data.Config ?? SiegeBalanceConfig.Default;
        float invBaseRate = cfg.InvBaseRate;
        float defBaseRate = cfg.DefBaseRate;
        float civBaseRate = cfg.CivBaseRate;
        int   invBaseMax  = cfg.InvBaseMax;
        int   defBaseMax  = cfg.DefBaseMax;
        int   civBaseMax  = cfg.CivBaseMax;

        if (data.WinState is WinState.InvadersWon or WinState.DefendersWon)
            return data;

        float invMorale  = data.InvaderMorale;
        float defMorale  = data.DefenderMorale;
        float civMorale  = data.CivilianMorale;

        float pipelineLoad = Math.Max(
            data.SimResult?.GetPopulation("Invader")?.Pressure  ?? 0f,
            Math.Max(
                data.SimResult?.GetPopulation("Defender")?.Pressure ?? 0f,
                data.SimResult?.GetPopulation("Civilian")?.Pressure ?? 0f));

        const float ThrottleStart = 0.60f;
        float pressureThrottle = pipelineLoad > ThrottleStart
            ? Math.Max(0f, 1f - (pipelineLoad - ThrottleStart) / (1f - ThrottleStart))
            : 1f;

        double targetFrameMs = cfg.TargetFrameMs;

        // FPS throttle: binary — if over target, zero all credit accumulation.
        // No gradual ramp. If we're over budget, stop adding fuel.
        float fpsThrottle = data.WallClockMs > targetFrameMs ? 0f : 1f;

        // ── Predictive agent budget ──────────────────────────────────────────
        // Use TOTAL frame time (WallClockMs), not just PathfindingMs.
        // Everything scales with agent count: pathfinding, separation, combat,
        // movement, rendering. One number captures the true cost.
        //
        // maxAffordable = currentAgents × (targetMs / actualMs) × 0.9
        //   At target FPS → maxAffordable ≈ currentAgents (stable)
        //   Above target  → maxAffordable > currentAgents (room to grow)
        //   Below target  → maxAffordable < currentAgents (must shrink via attrition)
        //   0.9 safety margin prevents oscillation around the target
        int totalActive = data.Invaders.Length
                        + data.Defenders.Length
                        + data.Civilians.Length;

        int maxAffordable = data.WallClockMs > 1.0 && data.ActiveAgentCount > 0
            ? (int)(data.ActiveAgentCount * (targetFrameMs / data.WallClockMs) * 0.9)
            : int.MaxValue;

        int totalHeadroom = Math.Max(0, maxAffordable - totalActive);

        float spawnThrottle = Math.Min(pressureThrottle, fpsThrottle);

        // When over budget, drain ALL banked credits — no burst when pressure eases.
        float invCredits = spawnThrottle < 1f ? 0f : data.InvaderCredits;
        float defCredits = spawnThrottle < 1f ? 0f : data.DefenderCredits;
        float civCredits = spawnThrottle < 1f ? 0f : data.CivilianCredits;

        float invBoost = data.WinState == WinState.DefendersWinning ? ComebackBoost : 1f;
        float defBoost = data.WinState == WinState.InvadersWinning  ? ComebackBoost : 1f;
        float civBoost = data.WinState == WinState.InvadersWinning  ? 0f : 1f;

        float newInvCredits = invCredits + invBaseRate * invMorale * invBoost * spawnThrottle;
        float newDefCredits = defCredits + defBaseRate * defMorale * defBoost * spawnThrottle;
        float newCivCredits = civCredits + civBaseRate * civMorale * civBoost * spawnThrottle;

        int invActive = data.Invaders.Length;
        int defActive = data.Defenders.Length;
        int civActive = data.Civilians.Length;

        int invMaxNow = (int)(invBaseMax * (0.5f + invMorale * PressureScale));
        int defMaxNow = (int)(defBaseMax * (0.5f + defMorale * PressureScale));
        int civMaxNow = civBaseMax;

        int invWant = Math.Min(data.InvaderPool,  Math.Min((int)newInvCredits, Math.Max(0, invMaxNow - invActive)));
        int defWant = Math.Min(data.DefenderPool, Math.Min((int)newDefCredits, Math.Max(0, defMaxNow - defActive)));
        int civWant = Math.Min(data.CivilianPool, Math.Min((int)newCivCredits, Math.Max(0, civMaxNow - civActive)));

        // Performance headroom cap
        int totalWant = invWant + defWant + civWant;
        if (totalWant > totalHeadroom && totalWant > 0)
        {
            float ratio = (float)totalHeadroom / totalWant;
            invWant = (int)(invWant * ratio);
            defWant = (int)(defWant * ratio);
            civWant = (int)(civWant * ratio);
        }

        // ── Build density grid from all current agents ───────────────────────
        BuildDensityGrid(
            data.Invaders,
            data.Defenders,
            data.Civilians);

        // ── Spawn with density checks ────────────────────────────────────────
        int baseId = data.TickNumber * 1000;

        var (newInvaders, invActual)  = invWant > 0 ? CreateNewInvaders(data,  invWant,  baseId) : (null, 0);
        var (newDefenders, defActual) = defWant > 0 ? CreateNewDefenders(data, defWant,  baseId + 300) : (null, 0);
        var (newCivilians, civActual) = civWant > 0 ? CreateNewCivilians(data, civWant,  baseId + 600) : (null, 0);

        // Only deduct credits for agents that actually spawned (density rejection may skip some)
        newInvCredits -= invActual;
        newDefCredits -= defActual;
        newCivCredits -= civActual;

        var finalInvaders  = newInvaders  is null ? data.Invaders  : data.Invaders.AddRange(newInvaders.Value);
        var finalDefenders = newDefenders is null ? data.Defenders : data.Defenders.AddRange(newDefenders.Value);
        var finalCivilians = newCivilians is null ? data.Civilians : data.Civilians.AddRange(newCivilians.Value);

        return data with
        {
            InvaderCredits  = newInvCredits,
            DefenderCredits = newDefCredits,
            CivilianCredits = newCivCredits,
            InvaderPool     = data.InvaderPool  - invActual,
            DefenderPool    = data.DefenderPool - defActual,
            CivilianPool    = data.CivilianPool - civActual,
            Invaders  = finalInvaders,
            Defenders = finalDefenders,
            Civilians = finalCivilians,
        };
    }

    // ── Density grid ─────────────────────────────────────────────────────────

    private static void BuildDensityGrid(
        ImmutableArray<SimAgent> invaders,
        ImmutableArray<SimAgent> defenders,
        ImmutableArray<SimAgent> civilians)
    {
        int cols = SiegeWorld.GridCols;
        int rows = SiegeWorld.GridRows;
        _gridW = (cols / CellSize) + 1;
        _gridH = (rows / CellSize) + 1;
        int size = _gridW * _gridH;

        if (_densityGrid == null || _densityGrid.Length < size)
            _densityGrid = new int[size];
        else
            Array.Clear(_densityGrid, 0, size);

        AddToDensity(invaders);
        AddToDensity(defenders);
        AddToDensity(civilians);
    }

    private static void AddToDensity(ImmutableArray<SimAgent> agents)
    {
        for (int i = 0; i < agents.Length; i++)
        {
            int cx = Math.Clamp((int)(agents[i].Position.X / CellSize), 0, _gridW - 1);
            int cy = Math.Clamp((int)(agents[i].Position.Y / CellSize), 0, _gridH - 1);
            _densityGrid![cy * _gridW + cx]++;
        }
    }

    private static bool IsCellFull(float x, float y)
    {
        int cx = Math.Clamp((int)(x / CellSize), 0, _gridW - 1);
        int cy = Math.Clamp((int)(y / CellSize), 0, _gridH - 1);
        return _densityGrid![cy * _gridW + cx] >= MaxPerCell;
    }

    private static void IncrementCell(float x, float y)
    {
        int cx = Math.Clamp((int)(x / CellSize), 0, _gridW - 1);
        int cy = Math.Clamp((int)(y / CellSize), 0, _gridH - 1);
        _densityGrid![cy * _gridW + cx]++;
    }

    // ── Goal jitter ──────────────────────────────────────────────────────────

    private static Vector2 Jitter(Vector2 center, float radius)
    {
        float dx = ((float)_rng.NextDouble() - 0.5f) * 2f * radius;
        float dy = ((float)_rng.NextDouble() - 0.5f) * 2f * radius;
        return new Vector2(center.X + dx, center.Y + dy);
    }

    // ── Spawn helpers ────────────────────────────────────────────────────────

    private static (ImmutableArray<SimAgent>?, int actualCount) CreateNewInvaders(
        SiegeTickData data, int count, int baseId)
    {
        var centerGoal = new Vector2(SiegeWorld.GateHoriz, (SiegeWorld.WallTop + SiegeWorld.WallBot) / 2f);

        var forwardZones = GetForwardSpawnZones(data);
        int forwardCount = forwardZones.Count > 0 ? (int)(count * 0.6f) : 0;
        int cardinalCount = count - forwardCount;

        var builder = ImmutableArray.CreateBuilder<SimAgent>(count);
        int id = baseId;
        int spawned = 0;

        // Forward spawns at claimed zone centers — clamped to courtyard layer
        int oL = SiegeWorld.WallLeft + 1, oR = SiegeWorld.WallRight - 1;
        int oT = SiegeWorld.WallTop + 1,  oB = SiegeWorld.WallBot - 1;
        int iL = SiegeWorld.InnerLeft,     iR = SiegeWorld.InnerRight;
        int iT = SiegeWorld.InnerTop,      iB = SiegeWorld.InnerBot;

        for (int i = 0; i < forwardCount; i++)
        {
            var zone = forwardZones[i % forwardZones.Count];
            // Clamp spawn box to outer courtyard (inside outer walls, outside inner walls)
            float x0 = Math.Max(zone.X - 6f, oL);
            float x1 = Math.Min(zone.X + 6f, oR);
            float y0 = Math.Max(zone.Y - 6f, oT);
            float y1 = Math.Min(zone.Y + 6f, oB);

            // Shrink if overlapping inner walls — don't spawn inside inner bailey
            if (zone.X < iL) x1 = Math.Min(x1, iL - 1);
            else if (zone.X > iR) x0 = Math.Max(x0, iR + 1);
            if (zone.Y < iT) y1 = Math.Min(y1, iT - 1);
            else if (zone.Y > iB) y0 = Math.Max(y0, iB + 1);

            var pos = SampleFreeWithDensity(x0, x1, y0, y1);
            if (pos == null) continue;
            var goal = Jitter(centerGoal, GoalJitter);

            bool isMage = _rng.NextDouble() < 0.15;
            var invStats = isMage
                ? new AgentStats(
                    Strength: 6f + (float)_rng.NextDouble() * 4f,
                    Agility: 7f + (float)_rng.NextDouble() * 4f,
                    Toughness: 5f + (float)_rng.NextDouble() * 4f,
                    Precision: 14f + (float)_rng.NextDouble() * 6f)
                : new AgentStats(
                    Strength: 12f + (float)_rng.NextDouble() * 6f,
                    Agility: 9f + (float)_rng.NextDouble() * 5f,
                    Toughness: 8f + (float)_rng.NextDouble() * 5f,
                    Precision: 8f + (float)_rng.NextDouble() * 6f);
            var invGear = isMage
                ? new Equipment(DamageType.Fire,
                    WeaponDamage: 8f + (float)_rng.NextDouble() * 5f, ArmorReduction: 1f)
                : new Equipment(DamageType.Slash,
                    WeaponDamage: 7f + (float)_rng.NextDouble() * 4f, ArmorReduction: 3f);

            builder.Add(new SimAgent(id++, pos.Value, goal, true, AgentRole.Invader,
                Behaviour: BehaviourState.Mustering,
                Stats: invStats, Skills: new AgentSkills(), Gear: invGear, Combat: new CombatState()));
            IncrementCell(pos.Value.X, pos.Value.Y);
            spawned++;
        }

        // Cardinal edge spawns
        var zones = GetInvaderZones();
        int startZone = data.TickNumber % zones.Length;
        for (int i = 0; i < cardinalCount; i++)
        {
            var z = zones[(startZone + i) % zones.Length];
            var pos = SampleFreeWithDensity(z.x0, z.x1, z.y0, z.y1);
            if (pos == null) continue;
            var goal = Jitter(centerGoal, GoalJitter);

            bool isMage = _rng.NextDouble() < 0.15;
            var invStats = isMage
                ? new AgentStats(
                    Strength: 6f + (float)_rng.NextDouble() * 4f,
                    Agility: 7f + (float)_rng.NextDouble() * 4f,
                    Toughness: 5f + (float)_rng.NextDouble() * 4f,
                    Precision: 14f + (float)_rng.NextDouble() * 6f)
                : new AgentStats(
                    Strength: 12f + (float)_rng.NextDouble() * 6f,
                    Agility: 9f + (float)_rng.NextDouble() * 5f,
                    Toughness: 8f + (float)_rng.NextDouble() * 5f,
                    Precision: 8f + (float)_rng.NextDouble() * 6f);
            var invGear = isMage
                ? new Equipment(DamageType.Fire,
                    WeaponDamage: 8f + (float)_rng.NextDouble() * 5f, ArmorReduction: 1f)
                : new Equipment(DamageType.Slash,
                    WeaponDamage: 7f + (float)_rng.NextDouble() * 4f, ArmorReduction: 3f);

            builder.Add(new SimAgent(id++, pos.Value, goal, true, AgentRole.Invader,
                Behaviour: BehaviourState.Mustering,
                Stats: invStats, Skills: new AgentSkills(), Gear: invGear, Combat: new CombatState()));
            IncrementCell(pos.Value.X, pos.Value.Y);
            spawned++;
        }

        return spawned > 0 ? (builder.ToImmutable(), spawned) : (null, 0);
    }

    private static List<Vector2> GetForwardSpawnZones(SiegeTickData data)
    {
        var zones = data.Zones;
        if (zones.IsDefaultOrEmpty)
            return [];

        var result = new List<Vector2>();
        foreach (var zone in zones)
            if (zone.SpawnEnabled && zone.Owner == AgentRole.Invader)
                result.Add(zone.SpawnPoint);
        return result;
    }

    private static (ImmutableArray<SimAgent>?, int actualCount) CreateNewDefenders(
        SiegeTickData data, int count, int baseId)
    {
        var gates = data.Gates;
        bool retreatMode = data.ActiveDefenceLine != CastleLayer.OuterCurtain;

        var priorityGates = new List<GateState>();
        var contestedGates = new List<GateState>();

        foreach (var g in gates)
        {
            bool isContested = g.Status is GateStatus.UnderAttack or GateStatus.Breached;
            if (retreatMode && g.Layer != CastleLayer.OuterCurtain && isContested)
                priorityGates.Add(g);
            else if (isContested)
                contestedGates.Add(g);
        }

        SortByUrgency(priorityGates);
        SortByUrgency(contestedGates);
        var hotGates = priorityGates.Count > 0 ? priorityGates : contestedGates;

        if (hotGates.Count == 0)
            hotGates.Add(gates[data.TickNumber % gates.Length]);

        int gateDefenders     = (int)(count * 0.25f);
        int sortieDefenders   = (int)(count * 0.30f);
        int archerDefenders   = (int)(count * 0.20f); // Pierce — wall archers
        int interiorDefenders = count - gateDefenders - sortieDefenders - archerDefenders;

        int perGate  = Math.Max(1, gateDefenders / hotGates.Count);
        int leftover = gateDefenders - perGate * hotGates.Count;

        int perSortie  = Math.Max(1, sortieDefenders / hotGates.Count);
        int sortieLeft = sortieDefenders - perSortie * hotGates.Count;
        bool canSortie = data.ActiveDefenceLine == CastleLayer.OuterCurtain;

        float ix0 = SiegeWorld.WallLeft  + 2f, ix1 = SiegeWorld.WallRight - 2f;
        float iy0 = SiegeWorld.WallTop   + 2f, iy1 = SiegeWorld.WallBot   - 2f;

        var builder = ImmutableArray.CreateBuilder<SimAgent>(count);
        int id = baseId;
        int spawned = 0;

        for (int gi = 0; gi < hotGates.Count; gi++)
        {
            var g       = hotGates[gi];
            int toSpawn = perGate + (gi < leftover ? 1 : 0);

            int   gr     = (int)g.Center.Y;
            bool  isHoriz = gr == SiegeWorld.WallTop || gr == SiegeWorld.WallBot;
            float inward  = isHoriz
                ? (gr == SiegeWorld.WallTop ?  2.5f : -2.5f)
                : ((int)g.Center.X == SiegeWorld.WallLeft ? 2.5f : -2.5f);

            float gx0 = isHoriz ? g.Center.X - 4f : g.Center.X + (inward > 0 ? inward : inward);
            float gx1 = isHoriz ? g.Center.X + 4f : g.Center.X + (inward > 0 ? inward + 3f : inward - 3f);
            float gy0 = isHoriz ? g.Center.Y + (inward > 0 ? inward : inward - 2f) : g.Center.Y - 4f;
            float gy1 = isHoriz ? g.Center.Y + (inward > 0 ? inward + 2f : inward) : g.Center.Y + 4f;

            Vector2 goalCenter = g.Center;

            for (int i = 0; i < toSpawn; i++)
            {
                var pos = SampleFreeWithDensity(gx0, gx1, gy0, gy1);
                if (pos == null) continue;
                var goal = Jitter(goalCenter, GoalJitter);
                var defStats = new AgentStats(
                    Strength: 8f + (float)_rng.NextDouble() * 4f,
                    Agility: 6f + (float)_rng.NextDouble() * 4f,
                    Toughness: 10f + (float)_rng.NextDouble() * 6f,
                    Precision: 8f + (float)_rng.NextDouble() * 4f);
                var defGear = new Equipment(DamageType.Blunt,
                    WeaponDamage: 5f + (float)_rng.NextDouble() * 3f,
                    ArmorReduction: 5f + (float)_rng.NextDouble() * 3f);
                builder.Add(new SimAgent(id++, pos.Value, goal, true, AgentRole.Defender,
                    Behaviour: BehaviourState.Holding,
                    Stats: defStats, Skills: new AgentSkills(), Gear: defGear, Combat: new CombatState()));
                IncrementCell(pos.Value.X, pos.Value.Y);
                spawned++;
            }
        }

        // Sortie defenders — spawn OUTSIDE gates to intercept invaders
        if (canSortie)
        {
            for (int gi = 0; gi < hotGates.Count; gi++)
            {
                var g       = hotGates[gi];
                int toSpawn = perSortie + (gi < sortieLeft ? 1 : 0);

                int   gr      = (int)g.Center.Y;
                bool  isHoriz = gr == SiegeWorld.WallTop || gr == SiegeWorld.WallBot;
                float outward = isHoriz
                    ? (gr == SiegeWorld.WallTop ? -4f : 4f)
                    : ((int)g.Center.X == SiegeWorld.WallLeft ? -4f : 4f);

                float sx0 = isHoriz ? g.Center.X - 4f : g.Center.X + outward;
                float sx1 = isHoriz ? g.Center.X + 4f : g.Center.X + outward + (outward > 0 ? 3f : -3f);
                float sy0 = isHoriz ? g.Center.Y + outward : g.Center.Y - 4f;
                float sy1 = isHoriz ? g.Center.Y + outward + (outward > 0 ? 3f : -3f) : g.Center.Y + 4f;

                // Clamp to world bounds
                sx0 = Math.Max(1f, sx0); sx1 = Math.Min(SiegeWorld.GridCols - 2f, sx1);
                sy0 = Math.Max(1f, sy0); sy1 = Math.Min(SiegeWorld.GridRows - 2f, sy1);
                if (sx0 > sx1) (sx0, sx1) = (sx1, sx0);
                if (sy0 > sy1) (sy0, sy1) = (sy1, sy0);

                var sortieGoal = FsmContext.OutsideGatePosition(g);

                for (int i = 0; i < toSpawn; i++)
                {
                    var pos = SampleFreeWithDensity(sx0, sx1, sy0, sy1);
                    if (pos == null) continue;
                    var goal = Jitter(sortieGoal, GoalJitter);
                    var defStats = new AgentStats(
                        Strength: 10f + (float)_rng.NextDouble() * 4f,
                        Agility: 8f + (float)_rng.NextDouble() * 4f,
                        Toughness: 10f + (float)_rng.NextDouble() * 6f,
                        Precision: 8f + (float)_rng.NextDouble() * 4f);
                    var defGear = new Equipment(DamageType.Slash,
                        WeaponDamage: 6f + (float)_rng.NextDouble() * 4f,
                        ArmorReduction: 4f + (float)_rng.NextDouble() * 3f);
                    builder.Add(new SimAgent(id++, pos.Value, goal, true, AgentRole.Defender,
                        Behaviour: BehaviourState.Sortie,
                        Stats: defStats, Skills: new AgentSkills(), Gear: defGear, Combat: new CombatState()));
                    IncrementCell(pos.Value.X, pos.Value.Y);
                    spawned++;
                }
            }
        }
        else
        {
            // No sortie when defence line has retreated — all extras go to gate defence
            gateDefenders += sortieDefenders;
        }

        // Interior reserve
        Vector2 keepCenter = SiegeWorld.SafeZones[4];
        for (int i = 0; i < interiorDefenders; i++)
        {
            var pos = SampleFreeWithDensity(ix0, ix1, iy0, iy1);
            if (pos == null) continue;
            var goal = Jitter(keepCenter, GoalJitter);
            var defStats = new AgentStats(
                Strength: 8f + (float)_rng.NextDouble() * 4f,
                Agility: 6f + (float)_rng.NextDouble() * 4f,
                Toughness: 10f + (float)_rng.NextDouble() * 6f,
                Precision: 8f + (float)_rng.NextDouble() * 4f);
            var defGear = new Equipment(DamageType.Blunt,
                WeaponDamage: 5f + (float)_rng.NextDouble() * 3f,
                ArmorReduction: 5f + (float)_rng.NextDouble() * 3f);
            builder.Add(new SimAgent(id++, pos.Value, goal, true, AgentRole.Defender,
                Behaviour: BehaviourState.Holding,
                Stats: defStats, Skills: new AgentSkills(), Gear: defGear, Combat: new CombatState()));
            IncrementCell(pos.Value.X, pos.Value.Y);
            spawned++;
        }

        // Archer defenders — Pierce damage, spawn on wall interior for ranged support
        for (int i = 0; i < archerDefenders; i++)
        {
            var pos = SampleFreeWithDensity(ix0, ix1, iy0, iy1);
            if (pos == null) continue;
            var goal = Jitter(keepCenter, GoalJitter);
            var archerStats = new AgentStats(
                Strength: 6f + (float)_rng.NextDouble() * 3f,
                Agility: 8f + (float)_rng.NextDouble() * 5f,
                Toughness: 7f + (float)_rng.NextDouble() * 4f,
                Precision: 12f + (float)_rng.NextDouble() * 5f);
            var archerGear = new Equipment(DamageType.Pierce,
                WeaponDamage: 4f + (float)_rng.NextDouble() * 3f,
                ArmorReduction: 2f + (float)_rng.NextDouble() * 2f);
            builder.Add(new SimAgent(id++, pos.Value, goal, true, AgentRole.Defender,
                Behaviour: BehaviourState.Holding,
                Stats: archerStats, Skills: new AgentSkills(), Gear: archerGear, Combat: new CombatState()));
            IncrementCell(pos.Value.X, pos.Value.Y);
            spawned++;
        }

        return spawned > 0 ? (builder.ToImmutable(), spawned) : (null, 0);
    }

    private static void SortByUrgency(List<GateState> gates)
    {
        gates.Sort((a, b) =>
        {
            int aScore = a.TicksUnderAttack + (a.Status == GateStatus.Breached ? 500 : 0);
            int bScore = b.TicksUnderAttack + (b.Status == GateStatus.Breached ? 500 : 0);
            return bScore.CompareTo(aScore);
        });
    }

    private static (ImmutableArray<SimAgent>?, int actualCount) CreateNewCivilians(
        SiegeTickData data, int count, int baseId)
    {
        Vector2 keepCenter = SiegeWorld.SafeZones[4];

        float x0 = SiegeWorld.WallLeft  + 2f, x1 = SiegeWorld.WallRight - 2f;
        float y0 = SiegeWorld.WallTop   + 2f, y1 = SiegeWorld.WallBot   - 2f;

        var builder = ImmutableArray.CreateBuilder<SimAgent>(count);
        int spawned = 0;
        for (int i = 0; i < count; i++)
        {
            var pos = SampleFreeWithDensity(x0, x1, y0, y1);
            if (pos == null) continue;
            var goal = Jitter(keepCenter, GoalJitter);
            var civStats = new AgentStats(Strength: 3f, Agility: 5f, Toughness: 4f, Precision: 3f);
            var civGear = new Equipment(DamageType.Blunt, WeaponDamage: 1f, ArmorReduction: 0f);
            builder.Add(new SimAgent(baseId + i, pos.Value, goal, true, AgentRole.Civilian,
                Behaviour: BehaviourState.Sheltering,
                Stats: civStats, Skills: new AgentSkills(), Gear: civGear, Combat: new CombatState()));
            IncrementCell(pos.Value.X, pos.Value.Y);
            spawned++;
        }
        return spawned > 0 ? (builder.ToImmutable(), spawned) : (null, 0);
    }

    /// <summary>
    /// Picks a random position within bounds that is not a wall AND not in a full density cell.
    /// Returns null if no viable position found after retries — credits carry to next tick.
    /// </summary>
    private static Vector2? SampleFreeWithDensity(float x0, float x1, float y0, float y1)
    {
        for (int attempt = 0; attempt < 12; attempt++)
        {
            float x = x0 + (float)_rng.NextDouble() * (x1 - x0);
            float y = y0 + (float)_rng.NextDouble() * (y1 - y0);
            if (SiegeWorld.IsWall((int)x, (int)y)) continue;
            if (IsCellFull(x, y)) continue;
            return new Vector2(x, y);
        }
        return null; // area is packed — skip this spawn, credits preserved
    }

    private static (float x0, float x1, float y0, float y1)[] GetInvaderZones()
    {
        int c = SiegeWorld.GridCols, r = SiegeWorld.GridRows;
        int wL = SiegeWorld.WallLeft,  wR = SiegeWorld.WallRight;
        int wT = SiegeWorld.WallTop,   wB = SiegeWorld.WallBot;

        float nY1 = Math.Max(1.5f, wT - 1.5f);
        float sY0 = Math.Min(r - 1.5f, wB + 1.5f);
        float wX1 = Math.Max(1.5f, wL - 1.5f);
        float eX0 = Math.Min(c - 1.5f, wR + 1.5f);

        return
        [
            (0.5f,   c - 0.5f,  0.5f,  nY1),
            (0.5f,   wX1,       nY1,   sY0),
            (0.5f,   c - 0.5f,  sY0,   r - 0.5f),
            (eX0,    c - 0.5f,  nY1,   sY0),
        ];
    }
}
