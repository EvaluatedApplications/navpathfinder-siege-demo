using System.Diagnostics;
using System.Text;
using NavPathfinder.Demo.Simulation;
using NavPathfinder.Sdk;
using NavPathfinder.Sdk.Integration;

bool isTty = !Console.IsOutputRedirected;

// ── Startup: ask user to maximise, then read the actual terminal size ─────
if (isTty)
{
    Console.OutputEncoding = Encoding.UTF8;
    Console.CursorVisible  = false;
    Console.Clear();

    Console.ForegroundColor = ConsoleColor.Yellow;
    Console.WriteLine("  ⚔  NavPathfinder SDK — Siege Simulation");
    Console.ResetColor();
    Console.WriteLine();
    Console.ForegroundColor = ConsoleColor.Gray;
    Console.WriteLine("  Maximise this terminal window (F11 or drag to full-screen),");
    Console.WriteLine("  then press ENTER to start.");
    Console.ResetColor();

    Console.ReadLine();

    // Grid fills the terminal exactly — no scrolling possible.
    // ~29 rows of overhead: 5 header + 1 FPS row + 3 population rows + 1 gate row
    // + 1 threat row + 1 spawn row + 1 zone row + 1 behaviour row + 1 defence/win
    // + 1 sdk + 3 legend/sep (faction glyphs + AI state colors) + ~10 margin.
    const int Overhead = 29;
    int cols = Math.Max(60,  Math.Min(240, Console.WindowWidth));
    int rows = Math.Max(20,  Math.Min(60,  Console.WindowHeight - Overhead));
    SiegeWorld.Initialize(cols, rows);

    Console.CursorVisible = false;
    Console.Clear();
}

// 256 KB buffered stdout — whole ANSI frame flushed in one WriteFile call.
var stdout = new StreamWriter(
    Console.OpenStandardOutput(),
    Encoding.UTF8,
    bufferSize: 1 << 18,
    leaveOpen: true);
Console.SetOut(stdout);

// ── Pipeline ──────────────────────────────────────────────────────────────
var world = new PathfindingWorld(
    licenseKey:     "20270306-8X4HMHOLqxd4iht4j7tgmmynnnIPC8L4DJjdGxv2E3s",
    tunerStatePath: "./siege-tuner");

var ctx = new NavPathfinderContext(world);
var (navMesh, domainCtx) = await SiegeWorld.CreateAsync(ctx);

var initialState = SiegeWorld.CreateInitialState(navMesh);
var pipeline     = SiegePipeline.Build(world, domainCtx);

var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };

// ── Render loop ───────────────────────────────────────────────────────────
// Measure the FULL iteration (pipeline + render + flush) for the 15fps cap.
// Show pipeline-only time in the HUD so the user sees pathfinding cost.
const double TargetMs = 1000.0 / 15.0;
var    state        = initialState;
var    sw           = new Stopwatch();
double prevPipeMs   = 0;
double prevRenderMs = 0;
double prevFps      = 0;

// Rolling 1-second FPS average — accumulate frames until 1000 ms elapsed, then commit.
double fpsAccumMs    = 0;
int    fpsAccumCount = 0;
double smoothedFps   = 0;

try
{
    while (!cts.Token.IsCancellationRequested)
    {
        var timedState = state with { WallClockMs = prevPipeMs, RenderMs = prevRenderMs, Fps = smoothedFps };

        sw.Restart();
        var result = await pipeline.RunAsync(timedState, cts.Token);
        prevPipeMs = sw.Elapsed.TotalMilliseconds;

        if (result.IsSuccess)
        {
            double renderStart = sw.Elapsed.TotalMilliseconds;
            if (isTty) Console.SetCursorPosition(0, 0);
            Console.Write(result.GetData().RenderedFrame ?? "");
            if (!isTty) Console.WriteLine();
            stdout.Flush();
            prevRenderMs = sw.Elapsed.TotalMilliseconds - renderStart;
            // Instantaneous FPS (used for accumulator only).
            prevFps = sw.Elapsed.TotalMilliseconds > 0 ? 1000.0 / sw.Elapsed.TotalMilliseconds : 0;
            // Accumulate towards 1-second average.
            fpsAccumMs    += sw.Elapsed.TotalMilliseconds;
            fpsAccumCount++;
            if (fpsAccumMs >= 1000.0)
            {
                smoothedFps   = fpsAccumCount > 0 ? 1000.0 / (fpsAccumMs / fpsAccumCount) : 0;
                fpsAccumMs    = 0;
                fpsAccumCount = 0;
            }
            else if (smoothedFps == 0)
            {
                // Show instantaneous until the first second completes.
                smoothedFps = prevFps;
            }
            state = SiegeWorld.NextTickState(result.GetData());
        }
        else if (result is EvalApp.Consumer.PipelineResult<NavPathfinder.Demo.Data.SiegeTickData>.Failure f)
        {
            Console.Error.WriteLine($"Pipeline error: {f.Exception?.Message}");
            Console.Error.WriteLine(f.Exception?.StackTrace);
            break;
        }

        // Lock to exactly 15fps using high-resolution spin-wait.
        // Task.Delay has ~15ms granularity on Windows — too coarse for smooth pacing.
        while (sw.Elapsed.TotalMilliseconds < TargetMs)
        {
            double gap = TargetMs - sw.Elapsed.TotalMilliseconds;
            if (gap > 2.0)
                Thread.Sleep(1); // yield CPU when >2ms remain
            else
                Thread.SpinWait(10); // spin for final precision
        }
    }
}
catch (OperationCanceledException) { /* graceful Ctrl+C */ }
finally
{
    if (isTty) Console.CursorVisible = true;
    stdout.Flush();
    stdout.Dispose();
}

Console.WriteLine("\nSiege simulation stopped.");
