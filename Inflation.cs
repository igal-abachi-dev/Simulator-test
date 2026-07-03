using System.Numerics;

namespace Simulator;


// ====================================================================
// 1) INFLATION — exact scalar spectrum, loop closure, induced GW signal
// ====================================================================
public static class Inflation
{
    private const double ObservedAs = 2.10e-9;
    private const double PivotMpcInv = 0.05;
    private const double HzPerMpcInv = 1.546e-15;
    private const double OmegaRadiationToday = 9.2e-5;

    private const double EpsPlateau = 1.0e-3;
    private const double EpsUsr = 2.5e-7;
    private const double UsrStart = 25.0;
    private const double UsrEnd = 27.3;
    private const double UsrEdge = 0.15;

    private static double[] _N = Array.Empty<double>();
    private static double[] _lnH = Array.Empty<double>();
    private static double[] _phi = Array.Empty<double>();
    private static double[] _V = Array.Empty<double>();
    private static double[] _VpOverV = Array.Empty<double>();
    private static double _H0;

    private static double Eps1(double N)
    {
        double lo = Math.Log(EpsUsr), hi = Math.Log(EpsPlateau);
        double well = 0.5 * (1 + Math.Tanh((N - UsrStart) / UsrEdge))
                          * 0.5 * (1 - Math.Tanh((N - UsrEnd) / UsrEdge));

        return Math.Exp(hi + (lo - hi) * well);
    }

    private static double Eps2(double N)
    {
        const double d = 1e-4;
        return (Math.Log(Eps1(N + d)) - Math.Log(Eps1(N - d))) / (2 * d);
    }

    private static double H(double N) => _H0 * Math.Exp(Util.Interp(N, _N, _lnH));
    private static double aH(double N) => Math.Exp(N) * H(N);

    private static void BuildBackground()
    {
        int n = 53001;

        _N = new double[n];
        _lnH = new double[n];
        _phi = new double[n];
        _V = new double[n];
        _VpOverV = new double[n];

        double nMin = -8.0, nMax = 45.0;
        double dN = (nMax - nMin) / (n - 1);
        for (int i = 0; i < n; i++)
            _N[i] = nMin + i * dN;

        _lnH[0] = 0.0;
        _phi[0] = 0.0;
        for (int i = 1; i < n; i++)
        {
            double e0 = Eps1(_N[i - 1]);
            double e1 = Eps1(_N[i]);
            _lnH[i] = _lnH[i - 1] - 0.5 * (e0 + e1) * dN;
            _phi[i] = _phi[i - 1] + 0.5 * (Math.Sqrt(2 * e0) + Math.Sqrt(2 * e1)) * dN;
        }

        _H0 = Math.Sqrt(ObservedAs * 8.0 * Math.PI * Math.PI * EpsPlateau);

        for (int i = 0; i < n; i++)
        {
            double h = H(_N[i]);
            _V[i] = h * h * (3.0 - Eps1(_N[i]));
        }

        for (int i = 1; i < n - 1; i++)
        {
            double dV = _V[i + 1] - _V[i - 1];
            double dPhi = _phi[i + 1] - _phi[i - 1];
            _VpOverV[i] = dPhi == 0 ? 0 : (dV / dPhi) / _V[i];
        }

        _VpOverV[0] = _VpOverV[1];
        _VpOverV[^1] = _VpOverV[^2];
    }

    private static double VofPhi(double phi) => Util.LogInterpPositive(phi, _phi, _V);
    private static double VpOverVofPhi(double phi) => Util.Interp(phi, _phi, _VpOverV);

    private static double PowerSpectrum(double k)
    {
        double f(double N) => k - 100.0 * aH(N);
        if (f(_N[0]) < 0) 
            return double.NaN;

        double lo = _N[0], hi = _N[^1];
        for (int i = 0; i < 100; i++)
        {
            double m = 0.5 * (lo + hi);
            if (f(m) > 0) lo = m;
            else hi = m;
        }

        double nStart = 0.5 * (lo + hi);
        double nEnd = Math.Min(nStart + 22.0, _N[^1] - 0.5);

        double a = Math.Exp(nStart), e1 = Eps1(nStart), hStart = H(nStart);
        Complex R = new Complex(1.0 / (2.0 * a * Math.Sqrt(e1 * k)), 0.0);
        Complex Rp = R * new Complex(-1.0 - 0.5 * Eps2(nStart), -k / (a * hStart));

        int steps = 4000;
        double step = (nEnd - nStart) / steps;
        double N = nStart;

        (Complex dR, Complex dRp) Deriv(double n, Complex r, Complex rp)
        {
            double x = k / aH(n);
            return (rp, -(3.0 - Eps1(n) + Eps2(n)) * rp - x * x * r);
        }

        for (int s = 0; s < steps; s++)
        {
            var k1 = Deriv(N, R, Rp);
            var k2 = Deriv(N + step / 2, R + step / 2 * k1.dR, Rp + step / 2 * k1.dRp);
            var k3 = Deriv(N + step / 2, R + step / 2 * k2.dR, Rp + step / 2 * k2.dRp);
            var k4 = Deriv(N + step, R + step * k3.dR, Rp + step * k3.dRp);

            R += step / 6.0 * (k1.dR + 2.0 * k2.dR + 2.0 * k3.dR + k4.dR);
            Rp += step / 6.0 * (k1.dRp + 2.0 * k2.dRp + 2.0 * k3.dRp + k4.dRp);

            N += step;
        }

        return Math.Pow(k, 3) / (2.0 * Math.PI * Math.PI) * R.Magnitude * R.Magnitude;
    }

