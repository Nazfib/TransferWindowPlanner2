using KSP.UI;
using KSP.UI.Screens;

namespace TransferWindowPlanner2.UI
{
public class TWPAlarm : AlarmTypeBase
{
    [AppUI_InputDateTime(guiName = "Alarm margin", datetimeMode = AppUIMemberDateTime.DateTimeModes.timespan)]
    public double Margin = 24 * 3600;

    public TWPAlarm()
    {
        // Needed to not break the alarm clock window. Because `CanSetAlarm` always returns false, it is never actually
        // called.
        iconURL = "xfer";
    }

    public TWPAlarm(Solver.TransferDetails transfer, double margin)
    {
        ut = transfer.DepartureTime - margin;
        eventOffset = Margin = margin;
        iconURL = "xfer";
        actions.warp = AlarmActions.WarpEnum.KillWarp;
        title = string.Format(
            "{0} -> {1} ({2})",
            transfer.Origin.Name,
            transfer.Destination.Name,
            KSPUtil.PrintDateDelta(transfer.TimeOfFlight, transfer.IsShort));
        description = transfer.Description();
    }

    public override string GetDefaultTitle() => "TWP Alarm";

    public override bool RequiresVessel() => false;

    public override bool CanSetAlarm(AlarmUIDisplayMode displayMode) => displayMode is AlarmUIDisplayMode.Edit;

    public override string CannotSetAlarmText() => "Use TWP to create an alarm";

    public override void OnInputPanelUpdate(AlarmUIDisplayMode displayMode)
    {
        ut = ut + eventOffset - Margin;
        eventOffset = Margin;
    }
}
}
