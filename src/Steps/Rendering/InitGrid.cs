using System.Collections.Immutable;
using EvalApp.Consumer;

using NavPathfinder.Demo.Data;
using NavPathfinder.Demo.Rendering;
using NavPathfinder.Demo.Simulation;

namespace NavPathfinder.Demo.Steps.Rendering;

/// <summary>
/// Step 1: Allocate/clear the cell grid and populate agent density caches.
/// Creates the RenderBuffer on first frame; reuses it on subsequent frames.
/// </summary>
public sealed class InitGrid : PureStep<SiegeTickData>
{
    private static RenderBuffer? _buffer;

    public override SiegeTickData Execute(SiegeTickData data)
    {
        int rows = SiegeWorld.GridRows, cols = SiegeWorld.GridCols;

        if (_buffer == null || _buffer.Rows != rows || _buffer.Cols != cols)
            _buffer = new RenderBuffer(rows, cols);

        _buffer.ClearFrame();

        // Count agents per cell for density rendering
        CountAgents(_buffer.InvCount, data.Invaders, rows, cols);
        CountAgents(_buffer.DefCount, data.Defenders, rows, cols);
        CountAgents(_buffer.CivCount, data.Civilians, rows, cols);

        // Track dominant AI state per cell PER FACTION for correct colour selection
        TrackDominantState(_buffer.InvDomState, data.Invaders, rows, cols);
        TrackDominantState(_buffer.DefDomState, data.Defenders, rows, cols);
        TrackDominantState(_buffer.CivDomState, data.Civilians, rows, cols);

        // Find highest-level agent per faction for leader glyph
        _buffer.InvLeaderCell = FindLeaderCell(data.Invaders, rows, cols);
        _buffer.DefLeaderCell = FindLeaderCell(data.Defenders, rows, cols);

        // Update FPS history ring buffer
        _buffer.FpsHistory[_buffer.FpsHistoryIndex % _buffer.FpsHistory.Length] = (float)data.Fps;
        _buffer.FpsHistoryIndex++;

        return data with
        {
            RenderBuffer = _buffer,
            IsFirstFrame = data.TickNumber == 0
        };
    }

    private static void CountAgents(int[,] counts, ImmutableArray<SimAgent> agents, int rows, int cols)
    {
        foreach (var a in agents)
        {
            int col = (int)a.Position.X, row = (int)a.Position.Y;
            if (row >= 0 && row < rows && col >= 0 && col < cols)
                counts[row, col]++;
        }
    }

    private static void TrackDominantState(BehaviourState[,] grid, ImmutableArray<SimAgent> agents,
        int rows, int cols)
    {
        foreach (var a in agents)
        {
            int col = (int)a.Position.X, row = (int)a.Position.Y;
            if (row >= 0 && row < rows && col >= 0 && col < cols)
            {
                if (StatePriority(a.Behaviour) > StatePriority(grid[row, col]))
                    grid[row, col] = a.Behaviour;
            }
        }
    }

    private static (int Row, int Col) FindLeaderCell(ImmutableArray<SimAgent> agents, int rows, int cols)
    {
        int bestLevel = 0;
        (int Row, int Col) cell = (-1, -1);
        foreach (var a in agents)
        {
            if (a.Skills.Level > bestLevel)
            {
                bestLevel = a.Skills.Level;
                int r = (int)a.Position.Y, c = (int)a.Position.X;
                if (r >= 0 && r < rows && c >= 0 && c < cols)
                    cell = (r, c);
            }
        }
        return cell;
    }

    internal static int StatePriority(BehaviourState s) => s switch
    {
        BehaviourState.Hidden      => 0,
        BehaviourState.Sheltering  => 1,
        BehaviourState.Mustering   => 2,
        BehaviourState.Holding     => 3,
        BehaviourState.Sortie      => 4,
        BehaviourState.Advancing   => 5,
        BehaviourState.Reinforcing => 6,
        BehaviourState.FallingBack => 7,
        BehaviourState.Pushing     => 8,
        BehaviourState.Claiming    => 9,
        BehaviourState.Fleeing     => 10,
        BehaviourState.Sieging     => 11,
        BehaviourState.Fighting    => 12,
        BehaviourState.Breaching   => 13,
        BehaviourState.LastStand   => 14,
        _                          => 0
    };
}
