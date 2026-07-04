using System.CommandLine;
using SimulatorV85.Application.Analysis;
using SimulatorV85.Application.Sweeps;
using SimulatorV85.Domain;
using SimulatorV85.Domain.Modules;
using SimulatorV85.Infrastructure.Configuration;
using SimulatorV85.Infrastructure.Interop;
using SimulatorV85.Infrastructure.Storage;

namespace SimulatorV85.Cli;

public static class Program
{
    public static int Main(string[] args)
    {
        var moduleOpt = new Option<string>("--module")
        {
            Description = "Module to run: all, inflation, life, measurement, blackhole, cosmology, observer, folding, chemistry, nbody",
            DefaultValueFactory = _ => "all"
        };
        var configOpt = new Option<string?>("--config") { Description = "JSON config file" };
        var runIdOpt = new Option<string?>("--run-id") { Description = "Run id. Defaults to UTC timestamp." };
        var rootOpt = new Option<string>("--root")
        {
            Description = "Runs root directory",
            DefaultValueFactory = _ => "runs"
        };
        var parallelOpt = new Option<int>("--parallel")
        {
            Description = "Maximum parallelism for sweeps",
            DefaultValueFactory = _ => Environment.ProcessorCount
        };
        var analyzeOpt = new Option<bool>("--analyze") { Description = "Run analysis after simulation/sweep" };
        var noPythonOpt = new Option<bool>("--no-python") { Description = "Skip Python plot/ML scripts during analysis" };
        var pythonOpt = new Option<string>("--python")
        {
            Description = "Python executable",
            DefaultValueFactory = _ => "python"
        };
        var runOpt = new Option<string?>("--run") { Description = "Existing run directory to analyze" };

        var simulate = new Command("simulate", "Run one parameter set")
        {
            moduleOpt, configOpt, runIdOpt, rootOpt, analyzeOpt, noPythonOpt, pythonOpt
        };
        simulate.SetAction(parse =>
        {
            string module = parse.GetValue(moduleOpt) ?? "all";
            string? config = parse.GetValue(configOpt);
            string runId = parse.GetValue(runIdOpt) ?? FileRunWorkspace.NewRunId();
            string root = parse.GetValue(rootOpt) ?? "runs";
            bool analyze = parse.GetValue(analyzeOpt);
            bool noPython = parse.GetValue(noPythonOpt);
            string python = parse.GetValue(pythonOpt) ?? "python";
            return CompositionRoot.Simulate(module, config, runId, root, analyze, noPython, python);
        });

        var sweep = new Command("sweep", "Run a parameter sweep")
        {
            moduleOpt, configOpt, runIdOpt, rootOpt, parallelOpt, noPythonOpt, pythonOpt
        };
        sweep.SetAction(parse =>
        {
            string module = parse.GetValue(moduleOpt) ?? "all";
            string? config = parse.GetValue(configOpt);
            string runId = parse.GetValue(runIdOpt) ?? FileRunWorkspace.NewRunId();
            string root = parse.GetValue(rootOpt) ?? "runs";
            int parallel = Math.Max(1, parse.GetValue(parallelOpt));
            bool noPython = parse.GetValue(noPythonOpt);
            string python = parse.GetValue(pythonOpt) ?? "python";
            return CompositionRoot.Sweep(module, config, runId, root, parallel, noPython, python);
        });

        var analyze = new Command("analyze", "Analyze an existing run") { runOpt, noPythonOpt, pythonOpt };
        analyze.SetAction(parse =>
        {
            string? run = parse.GetValue(runOpt);
            bool noPython = parse.GetValue(noPythonOpt);
            string python = parse.GetValue(pythonOpt) ?? "python";
            if (string.IsNullOrWhiteSpace(run))
            {
                Console.Error.WriteLine("analyze requires --run runs/<run_id>");
                return 2;
            }
            return CompositionRoot.Analyze(run, noPython, python);
        });

        var rootCommand = new RootCommand("Simulator v8.6 — Observable Anomaly Sandbox with Folding, Chemistry, and N-body")
        {
            simulate,
            sweep,
            analyze
        };

        return rootCommand.Parse(args).Invoke();
    }
}

