using SimulatorV86.Application.Analysis;
using SimulatorV86.Domain;

static void AssertTrue(bool condition, string message)
{
    if (!condition) throw new Exception("Assertion failed: " + message);
}

static void AssertNear(double actual, double expected, double tol, string message)
{
    if (double.IsNaN(actual) || Math.Abs(actual - expected) > tol)
        throw new Exception($"Assertion failed: {message}. Expected {expected}, got {actual}.");
}

// Pearson sanity.
AssertNear(MathUtil.Pearson(new[] { 1.0, 2.0, 3.0 }, new[] { 2.0, 4.0, 6.0 }), 1.0, 1e-12, "Pearson perfect positive correlation");
AssertNear(MathUtil.Pearson(new[] { 1.0, 2.0, 3.0 }, new[] { 6.0, 4.0, 2.0 }), -1.0, 1e-12, "Pearson perfect negative correlation");

// Benjamini-Hochberg step-up q-values.
var q = Analyzer.BenjaminiHochberg(new[] { 0.001, 0.02, 0.04, 0.2 });
AssertNear(q[0], 0.004, 1e-12, "BH q0");
AssertNear(q[1], 0.04, 1e-12, "BH q1");
AssertNear(q[2], 0.05333333333333334, 1e-12, "BH q2");
AssertNear(q[3], 0.2, 1e-12, "BH q3");

// Pseudo-replication guard: 8 base cases x 3 replicates must test as n=8, not n=24.
string dir = Path.Combine(Path.GetTempPath(), "simulator_v86_stats_test_" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(dir);
File.WriteAllText(Path.Combine(dir, "scores.csv"), "run_id,case_id,module,score\n");
File.WriteAllText(Path.Combine(dir, "observables.csv"), "run_id,case_id,module,observable,value,unit,anomaly_score,interpretation\n");
for (int i = 0; i < 8; i++)
{
    for (int rep = 0; rep < 3; rep++)
    {
        string caseId = $"case_{i:0000}_rep{rep:00}";
        double x = i + 1;
        double y = 2 * (i + 1) + 0.01 * rep;
        Csv.AppendLine(Path.Combine(dir, "observables.csv"), new[] { "run", caseId, "inflation", "peak_scalar_power", Csv.D(x), "x", "0", "test" });
        Csv.AppendLine(Path.Combine(dir, "observables.csv"), new[] { "run", caseId, "life", "growth_exponent", Csv.D(y), "y", "0", "test" });
        Csv.AppendLine(Path.Combine(dir, "scores.csv"), new[] { "run", caseId, "inflation", "0.5" });
        Csv.AppendLine(Path.Combine(dir, "scores.csv"), new[] { "run", caseId, "life", "0.5" });
    }
}
var result = new Analyzer().Analyze(dir);
var rec = result.Correlations.FirstOrDefault(c => c.ObservableA.Contains("inflation.peak_scalar_power") || c.ObservableB.Contains("inflation.peak_scalar_power"));
AssertTrue(rec is not null, "Expected test correlation exists");
AssertTrue(rec!.N == 8, "Correlation uses independent base cases, not raw replicate rows");
AssertTrue(result.BaseCaseCount == 8, "Base case count equals independent units");

Console.WriteLine("SimulatorV86.Tests passed.");
