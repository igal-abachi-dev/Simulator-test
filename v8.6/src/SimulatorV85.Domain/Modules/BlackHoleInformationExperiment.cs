using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using SimulatorV85.Domain;

namespace SimulatorV85.Domain.Modules;

public sealed class BlackHoleInformationExperiment : IExperiment
{
    public string Name => "blackhole";
    public string Description => "Toy black-hole information module: evaporation, Page curve, island-inspired correction.";

    public ExperimentResult Run(ExperimentSpec spec, IArtifactSink artifacts, CancellationToken ct = default)
    {
        double m0 = spec.Get("blackhole.initialMassPlanck", spec.Get("initialMassPlanck", 1e8));
        double island = spec.Get("blackhole.islandStrength", spec.Get("islandStrength", 1.0));
        int steps = spec.GetInt("blackhole.steps", spec.GetInt("steps", 400));
        string path = artifacts.OutputPath(Name, spec.CaseId, "blackhole_page_curve.csv");
        double s0 = 4 * Math.PI * m0 * m0;
        double maxEntropy = 0, finalEntropy = 0, pageTime = 0.5;
        using (var sw = new StreamWriter(path))
        {
            sw.WriteLine("t_over_lifetime,mass,hawking_entropy_semiclassical,radiation_entropy_unitary,radiation_entropy_island_toy,information_recovered,unitarity_residual");
            for (int i = 0; i <= steps; i++)
            {
                double t = i / (double)steps;
                double mass = m0 * Math.Pow(Math.Max(1e-12, 1 - t), 1.0 / 3.0);
                double bhEntropy = 4 * Math.PI * mass * mass;
                double emitted = Math.Max(0, s0 - bhEntropy);
                double unitary = Math.Min(emitted, bhEntropy);
                double islandCurve = (1 - island) * emitted + island * unitary;
                double infoRecovered = Math.Max(0, emitted - islandCurve);
                double residual = i == steps ? islandCurve / s0 : Math.Abs(islandCurve - unitary) / s0;
                if (islandCurve > maxEntropy) { maxEntropy = islandCurve; pageTime = t; }
                finalEntropy = islandCurve;
                sw.WriteLine($"{t:F6},{mass:E8},{emitted:E8},{unitary:E8},{islandCurve:E8},{infoRecovered:E8},{residual:E8}");
            }
        }
        double finalResidual = finalEntropy / Math.Max(s0, 1e-300);
        double score = MathUtil.Clamp((1 - finalResidual) * MathUtil.InterestNearThreshold(pageTime, 0.5, 1), 0, 1);
        var obs = new List<ObservableRecord>
        {
            new(Name, "page_time", pageTime, "lifetime_fraction", MathUtil.InterestNearThreshold(pageTime, 0.5, 1),
                "Turnover time of the radiation entropy curve.", spec.CaseId),
            new(Name, "final_unitarity_residual", finalResidual, "fraction", MathUtil.Clamp(finalResidual, 0, 1),
                "Residual final radiation entropy; lower is more unitary in this toy model.", spec.CaseId),
            new(Name, "max_radiation_entropy", maxEntropy, "Planck^0", 0,
                "Maximum radiation entropy in the toy Page curve.", spec.CaseId),
            new(Name, "island_strength", island, "dimensionless", 0,
                "Interpolation strength for the island-inspired correction.", spec.CaseId)
        };
        return new ExperimentResult(artifacts.RunId, spec.CaseId, Name, "PageCurveToy", spec.Parameters,
            obs, new[] { path }, new[] { "Toy information-flow model, not a quantum-gravity solver." }, score);
    }
}
