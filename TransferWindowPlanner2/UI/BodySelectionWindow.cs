using System;
using System.Collections.Generic;
using ClickThroughFix;
using TransferWindowPlanner2.Solver;
using UnityEngine;

namespace TransferWindowPlanner2.UI
{
using static GuiUtils;

public class BodySelectionWindow : MonoBehaviour
{
    private MainWindow _mainWindow = null!;
    private const int Width = 200;

    public enum SelectionKind
    {
        None,
        Central,
        Departure,
        Arrival,
    }

    private SelectionKind _which = SelectionKind.None;

    public SelectionKind Which
    {
        get => _which;
        internal set
        {
            if (_which == value) { return; }
            _which = value;
            if (value is SelectionKind.Central) { _showVessels = false; }
            _isDirty = true;
        }
    }

    private bool _showVessels = false;

    public bool ShowVessels
    {
        get => _showVessels;
        set
        {
            if (_showVessels == value) { return; }
            _showVessels = value;
            _isDirty = true;
        }
    }

    private bool _isDirty = false;
    private readonly List<Endpoint> _endpoints = new List<Endpoint>();
    private Vector2 _scrollPos;

    public static BodySelectionWindow Setup(MainWindow mainWindow)
    {
        var window = mainWindow.gameObject.AddComponent<BodySelectionWindow>();
        window._mainWindow = mainWindow;
        return window;
    }

    private void WindowGUI(int id)
    {
        using var scope = new GUILayout.VerticalScope();

        using (new GuiEnabled(Which is SelectionKind.Arrival))
        {
            ShowVessels = GUILayout.Toggle(ShowVessels, "Show vessels");
        }

        var endpoint = ShowSelection();

        if (endpoint.HasValue)
        {
            SetEndpoint(endpoint.Value);
            Which = SelectionKind.None;
        }
    }

    private void RefreshList()
    {
        _isDirty = false;
        if (Which is SelectionKind.None) { return; }
        _endpoints.Clear();

        if (Which is SelectionKind.Central)
        {
            foreach (var cb in FlightGlobals.Bodies) { _endpoints.Add(new Endpoint(cb)); }
            return;
        }

        var central = _mainWindow.CentralBody;
        foreach (var cb in FlightGlobals.Bodies)
        {
            if (cb == central) { continue; }
            if (central != null && cb.referenceBody != central) { continue; }
            _endpoints.Add(new Endpoint(cb));
        }

        if (_showVessels)
        {
            foreach (var v in FlightGlobals.Vessels)
            {
                if (v.LandedOrSplashed) { continue; }
                if (v.orbit.referenceBody != central) { continue; }
                _endpoints.Add(new Endpoint(v));
            }
        }
    }

    private void Update()
    {
        if (_isDirty) { RefreshList(); }
    }

    private bool EndpointIsValid(Endpoint ep)
    {
        return Which switch
        {
            SelectionKind.None => true,
            SelectionKind.Central => ep.IsCelestial,
            SelectionKind.Departure => ep != _mainWindow.ArrivalBody,
            SelectionKind.Arrival => ep != _mainWindow.DepartureBody,
            _ => throw new ArgumentOutOfRangeException(),
        };
    }

    private Endpoint? ShowSelection()
    {
        using var scroll = new GUILayout.ScrollViewScope(_scrollPos, false, true);
        _scrollPos = scroll.scrollPosition;

        using var scope = new GUILayout.VerticalScope();

        foreach (var ep in _endpoints)
        {
            if (GUILayout.Button(ep.Name, EndpointIsValid(ep) ? ButtonStyle : InvalidButtonStyle)) { return ep; }
        }
        return null;
    }

    private void SetEndpoint(Endpoint ep)
    {
        switch (Which)
        {
            case SelectionKind.None:
                return;
            case SelectionKind.Central:
                if (ep.Celestial == null) { throw new Exception("Can't set central body to a vessel"); }
                _mainWindow.CentralBody = ep.Celestial;
                break;
            case SelectionKind.Departure:
                _mainWindow.DepartureBody = ep;
                break;
            case SelectionKind.Arrival:
                _mainWindow.ArrivalBody = ep;
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    private string GetTitle()
    {
        return Which switch
        {
            SelectionKind.None => "",
            SelectionKind.Central => "Central body",
            SelectionKind.Departure => "Origin body",
            SelectionKind.Arrival => "Destination body",
            _ => throw new ArgumentOutOfRangeException(),
        };
    }

    public void OnGUI()
    {
        GUI.skin = HighLogic.Skin;
        if (Which is SelectionKind.None) { return; }

        var winPos = new Rect(
            _mainWindow.WinPos.x - Width - 10,
            _mainWindow.WinPos.y,
            Width,
            _mainWindow.WinPos.height
        );
        ClickThruBlocker.GUILayoutWindow(
            GetHashCode(), winPos, WindowGUI, GetTitle(),
            GUILayout.ExpandWidth(false), GUILayout.ExpandHeight(false));
    }
}
}
