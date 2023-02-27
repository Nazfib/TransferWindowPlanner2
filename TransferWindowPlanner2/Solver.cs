using UnityEngine;

namespace TransferWindowPlanner2;

public class Solver
{
    internal readonly int NDepartures;
    internal readonly int NArrivals;

    internal double GravParameter;

    internal readonly double[] DepTime;
    internal readonly Vector3d[] DepPos;
    internal readonly Vector3d[] DepCbVel;

    internal readonly double[] ArrTime;
    internal readonly Vector3d[] ArrPos;
    internal readonly Vector3d[] ArrCbVel;

    internal readonly Vector3d[,] DepVel;
    internal readonly Vector3d[,] ArrVel;
    internal readonly double[,] DepC3;
    internal readonly double[,] ArrC3;
    internal readonly double[,] TotalC3;

    internal double MinDepC3, MaxDepC3, MinArrC3, MaxArrC3, MinTotalC3, MaxTotalC3;

    public Solver(int nDepartures, int nArrivals)
    {
        NDepartures = nDepartures;
        NArrivals = nArrivals;

        DepTime = new double[nDepartures];
        DepPos = new Vector3d[nDepartures];
        DepCbVel = new Vector3d[nDepartures];
        // 400 * (8+24+24) = 43.75 kiB

        ArrTime = new double[nArrivals];
        ArrPos = new Vector3d[nArrivals];
        ArrCbVel = new Vector3d[nArrivals];
        // 400 * (8+24+24) = 43.75 kiB

        DepVel = new Vector3d[nDepartures, nArrivals];
        ArrVel = new Vector3d[nDepartures, nArrivals];
        DepC3 = new double[nDepartures, nArrivals];
        ArrC3 = new double[nDepartures, nArrivals];
        TotalC3 = new double[nDepartures, nArrivals];
        // (400 * 400) * (2*24 + 3*8) = 10.98 MiB
    }

    public void GeneratePorkchop(
        CelestialBody cbOrigin, CelestialBody cbDestination,
        double earliestDeparture, double latestDeparture,
        double earliestArrival, double latestArrival)
    {
        Debug.Log("[GeneratePorkchop] Filling input arrays...");
        FillInputs(cbOrigin, cbDestination, earliestDeparture, latestDeparture, earliestArrival, latestArrival);
        Debug.Log("[GeneratePorkchop] Solving Lambert's Problems...");
        SolveAllProblems();
        Debug.Log("[GeneratePorkchop] Done.");
        Debug.Log($"[GeneratePorkchop] C3 Departure: {MinDepC3} - {MaxDepC3}");
        Debug.Log($"[GeneratePorkchop] C3 Arrival: {MinArrC3} - {MaxArrC3}");
        Debug.Log($"[GeneratePorkchop] C3 Total: {MinTotalC3} - {MaxTotalC3}");
    }

    private void FillInputs(
        CelestialBody cbOrigin, CelestialBody cbDestination,
        double earliestDeparture, double latestDeparture,
        double earliestArrival, double latestArrival)
    {
        // Departure positions
        var depStep = (latestDeparture - earliestDeparture) / (NDepartures - 1);
        for (var i = 0; i < NDepartures; ++i)
        {
            var utDep = earliestDeparture + i * depStep;
            var startPos = cbOrigin.orbit.getRelativePositionAtUT(utDep);
            var startVel = cbOrigin.orbit.getOrbitalVelocityAtUT(utDep);

            DepTime[i] = utDep;
            DepPos[i] = startPos;
            DepCbVel[i] = startVel;
        }

        // Arrival positions
        var arrStep = (latestArrival - earliestArrival) / (NArrivals - 1);
        for (var j = 0; j < NArrivals; ++j)
        {
            var utArr = earliestArrival + j * arrStep;
            var endPos = cbDestination.orbit.getRelativePositionAtUT(utArr);
            var endVel = cbDestination.orbit.getOrbitalVelocityAtUT(utArr);

            ArrTime[j] = utArr;
            ArrPos[j] = endPos;
            ArrCbVel[j] = endVel;
        }

        GravParameter = cbOrigin.referenceBody.gravParameter;
    }

    private void SolveAllProblems()
    {
        MinDepC3 = MinArrC3 = MinTotalC3 = double.PositiveInfinity;
        MaxDepC3 = MaxArrC3 = MaxTotalC3 = double.NegativeInfinity;

        for (var i = 0; i < NDepartures; ++i)
        for (var j = 0; j < NArrivals; ++j)
        {
            SolveSingleProblem(
                GravParameter,
                DepPos[i], ArrPos[j], ArrTime[j] - DepTime[i],
                out DepVel[i, j], out ArrVel[i, j]);

            var depC3 = (DepVel[i, j] - DepCbVel[i]).sqrMagnitude;
            DepC3[i, j] = depC3;
            if (depC3 < MinDepC3) { MinDepC3 = depC3; }
            if (depC3 > MaxDepC3) { MaxDepC3 = depC3; }

            var arrC3 = (ArrVel[i, j] - ArrCbVel[j]).sqrMagnitude;
            ArrC3[i, j] = arrC3;
            if (arrC3 < MinArrC3) { MinArrC3 = arrC3; }
            if (arrC3 > MaxArrC3) { MaxArrC3 = arrC3; }

            var totalC3 = depC3 + arrC3;
            TotalC3[i, j] = totalC3;
            if (totalC3 < MinTotalC3) { MinTotalC3 = totalC3; }
            if (totalC3 > MaxTotalC3) { MaxTotalC3 = totalC3; }
        }
    }

    private static void SolveSingleProblem(
        double mu, Vector3d pos1, Vector3d pos2, double timeOfFlight,
        out Vector3d vel1, out Vector3d vel2)
    {
        // TODO: call a Lambert solver routine
        vel1 = Vector3d.zero;
        vel2 = Vector3d.zero;
    }
}