internal static class CompositionRoot
{
    public static int Simulate(string module, string? config, string runId, string root, bool analyze, bool noPython, string python)
    {
        try
        {
            var workspace = new FileRunWorkspace(runId, root);
            var configLoader = new JsonConfigLoader();
            var runner = new SweepRunner(ExperimentRegistry.Create());
            var store = new FileResultStore(workspace);
            var parameters = configLoader.LoadParameters(config);
            var results = runner.RunSingle(module, parameters, workspace);
            foreach (var r in results) store.Save(r);
            WriteManifest(workspace, "simulate", results.Count, results.Select(x => x.Module).Distinct().ToArray());
            Console.WriteLine($"Simulation complete: {workspace.RunDir}");
            if (analyze) Analyze(workspace.RunDir, noPython, python);
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex.Message);
            Console.Error.WriteLine(ex);
            return 1;
        }
    }

    public static int Sweep(string module, string? config, string runId, string root, int parallel, bool noPython, string python)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(config)) throw new ArgumentException("sweep requires --config <file.json>");
            var workspace = new FileRunWorkspace(runId, root);
            var configLoader = new JsonConfigLoader();
            var runner = new SweepRunner(ExperimentRegistry.Create());
            var store = new FileResultStore(workspace);
            var sweep = configLoader.LoadSweep(config);
            var results = runner.RunAsync(module, sweep, workspace, parallel).GetAwaiter().GetResult();
            foreach (var r in results) store.Save(r);
            WriteManifest(workspace, "sweep", sweep.Cases * Math.Max(1, sweep.Replicates), results.Select(x => x.Module).Distinct().ToArray());
            Console.WriteLine($"Sweep complete: {workspace.RunDir}");
            Analyze(workspace.RunDir, noPython, python);
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex.Message);
            Console.Error.WriteLine(ex);
            return 1;
        }
    }

    public static int Analyze(string runDir, bool noPython, string python)
    {
        try
        {
            var analyzer = new Analyzer();
            var result = analyzer.Analyze(runDir);
            analyzer.Write(runDir, result);
            Console.WriteLine($"Analysis written to {Path.Combine(runDir, "analysis")}");

            if (!noPython)
            {
                var py = new PythonProcessRunner();
                string scripts = FindScriptRoot();
                py.TryRun(python, Path.Combine(scripts, "plot_report.py"), runDir);
                py.TryRun(python, Path.Combine(scripts, "ml_surrogate.py"), runDir);
            }
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex.Message);
            Console.Error.WriteLine(ex);
            return 1;
        }
    }

    private static void WriteManifest(FileRunWorkspace workspace, string command, int cases, IReadOnlyList<string> modules)
    {
        workspace.WriteManifest(new RunManifest(
            workspace.RunId,
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow,
            command,
            "net10.0",
            Directory.GetCurrentDirectory(),
            FileRunWorkspace.TryGetGitCommit(),
            cases,
            modules,
            new[]
            {
                "v8.6 uses causal inter-module handoffs, null-calibrated anomaly search, ensemble replicates, SIGW kernel convolution, and kinetic RAF knockouts.",
                "This is an observable anomaly sandbox, not proof of the simulation hypothesis.",
                "Python plots/ML are optional post-processing adapters."
            }));
    }

    private static string FindScriptRoot()
    {
        string cwd = Directory.GetCurrentDirectory();
        var candidates = new[]
        {
            Path.Combine(cwd, "scripts"),
            Path.Combine(AppContext.BaseDirectory, "scripts"),
            Path.Combine(cwd, "..", "..", "..", "scripts"),
            Path.Combine(cwd, "..", "..", "..", "..", "scripts")
        };
        return candidates.FirstOrDefault(Directory.Exists) ?? Path.Combine(cwd, "scripts");
    }
}