    private static void LoopClosure()
    {
        using var sw = new StreamWriter("out/inflation_loop_closure.csv");
        sw.WriteLine("N,inputPhi,forwardPhi,inputEps,forwardEps,relativeEpsError");

        double N = _N[0];
        double phi = _phi[0];
        double dphi = Math.Sqrt(2.0 * Eps1(N));
        double dN = 0.005;
        double rms = 0;
        int count = 0;

        double Accel(double p, double dp)
        {
            double r = VpOverVofPhi(p);
            return 0.5 * dp * dp * dp - 3.0 * dp - r * (3.0 - 0.5 * dp * dp);
        }

        while (N <= _N[^1] - dN)
        {
            double inputPhi = Util.Interp(N, _N, _phi);
            double inputEps = Eps1(N);
            double forwardEps = 0.5 * dphi * dphi;
            double rel = Math.Abs(forwardEps - inputEps) / Math.Max(inputEps, 1e-30);
            if (N > 0 && N < 35)
            {
                rms += rel * rel;
                count++;
            }

            if (Math.Abs((N * 10.0) - Math.Round(N * 10.0)) < 0.003)
                sw.WriteLine($"{N:F3},{inputPhi:E8},{phi:E8},{inputEps:E8},{forwardEps:E8},{rel:E4}");

            double a1 = dphi, b1 = Accel(phi, dphi);
            double a2 = dphi + dN / 2 * b1, b2 = Accel(phi + dN / 2 * a1, dphi + dN / 2 * b1);
            double a3 = dphi + dN / 2 * b2, b3 = Accel(phi + dN / 2 * a2, dphi + dN / 2 * b2);
            double a4 = dphi + dN * b3, b4 = Accel(phi + dN * a3, dphi + dN * b3);

            phi += dN / 6 * (a1 + 2 * a2 + 2 * a3 + a4);
            dphi += dN / 6 * (b1 + 2 * b2 + 2 * b3 + b4);

            N += dN;
        }

        File.WriteAllText("out/inflation_loop_closure_summary.txt",
            $"RMS relative epsilon closure error, 0<N<35: {Math.Sqrt(rms / Math.Max(1, count)):E4}\n" +
            "Small error means the reconstructed V(phi) is close to a self-consistent canonical single-field background.\n" +
            "Large spikes near USR edges mean the epsilon(N) feature is more a reconstruction than a natural potential.\n");
    }

    private sealed class CurvatureSpectrum
    {
        public required double[] K;
        public required double[] P;
        public double Eval(double k) => Util.LogInterpPositive(k, K, P, ObservedAs);
    }

    private static double RadiationKernelIbar2(double u, double v)
    {
        double uv = u * v;
        if (uv <= 0) return 0;

        double s = u * u + v * v - 3.0;
        double pref = 3.0 * s / (4.0 * u * u * u * v * v * v);
        double num = 3.0 - (u + v) * (u + v);
        double den = 3.0 - (u - v) * (u - v);
        double logArg = Math.Abs(num / den);

        if (logArg < 1e-300) 
            logArg = 1e-300;

        double logTerm = Math.Log(logArg);
        double real = -4.0 * u * v + s * logTerm;
        double imag2 = (u + v > Math.Sqrt(3.0)) ? Math.PI * Math.PI * s * s : 0.0;

        return 0.5 * pref * pref * (real * real + imag2);
    }

