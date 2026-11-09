using System;
using System.Globalization;
using UnityEngine;

public enum DurationUnit { Days, Months, Years }

/// <summary>How a running mission's progress is driven.</summary>
public enum ProgressMode
{
    /// <summary>Progress follows the game clock (elapsed / duration). Respects fast-forward.</summary>
    TimeBased,
    /// <summary>Progress is only changed by other systems via MissionManager.SetProgress.</summary>
    Manual
}

[Serializable]
public class MissionPayload
{
    public string type;
    public int quantity = 1;
    [TextArea] public string description;

    public override string ToString()
    {
        string s = $"{type} x{quantity}";
        return string.IsNullOrEmpty(description) ? s : s + "\n" + description;
    }
}

[Serializable]
public struct MissionDuration
{
    public float value;
    public DurationUnit unit;

    public double ToDays()
    {
        switch (unit)
        {
            case DurationUnit.Years: return value * 365.0;
            case DurationUnit.Months: return value * 30.0;
            default: return value;
        }
    }

    public override string ToString()
    {
        string u = unit.ToString().ToLowerInvariant();
        if (Mathf.Approximately(value, 1f)) u = u.TrimEnd('s');
        return value.ToString("0.##", CultureInfo.InvariantCulture) + " " + u;
    }
}

[Serializable]
public class MissionRequirement
{
    public string type;
    [Tooltip("Text value, e.g. 'Soft Landing'. Leave empty for numeric requirements.")]
    public string value;
    public bool hasMinimum;
    public float minimum;
    public string unit;

    public string ToDisplayString()
    {
        if (!string.IsNullOrEmpty(value)) return $"{type}: {value}";
        if (hasMinimum) return $"{type}: min {minimum.ToString("0.##", CultureInfo.InvariantCulture)} {unit}".TrimEnd();
        return type;
    }
}
