using Contracts;

namespace TransferWindowPlanner2
{
public class ParkingOrbitRendererHack
{
    // Ugly hack: Creating a new class derived from OrbitTargetRenderer does not work - the orbit lags behind the camera
    // movement when panning. Therefore, we need to use one of the built-in classes designed for rendering orbits: those
    // inheriting from OrbitTargetRenderer. Only the ContractOrbitRenderer is available without the DLC.
    private readonly ContractOrbitRenderer _renderer;

    private ParkingOrbitRendererHack(ContractOrbitRenderer renderer)
    {
        _renderer = renderer;
    }

    public static ParkingOrbitRendererHack Setup(
        CelestialBody cb, double alt, double inc, double lan, bool activedraw = true)
    {
        // The ContractOrbitRenderer.Setup method requires a non-null contract; we provide a default-initialized one,
        // because anything else is really annoying to setup.
        // However, the *_onUpdateCaption methods don't work with a default-initialized Contract: they need a valid
        // Agent, which we can't provide (it's a protected field of the Contract class).
        // So, the full workaround is this: provide a default-initialized Contract to the Setup method, then immediately
        // set it to null before the caption update methods can make use of it.
        var orbit = new Orbit(inc, 0, cb.Radius + alt, lan, 0, 0, 0, cb);
        var renderer = ContractOrbitRenderer.Setup(new Contract(), orbit, activedraw);
        renderer.contract = null;
        return new ParkingOrbitRendererHack(renderer);
    }

    public void Cleanup()
    {
        _renderer.Cleanup();
    }
}
}
