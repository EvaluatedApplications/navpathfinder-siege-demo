# NavPathfinder — Siege Demo

~4,000 autonomous agents. Castle siege. Locked 15 fps. In your terminal.

## ⬇️ Quick Start

```
git clone https://github.com/EvaluatedApplications/navpathfinder-siege-demo.git
```

Double-click **`RUN.bat`** — maximise the terminal, press Enter. Ctrl+C to quit.

Requires [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0).

```bash
cd src && dotnet run -c Release
```

Maximise your terminal, press Enter. Ctrl+C to quit. Building from source requires .NET 8.

---

## Why This Exists

Threading in game simulation is an unsolved problem for most teams.

You know the pattern: you need thousands of agents pathfinding every frame. You `Task.WhenAll` them. It works on your machine. Then QA finds a race condition in the spatial grid. You add locks. Performance drops. You try lock-free structures. Now you need a principal-level engineer who understands memory ordering to review every change. Six months later you have a bespoke threading layer that one person on the team understands and nobody wants to touch.

This demo is what happens when you skip all of that.

The game code in `src/` is entirely sequential. Every step is a pure function: data in, data out, no shared mutable state. An immutable record flows through 28 steps per frame. The developer who wrote these steps never thought about threads, locks, or contention. They just wrote game logic.

The runtime handles the rest. It measures actual resource pressure at each pipeline gate — CPU, disk, network — and adjusts parallelism at runtime. Not a fixed thread pool. Not a config file you tune by hand. The runtime observes wait times across gates and converges on optimal concurrency for your specific hardware, workload, and frame budget. It saves what it learns to `tuner-state/` so the next run warm-starts.

**The result: async execution at native speed.** The overhead of the pipeline abstraction is measured in microseconds per step, not milliseconds. You get adaptive multi-core throughput with the safety of single-threaded code. That's not a tradeoff most frameworks offer — it's usually one or the other.

---

## What You're Watching

Three factions fight over a layered castle with three concentric defence lines:

- **Invaders** muster at the edges, assault gates, breach them, push inward
- **Defenders** sortie outside the walls, fight at chokepoints, fall back when outnumbered
- **Civilians** flee toward the interior, shelter when they reach safety

When 66% of a layer's gates fall, the defence line retreats. The siege ends when the keep falls or the invaders break.

### Each frame, 28 steps execute

Gate breach pressure. AI state machines (16 states across 3 factions). Threat and defence influence maps. Multi-agent pathfinding through a triangulated navmesh. Melee combat with damage types, armour, crits, and counter-attacks. Spawning and attrition. ANSI rendering in a single buffered write.

One immutable data record flows through all 28 steps. No step can see another step's intermediate state. Race conditions are structurally impossible.

---

## The Dwarf Fortress Comparison

This demo exists because of a specific bottleneck: DF-scale simulation hits a wall around 200 agents. The pathfinding is what kills it.

| | Dwarf Fortress | This Demo |
|--|---------------|-----------|
| Agents before FPS drops | ~200 | ~3,900 (equilibrium) |
| Pathfinding | Single-thread A* per agent | Multi-agent, batched, adaptive parallelism |
| World representation | Tile grid | Triangle mesh (CDT with Ruppert refinement) |
| Threading model | Single-threaded | Automatic — you don't manage it |
| Simulation depth | Deep (fluids, temperature, z-axis, personalities) | Focused (movement, combat, morale, zone control) |
| Frame pacing | Variable | Locked 15 fps (sub-ms spin-wait) |

**DF wins on depth**, and it always will — 20 years of simulation systems. But the pathfinding-and-movement layer, the thing that makes DF crawl at fortress scale, is a solved problem when you let the SDK and runtime handle it.

The point isn't replacing DF's simulation. It's that this particular problem — scaling autonomous agents to thousands while keeping frame rate locked — doesn't need to be your team's problem anymore.

---

## What The Runtime Actually Gives You

Without it, scaling this demo to ~4,000 agents at locked 15 fps requires:

- A work-stealing scheduler tuned to your core count
- Lock-free spatial data structures for concurrent pathfinding
- Contention-aware batching that adapts to workload variance per frame
- Memory-ordered state transitions with no torn reads
- An engineer who has shipped this exact kind of system before

With it, you write this:

```csharp
// One step. Pure function. No threading code. No locks.
public class TickSimulation : ContextPureStep<GlobalCtx, DomainCtx, SiegeTickData>
{
    protected override async ValueTask<SiegeTickData> TransformAsync(
        SiegeTickData data, GlobalCtx global, DomainCtx domain,
        StepContext ctx, CancellationToken ct)
    {
        var result = await domain.AgentService!.TickAsync(
            data.AgentDtos, domain.NavMesh!, data.TickOptions, ct);
        return data with { PathResults = result };
    }
}
```

The runtime wraps this in a resource gate, measures how long the await takes relative to other gates in the pipeline, and adjusts how many of these can execute concurrently across your ForEach collections. Every frame. Automatically.

You get the throughput of a hand-rolled native threading layer with the safety guarantees of immutable functional code. That's the product.

---

## Updated DLLs

The `lib/` folder contains the latest compiled DLLs from the private repository:
- `EvalApp.Consumer.dll` — EvalApp runtime with adaptive tuning
- `NavPathfinder.Sdk.dll` — Navigation pathfinding SDK

No license key required for demo — runs with sequential execution (functional, performant for demo scale).

---

## Read The Code

```
src/
├── Program.cs                       # Frame loop, world creation
├── Simulation/
│   ├── SiegePipeline.cs             # ← START HERE: 28-step pipeline wiring
│   └── SiegeWorld.cs                # Navmesh bake, castle layout
├── AI/                              # FSMs per faction (pure data transforms)
├── Data/                            # Immutable records — the pipeline payload
├── Steps/                           # Every pipeline step
│   ├── TickSimulation.cs            # The SDK call (5 lines)
│   ├── EvaluateAgentAI.cs           # Agent behaviour dispatch
│   ├── ResolveCombat.cs             # Damage resolution
│   └── Rendering/                   # ANSI terminal output
└── NavPathfinder.SiegeDemo.csproj   # DLL references (no source deps)

lib/                                 # Pre-compiled (no source)
├── NavPathfinder.Sdk.dll
├── NavPathfinder.Core.dll
├── EvalApp.dll
└── EvalApp.Licensing.dll
```

**`SiegePipeline.cs`** shows how the 28 steps compose. **`TickSimulation.cs`** is the SDK integration — it's 5 lines. **`DefenderFsm.cs`** shows the AI logic — pure data, no SDK dependency, no threading concern.

Every `.cs` file in `src/` is game logic. None of it manages threads. None of it can race.

---

## Measured Performance

| Metric | Value |
|--------|-------|
| Equilibrium population | ~3,900 agents |
| Frame rate | Locked 15 fps |
| Sim tick cost | ~39 ms |
| Render cost | ~25 ms |
| Frame pacing | Sub-millisecond accuracy (hybrid spin-wait) |

Population is governed by combat attrition, not artificial caps. The tuner warm-starts from `tuner-state/` on subsequent runs.

---

## License

Source code in `src/` is provided for evaluation and integration reference. DLLs in `lib/` are proprietary. Beta keys expire 2026-09-09. Contact us for production licensing.
