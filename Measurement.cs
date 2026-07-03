namespace Simulator;

// ====================================================================
// 3) MEASUREMENT — CSL lambda-rC observable landscape + null test
// ====================================================================
public static class Measurement
{
    private const double SecondsPerRun = 1.0;

    private static double GeometricSeparation(double d, double rC) => 1.0 - Math.Exp(-d * d / (4.0 * rC * rC));

    private static double EffectiveN2(double nucleons, double radiusMeters, double rC)
    {
        if (radiusMeters <= rC) return nucleons * nucleons;
        double domains = Math.Pow(radiusMeters / rC, 3.0);
        return nucleons * nucleons / Math.Max(1.0, domains);
    }

    private static double CslCollapseRate(double lambda, double rC, double nucleons, double radius,
        double separation)
    {
        return lambda * EffectiveN2(nucleons, radius, rC) * GeometricSeparation(separation, rC);
    }

    private static double VisibilityAfterCsl(double lambda, double rC, double nucleons, double radius,
        double separation, double time)
    {
        double rate = CslCollapseRate(lambda, rC, nucleons, radius, separation);
        return Math.Exp(-rate * time);
    }

    private static double RelativeXrayRate(double lambda, double rC, double energyKev)
    {
        // Toy scaling: spontaneous radiation bounds scale roughly like lambda/rC^2,
        // with a soft 1/E spectral shape. Normalized to GRW-like parameters.
        double refLambda = 1e-16;
        double refRC = 1e-7;

        return (lambda / refLambda) * (refRC * refRC / (rC * rC)) * (10.0 / Math.Max(energyKev, 1e-6));
    }

    public static void Run()
    {
        using (var sw = new StreamWriter("out/measurement_csl_exclusion_landscape.csv"))
        {
            sw.WriteLine(
                "lambda,rC,visibilityLossMatterWave,relativeXrayRate10keV,excludedByToyMatterWave,excludedByToyXray,excludedEither");
            double[] lambdas = Enumerable.Range(0, 29).Select(i => Math.Pow(10, -22 + i * 0.7)).ToArray();
            double[] rcs = Enumerable.Range(0, 25).Select(i => Math.Pow(10, -9 + i * 0.18)).ToArray();

            foreach (double rC in rcs)
            {
                foreach (double lambda in lambdas)
                {
                    double visibility = VisibilityAfterCsl(lambda, rC,
                        nucleons: 1e8,
                        radius: 50e-9,
                        separation: 100e-9,
                        time: SecondsPerRun);

                    double loss = 1.0 - visibility;
                    double xray = RelativeXrayRate(lambda, rC, energyKev: 10.0);

                    bool matterExcluded = loss > 0.10;
                    bool xrayExcluded = xray > 1e7; // toy threshold representing a strong X-ray non-observation.
                    sw.WriteLine(
                        $"{lambda:E6},{rC:E6},{loss:E6},{xray:E6},{matterExcluded},{xrayExcluded},{matterExcluded || xrayExcluded}");
                }
            }
        }

        using (var sw = new StreamWriter("out/measurement_csl_xray_spectrum.csv"))
        {
            sw.WriteLine("energy_keV,relativeRate_GRW,relativeRate_Adler");
            foreach (double e in new[] { 1.0, 2.0, 5.0, 10.0, 20.0, 50.0, 100.0, 140.0 })
            {
                double grw = RelativeXrayRate(1e-16, 1e-7, e);
                double adler = RelativeXrayRate(1e-8, 1e-7, e);
                sw.WriteLine($"{e:F1},{grw:E6},{adler:E6}");
            }
        }

        using (var sw = new StreamWriter("out/measurement_visibility_budget.csv"))
        {
            sw.WriteLine("scenario,environmentVisibility,cslVisibility,totalVisibility");
            var scenarios = new[]
            {
                ("clean_large_molecule", 0.95, 1e-16, 1e-7, 1e6, 5e-9, 100e-9),
                ("nanoparticle_frontier_GRW", 0.90, 1e-16, 1e-7, 1e8, 50e-9, 100e-9),
                ("nanoparticle_frontier_Adler", 0.90, 1e-8, 1e-7, 1e8, 50e-9, 100e-9),
                ("macroscopic_grain", 0.50, 1e-16, 1e-7, 1e14, 1e-6, 100e-9)
            };

            foreach (var s in scenarios)
            {
                double cslV = VisibilityAfterCsl(s.Item3, s.Item4, s.Item5, s.Item6, s.Item7, SecondsPerRun);
                sw.WriteLine($"{s.Item1},{s.Item2:E6},{cslV:E6},{s.Item2 * cslV:E6}");
            }
        }

        using (var sw = new StreamWriter("out/measurement_consciousness_null.csv"))
        {
            sw.WriteLine("trials,hypotheticalBias,pObserved,zScore,fiveSigmaDetectableBias");
            double p0 = 0.75, p1 = 0.25;
            int seed = 123;
            foreach (int trials in new[] { 10_000, 1_000_000, 100_000_000 })
            {
                foreach (double bias in new[] { 0.0, 1e-5, 1e-4, 1e-3 })
                {
                    // For the largest trial count, avoid a slow Monte Carlo and use expected z.
                    double observed;
                    double z;
                    if (trials <= 1_000_000)
                    {
                        var rng = new Random(seed++);
                        double ps = Util.Clamp(p0 + bias, 0, 1);
                        int zeros = 0;

                        for (int i = 0; i < trials; i++)
                            if (rng.NextDouble() < ps)
                                zeros++;

                        observed = zeros / (double)trials;
                        z = (zeros - trials * p0) / Math.Sqrt(trials * p0 * p1);
                    }
                    else
                    {
                        observed = p0 + bias;
                        z = bias * Math.Sqrt(trials / (p0 * p1));
                    }

                    double detect = 5.0 * Math.Sqrt(p0 * p1 / trials);
                    sw.WriteLine($"{trials},{bias:E3},{observed:E6},{z:E6},{detect:E6}");
                }
            }
        }

        using (var sw = new StreamWriter("out/measurement_summary.txt"))
        {
            sw.WriteLine("MEASUREMENT v7 — CSL observables");
            sw.WriteLine("Outputs:");
            sw.WriteLine("  measurement_csl_exclusion_landscape.csv: toy lambda-rC exclusion map.");
            sw.WriteLine("  measurement_csl_xray_spectrum.csv: spontaneous-radiation scaling observable.");
            sw.WriteLine("  measurement_visibility_budget.csv: environment vs CSL visibility loss.");
            sw.WriteLine("  measurement_consciousness_null.csv: null-test sensitivity for a non-standard bias.");
            sw.WriteLine(
                "Important: the X-ray/exclusion map is a scaling toy, not an official recast of XENONnT or any lab result.");
        }

        Console.WriteLine("Measurement -> CSL landscape + X-ray scaling + null test, see out/measurement_*.*");
    }
}