using System;

namespace TransferWindowPlanner2
{
public readonly struct Endpoint
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

    public string Name => Celestial != null
        ? Celestial.displayName.LocalizeRemoveGender()
        : Vessel != null
            ? Vessel.GetDisplayName().LocalizeRemoveGender()
            : throw new InvalidOperationException("Both Cb and Vessel are null");

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


    public bool Equals(Endpoint other) =>
        Orbit.Equals(other.Orbit) && Equals(Celestial, other.Celestial) && Equals(Vessel, other.Vessel);

    public override bool Equals(object? obj) => obj is Endpoint other && Equals(other);

    public override int GetHashCode()
    {
        unchecked
        {
            var hashCode = Orbit.GetHashCode();
            hashCode = hashCode * 397 ^ Name.GetHashCode();
            hashCode = hashCode * 397 ^ (Celestial != null ? Celestial.GetHashCode() : 0);
            return hashCode;
        }
    }
}
}
