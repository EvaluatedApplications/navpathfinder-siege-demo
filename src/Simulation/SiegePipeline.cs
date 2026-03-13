using EvalApp.Consumer;
using static EvalApp.Consumer.EvalApp;

using NavPathfinder.Demo.Data;
using NavPathfinder.Demo.Steps;
using NavPathfinder.Demo.Steps.Combat;
using NavPathfinder.Demo.Steps.Rendering;
using NavPathfinder.Sdk;
using NavPathfinder.Sdk.Integration;

namespace NavPathfinder.Demo.Simulation;

public static class SiegePipeline
{
    public static ICompiledPipeline<SiegeTickData> BuildHeadless(
        PathfindingWorld world,
        SiegeDomainContext domainCtx,
        TunableConfig? cpuBounds = null)
    {
        App("SiegeDemoHeadless")
            .WithNavPathfinder(world)
            .WithResource(ResourceKind.Cpu, cpuBounds ?? new TunableConfig(1, 100, 50))
            .WithTuning()
            .DefineDomain("Siege", domainCtx)
                .DefineTask<SiegeTickData>("Tick")
                    // 1. Gate FSM (pure)
                    .AddStep<EvaluateGateStates>("EvaluateGates")
                    // 2. Gate mesh changes — update NavMesh BEFORE pathfinding
                    .If(
                        d => d.GateChanges != null && d.GateChanges.Value.Length > 0,
                        then: t => t.Gate(ResourceKind.Cpu, null, g => g
                            .AddStep<ApplyGateTransitions>("ApplyGateTransitions")))
                    // 3. Win condition — staged: outer→inner→keep
                    .AddStep<EvaluateWinCondition>("WinCondition")
                    // 4. Morale from gate states (pure)
                    .AddStep<ComputeMorale>("Morale")
                    // 5. Agent AI — data-driven FSM + goal resolution (pure)
                    .AddStep<EvaluateAgentAI>("AgentAI")
                    // 6. Threat map — IInfluenceMapService
                    .Gate(ResourceKind.Cpu, null, g => g
                        .AddStep<ComputeThreatMap>("ThreatMap"))
                    // 7. Defence map — IInfluenceMapService
                    .Gate(ResourceKind.Cpu, null, g => g
                        .AddStep<ComputeDefenceMap>("DefenceMap"))
                    // 8. Breach corridors — ISingleAgentService A*
                    .Gate(ResourceKind.Cpu, null, g => g
                        .AddStep<ComputeBreachPaths>("BreachPaths"))
                    // 9. Prepare all population DTOs (pure)
                    .AddStep<PrepareSimDtos>("PrepareDtos")
                    // 10. Tick all populations — ISimulationService
                    .Gate(ResourceKind.Cpu, null, g => g
                        .AddStep<TickSimulation>("TickSim"))
                    // 12. Zone control — contest/claim from positions (pure)
                    .AddStep<UpdateZoneControl>("ZoneControl")
                    // 13. Defence line evaluation — staged retreat (pure)
                    .AddStep<EvaluateDefenceLine>("DefenceLine")
                    // 14. Movement
                    .AddStep<ApplyMovement>("ApplyMovement")
                    // 14b. Apply deferred damage from previous tick
                    .AddStep<ApplyPendingDamageStep>("ApplyPendingDamage")
                    // 15. Combat — IEngagementService
                    .Gate(ResourceKind.Cpu, null, g => g
                        .AddStep<ResolveCombat>("Combat"))
                    // 15b. Filter combat events
                    .AddStep<CollectCombatLogStep>("CollectCombatLog")
                    // 16. Spawn
                    .AddStep<SpawnAgents>("Spawn")
                    // 17. Separation — ISeparationService
                    .Gate(ResourceKind.Cpu, null, g => g
                        .AddStep<EnforceSeparation>("Separation"))
                    // 18. Performance metrics (pure)
                    .AddStep<CollectMetrics>("Metrics")
                    .Run(out var pipeline)
                .Build();

        return pipeline;
    }

