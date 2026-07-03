using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

/*
======================================================================
Simulator — observables + self-consistency

 v6 already did the important conceptual upgrades: exact Mukhanov-Sasaki
 modes through an ultra-slow-roll feature, reconstructed V(phi), RAF
 structure/ignition, and finite-size CSL vs environmental decoherence.

 v7 moves one step closer to what a real research workflow would ask:

 1) INFLATION / PBH / SIGW
    - Keep exact Mukhanov-Sasaki evolution for the scalar curvature spectrum.
    - Reconstruct H(N), phi(N), V(phi), then FORWARD-INTEGRATE that V(phi)
      as a closure test. If the reconstructed potential does not reproduce
      the input epsilon(N), the model is just a parametrisation, not a
      self-consistent single-field background.
    - Convert the scalar peak into the observable second-order stochastic
      gravitational-wave spectrum Omega_GW(f). This is the actual lab/sky
      observable for PBH-producing small-scale curvature peaks.

 2) ORIGIN OF LIFE / RAF KINETICS
    - Still use rigorous RAF/maxRAF structure.
    - Add a deterministic kinetic growth exponent: the early-time slope of
      log RAF population under food-buffered mass-action dynamics.
    - Cross-check structural essentiality against kinetic essentiality. A
      reaction can be graph-essential but kinetically weak, or structurally
      replaceable yet dynamically rate-limiting.

 3) MEASUREMENT / CSL OBSERVABLES
    - Keep the honest stance: standard quantum mechanics has no consciousness
      term. Any consciousness bias is a non-standard null test.
    - Add a toy but physically-scaled CSL exclusion landscape in (lambda,rC),
      using the two observables collapse models are actually constrained by:
      matter-wave visibility loss and spontaneous X-ray-like radiation.
    - The X-ray module is a scaling/recast toy, not an official experimental
      limit; it is there to show the correct observable dependence.

 Single file, no NuGet packages. Output is CSV/TXT in ./out.
 Run: dotnet run -- all | inflation | life | consciousness | measurement
======================================================================
*/
namespace Simulator;

internal class Program
{
    static void Main(string[] args)
    {
        CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
        Directory.CreateDirectory("out");
        string mode = args.Length == 0 ? "all" : args[0].Trim().ToLowerInvariant();

        if (mode is "all" or "inflation") Inflation.Run();
        if (mode is "all" or "life") OriginOfLife.Run();
        if (mode is "all" or "consciousness" or "measurement") Measurement.Run();

        Console.WriteLine("Done. CSV/TXT files written to ./out");
    }
}