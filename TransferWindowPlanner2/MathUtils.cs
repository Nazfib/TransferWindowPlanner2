using System;

namespace TransferWindowPlanner2;

public static class MathUtils
{
    public static double SynodicPeriod(double p1, double p2)
    {
        return Math.Abs(1 / (1 / p1 - 1 / p2));
    }

    public static double HohmannTime(double mu, double sma1, double sma2)
    {
        var a = (sma1 + sma2) * 0.5;
        return Math.PI * Math.Sqrt(a * a * a / mu);
    }

    public static double ΔvForC3(double mu, double c3, double periapsis, bool circularize)
    {
        var vStart = circularize
            ? Math.Sqrt(mu / periapsis)
            : Math.Sqrt(2 * mu / periapsis);

        return Math.Sqrt(2 * mu / periapsis + c3) - vStart;
    }

    public static double Wrap2Pi(double x)
    {
        x %= 2 * Math.PI;
        if (x < 0) { x += 2 * Math.PI; }
        return x;
    }
}
