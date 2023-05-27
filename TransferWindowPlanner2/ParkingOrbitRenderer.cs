using KSP.Localization;
using KSP.UI.Screens.Mapview;

namespace TransferWindowPlanner2
{
public class ParkingOrbitRenderer : OrbitTargetRenderer
{
    public static ParkingOrbitRenderer Setup(
        CelestialBody cb, double alt, double inc, double lan, bool activedraw = true)
    {
        var orbit = new Orbit(inc, 0, cb.Radius + alt, lan, 0, 0, 0, cb);
        return OrbitTargetRenderer.Setup<ParkingOrbitRenderer>("ParkingOrbit", 0, orbit, activedraw);
    }

    protected override void UpdateLocals()
    {
        targetVessel = FlightGlobals.ActiveVessel;
        base.UpdateLocals();
    }

    protected override void ascNode_OnUpdateCaption(MapNode n, MapNode.CaptionData data)
    {
        if (!activeDraw) { return; }
        data.Header = Localizer.Format(
            "#autoLOC_277932", // <<1>>Ascending Node: <<2>>°<<3>>
            startColor, relativeInclination.ToString("0.0"), "</color>");
    }

    protected override void descNode_OnUpdateCaption(MapNode n, MapNode.CaptionData data)
    {
        if (!activeDraw) { return; }
        data.Header = Localizer.Format(
            "#autoLOC_277943", // <<1>>Descending Node: <<2>>°<<3>>
            startColor, (-relativeInclination).ToString("0.0"), "</color>");
    }
}
}
