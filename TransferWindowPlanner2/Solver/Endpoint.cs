using System;

namespace TransferWindowPlanner2.Solver
{
public readonly struct Endpoint
    : IEquatable<Endpoint>
{
    public Orbit Orbit => Celestial != null
        ? Celestial.orbit
        : Vessel != null
            ? Vessel.orbit
            : throw new InvalidOperationException("Both Cb and Vessel are null");

    public readonly CelestialBody? Celestial;
    public readonly Vessel? Vessel;

    public bool IsCelestial => Celestial != null;
    public bool IsVessel => Vessel != null;

    public bool IsNull => Celestial == null && Vessel == null;

    public string Name => Celestial != null
        ? Celestial.displayName.LocalizeRemoveGender()
        : Vessel != null
            ? Vessel.GetDisplayName().LocalizeRemoveGender()
            : "<null>";

    public Endpoint(CelestialBody celestial)
    {
        Celestial = celestial;
        Vessel = null;
    }

    public Endpoint(Vessel v)
    {
        Celestial = null;
        Vessel = v;
    }


    public bool Equals(Endpoint other) => Equals(Celestial, other.Celestial) && Equals(Vessel, other.Vessel);

    public override bool Equals(object? obj) => obj is Endpoint other && Equals(other);

    public override int GetHashCode()
    {
        unchecked
        {
            return (Celestial != null ? Celestial.GetHashCode() : 0) * 397 ^
                   (Vessel != null ? Vessel.GetHashCode() : 0);
        }
    }

    public static bool operator ==(Endpoint left, Endpoint right) => left.Equals(right);

    public static bool operator !=(Endpoint left, Endpoint right) => !left.Equals(right);
}
}
