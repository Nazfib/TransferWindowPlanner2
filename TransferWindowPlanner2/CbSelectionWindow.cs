using UnityEngine;

namespace TransferWindowPlanner2
{
using static GuiUtils;

public class CbSelectionWindow : MonoBehaviour
{
    internal bool IsVisible;
    private Rect _winPos = new Rect(200, 200, 200, 200);
    public CelestialBody SelectedBody { get; private set; } = null!;
    public CelestialBody? CompareBody { get; internal set; } = null;
    private string _title = "CB Selection window";

    public static CbSelectionWindow Setup(MainWindow mainWindow, string title, CelestialBody initialSelection)
    {
        var window = mainWindow.gameObject.AddComponent<CbSelectionWindow>();
        window._title = title;
        window.SelectedBody = initialSelection;
        return window;
    }

    private void WindowGUI(int id)
    {
        var cb = ShowCbSelection();
        GUI.DragWindow();

        if (cb == null) { return; }
        SelectedBody = cb;
        IsVisible = false;
    }

    private CelestialBody? ShowCbSelection()
    {
        using var scope = new GUILayout.VerticalScope();

        foreach (var cb in FlightGlobals.Bodies)
        {
            if (cb.isStar) { continue; }

            if (GUILayout.Button(
                    cb.displayName.LocalizeRemoveGender(),
                    CompareBody == null || MainWindow.ValidCbCombination(cb, CompareBody)
                        ? ButtonStyle
                        : InvalidButtonStyle)) { return cb; }
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
