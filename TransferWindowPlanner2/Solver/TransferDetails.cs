using MechJebLib.Primitives;
using MechJebLib.Utils;
using static System.Math;
using MechJebLib.Lambert;
using static MechJebLib.Utils.Statics;

namespace TransferWindowPlanner2.Solver
{
using static MoreMaths;

public struct TransferDetails
{
    public bool IsValid;
    public Endpoint Origin;
    public Endpoint Destination;

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


public partial class Solver
{
    public TransferDetails CalculateDetails(double tDep, double tArr)
    {
        if (tArr <= tDep) { return new TransferDetails { IsValid = false }; }

        var (depPos, depCbVel) = BodyStateVectorsAt(_origin, tDep);
        var (arrPos, arrCbVel) = BodyStateVectorsAt(_destination, tArr);
        var timeOfFlight = tArr - tDep;
        var (depVel, arrVel) = Gooding.Solve(
            _origin.Orbit.referenceBody.gravParameter, depPos, depCbVel, arrPos, timeOfFlight, 0);

        var depVInf = depVel - depCbVel;
        var arrVInf = arrVel - arrCbVel;

        var depC3 = depVInf.sqrMagnitude;
        var arrC3 = arrVInf.sqrMagnitude;

        var depΔv = _origin.IsCelestial
            ? ΔvFromC3(
                _origin.Celestial!.gravParameter, _origin.Celestial!.sphereOfInfluence, depC3, _departurePeR, true)
            : Sqrt(depC3);
        var arrΔv = _destination.IsCelestial
            ? ΔvFromC3(
                _destination.Celestial!.gravParameter, _destination.Celestial!.sphereOfInfluence, arrC3, _arrivalPeR,
                _circularize)
            : Sqrt(arrC3);
        if (double.IsNaN(depΔv) || double.IsNaN(arrΔv)) { return new TransferDetails { IsValid = false }; }

        var (originPosAtArrival, _) = BodyStateVectorsAt(_origin, tArr);
        var arrDistance = (originPosAtArrival - arrPos).magnitude;

        var depAsySpherical = depVInf.cart2sph;
        var depAsyDecl = 0.25 * TAU - depAsySpherical[1];
        var depAsyRA = depAsySpherical[2];

        var arrAsySpherical = arrVInf.cart2sph;
        var arrAsyDecl = 0.25 * TAU - arrAsySpherical[1];
        var arrAsyRA = arrAsySpherical[2];

        var (depInc, depLAN) = LANAndIncForAsymptote(_departureMinInc, depAsyDecl, depAsyRA);

        var depPeDir = _origin.IsCelestial
            ? PeriapsisDirection(_origin.Celestial!.gravParameter, depVInf, _departurePeR, depInc, depLAN)
            : V3.zero;

        return new TransferDetails
        {
            IsValid = true,
            Origin = _origin,
            Destination = _destination,
            DepartureTime = tDep,
            DeparturePeriapsis = _origin.IsCelestial ? _departurePeR - _origin.Celestial!.Radius : 0.0,
            DepartureInclination = depInc,
            DepartureLAN = depLAN,
            DepartureVInf = depVInf,
            DeparturePeDirection = depPeDir,
            DepartureAsyRA = depAsyRA,
            DepartureAsyDecl = depAsyDecl,
            DepartureC3 = depC3,
            DepartureΔv = depΔv,
            ArrivalTime = tArr,
            ArrivalPeriapsis = _destination.IsCelestial ? _arrivalPeR - _destination.Celestial!.Radius : 0.0,
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
