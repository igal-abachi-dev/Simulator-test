using System.Text;

namespace Simulator;

// ====================================================================
// 2) ORIGIN OF LIFE — RAF structure + autocatalytic growth exponent
// ====================================================================
public static class OriginOfLife
{
    private sealed class Chem
    {
        public required string[] Mol;
        public required int[] FoodIds;
        public required (int a, int b, int p)[] Rxn;
        public required Dictionary<string, int> Idx;
        public required HashSet<int> FoodSet;
    }

    private static Chem Build(int maxLen)
    {
        var mols = new List<string>();
        for (int len = 1; len <= maxLen; len++)
            for (int v = 0; v < (1 << len); v++)
            {
                var sb = new StringBuilder(len);
                for (int bit = len - 1; bit >= 0; bit--)
                    sb.Append(((v >> bit) & 1) == 1 ? '1' : '0');

                mols.Add(sb.ToString());
            }

        var idx = new Dictionary<string, int>();
        for (int i = 0; i < mols.Count; i++) idx[mols[i]] = i;
        int[] food = mols.Where(m => m.Length <= 2).Select(m => idx[m]).ToArray();
        var rxn = new List<(int, int, int)>();

        foreach (string A in mols)
            foreach (string B in mols)
                if (A.Length + B.Length <= maxLen)
                    rxn.Add((idx[A], idx[B], idx[A + B]));

        return new Chem
        {
            Mol = mols.ToArray(),
            FoodIds = food,
            FoodSet = new HashSet<int>(food),
            Rxn = rxn.ToArray(),
            Idx = idx
        };
    }

    private static int LongestCommonPrefix(string a, string b)
    {
        int n = Math.Min(a.Length, b.Length), i = 0;
        while (i < n && a[i] == b[i])
            i++;
        return i;
    }

    private static List<int>[] AssignTemplateCatalysis(Chem c, double baseP, double templateBoost, Random rng)
    {
        var per = new List<int>[c.Rxn.Length];
        for (int ri = 0; ri < c.Rxn.Length; ri++)
        {
            var (_, _, prod) = c.Rxn[ri];
            string product = c.Mol[prod];
            List<int>? cats = null;

            for (int m = 0; m < c.Mol.Length; m++)
            {
                string cat = c.Mol[m];
                double sim = LongestCommonPrefix(cat, product) / (double)Math.Max(product.Length, 1);
                double p = Util.Clamp(baseP * (1.0 + templateBoost * sim * sim), 0.0, 0.8);
                if (rng.NextDouble() < p) (cats ??= new List<int>()).Add(m);
            }

            per[ri] = cats!;
        }

        return per;
    }

    private static HashSet<int> MaxRaf(IEnumerable<int> candidate, Chem c, List<int>[] cats)
    {
        var R = new HashSet<int>(candidate);
        while (true)
        {
            var reachable = new HashSet<int>(c.FoodIds);
            bool changed = true;
            while (changed)
            {
                changed = false;
                foreach (int ri in R)
                {
                    var (a, b, p) = c.Rxn[ri];
                    if (reachable.Contains(a) && reachable.Contains(b) && !reachable.Contains(p))
                    {
                        reachable.Add(p);
                        changed = true;
                    }
                }
            }

            var newR = new HashSet<int>();
            foreach (int ri in R)
            {
                var (a, b, _) = c.Rxn[ri];
                if (reachable.Contains(a) && reachable.Contains(b) && (cats[ri]?.Any(reachable.Contains) ?? false))
                    newR.Add(ri);
            }

            if (newR.Count == R.Count)
                return newR;
            R = newR;
        }
    }

    private static HashSet<int> SpeciesIn(HashSet<int> raf, Chem c)
    {
        var set = new HashSet<int>(c.FoodIds);
        foreach (int ri in raf)
        {
            var (a, b, p) = c.Rxn[ri];
            set.Add(a);
            set.Add(b);
            set.Add(p);
        }

        return set;
    }

    private static double GrowthExponent(HashSet<int> raf, Chem c, List<int>[] cats, bool catalysisOn,
        HashSet<int>? removed = null)
    {
        var active = new HashSet<int>(raf);
        if (removed != null) active.ExceptWith(removed);
        if (active.Count == 0)
            return double.NaN;

        var species = SpeciesIn(active, c);
        var nonFood = species.Where(x => !c.FoodSet.Contains(x)).ToArray();
        var y = new Dictionary<int, double>();

        foreach (int s in species)
            y[s] = c.FoodSet.Contains(s) ? 500.0 : 1e-6;

        const double kUncat = 3e-8;
        const double kCat = 2.5e-4;
        const double dilution = 0.03;
        const double dt = 0.2;
        const double tMax = 250.0;
        var series = new List<(double t, double y)>();

        for (double t = 0; t <= tMax; t += dt)
        {
            var dy = new Dictionary<int, double>();
            foreach (int s in species)
                dy[s] = 0.0;

            foreach (int ri in active)
            {
                var (a, b, p) = c.Rxn[ri];
                if (!y.ContainsKey(a) || !y.ContainsKey(b) || !y.ContainsKey(p)) continue;

                double react = a == b ? y[a] * Math.Max(0, y[a] - 1) / 2.0 : y[a] * y[b];
                double catAmount = 0.0;

                if (catalysisOn && cats[ri] != null)
                    foreach (int cat in cats[ri])
                        if (y.TryGetValue(cat, out double amt))
                            catAmount += amt;

                double rate = react * (kUncat + kCat * catAmount);
                if (!c.FoodSet.Contains(a)) 
                    dy[a] -= rate;
                if (!c.FoodSet.Contains(b)) 
                    dy[b] -= rate;

                dy[p] += rate;
            }

            foreach (int s in nonFood) dy[s] -= dilution * y[s];
            foreach (int s in nonFood) y[s] = Math.Max(0.0, y[s] + dt * dy[s]);
            foreach (int f in c.FoodIds)
                if (y.ContainsKey(f))
                    y[f] = 500.0;

            double total = 0.0;
            foreach (int s in nonFood)
                total += y[s];

            series.Add((t, total));
        }

        return Util.FitLogSlope(series, 20.0, 140.0);
    }

