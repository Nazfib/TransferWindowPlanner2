using System.ComponentModel;
using MechJebLib.Core;
using MechJebLib.Primitives;
using static MechJebLib.Utils.Statics;

namespace TransferWindowPlanner2
{
using static MoreMaths;

public class Solver
{
    private readonly int _nDepartures;
    private readonly int _nArrivals;

    private readonly V3[] _depPos;
    private readonly V3[] _depVel;
    private readonly V3[] _arrPos;
    private readonly V3[] _arrVel;

    internal readonly double[,] DepΔv;
    internal readonly double[,] ArrΔv;
    internal readonly double[,] TotalΔv;

    internal double MinDepΔv, MinArrΔv, MinTotalΔv;
    internal (int, int) MinDepPoint, MinArrPoint, MinTotalPoint;

    private double _earliestDeparture;
    private double _latestDeparture;
    private double _earliestArrival;
    private double _latestArrival;
    private double _departurePeR;
    private double _arrivalPeR;
    private bool _circularize;
    private double _soiDeparture;
    private double _soiArrival;
    private double _gravParameterTransfer;
    private double _gravParameterDeparture;
    private double _gravParameterArrival;

    private readonly BackgroundWorker _backgroundWorker;

    internal enum BackgroundWorkerState
    {
        Idle, Working, Done,
    }

    internal BackgroundWorkerState WorkerState = BackgroundWorkerState.Idle;

    public Solver(int nDepartures, int nArrivals)
    {
        _nDepartures = nDepartures;
        _nArrivals = nArrivals;

        _depPos = new V3[nDepartures];
        _depVel = new V3[nDepartures];
        _arrPos = new V3[nArrivals];
        _arrVel = new V3[nArrivals];
        // 400 * (4*24) = 37.5 kiB

        DepΔv = new double[nDepartures, nArrivals];
        ArrΔv = new double[nDepartures, nArrivals];
        TotalΔv = new double[nDepartures, nArrivals];
        // (400 * 400) * (3*8) = 3.66 MiB

        _backgroundWorker = new BackgroundWorker();
    }

    public void GeneratePorkchop(
        CelestialBody cbOrigin, CelestialBody cbDestination,
        double earliestDeparture, double latestDeparture,
        double earliestArrival, double latestArrival,
        double departureAltitude, double arrivalAltitude, bool circularize)
    {
        _earliestDeparture = earliestDeparture;
        _latestDeparture = latestDeparture;
        _earliestArrival = earliestArrival;
        _latestArrival = latestArrival;
        _departurePeR = cbOrigin.Radius + departureAltitude;
        _arrivalPeR = cbDestination.Radius + arrivalAltitude;
        _circularize = circularize;

        _soiDeparture = cbOrigin.sphereOfInfluence;
        _soiArrival = cbDestination.sphereOfInfluence;
        _gravParameterDeparture = cbOrigin.gravParameter;
        _gravParameterArrival = cbDestination.gravParameter;
        _gravParameterTransfer = cbOrigin.referenceBody.gravParameter;

        for (var i = 0; i < _nDepartures; ++i)
        {
            (_depPos[i], _depVel[i]) = BodyStateVectorsAt(cbOrigin.orbit, DepartureTime(i));
        }
        for (var j = 0; j < _nArrivals; ++j)
        {
            (_arrPos[j], _arrVel[j]) = BodyStateVectorsAt(cbDestination.orbit, ArrivalTime(j));
        }

        WorkerState = BackgroundWorkerState.Working;
        _backgroundWorker.DoWork += (sender, args) => { SolveAllProblems(); };
        _backgroundWorker.RunWorkerCompleted += (sender, args) => { WorkerState = BackgroundWorkerState.Done; };
        _backgroundWorker.RunWorkerAsync();
    }

    private static (V3, V3) BodyStateVectorsAt(Orbit orbit, double time) =>
        Maths.StateVectorsFromKeplerian(
            orbit.referenceBody.gravParameter, orbit.semiLatusRectum, orbit.eccentricity, Deg2Rad(orbit.inclination),
            Deg2Rad(orbit.LAN), Deg2Rad(orbit.argumentOfPeriapsis), orbit.TrueAnomalyAtUT(time));

    internal (double, double) TimesFor((int i, int j) t) => TimesFor(t.i, t.j);

    internal (double, double) TimesFor(int i, int j) => (DepartureTime(i), ArrivalTime(j));

    private double ArrivalTime(int j)
    {
        // Top to bottom -> decreasing arrival time
        var arrStep = (_latestArrival - _earliestArrival) / (_nArrivals - 1);
        var tArr = _latestArrival - j * arrStep;
        return tArr;
    }

    private double DepartureTime(int i)
    {
        // Left to right -> increasing departure time
        var depStep = (_latestDeparture - _earliestDeparture) / (_nDepartures - 1);
        var tDep = _earliestDeparture + i * depStep;
        return tDep;
    }

