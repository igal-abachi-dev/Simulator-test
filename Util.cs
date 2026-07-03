namespace Simulator;

public static class Util
{
    public static double Clamp(double x, double lo, double hi) => x < lo ? lo : (x > hi ? hi : x);

    public static double Lerp(double a, double b, double t) => a + (b - a) * t;

    public static double Interp(double x, double[] xs, double[] ys)
    {
        if (xs.Length == 0)
            throw new InvalidOperationException("empty interpolation grid");

        if (x <= xs[0]) return ys[0];
        if (x >= xs[^1]) return ys[^1];

        int lo = 0, hi = xs.Length - 1;
        while (hi - lo > 1)
        {
            int m = (lo + hi) / 2;
            if (xs[m] <= x) lo = m;
            else hi = m;
        }

        double t = (x - xs[lo]) / (xs[hi] - xs[lo]);
        return Lerp(ys[lo], ys[hi], t);
    }

    public static double LogInterpPositive(double x, double[] xs, double[] ys, double floor = 1e-300)
    {
        if (x <= xs[0]) return Math.Max(floor, ys[0]);
        if (x >= xs[^1]) return Math.Max(floor, ys[^1]);

        int lo = 0, hi = xs.Length - 1;
        while (hi - lo > 1)
        {
            int m = (lo + hi) / 2;
            if (xs[m] <= x) lo = m;
            else hi = m;
        }

        double t = (Math.Log(x) - Math.Log(xs[lo])) / (Math.Log(xs[hi]) - Math.Log(xs[lo]));
        double a = Math.Log(Math.Max(floor, ys[lo]));
        double b = Math.Log(Math.Max(floor, ys[hi]));

        return Math.Exp(Lerp(a, b, t));
    }

    public static double FitLogSlope(List<(double t, double y)> series, double tMin, double tMax)
    {
        var pts = series.Where(p => p.t >= tMin && p.t <= tMax && p.y > 0).ToList();
        if (pts.Count < 3)
            return double.NaN;

        double sx = 0, sy = 0, sxx = 0, sxy = 0;
        foreach (var (t, y) in pts)
        {
            double ly = Math.Log(y);
            sx += t;
            sy += ly;
            sxx += t * t;
            sxy += t * ly;
        }

        double n = pts.Count;
        double den = n * sxx - sx * sx;
        return Math.Abs(den) < 1e-30 ? double.NaN : (n * sxy - sx * sy) / den;
    }
}