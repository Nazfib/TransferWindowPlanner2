using System;
using MechJebLib.Primitives;
using static MechJebLib.Functions.Astro;
using static MechJebLib.Utils.Statics;

namespace TransferWindowPlanner2.Solver
{
/// <summary>
/// Some more math functions that don't exist in MechJebLib.
/// </summary>
public static class MoreMaths
{
    public static double SynodicPeriod(double p1, double p2) => Math.Abs(1.0 / (1.0 / p1 - 1.0 / p2));

    public static double HohmannTime(double mu, double sma1, double sma2)
    {
        var a = (sma1 + sma2) * 0.5;
        return .5 * TAU * Math.Sqrt(a * a * a / mu);
    }

    private static double PeriapsisVelocityElliptical(double mu, double periapsis, double apoapsis)
    {
        var sma = SmaFromApsides(periapsis, apoapsis);
        return VmagFromVisViva(mu, sma, periapsis);
    }

    private static double PeriapsisVelocitySquared(double mu, double sphereOfInfluence, double c3, double periapsis) =>
        2 * mu / periapsis + c3 - 2 * mu / sphereOfInfluence;

    public static double ΔvFromC3(double mu, double sphereOfInfluence, double c3, double periapsis, double apoapsis)
    {
        var vStart = PeriapsisVelocityElliptical(mu, periapsis, apoapsis);

        // ReSharper disable once InconsistentNaming
        var Δv = Math.Sqrt(PeriapsisVelocitySquared(mu, sphereOfInfluence, c3, periapsis)) - vStart;
        return Δv > 0 ? Δv : double.NaN;
    }

    public static V3 PeriapsisDirection(double mu, V3 vInf, double periapsis, double inclination, double lan)
    {
        // Ignore the slight difference between the direction of the escape velocity at SOI radius, and velocity at
        // infinite distance.
        var c3 = vInf.sqrMagnitude;
        var sma = -mu / c3;
        var ecc = 1.0 - periapsis / sma;
        var nuInf = SafeAcos(-1.0 / ecc);

        var normal = new V3(1.0, inclination, lan - .25 * TAU).sph2cart;
        var peDir = Q3.AngleAxis(-nuInf, normal) * vInf;
        return peDir.normalized;
    }

    public static (double inc, double lan) LANAndIncForAsymptote(
        double minInc, double declination, double rightAscension)
    {
        double inc, lan;
        if (Math.Abs(declination) < minInc)
        {
            inc = minInc;
            lan = rightAscension - Math.Asin(Math.Tan(declination) / Math.Tan(minInc));
        }
        else
        {
            inc = Math.Abs(declination);
            lan = rightAscension - 0.25 * TAU * Math.Sign(declination);
        }
        return (inc, Clamp2Pi(lan));
    }
}
}
