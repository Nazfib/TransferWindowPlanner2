using System;
using MechJebLib.Core;
using MechJebLib.Primitives;
using MechJebLib.Utils;
using static MechJebLib.Utils.Statics;

namespace TransferWindowPlanner2.Solver
{
using static MoreMaths;

public partial class Solver : BackgroundJob<int>
{
    private Endpoint _origin;
    private Endpoint _destination;

    private readonly int _nDepartures;
    private readonly int _nArrivals;
    private bool _hasPrincipia;

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
    private double _departureMinInc;
    private double _arrivalPeR;
    private bool _circularize;
    private double _soiDeparture;
    private double _soiArrival;
    private double _gravParameterTransfer;
    private double _gravParameterDeparture;
    private double _gravParameterArrival;

    public Solver(int nDepartures, int nArrivals, bool hasPrincipia)
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

        _hasPrincipia = hasPrincipia;
    }

    public void GeneratePorkchop(
        Endpoint origin, Endpoint destination,
        double earliestDeparture, double latestDeparture,
        double earliestArrival, double latestArrival,
        double departureAltitude, double departureMinInclination,
        double arrivalAltitude, bool circularize)
    {
        _origin = origin;
        _destination = destination;

        _earliestDeparture = earliestDeparture;
        _latestDeparture = latestDeparture;
        _earliestArrival = earliestArrival;
        _latestArrival = latestArrival;

        if (origin.IsCelestial)
        {
            _departurePeR = origin.Celestial!.Radius + departureAltitude;
            _departureMinInc = departureMinInclination;
            _soiDeparture = origin.Celestial!.sphereOfInfluence;
            _gravParameterDeparture = origin.Celestial!.gravParameter;
        }
        else { _departurePeR = _soiDeparture = _gravParameterDeparture = 0.0; }
        if (destination.IsCelestial)
        {
            _arrivalPeR = destination.Celestial!.Radius + arrivalAltitude;
            _circularize = circularize;
            _soiArrival = destination.Celestial!.sphereOfInfluence;
            _gravParameterArrival = destination.Celestial!.gravParameter;
        }
        else { _arrivalPeR = _soiArrival = _gravParameterArrival = 0.0; }

        _gravParameterTransfer = origin.Orbit.referenceBody.gravParameter;

        for (var i = 0; i < _nDepartures; ++i)
        {
            (_depPos[i], _depVel[i]) = BodyStateVectorsAt(origin, DepartureTime(i));
        }
        for (var j = 0; j < _nArrivals; ++j)
        {
            (_arrPos[j], _arrVel[j]) = BodyStateVectorsAt(destination, ArrivalTime(j));
        }

        StartJob(null);
    }

    private (V3, V3) BodyStateVectorsAt(Endpoint body, double time)
    {
        var orbit = body.Orbit;
        var mu = orbit.referenceBody.gravParameter;
        if (_hasPrincipia)
        {
            // For increased accuracy with Principia: the 'orbit' of a CB is the osculating orbit at the time when the
            // plot is generated. This is inaccurate for a number of reasons (third-body perturbations mostly), but the
            // most significant is in many cases the fact that stock only takes the μ of the parent body into account;
            // when Principia is installed, we want to make sure we use the μ of both bodies.

            // FIXME: look into Principia's own API for getting the position of a CB.

            // ReSharper disable once Unity.NoNullPropagation
            mu += body.Celestial?.gravParameter ?? 0.0;
        }

        return Maths.StateVectorsFromKeplerian(
            mu, orbit.semiLatusRectum, orbit.eccentricity, Deg2Rad(orbit.inclination),
            Deg2Rad(orbit.LAN), Deg2Rad(orbit.argumentOfPeriapsis), orbit.TrueAnomalyAtUT(time));
    }

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
            var depΔv = DepΔv[i, j] = _gravParameterDeparture > 0.0
                ? ΔvFromC3(_gravParameterDeparture, _soiDeparture, depC3, _departurePeR, true)
                : Math.Sqrt(depC3);
            if (depΔv < MinDepΔv)
            {
                MinDepΔv = depΔv;
                MinDepPoint = (i, j);
            }

            var arrC3 = (arrVel - arrCbVel).sqrMagnitude;
            var arrΔv = ArrΔv[i, j] = _gravParameterArrival > 0.0
                ? ΔvFromC3(_gravParameterArrival, _soiArrival, arrC3, _arrivalPeR, _circularize)
                : Math.Sqrt(arrC3);
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

    protected override int Run(object? o)
    {
        SolveAllProblems();
        return 0;
    }
}
}
