using System;
using UnityEngine;

namespace TransferWindowPlanner2
{
using static GuiUtils;

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

public class CbSelectionWindow : MonoBehaviour
{
    internal bool IsVisible;
    private Rect _winPos = new Rect(200, 200, 200, 50);
    private bool _showVessels = false;
    public Endpoint SelectedBody { get; private set; }
    public CelestialBody? CentralBody { get; internal set; } = null;
    private string _title = "CB Selection window";

    public static CbSelectionWindow Setup(MainWindow mainWindow, string title, Endpoint initialSelection)
    {
        var window = mainWindow.gameObject.AddComponent<CbSelectionWindow>();
        window._title = title;
        window.SelectedBody = initialSelection;
        return window;
    }

    private void WindowGUI(int id)
    {
        var endpoint = ShowSelection();
        GUI.DragWindow();

        if (endpoint == null) { return; }
        SelectedBody = endpoint.Value;
        IsVisible = false;
    }

    private Endpoint? ShowSelection()
    {
        using var scope = new GUILayout.VerticalScope();

        if (CentralBody != null) { _showVessels = GUILayout.Toggle(_showVessels, "Show vessels"); }

        if (_showVessels)
        {
            foreach (var v in FlightGlobals.Vessels)
            {
                if (v.LandedOrSplashed) { continue; }
                if (v.orbit.referenceBody != CentralBody!) { continue; }
                if (GUILayout.Button(v.GetDisplayName().LocalizeRemoveGender(), ButtonStyle))
                {
                    return new Endpoint(v);
                }
            }
        }
        else
        {
            foreach (var cb in FlightGlobals.Bodies)
            {
                if (CentralBody != null && cb == CentralBody) { continue; }

                if (CentralBody != null && cb.referenceBody != CentralBody) { continue; }

                if (GUILayout.Button(cb.displayName.LocalizeRemoveGender(), ButtonStyle)) { return new Endpoint(cb); }
            }
        }

        return null;
    }

    public void OnGUI()
    {
        GUI.skin = HighLogic.Skin;
        if (IsVisible) { _winPos = GUILayout.Window(GetHashCode(), _winPos, WindowGUI, _title); }
    }
}
}
