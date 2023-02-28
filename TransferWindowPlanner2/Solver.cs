using System;
using static TransferWindowPlanner2.MathUtils;

namespace TransferWindowPlanner2;

public class Solver
{
    private readonly int _nDepartures;
    private readonly int _nArrivals;

    internal readonly double[,] DepΔv;
    internal readonly double[,] ArrΔv;
    internal readonly double[,] TotalΔv;

    internal double MinDepΔv, MinArrΔv, MinTotalΔv;
    internal (int, int) MinDepPoint, MinArrPoint, MinTotalPoint;

    private CelestialBody? _cbOrigin;
    private CelestialBody? _cbDestination;
    private double _earliestDeparture;
    private double _latestDeparture;
    private double _earliestArrival;
    private double _latestArrival;
    private double _departurePeR;
    private double _arrivalPeR;
    private bool _circularize;

    public Solver(int nDepartures, int nArrivals)
    {
        _nDepartures = nDepartures;
        _nArrivals = nArrivals;

        DepΔv = new double[nDepartures, nArrivals];
        ArrΔv = new double[nDepartures, nArrivals];
        TotalΔv = new double[nDepartures, nArrivals];
        // (400 * 400) * (3*8) = 3.66 MiB
    }

    public void GeneratePorkchop(
        CelestialBody cbOrigin, CelestialBody cbDestination,
        double earliestDeparture, double latestDeparture,
        double earliestArrival, double latestArrival,
        double departureAltitude, double arrivalAltitude, bool circularize)
    {
        _cbOrigin = cbOrigin;
        _cbDestination = cbDestination;
        _earliestDeparture = earliestDeparture;
        _latestDeparture = latestDeparture;
        _earliestArrival = earliestArrival;
        _latestArrival = latestArrival;
        _departurePeR = cbOrigin.Radius + departureAltitude;
        _arrivalPeR = cbDestination.Radius = departureAltitude;
        _circularize = circularize;
        SolveAllProblems();
    }

    internal (double, double) TimesFor((int, int) tuple)
    {
        var (i, j) = tuple;
        return TimesFor(i, j);
    }

    internal (double, double) TimesFor(int i, int j)
    {
        var depStep = (_latestDeparture - _earliestDeparture) / (_nDepartures - 1);
        var arrStep = (_latestArrival - _earliestArrival) / (_nArrivals - 1);

        // Left to right -> increasing departure time
        var tDep = _earliestDeparture + i * depStep;
        // Top to bottom -> decreasing arrival time
        var tArr = _latestArrival - j * arrStep;
        return (tDep, tArr);
    }

    private void SolveAllProblems()
    {
        if (_cbOrigin == null || _cbDestination == null) { return; }

        MinDepΔv = MinArrΔv = MinTotalΔv = double.PositiveInfinity;

        for (var i = 0; i < _nDepartures; ++i)
        for (var j = 0; j < _nArrivals; ++j)
        {
            // Left to right -> increasing departure time
            var (tDep, tArr) = TimesFor(i, j);

            var timeOfFlight = tArr - tDep;
            if (timeOfFlight > 0)
            {
                SolveSingleProblem(
                    _cbOrigin, _cbDestination, tDep, tArr,
                    out _, out _,
                    out var depCbVel, out var arrCbVel,
                    out var depVel, out var arrVel);

                var depC3 = (depVel - depCbVel).sqrMagnitude;
                var depΔv = DepΔv[i, j] = ΔvForC3(_cbOrigin.gravParameter, depC3, _departurePeR, true);
                if (depΔv < MinDepΔv)
                {
                    MinDepΔv = depΔv;
                    MinDepPoint = (i, j);
                }

                var arrC3 = (arrVel - arrCbVel).sqrMagnitude;
                var arrΔv = ArrΔv[i, j] = ΔvForC3(_cbDestination.gravParameter, arrC3, _arrivalPeR, _circularize);
                if (arrΔv < MinArrΔv)
                {
                    MinArrΔv = arrΔv;
                    MinArrPoint = (i, j);
                }

                var totalΔv = TotalΔv[i, j] = depΔv + arrΔv;
                if (totalΔv < MinTotalΔv)
                {
                    MinTotalΔv = totalΔv;
                    MinTotalPoint = (i, j);
                }
            }
            else { DepΔv[i, j] = ArrΔv[i, j] = TotalΔv[i, j] = double.NaN; }
        }
    }