    private void SolveAllProblems()
    {
        MinDepΔv = MinArrΔv = MinTotalΔv = double.PositiveInfinity;

        for (var i = 0; i < _nDepartures; ++i)
        for (var j = 0; j < _nArrivals; ++j)
        {
            // Left to right -> increasing departure time
            var (tDep, tArr) = TimesFor(i, j);

            var timeOfFlight = tArr - tDep;
            if (timeOfFlight <= 0)
            {
                DepΔv[i, j] = ArrΔv[i, j] = TotalΔv[i, j] = double.NaN;
                continue;
            }

            var depPos = _depPos[i];
            var arrPos = _arrPos[j];
            var depCbVel = _depVel[i];
            var arrCbVel = _arrVel[j];

            var (depVel, arrVel) = Gooding.Solve(_gravParameterTransfer, depPos, depCbVel, arrPos, timeOfFlight, 0);

            var depC3 = (depVel - depCbVel).sqrMagnitude;
            var depΔv = DepΔv[i, j] = ΔvFromC3(_gravParameterDeparture, _soiDeparture, depC3, _departurePeR, true);
            if (depΔv < MinDepΔv)
            {
                MinDepΔv = depΔv;
                MinDepPoint = (i, j);
            }

            var arrC3 = (arrVel - arrCbVel).sqrMagnitude;
            var arrΔv = ArrΔv[i, j] = ΔvFromC3(_gravParameterArrival, _soiArrival, arrC3, _arrivalPeR, _circularize);
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
    }

    public struct TransferDetails
    {
        public bool IsValid;
        public CelestialBody Origin;
        public CelestialBody Destination;

        // Departure:
        public double DepartureTime;

        public double DeparturePeriapsis;

        public double DepartureInclination;

        public double DepartureLAN;

        public V3 DepartureVInf;

        public V3 DeparturePeDirection;

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
        public bool IsShort => TimeOfFlight < 25 * KSPUtil.dateTimeFormatter.Day;

        public string Description() =>
            $@"Transfer: {KSPUtil.PrintDateDelta(TimeOfFlight, IsShort)}
Departure: {KSPUtil.PrintDate(DepartureTime, IsShort)}
    Altitude: {DeparturePeriapsis.ToSI()}m
    Inclination: {Rad2Deg(DepartureInclination):N2} °
    LAN: {Rad2Deg(DepartureLAN):N2} °
    C3: {DepartureC3 / 1e6:F2}km²/s²
    Δv: {DepartureΔv.ToSI()}m/s
Arrival: {KSPUtil.PrintDate(ArrivalTime, IsShort)}
    Altitude:{ArrivalPeriapsis.ToSI()}m
    Distance between bodies: {ArrivalDistance.ToSI()}m
    C3: {ArrivalC3 / 1e6:F2}km²/s²
    Δv: {ArrivalΔv.ToSI()}m/s
Total Δv: {TotalΔv.ToSI()}m/s";
    }

    public static TransferDetails CalculateDetails(
        CelestialBody origin, CelestialBody destination,
        double depPeA, double arrPeA,
        double depMinInc, bool circularize,
        double tDep, double tArr)
    {
        if (tArr <= tDep) { return new TransferDetails { IsValid = false }; }

        var (depPos, depCbVel) = BodyStateVectorsAt(origin.orbit, tDep);
        var (arrPos, arrCbVel) = BodyStateVectorsAt(destination.orbit, tArr);
        var timeOfFlight = tArr - tDep;
        var (depVel, arrVel) = Gooding.Solve(
            origin.referenceBody.gravParameter, depPos, depCbVel, arrPos, timeOfFlight, 0);
        var depPeR = depPeA + origin.Radius;
        var arrPeR = arrPeA + destination.Radius;

        var depVInf = depVel - depCbVel;
        var arrVInf = arrVel - arrCbVel;

        var depC3 = depVInf.sqrMagnitude;
        var arrC3 = arrVInf.sqrMagnitude;

        var depΔv = ΔvFromC3(origin.gravParameter, origin.sphereOfInfluence, depC3, depPeR, true);
        var arrΔv = ΔvFromC3(destination.gravParameter, destination.sphereOfInfluence, arrC3, arrPeR, circularize);
        if (double.IsNaN(depΔv) || double.IsNaN(arrΔv)) { return new TransferDetails { IsValid = false }; }

        var (originPosAtArrival, _) = BodyStateVectorsAt(origin.orbit, tArr);
        var arrDistance = (originPosAtArrival - arrPos).magnitude;

        var depAsySpherical = depVInf.cart2sph;
        var depAsyDecl = 0.5 * PI - depAsySpherical[1];
        var depAsyRA = depAsySpherical[2];

        var arrAsySpherical = arrVInf.cart2sph;
        var arrAsyDecl = 0.5 * PI - arrAsySpherical[1];
        var arrAsyRA = arrAsySpherical[2];

        var (depInc, depLAN) = LANAndIncForAsymptote(depMinInc, depAsyDecl, depAsyRA);

        var depPeDir = PeriapsisDirection(origin.gravParameter, depVInf, depPeR, depInc, depLAN);

        return new TransferDetails
        {
            IsValid = true,
            Origin = origin,
            Destination = destination,
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
            TimeOfFlight = timeOfFlight,
            TotalΔv = depΔv + arrΔv,
        };
    }
}
}
