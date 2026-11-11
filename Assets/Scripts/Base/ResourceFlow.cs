using System;
using UnityEngine;

/// <summary>
/// The running cost of the base: electricity and money, hour by hour of game time. Every hour the buildings use power (it varies a little around the
/// average) and the base pays its upkeep. When the load is higher than the Power Grid can make, or the upkeep cannot be paid, the power is short:
/// construction, upgrades and testing stop until the next hour (the Power Grid itself can still be upgraded).
/// </summary>
public class ResourceFlow : MonoBehaviour
{
    public static ResourceFlow Instance { get; private set; }
    public static void ResetStatics() { Instance = null; }

    /// <summary>Upkeep in $M per day for each MW the buildings draw, and for the grid itself (by grid level).</summary>
    public const float MoneyPerMWDay = 0.1f;
    static readonly float[] GridUpkeepM = { 2f, 5f, 10f };

    public static bool PowerShort => Instance != null && Instance.shortNow;
    public static float LoadFactor => Instance != null ? Instance.load : 1f;

    /// <summary>The average cost of running the base, $M per game day.</summary>
    public static float DailyCostM
    {
        get
        {
            var sim = BaseSim.Instance; if (sim == null) return 0f;
            return sim.PowerDemand * MoneyPerMWDay + GridUpkeepM[Mathf.Clamp(sim.PowerLevel, 1, GridUpkeepM.Length) - 1];
        }
    }

    public static float GridUpkeepOf(int level) => GridUpkeepM[Mathf.Clamp(level, 1, GridUpkeepM.Length) - 1];

    /// <summary>One hour (or several, when the game runs fast) of use: MWh, $M, and how the load changed since the hour before.</summary>
    public event Action<float, float, float, int> HourUsed;

    long lastHour = long.MinValue;
    float load = 1f, lastDemand = -1f;
    bool shortNow, warned;

    void Awake() => Instance = this;
    void OnDestroy() { if (Instance == this) Instance = null; }

    void Update()
    {
        var clock = GameClock.Instance; var sim = BaseSim.Instance;
        if (clock == null || sim == null) return;
        long h = clock.Now.Ticks / TimeSpan.TicksPerHour;
        if (lastHour == long.MinValue) { lastHour = h; return; }
        if (h == lastHour) return;
        int hours = (int)Mathf.Min(48, h - lastHour);
        lastHour = h;
        float mwh = 0f, money = 0f, delta = 0f;
        for (int i = 0; i < hours; i++) Tick(sim, ref mwh, ref money, ref delta);
        HourUsed?.Invoke(mwh, money, delta, hours);
    }

    void Tick(BaseSim sim, ref float mwh, ref float money, ref float delta)
    {
        float demand = sim.PowerDemand, supply = sim.PowerSupply;
        // the load wanders a little around the average (some hours more, some less)
        load = Mathf.Clamp(Mathf.Lerp(load, 1f, 0.4f) + UnityEngine.Random.Range(-0.08f, 0.08f), 0.86f, 1.14f);
        float use = demand * load;
        float costM = DailyCostM / 24f;
        bool paid = Agency.TrySpend((long)(costM * 1_000_000f));
        if (!paid) Agency.TrySpend(Agency.Money);                              // the last of the money goes on the bill
        bool wasShort = shortNow;
        shortNow = use > supply + 0.01f || !paid;
        if (shortNow && !wasShort) Hud.Toast(paid ? "Power load is too high: work has stopped. Upgrade the Power Grid." : "The power bill could not be paid: work has stopped.");
        mwh += use; money += costM;
        if (lastDemand >= 0f) delta += demand - lastDemand;
        lastDemand = demand;
    }

    public float PowerUse => (BaseSim.Instance != null ? BaseSim.Instance.PowerDemand : 0f) * load;
}
