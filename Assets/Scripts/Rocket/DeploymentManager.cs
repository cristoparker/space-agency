using System;
using System.Globalization;
using UnityEngine;

/// <summary>
/// Owns the one rocket standing on the launch pad: shows it in the scene, remembers its launch date and contract,
/// and tells the calendar / mission list. Add it once to the Game scene (Tools / Space Agency / Setup Rocket Builder does).
/// </summary>
public class DeploymentManager : MonoBehaviour
{
    public static DeploymentManager Instance { get; private set; }
    const string Key = "SpaceAgency.Deployment";

    public PartLibrary library;
    [Tooltip("Old placeholder rocket on the pad; hidden while a deployed rocket stands there.")]
    public GameObject placeholder;
    public Vector3 rocketPosition = new Vector3(8f, 5.26f, 0f);

    public Deployment Current { get; private set; }
    public event Action Changed;
    GameObject padRocket;

    void Awake() => Instance = this;
    void OnDestroy() { if (Instance == this) Instance = null; }

    void Start()
    {
        try { var d = JsonUtility.FromJson<Deployment>(PlayerPrefs.GetString(Key, "")); if (d != null && !string.IsNullOrEmpty(d.design)) Current = d; }
        catch { /* nothing saved */ }
        Show();
        Changed?.Invoke();
    }

    public DateTime? LaunchDate
    {
        get
        {
            if (Current == null || !DateTime.TryParseExact(Current.date, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d)) return null;
            return d;
        }
    }

    public bool IsScheduledFor(DateTime day) => LaunchDate.HasValue && LaunchDate.Value.Date == day.Date;
    public bool IsMissionDeployed(string missionId) => Current != null && !string.IsNullOrEmpty(missionId) && Current.missionId == missionId;

    public void Schedule(string rocketName, RocketDesign design, MissionDefinition mission, DateTime date)
    {
        Current = new Deployment
        {
            rocketName = rocketName, design = design.ToJson(), missionId = mission != null ? mission.id : "",
            missionName = mission != null ? mission.displayName : "", date = date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        };
        PlayerPrefs.SetString(Key, JsonUtility.ToJson(Current));
        Show();
        Changed?.Invoke();
    }

    public void Cancel()
    {
        Current = null;
        PlayerPrefs.DeleteKey(Key);
        Show();
        Changed?.Invoke();
    }

    void Show()
    {
        if (padRocket != null) Destroy(padRocket);
        padRocket = null;
        if (placeholder != null) placeholder.SetActive(Current == null);
        if (Current == null || library == null) return;
        var design = RocketDesign.FromJson(Current.design);
        design.stack.RemoveAll(id => library.Get(id) == null);
        padRocket = RocketAssembler.Build(design, library, out _);
        padRocket.name = "Deployed Rocket (" + Current.rocketName + ")";
        padRocket.transform.SetParent(transform, false);
        padRocket.transform.position = rocketPosition;
    }
}
