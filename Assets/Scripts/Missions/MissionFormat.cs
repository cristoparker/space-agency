using System.Globalization;
using System.Text;

/// <summary>Turns mission data into display text. Change formatting here only.</summary>
public static class MissionFormat
{
    static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    public static string Money(long value) => "$" + value.ToString("N0", Inv);
    public static string Mass(float kg) => kg.ToString("N0", Inv) + " kg";

    public static string Details(MissionDefinition m)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"<color=#1565C0>Type:</color> {m.missionType}");
        sb.AppendLine($"<color=#1565C0>Target:</color> {m.target}");
        sb.AppendLine($"<color=#1565C0>Budget:</color> {Money(m.budget)}");
        sb.AppendLine($"<color=#1565C0>Mass limit:</color> {Mass(m.massLimit)}");
        sb.AppendLine($"<color=#1565C0>Payload:</color> {m.payload}");
        sb.AppendLine($"<color=#1565C0>Duration:</color> {m.duration}");
        sb.AppendLine();
        sb.AppendLine("<color=#1565C0>Objective:</color>");
        sb.AppendLine(m.objective);
        if (m.specialRequirements != null && m.specialRequirements.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("<color=#1565C0>Special requirements:</color>");
            foreach (var r in m.specialRequirements) sb.AppendLine("- " + r.ToDisplayString());
        }
        return sb.ToString().TrimEnd();
    }
}
