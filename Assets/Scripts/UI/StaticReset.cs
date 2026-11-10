using UnityEngine;

/// <summary>
/// With "Enter Play Mode Options" the domain is not reloaded between runs, so static fields keep their values (a plot picker that was
/// open when Play stopped would stay "active" and block the map). Everything static that holds UI state is reset when Play starts.
/// </summary>
static class StaticReset
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reset()
    {
        PlotPicker.ResetStatics();
        LaunchSequence.ResetStatics();
        VisualSettings.ResetStatics();
        CinematicCamera.ResetStatics();
        LaunchResultUI.ResetStatics();
        FlightManager.ResetStatics();
        UIConfirm.ResetStatics();
        FacilityPanelUI.ResetStatics();
        FacilityPreview.ResetStatics();
        BuildingSelector.LabelsVisible = true;
        DayNightCycle.HazeEnabled = true;
    }
}