    public static ICompiledPipeline<SiegeTickData> Build(PathfindingWorld world, SiegeDomainContext domainCtx)
    {
        App("SiegeDemo")
            .WithNavPathfinder(world)
            .WithResource(ResourceKind.Cpu)
            .WithTuning()
            .DefineDomain("Siege", domainCtx)
                .DefineTask<SiegeTickData>("Tick")
                    // 1. Gate FSM (pure)
                    .AddStep<EvaluateGateStates>("EvaluateGates")
                    // 2. Gate mesh changes — update NavMesh BEFORE pathfinding
                    .If(
                        d => d.GateChanges != null && d.GateChanges.Value.Length > 0,
                        then: t => t.Gate(ResourceKind.Cpu, null, g => g
                            .AddStep<ApplyGateTransitions>("ApplyGateTransitions")))
                    // 3. Win condition — staged: outer→inner→keep
                    .AddStep<EvaluateWinCondition>("WinCondition")
                    // 4. Morale from gate states (pure)
                    .AddStep<ComputeMorale>("Morale")
                    // 5. Agent AI — data-driven FSM + goal resolution (pure)
                    .AddStep<EvaluateAgentAI>("AgentAI")
                    // 6. Threat map — IInfluenceMapService
                    .Gate(ResourceKind.Cpu, null, g => g
                        .AddStep<ComputeThreatMap>("ThreatMap"))
                    // 7. Defence map — IInfluenceMapService
                    .Gate(ResourceKind.Cpu, null, g => g
                        .AddStep<ComputeDefenceMap>("DefenceMap"))
                    // 8. Breach corridors — ISingleAgentService A*
                    .Gate(ResourceKind.Cpu, null, g => g
                        .AddStep<ComputeBreachPaths>("BreachPaths"))
                    // 9. Prepare all population DTOs (pure)
                    .AddStep<PrepareSimDtos>("PrepareDtos")
                    // 10. Tick all populations — ISimulationService
                    .Gate(ResourceKind.Cpu, null, g => g
                        .AddStep<TickSimulation>("TickSim"))
                    // 12. Zone control — contest/claim (pure)
                    .AddStep<UpdateZoneControl>("ZoneControl")
                    // 13. Defence line evaluation (pure)
                    .AddStep<EvaluateDefenceLine>("DefenceLine")
                    // 14. Movement
                    .AddStep<ApplyMovement>("ApplyMovement")
                    // 14b. Apply deferred damage from previous tick
                    .AddStep<ApplyPendingDamageStep>("ApplyPendingDamage")
                    // 15. Combat — IEngagementService
                    .Gate(ResourceKind.Cpu, null, g => g
                        .AddStep<ResolveCombat>("Combat"))
                    // 15b. Filter combat events
                    .AddStep<CollectCombatLogStep>("CollectCombatLog")
                    // 16. Spawn
                    .AddStep<SpawnAgents>("Spawn")
                    // 17. Separation — ISeparationService
                    .Gate(ResourceKind.Cpu, null, g => g
                        .AddStep<EnforceSeparation>("Separation"))
                    // 18. Performance metrics (pure)
                    .AddStep<CollectMetrics>("Metrics")
                    // 19. Init grid — allocate buffer, count agents, track states
                    .AddStep<InitGrid>("InitGrid")
                    // 20-24. Game panels (only while in progress)
                    .AddStep<RenderGameGrid>("RenderGrid")
                    .AddStep<RenderMetrics>("RenderMetrics")
                    .AddStep<RenderPops>("RenderPops")
                    .AddStep<RenderZones>("RenderZones")
                    .AddStep<RenderBehaviours>("RenderBehaviours")
                    // 25. Combat log — narrative display
                    .AddStep<RenderCombatLog>("RenderCombatLog")
                    // 26. Status — defence line + win banner (always)
                    .AddStep<RenderStatus>("RenderStatus")
                    // 26. Legend — only on first frame
                    .AddStep<RenderLegend>("RenderLegend")
                    // 27. Serialize — header + accumulated output → RenderedFrame
                    .AddStep<SerializeFrame>("SerializeFrame")
                    .Run(out var pipeline)
                .Build();

        return pipeline;
    }
}