    private static double SigwOmegaToday(double k, CurvatureSpectrum spec)
    {
        int nV = 70;
        int nU = 70;
        double vMin = 0.05, vMax = 5.0;

        double dLogV = Math.Log(vMax / vMin) / (nV - 1);
        double integral = 0.0;

        for (int iv = 0; iv < nV; iv++)
        {
            double v = vMin * Math.Exp(iv * dLogV);
            double dvWeight = v * dLogV;
            double uMin = Math.Max(Math.Abs(1.0 - v), 0.05);
            double uMax = 1.0 + v;

            if (uMax <= uMin) continue;

            double dLogU = Math.Log(uMax / uMin) / (nU - 1);

            for (int iu = 0; iu < nU; iu++)
            {
                double u = uMin * Math.Exp(iu * dLogU);
                double duWeight = u * dLogU;
                double triangle = 4.0 * v * v - Math.Pow(1.0 + v * v - u * u, 2.0);

                if (triangle <= 0) continue;

                double geom = Math.Pow(triangle / (4.0 * u * v), 2.0);
                double val = geom * RadiationKernelIbar2(u, v) * spec.Eval(k * u) * spec.Eval(k * v);
                double edge = (iv == 0 || iv == nV - 1 ? 0.5 : 1.0) * (iu == 0 || iu == nU - 1 ? 0.5 : 1.0);

                integral += edge * val * duWeight * dvWeight;
            }
        }

        // Radiation-era result redshifted to today. This is a compact numerical
        // implementation of the standard SIGW convolution, not a Boltzmann solver.
        return 0.83 * OmegaRadiationToday * integral / 24.0;
    }

    public static void Run()
    {
        BuildBackground();
        LoopClosure();

        var kList = new List<double>();
        var pList = new List<double>();
        double peakP = 0, peakK = 0, plateau = double.NaN;
        double kPivotCode = aH(0.0);

        using (var sw = new StreamWriter("out/inflation_exact_scalar_spectrum.csv"))
        {
            sw.WriteLine("N_exit,k_over_kpivot,k_Mpc_inv,frequency_Hz,P_R_exact,enhancement_vs_As");
            for (double nExit = 0; nExit <= 33.0; nExit += 0.5)
            {
                double k = aH(nExit);
                double P = PowerSpectrum(k);

                if (double.IsNaN(P)) continue;
                if (double.IsNaN(plateau)) 
                    plateau = P;

                if (P > peakP)
                {
                    peakP = P;
                    peakK = k;
                }

                kList.Add(k / kPivotCode * PivotMpcInv);
                pList.Add(P);

                double kMpc = k / kPivotCode * PivotMpcInv;

                sw.WriteLine(
                    $"{nExit:F1},{k / kPivotCode:E5},{kMpc:E5},{kMpc * HzPerMpcInv:E5},{P:E6},{P / ObservedAs:E3}");
            }
        }

        var spec = new CurvatureSpectrum
        {
            K = kList.ToArray(),
            P = pList.ToArray()
        };

        using (var sw = new StreamWriter("out/inflation_reconstructed_potential.csv"))
        {
            sw.WriteLine("N,phi,H,V,VpOverV,epsilonH");
            for (int i = 0; i < _N.Length; i += 250)
                sw.WriteLine($"{_N[i]:F4},{_phi[i]:E8},{H(_N[i]):E8},{_V[i]:E8},{_VpOverV[i]:E8},{Eps1(_N[i]):E8}");
        }

        double kPeakMpc = peakK / kPivotCode * PivotMpcInv;
        using (var sw = new StreamWriter("out/inflation_sigw_observable.csv"))
        {
            sw.WriteLine("k_Mpc_inv,frequency_Hz,OmegaGW_today");
            for (int i = -24; i <= 24; i++)
            {
                double k = kPeakMpc * Math.Exp(i * 0.12);
                double om = SigwOmegaToday(k, spec);
                sw.WriteLine($"{k:E6},{k * HzPerMpcInv:E6},{om:E6}");
            }
        }

        using (var sw = new StreamWriter("out/inflation_summary.txt"))
        {
            sw.WriteLine("INFLATION v7 — exact scalar spectrum + loop closure + scalar-induced GW observable");
            sw.WriteLine($"plateau P_R     : {plateau:E4}  target As={ObservedAs:E4}");
            sw.WriteLine($"peak P_R        : {peakP:E4}");
            sw.WriteLine($"peak k          : {kPeakMpc:E4} Mpc^-1");
            sw.WriteLine($"peak f          : {kPeakMpc * HzPerMpcInv:E4} Hz");
            sw.WriteLine($"enhancement     : {peakP / Math.Max(plateau, 1e-300):E4}");
            sw.WriteLine("New in v7: Omega_GW(f), the real observable associated with a PBH-scale scalar peak.");
            sw.WriteLine(
                "Read inflation_loop_closure_summary.txt before trusting the reconstructed potential as first-principles.");
        }

        Console.WriteLine(
            $"Inflation -> peak P_R={peakP:E2}, f~{kPeakMpc * HzPerMpcInv:E2} Hz, see out/inflation_*.*");
    }
}