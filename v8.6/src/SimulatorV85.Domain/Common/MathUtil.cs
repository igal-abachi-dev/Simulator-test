using System;
using System.Collections.Generic;
using System.Linq;

namespace SimulatorV85.Domain;

public static class MathUtil
{
    public static double Clamp(double x, double lo, double hi) => Math.Min(hi, Math.Max(lo, x));
    public static double Sigmoid(double x) => 1.0 / (1.0 + Math.Exp(-x));
    public static double SafeLog(double x) => Math.Log(Math.Max(x, 1e-300));

    public static double[] LinSpace(double lo, double hi, int n)
    {
        if (n <= 1) return new[] { lo };
        var a = new double[n];
        for (int i = 0; i < n; i++) a[i] = lo + (hi - lo) * i / (n - 1.0);
        return a;
    }

    public static double[] LogSpace(double lo, double hi, int n)
    {
        double l0 = Math.Log10(lo), l1 = Math.Log10(hi);
        return LinSpace(l0, l1, n).Select(x => Math.Pow(10, x)).ToArray();
    }

    public static double Pearson(IReadOnlyList<double> xs, IReadOnlyList<double> ys)
    {
        int n = Math.Min(xs.Count, ys.Count);
        if (n < 3) return double.NaN;
        double mx = 0, my = 0;
        for (int i = 0; i < n; i++) { mx += xs[i]; my += ys[i]; }
        mx /= n; my /= n;
        double cov = 0, vx = 0, vy = 0;
        for (int i = 0; i < n; i++)
        {
            double dx = xs[i] - mx, dy = ys[i] - my;
            cov += dx * dy; vx += dx * dx; vy += dy * dy;
        }
        return vx <= 0 || vy <= 0 ? double.NaN : cov / Math.Sqrt(vx * vy);
    }

    public static double AnomalyOutsideBand(double value, double expectedMin, double expectedMax)
    {
        if (double.IsNaN(value) || double.IsInfinity(value)) return 1;
        if (value >= expectedMin && value <= expectedMax) return 0;
        double scale = Math.Max(Math.Abs(expectedMax - expectedMin), 1e-30);
        double d = value < expectedMin ? expectedMin - value : value - expectedMax;
        return Clamp(d / scale, 0, 1);
    }

    public static double InterestNearThreshold(double value, double threshold, double logWidth = 2.0)
    {
        double ratio = Math.Abs(Math.Log10(Math.Max(value, 1e-300) / threshold));
        return Clamp(1.0 - ratio / logWidth, 0, 1);
    }
}
