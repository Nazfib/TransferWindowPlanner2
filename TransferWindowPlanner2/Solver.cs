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
        _arrivalPeR = cbDestination.Radius + departureAltitude;
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
                var depΔv = DepΔv[i, j] = ΔvForC3(
                    _cbOrigin.gravParameter, _cbOrigin.sphereOfInfluence, depC3, _departurePeR, true);
                if (depΔv < MinDepΔv)
                {
                    MinDepΔv = depΔv;
                    MinDepPoint = (i, j);
                }

                var arrC3 = (arrVel - arrCbVel).sqrMagnitude;
                var arrΔv = ArrΔv[i, j] = ΔvForC3(
                    _cbDestination.gravParameter, _cbDestination.sphereOfInfluence, arrC3, _arrivalPeR, _circularize);
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
        public bool IsValid;

        // Departure:
        public double DepartureTime;

        public double DeparturePeriapsis;

        public double DepartureInclination;

        public double DepartureLAN;

        public Vector3d DepartureVInf;

        public Vector3d DeparturePeDirection;

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
        if (tArr < tDep) { return new TransferDetails(); }

        SolveSingleProblem(
            origin,
            destination, tDep, tArr,
            out var depPos,
            out var arrPos,
            out var depCbVel,
            out var arrCbVel,
            out var depVel,
            out var arrVel
        );
        var depPeR = depPeA + origin.Radius;
        var arrPeR = arrPeA + destination.Radius;

        var depVInf = depVel - depCbVel;
        var arrVInf = arrVel - arrCbVel;

        var depC3 = depVInf.sqrMagnitude;
        var arrC3 = arrVInf.sqrMagnitude;

        var depΔv = ΔvForC3(origin.gravParameter, origin.sphereOfInfluence, depC3, depPeR, true);
        var arrΔv = ΔvForC3(destination.gravParameter, destination.sphereOfInfluence, arrC3, arrPeR, circularize);

        // See the comment in SolveSingleProblem
        var arrDepTa = origin.orbit.TrueAnomalyAtUT(tArr);
        var arrDistance = (origin.orbit.getPositionFromTrueAnomaly(arrDepTa, false) - arrPos).magnitude;

        var depAsyRA = Wrap2Pi(Math.Atan2(depVInf.y, depVInf.x));
        var arrAsyRA = Wrap2Pi(Math.Atan2(arrVInf.y, arrVInf.x));

        var depAsyDecl = Math.Asin(depVInf.z / depVInf.magnitude);
        var arrAsyDecl = Math.Asin(arrVInf.z / arrVInf.magnitude);

        double depLAN;
        if (Math.Abs(depAsyDecl) < depInc) { depLAN = depAsyRA - Math.Asin(Math.Tan(depAsyDecl) / Math.Tan(depInc)); }
        else
        {
            depInc = Math.Abs(depAsyDecl);
            depLAN = depAsyRA - 0.5 * Math.PI * Math.Sign(depAsyDecl);
        }
        depLAN = Wrap2Pi(depLAN);

        var depPeDir = PeriapsisDirection(
            origin.gravParameter, origin.sphereOfInfluence, depVInf, depPeR, depInc, depLAN);

        return new TransferDetails
        {
            IsValid = true,
            DepartureTime = tDep,
            DeparturePeriapsis = depPeA,
            DepartureInclination = depInc,
            DepartureLAN = depLAN,
            DepartureVInf = depVInf,
            DeparturePeDirection = depPeDir,
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
        // Unity internally uses a left-handed coordinate system, with the +y axis pointing to the North pole. However,
        // the Orbit class methods return coordinates with the y and z axes flipped; this creates a right-handed
        // coordinate system with the +z axis pointing to the north pole.
        // We need to use the methods taking a true anomaly instead of those taking an UT, because the TrueAnomaly
        // methods have a boolean parameter to skip the WorldToLocal conversion (which rotates the resulting vector away
        // from the Planetarium system to align with the global Unity coordinate system).
        var taDep = origin.orbit.TrueAnomalyAtUT(tDep);
        var taArr = destination.orbit.TrueAnomalyAtUT(tArr);
        origin.orbit.GetOrbitalStateVectorsAtTrueAnomaly(taDep, tDep, false, out depPos, out depCbVel);
        destination.orbit.GetOrbitalStateVectorsAtTrueAnomaly(taArr, tArr, false, out arrPos, out arrCbVel);

        var mu = origin.referenceBody.gravParameter;

        MechJebLib.Maths.Gooding.Solve(
            mu, depPos, depCbVel, arrPos,
            tArr - tDep, 0,
            out depVel, out arrVel);
    }
}
