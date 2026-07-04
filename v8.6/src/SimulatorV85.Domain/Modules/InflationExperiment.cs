using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Threading;
using SimulatorV85.Domain;

namespace SimulatorV85.Domain.Modules;

public sealed class InflationExperiment : IExperiment
{
    public string Name => "inflation";
    public string Description => "Exact Mukhanov-Sasaki toy spectrum, loop-closure diagnostics, and SIGW observable proxy.";

    private const double ObservedAs = 2.10e-9;
    private const double OmegaRadiationToday = 9.2e-5;

    public ExperimentResult Run(ExperimentSpec spec, IArtifactSink artifacts, CancellationToken ct = default)
    {
        double epsPlateau = spec.Get("inflation.epsPlateau", spec.Get("usr.epsPlateau", spec.Get("epsPlateau", 1.0e-3)));
        double epsUsr = spec.Get("inflation.usrDepth", spec.Get("inflation.usr.depth", spec.Get("usr.depth", spec.Get("usrDepth", 2.5e-7))));
        double usrStart = spec.Get("inflation.usrStart", spec.Get("inflation.usr.start", spec.Get("usr.start", spec.Get("usrStart", 25.0))));
        double usrWidth = spec.Get("inflation.usrWidth", spec.Get("inflation.usr.width", spec.Get("usr.width", spec.Get("usrWidth", 2.3))));
        double usrEnd = usrStart + usrWidth;
        double edge = spec.Get("inflation.usrEdge", spec.Get("inflation.usr.edge", spec.Get("usr.edge", spec.Get("usrEdge", 0.15))));

        int msSteps = spec.GetInt("inflation.ms.steps", spec.GetInt("ms.steps", spec.GetInt("mukhanovSasaki.steps", 3200)));
        double startRatio = spec.Get("inflation.ms.startRatio", spec.Get("ms.startRatio", spec.Get("bunchDavies.startRatio", 150.0)));

        var bg = new UsrBackground(epsPlateau, epsUsr, usrStart, usrEnd, edge);
        bg.Build();

        string spectrumPath = artifacts.OutputPath(Name, spec.CaseId, "inflation_exact_scalar_spectrum.csv");
        string potentialPath = artifacts.OutputPath(Name, spec.CaseId, "inflation_reconstructed_potential.csv");
        string sigwPath = artifacts.OutputPath(Name, spec.CaseId, "inflation_sigw_observable.csv");
        string loopPath = artifacts.OutputPath(Name, spec.CaseId, "inflation_loop_closure.csv");

        double peak = 0, peakK = 0, plateau = double.NaN;
        var spectrum = new List<(double kRatio, double pR)>();
        using (var sw = new StreamWriter(spectrumPath))
        {
            sw.WriteLine("N_exit,k_over_kpivot,P_R_exact,enhancement_vs_As");
            double kPivot = bg.AH(0);
            for (double nExit = 0; nExit <= 34; nExit += 1.0)
            {
                ct.ThrowIfCancellationRequested();
                double k = bg.AH(nExit);
                double p = PowerSpectrum(bg, k, msSteps, startRatio);
                if (double.IsNaN(p)) continue;
                if (double.IsNaN(plateau)) plateau = p;
                double kRatio = k / kPivot;
                spectrum.Add((kRatio, p));
                if (p > peak) { peak = p; peakK = kRatio; }
                sw.WriteLine($"{nExit:F1},{kRatio:E6},{p:E8},{p / ObservedAs:E6}");
            }
        }

        double loopError = WriteReconstructedPotential(bg, potentialPath, loopPath);
        double sigwPeak = WriteSigwConvolution(sigwPath, spectrum, peakK);
        double sigwFrequency = FrequencyFromKRatio(peakK);
        double pbhThreshold = 1e-2;
        double pbhInterest = MathUtil.InterestNearThreshold(peak, pbhThreshold, logWidth: 2.5);
        double closurePenalty = MathUtil.Clamp(loopError, 0, 1);
        double score = MathUtil.Clamp(0.65 * pbhInterest + 0.35 * (1 - closurePenalty), 0, 1);

        var obs = new List<ObservableRecord>
        {
            new(Name, "peak_scalar_power", peak, "dimensionless", pbhInterest,
                "Maximum scalar curvature power; PBH-interest is highest near P_R~1e-2.", spec.CaseId),
            new(Name, "sigw_peak_omega", sigwPeak, "Omega_GW", MathUtil.InterestNearThreshold(sigwPeak, 1e-9, 3),
                "Scalar-induced gravitational-wave energy density proxy.", spec.CaseId),
            new(Name, "sigw_peak_frequency_hz", sigwFrequency, "Hz", MathUtil.InterestNearThreshold(sigwFrequency, 1e-3, 4),
                "Frequency proxy for comparing to PTA/LISA-like bands.", spec.CaseId),
            new(Name, "loop_closure_error", loopError, "relative", MathUtil.Clamp(loopError, 0, 1),
                "Forward/self-consistency diagnostic for the reconstructed background.", spec.CaseId),
            new(Name, "usr_width", usrWidth, "e-fold", 0,
                "Ultra-slow-roll feature width used in the run.", spec.CaseId),
            new(Name, "usr_depth", epsUsr, "epsilon", 0,
                "Ultra-slow-roll epsilon floor used in the run.", spec.CaseId)
        };

        var warnings = new List<string>
        {
            "USR background is an effective epsilon(N) reconstruction, not a fundamental potential derivation.",
            "SIGW calculation uses the standard radiation-era convolution kernel over the simulated scalar spectrum; it is still not a detector likelihood or full Boltzmann pipeline."
        };

        return new ExperimentResult(artifacts.RunId, spec.CaseId, Name, "USR+MS+SIGW-proxy", spec.Parameters,
            obs, new[] { spectrumPath, potentialPath, sigwPath, loopPath }, warnings, score);
    }

