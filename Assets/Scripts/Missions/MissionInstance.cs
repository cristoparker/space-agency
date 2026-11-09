using System;
using UnityEngine;

public enum MissionState { Running, Completed }

/// <summary>Runtime state of a started mission. Progress is a plain 0..1 value anything can update.</summary>
public class MissionInstance
{
    public MissionDefinition Definition { get; }
    public MissionState State { get; internal set; } = MissionState.Running;
    public double StartGameTime { get; }
    public DateTime StartDate { get; }

    /// <summary>0..1</summary>
    public float Progress { get; private set; }
    public int Percent => Mathf.Clamp(Mathf.FloorToInt(Progress * 100f), 0, 100);

    public MissionInstance(MissionDefinition definition, double startGameTime, DateTime startDate)
    {
        Definition = definition;
        StartGameTime = startGameTime;
        StartDate = startDate;
    }

    public void SetProgress(float value) => Progress = Mathf.Clamp01(value);
}
