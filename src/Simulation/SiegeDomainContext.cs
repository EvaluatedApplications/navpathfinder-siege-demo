using EvalApp.Consumer;

using NavPathfinder.Sdk.Abstractions;
using NavPathfinder.Sdk.Services;

namespace NavPathfinder.Demo.Simulation;

public sealed class SiegeDomainContext : DomainContext
{
    public SiegeDomainContext(
        ISimulationService          simulationService,
        IDynamicObstacleService     obstacleService,
        ISingleAgentService         scoutService,
        IInfluenceMapService        influenceService,
        ISeparationService          separationService,
        IEngagementService          engagementService)
    {
        SimulationService  = simulationService;
        ObstacleService    = obstacleService;
        ScoutService       = scoutService;
        InfluenceService   = influenceService;
        SeparationService  = separationService;
        EngagementService  = engagementService;
    }

    public ISimulationService       SimulationService  { get; }
    public IDynamicObstacleService  ObstacleService    { get; }
    /// <summary>Individual A* queries — used for breach-corridor visualisation.</summary>
    public ISingleAgentService      ScoutService       { get; }
    /// <summary>Spatial influence map — used for battlefield threat tracking.</summary>
    public IInfluenceMapService     InfluenceService   { get; }
    public ISeparationService       SeparationService  { get; }
    public IEngagementService       EngagementService  { get; }
}