    private static double PowerSpectrum(UsrBackground bg, double k, int steps, double startRatio)
    {
        steps = Math.Clamp(steps, 500, 20000);
        startRatio = Math.Clamp(startRatio, 50.0, 500.0);
        double startF(double n) => k - startRatio * bg.AH(n);
        if (startF(bg.NMin) < 0) return double.NaN;
        double lo = bg.NMin, hi = bg.NMax;
        for (int i = 0; i < 90; i++)
        {
            double m = 0.5 * (lo + hi);
            if (startF(m) > 0) lo = m; else hi = m;
        }
        double nStart = 0.5 * (lo + hi);
        double nEnd = Math.Min(nStart + 24, bg.NMax - 0.5);
        double a = Math.Exp(nStart), e1 = bg.Eps1(nStart), h0 = bg.H(nStart);
        Complex r = new(1.0 / (2 * a * Math.Sqrt(e1 * k)), 0);
        Complex rp = r * new Complex(-1 - 0.5 * bg.Eps2(nStart), -k / (a * h0));
        double dN = (nEnd - nStart) / steps;
        double n = nStart;

        (Complex dr, Complex drp) Deriv(double xN, Complex xR, Complex xRp)
        {
            double x = k / bg.AH(xN);
            return (xRp, -(3 - bg.Eps1(xN) + bg.Eps2(xN)) * xRp - x * x * xR);
        }

        for (int s = 0; s < steps; s++)
        {
            var k1 = Deriv(n, r, rp);
            var k2 = Deriv(n + dN / 2, r + dN / 2 * k1.dr, rp + dN / 2 * k1.drp);
            var k3 = Deriv(n + dN / 2, r + dN / 2 * k2.dr, rp + dN / 2 * k2.drp);
            var k4 = Deriv(n + dN, r + dN * k3.dr, rp + dN * k3.drp);
            r += dN / 6 * (k1.dr + 2 * k2.dr + 2 * k3.dr + k4.dr);
            rp += dN / 6 * (k1.drp + 2 * k2.drp + 2 * k3.drp + k4.drp);
            n += dN;
        }
        return Math.Pow(k, 3) / (2 * Math.PI * Math.PI) * r.Magnitude * r.Magnitude;
    }

    private static double WriteReconstructedPotential(UsrBackground bg, string potentialPath, string loopPath)
    {
        var rows = new List<(double N, double phi, double eps, double H, double V)>();
        double phi = 0;
        for (double n = bg.NMin; n <= bg.NMax; n += 0.1)
        {
            double eps = bg.Eps1(n);
            phi += Math.Sqrt(2 * eps) * 0.1;
            double h = bg.H(n);
            double v = h * h * (3 - eps);
            rows.Add((n, phi, eps, h, v));
        }

        using (var sw = new StreamWriter(potentialPath))
        {
            sw.WriteLine("N,phi,epsilon_input,H,V_reconstructed");
            foreach (var r in rows) sw.WriteLine($"{r.N:F3},{r.phi:E8},{r.eps:E8},{r.H:E8},{r.V:E8}");
        }

        double maxRel = 0;
        using (var sw = new StreamWriter(loopPath))
        {
            sw.WriteLine("N,epsilon_input,epsilon_forward_proxy,relative_error");
            for (int i = 1; i < rows.Count - 1; i++)
            {
                double dlnV = (Math.Log(rows[i + 1].V) - Math.Log(rows[i - 1].V)) / (rows[i + 1].phi - rows[i - 1].phi);
                double epsForward = 0.5 * dlnV * dlnV;
                double rel = Math.Abs(epsForward - rows[i].eps) / Math.Max(rows[i].eps, 1e-30);
                maxRel = Math.Max(maxRel, Math.Min(rel, 10));
                sw.WriteLine($"{rows[i].N:F3},{rows[i].eps:E8},{epsForward:E8},{rel:E8}");
            }
        }
        return MathUtil.Clamp(maxRel / 10.0, 0, 1);
    }

