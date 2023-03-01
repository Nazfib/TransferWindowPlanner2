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

    private static double PeriapsisVelocitySquared(double mu, double sphereOfInfluence, double c3, double periapsis)
    {
        return 2 * mu / periapsis + c3 - 2 * mu / sphereOfInfluence;
    }

    public static double ΔvForC3(double mu, double sphereOfInfluence, double c3, double periapsis, bool circularize)
    {
        var vStart = circularize
            ? Math.Sqrt(mu / periapsis)
            : Math.Sqrt(2 * mu / periapsis);

        return Math.Sqrt(PeriapsisVelocitySquared(mu, sphereOfInfluence, c3, periapsis)) - vStart;
    }

    public static Vector3d PeriapsisDirection(
        double mu, double sphereOfInfluence, Vector3d vInf, double periapsis, double inclination, double lan)
    {
        var depC3 = vInf.sqrMagnitude;
        var depPeVel2 = PeriapsisVelocitySquared(mu, sphereOfInfluence, depC3, periapsis);
        var depEcc = periapsis * depPeVel2 / mu - 1;
        var depNormal = new Vector3d(
            Math.Sin(lan) * Math.Sin(inclination),
            -Math.Cos(lan) * Math.Sin(inclination),
            Math.Cos(inclination));
        var depPeDir = PeriapsisDirectionHelper(vInf, -1 / depEcc, depNormal);
        return depPeDir;
    }

    private static Vector3d PeriapsisDirectionHelper(Vector3d vInf, double cosTrueAnomaly, Vector3d normal)
    {
        // Given:
        // - the velocity after escape (asymptote of the hyperbola)
        // - the cosine of the angle between the asymptote and the periapsis
        // - a vector, normal to the plane.

        vInf = vInf.normalized;

        // We have three equations of three unknowns (v.x, v.y, v.z):
        //   dot(v, vInf) = cosTrueAnomaly
        //   norm(v) = 1  [Unit vector]
        //   dot(v, normal) = 0  [Perpendicular to normal]
        //
        // Solution is defined iff:
        //   normal.z != 0
        //   vInf.y != 0 or (vInf.z != 0 and normal.y != 0) [because we are solving for v.x first]
        //   vInf is not parallel to normal

        // Intermediate terms
        var f = vInf.y - vInf.z * normal.y / normal.z;
        var g = (vInf.z * normal.x - vInf.x * normal.z) / (vInf.y * normal.z - vInf.z * normal.y);
        var h = (normal.x + g * normal.y) / normal.z;
        var m = normal.y * normal.y + normal.z * normal.z;
        var n = f * normal.z * normal.z / cosTrueAnomaly;

        // Quadratic coefficients
        var a = 1 + g * g + h * h;
        var b = 2 * (g * m + normal.x * normal.y) / n;
        var c = m * cosTrueAnomaly / (f * n) - 1;

        // Quadratic formula without loss of significance (Numerical Recipes eq. 5.6.4)
        double q;
        if (b < 0) { q = -0.5 * (b - Math.Sqrt(b * b - 4 * a * c)); }
        else { q = -0.5 * (b + Math.Sqrt(b * b - 4 * a * c)); }

        Vector3d v;
        v.x = q / a;
        v.y = g * v.x + cosTrueAnomaly / f;
        v.z = -(v.x * normal.x + v.y * normal.y) / normal.z;

        if (Vector3d.Dot(Vector3d.Cross(v, vInf), normal) < 0)
        {
            // Wrong orbital direction
            v.x = c / q;
            v.y = g * v.x + cosTrueAnomaly / f;
            v.z = -(v.x * normal.x + v.y * normal.y) / normal.z;
        }
        return v;
    }

    public static double Wrap2Pi(double x)
    {
        x %= 2 * Math.PI;
        if (x < 0) { x += 2 * Math.PI; }
        return x;
    }
}