    public struct TransferDetails
    {
        // Departure:
        public double DepartureTime;

        public double DeparturePeriapsis;

        public double DepartureInclination;

        public double DepartureLAN;

        public double DepartureAsyRA;

        public double DepartureAsyDecl;

        public double DepartureC3;

        public double DepartureΔv;

        // Arrival:
        public double ArrivalTime;

        public double ArrivalPeriapsis;

        public double ArrivalDistance;

        public double ArrivalAsyRA;

        public double ArrivalAsyDecl;

        public double ArrivalC3;

        public double ArrivalΔv;

        // Transfer:
        public double TimeOfFlight;

        public double TotalΔv;
    }

    public static TransferDetails CalculateDetails(
        CelestialBody origin, CelestialBody destination,
        double depPeA, double arrPeA,
        double depInc, bool circularize,
        double tDep, double tArr)
    {
        SolveSingleProblem(
            origin,
            destination, tDep, tArr, out var depPos,
            out var arrPos,
            out var depCbVel,
            out var arrCbVel,
            out var depVel,
            out var arrVel
        );
        var depPeR = depPeA + origin.Radius;
        var arrPeR = arrPeA + destination.Radius;

        var depRelVel = depVel - depCbVel;
        var arrRelVel = arrVel - arrCbVel;

        var depC3 = depRelVel.sqrMagnitude;
        var arrC3 = arrRelVel.sqrMagnitude;

        var depΔv = ΔvForC3(origin.gravParameter, depC3, depPeR, true);
        var arrΔv = ΔvForC3(destination.gravParameter, arrC3, arrPeR, circularize);

        var arrDistance = (origin.orbit.getRelativePositionAtUT(tDep).xzy - arrPos).magnitude;

        var depAsyRA = Math.Atan2(depRelVel.y, depRelVel.x);
        var arrAsyRA = Math.Atan2(arrRelVel.y, arrRelVel.x);

        var depAsyDecl = Math.Asin(depRelVel.z / depRelVel.magnitude);
        var arrAsyDecl = Math.Asin(arrRelVel.z / arrRelVel.magnitude);

        double depLAN;
        if (Math.Abs(depAsyDecl) < depInc) { depLAN = depAsyRA - Math.Asin(Math.Tan(depAsyDecl) / Math.Tan(depInc)); }
        else
        {
            depInc = Math.Abs(depAsyDecl);
            depLAN = depAsyRA - 0.5 * Math.PI * Math.Sign(depAsyDecl);
        }
        if (depLAN < 0) { depLAN += 2 * Math.PI; }


        return new TransferDetails
        {
            DepartureTime = tDep,
            DeparturePeriapsis = depPeA,
            DepartureInclination = depInc,
            DepartureLAN = depLAN,
            DepartureAsyRA = depAsyRA,
            DepartureAsyDecl = depAsyDecl,
            DepartureC3 = depC3,
            DepartureΔv = depΔv,
            ArrivalTime = tArr,
            ArrivalPeriapsis = arrPeA,
            ArrivalDistance = arrDistance,
            ArrivalAsyRA = arrAsyRA,
            ArrivalAsyDecl = arrAsyDecl,
            ArrivalC3 = arrC3,
            ArrivalΔv = arrΔv,
            TimeOfFlight = tArr - tDep,
            TotalΔv = depΔv + arrΔv,
        };
    }

    private static void SolveSingleProblem(
        CelestialBody origin, CelestialBody destination,
        double tDep, double tArr,
        out Vector3d depPos, out Vector3d arrPos,
        out Vector3d depCbVel, out Vector3d arrCbVel,
        out Vector3d depVel, out Vector3d arrVel)
    {
        // TODO: we're calculating the same value 400 times for each. Look into caching it.
        depPos = origin.orbit.getRelativePositionAtUT(tDep).xzy;
        depCbVel = origin.orbit.getOrbitalVelocityAtUT(tDep).xzy;
        arrPos = destination.orbit.getRelativePositionAtUT(tArr).xzy;
        arrCbVel = destination.orbit.getOrbitalVelocityAtUT(tArr).xzy;

        var mu = origin.referenceBody.gravParameter;

        MechJebLib.Maths.Gooding.Solve(
            mu, depPos, depCbVel, arrPos,
            tArr - tDep, 0,
            out depVel, out arrVel);
    }
}