    private sealed class CurvatureSpectrum
    {
        public required double[] K;
        public required double[] P;

        public double Eval(double k) => InterpLogSpectrum(k, K, P, ObservedAs);
    }

    private static double WriteSigwConvolution(string path, IReadOnlyList<(double kRatio, double pR)> spectrum, double peakK)
    {
        if (spectrum.Count < 4)
        {
            File.WriteAllText(path, "frequency_hz,k_over_kpeak,Omega_GW_today\n");
            return 0;
        }

        var spec = new CurvatureSpectrum
        {
            K = spectrum.Select(x => x.kRatio).ToArray(),
            P = spectrum.Select(x => x.pR).ToArray()
        };

        double maxOmega = 0;
        using var sw = new StreamWriter(path);
        sw.WriteLine("frequency_hz,k_over_kpeak,Omega_GW_today");
        for (double qLog = -2.5; qLog <= 2.5; qLog += 0.1)
        {
            double q = peakK * Math.Pow(10, qLog);
            double omega = SigwOmegaToday(q, spec);
            maxOmega = Math.Max(maxOmega, omega);
            sw.WriteLine($"{FrequencyFromKRatio(q):E8},{q / Math.Max(peakK, 1e-300):E8},{omega:E8}");
        }
        return maxOmega;
    }

    private static double RadiationKernelIbar2(double u, double v)
    {
        double uv = u * v;
        if (uv <= 0) return 0;
        double s = u * u + v * v - 3.0;
        double pref = 3.0 * s / (4.0 * u * u * u * v * v * v);
        double num = 3.0 - (u + v) * (u + v);
        double den = 3.0 - (u - v) * (u - v);
        if (Math.Abs(den) < 1e-300) den = Math.CopySign(1e-300, den == 0 ? 1 : den);
        double logArg = Math.Abs(num / den);
        if (logArg < 1e-300) logArg = 1e-300;
        double logTerm = Math.Log(logArg);
        double real = -4.0 * u * v + s * logTerm;
        double imag2 = (u + v > Math.Sqrt(3.0)) ? Math.PI * Math.PI * s * s : 0.0;
        return 0.5 * pref * pref * (real * real + imag2);
    }
	