    public static void Run()
    {
        int L = 5;
        var chem = Build(L);

        var rng = new Random(707);
        List<int>[] cats;
        HashSet<int> raf;
        int attempts = 0;

        do
        {
            cats = AssignTemplateCatalysis(chem, baseP: 8e-3, templateBoost: 8.0, rng);
            raf = MaxRaf(Enumerable.Range(0, chem.Rxn.Length), chem, cats);
            attempts++;
        } while (raf.Count < 8 && attempts < 2000);

        double lambdaOn = GrowthExponent(raf, chem, cats, catalysisOn: true);
        double lambdaOff = GrowthExponent(raf, chem, cats, catalysisOn: false);

        using (var sw = new StreamWriter("out/life_growth_exponent.csv"))
        {
            sw.WriteLine("maxRafSize,lambdaCatalysisOn,lambdaCatalysisOff,autocatalyticAdvantage");
            sw.WriteLine($"{raf.Count},{lambdaOn:E6},{lambdaOff:E6},{lambdaOn - lambdaOff:E6}");
        }

        using (var sw = new StreamWriter("out/life_structural_vs_kinetic_essentiality.csv"))
        {
            sw.WriteLine(
                "reactionIndex,A,B,product,structuralEssential,remainingRafSize,lambdaWithout,kineticDropFraction");
            foreach (int ri in raf.OrderBy(x => x))
            {
                var reduced = MaxRaf(raf.Where(x => x != ri), chem, cats);

                bool structuralEssential = reduced.Count == 0 || reduced.Count < raf.Count * 0.5;
                double lamWithout = GrowthExponent(raf, chem, cats, catalysisOn: true,
                    removed: new HashSet<int> { ri });

                double drop = double.IsNaN(lamWithout) || Math.Abs(lambdaOn) < 1e-30
                    ? double.NaN
                    : (lambdaOn - lamWithout) / Math.Abs(lambdaOn);

                var (a, b, p) = chem.Rxn[ri];
                sw.WriteLine(
                    $"{ri},{chem.Mol[a]},{chem.Mol[b]},{chem.Mol[p]},{structuralEssential},{reduced.Count},{lamWithout:E6},{drop:E6}");
            }
        }

        using (var sw = new StreamWriter("out/life_growth_phase_scan.csv"))
        {
            sw.WriteLine("baseP,trial,maxRafSize,lambdaOn,lambdaOff,advantage");
            foreach (double p in new[] { 3e-3, 5e-3, 7e-3, 9e-3, 1.2e-2, 1.6e-2 })
            {
                for (int trial = 0; trial < 10; trial++)
                {
                    var localCats = AssignTemplateCatalysis(chem, p, 8.0, rng);
                    var localRaf = MaxRaf(Enumerable.Range(0, chem.Rxn.Length), chem, localCats);
                    if (localRaf.Count == 0)
                    {
                        sw.WriteLine($"{p},{trial},0,NaN,NaN,NaN");
                        continue;
                    }

                    double on = GrowthExponent(localRaf, chem, localCats, true);
                    double off = GrowthExponent(localRaf, chem, localCats, false);
                    sw.WriteLine($"{p},{trial},{localRaf.Count},{on:E6},{off:E6},{on - off:E6}");
                }
            }
        }

        using (var sw = new StreamWriter("out/life_summary.txt"))
        {
            sw.WriteLine("ORIGIN OF LIFE v7 — RAF structure + autocatalytic growth exponent");
            sw.WriteLine($"chosen maxRAF size         : {raf.Count}");
            sw.WriteLine($"growth exponent, catalysis : {lambdaOn:E4}");
            sw.WriteLine($"growth exponent, no cats   : {lambdaOff:E4}");
            sw.WriteLine($"autocatalytic advantage    : {lambdaOn - lambdaOff:E4}");
            sw.WriteLine("Positive advantage means true self-amplification rather than mere food throughput.");
            sw.WriteLine(
                "Compare structuralEssential with kineticDropFraction in life_structural_vs_kinetic_essentiality.csv.");
        }

        Console.WriteLine(
            $"Life -> RAF size={raf.Count}, growth advantage={lambdaOn - lambdaOff:E2}, see out/life_*.*");
    }
}