    private static double SigwKernelIntegral(double kRatio, IReadOnlyList<(double kRatio, double pR)> spectrum)
    {
        // Lightweight SIGW convolution proxy. It preserves the key feature missing in v8.3:
        // Omega_GW is computed from the scalar spectrum shape instead of being drawn as a log-normal bump.
        // The resonance weight emphasizes u+v≈sqrt(3), the known radiation-era kernel feature.
        double sum = 0;
        double du = 0.18, dv = 0.18;
        for (double u = 0.25; u <= 5.0; u += du)
        for (double v = Math.Abs(1 - u) + 0.05; v <= 1 + u; v += dv)
        {
            double pu = InterpLogSpectrum(kRatio * u, spectrum);
            double pv = InterpLogSpectrum(kRatio * v, spectrum);
            if (pu <= 0 || pv <= 0) continue;
            double tri = 4 * u * u * v * v - Math.Pow(1 + v * v - u * u, 2);
            if (tri <= 0) continue;
            double geom = Math.Pow(tri / (4 * u * v), 2);
            double resonance = 1.0 / (1.0 + 35.0 * Math.Pow(u + v - Math.Sqrt(3.0), 2));
            double damping = 1.0 / Math.Pow(1 + u * u + v * v, 2);
            sum += geom * resonance * damping * pu * pv * du * dv;
        }
        return 0.45 * sum;
    }
	
	
    private static double SigwOmegaToday(double kRatio, CurvatureSpectrum spec)
    {
        const int nV = 64;
        const int nU = 64;
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
                double value = geom * RadiationKernelIbar2(u, v) * spec.Eval(kRatio * u) * spec.Eval(kRatio * v);
                double edge = (iv == 0 || iv == nV - 1 ? 0.5 : 1.0) * (iu == 0 || iu == nU - 1 ? 0.5 : 1.0);
                integral += edge * value * duWeight * dvWeight;
            }
        }

        // Radiation-era SIGW result redshifted to today. This is still a compact simulator-level
        // observable, not a real likelihood against PTA/LISA data.
        return 0.83 * OmegaRadiationToday * integral / 24.0;
    }

    private static double InterpLogSpectrum(double k, IReadOnlyList<(double kRatio, double pR)> spectrum)
    {
        if (k <= spectrum[0].kRatio || k >= spectrum[^1].kRatio) return 0;
        double x = Math.Log(k);
        for (int i = 1; i < spectrum.Count; i++)
        {
            if (k <= spectrum[i].kRatio)
            {
                double x0 = Math.Log(spectrum[i - 1].kRatio), x1 = Math.Log(spectrum[i].kRatio);
                double y0 = Math.Log(Math.Max(spectrum[i - 1].pR, 1e-300));
                double y1 = Math.Log(Math.Max(spectrum[i].pR, 1e-300));
                double t = (x - x0) / Math.Max(x1 - x0, 1e-30);
                return Math.Exp(y0 + t * (y1 - y0));
            }
        }
        return 0;
    }

    private static double InterpLogSpectrum(double k, double[] ks, double[] ps, double fallback)
    {
        if (ks.Length == 0) return fallback;
        if (k <= ks[0] || k >= ks[^1]) return fallback;
        double x = Math.Log(k);
        for (int i = 1; i < ks.Length; i++)
        {
            if (k <= ks[i])
            {
                double x0 = Math.Log(ks[i - 1]), x1 = Math.Log(ks[i]);
                double y0 = Math.Log(Math.Max(ps[i - 1], 1e-300));
                double y1 = Math.Log(Math.Max(ps[i], 1e-300));
                double t = (x - x0) / Math.Max(x1 - x0, 1e-30);
                return Math.Exp(y0 + t * (y1 - y0));
            }
        }
        return fallback;
    }

    private static double FrequencyFromKRatio(double kOverPivot)
    {
        // Pivot-mapped frequency proxy. Use only for detector-band orientation.
        const double pivotHz = 7.7e-17;
        return pivotHz * kOverPivot;
    }

    private sealed class UsrBackground
    {
        private readonly double _epsPlateau, _epsUsr, _usrStart, _usrEnd, _edge;
        private double[] _n = Array.Empty<double>();
        private double[] _lnH = Array.Empty<double>();
        private double _h0;
        public double NMin => -8;
        public double NMax => 45;

        public UsrBackground(double epsPlateau, double epsUsr, double usrStart, double usrEnd, double edge)
        { _epsPlateau = epsPlateau; _epsUsr = epsUsr; _usrStart = usrStart; _usrEnd = usrEnd; _edge = edge; }

        public void Build()
        {
            int count = 14001;
            _n = new double[count]; _lnH = new double[count];
            double d = (NMax - NMin) / (count - 1.0);
            for (int i = 0; i < count; i++) _n[i] = NMin + i * d;
            for (int i = 1; i < count; i++) _lnH[i] = _lnH[i - 1] - 0.5 * (Eps1(_n[i]) + Eps1(_n[i - 1])) * d;
            _h0 = Math.Sqrt(ObservedAs * 8 * Math.PI * Math.PI * _epsPlateau);
        }

        public double Eps1(double n)
        {
            double lo = Math.Log(_epsUsr), hi = Math.Log(_epsPlateau);
            double well = 0.25 * (1 + Math.Tanh((n - _usrStart) / _edge)) * (1 - Math.Tanh((n - _usrEnd) / _edge));
            return Math.Exp(hi + (lo - hi) * well);
        }

        public double Eps2(double n)
        {
            const double d = 1e-4;
            return (Math.Log(Eps1(n + d)) - Math.Log(Eps1(n - d))) / (2 * d);
        }

        public double H(double n) => _h0 * Math.Exp(Interp(n, _n, _lnH));
        public double AH(double n) => Math.Exp(n) * H(n);

        private static double Interp(double x, double[] xs, double[] ys)
        {
            if (x <= xs[0]) return ys[0];
            if (x >= xs[^1]) return ys[^1];
            int lo = 0, hi = xs.Length - 1;
            while (hi - lo > 1) { int m = (lo + hi) / 2; if (xs[m] <= x) lo = m; else hi = m; }
            double t = (x - xs[lo]) / (xs[hi] - xs[lo]);
            return ys[lo] + t * (ys[hi] - ys[lo]);
        }
    }
}
