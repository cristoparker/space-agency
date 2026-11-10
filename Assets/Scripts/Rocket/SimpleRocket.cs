using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// The simple rocket builder. The player only picks one thing per category (plus how many tanks / boosters / fins) and
/// <see cref="SimpleBuilder"/> turns that into the full part stack the rest of the game already understands.
/// </summary>
[Serializable]
public class SimpleBuild
{
    public string headId, payloadId;                 // 1. payload: the head (capacity) and the cargo in it
    public string customPayload;                     //    name of a cargo built in the Payload Builder (replaces payloadId)
    public string tankId; public int tankCount = 1;  // 2. fuel tanks (body)
    public string engineId;                          // 3. engine
    public string upperEngineId;                     //    optional upper-stage engine
    public string upperTankId; public int upperTankCount = 1;
    public string boosterId; public int boosterCount = 2;   // 4. outer layer
    public string finId; public int finCount = 4;
    public List<string> extras = new List<string>(); // 5. parachute, comms, power, RCS, legs: no visual
}

/// <summary>A cargo the player built in the Payload Builder: a base payload plus the power and comms fitted around it.</summary>
[Serializable]
public class CustomPayload
{
    public string name, payloadId;
    public List<string> equipment = new List<string>();
    public float massKg, costM;
}

[Serializable] class CustomPayloadList { public List<CustomPayload> items = new List<CustomPayload>(); }

public static class CustomPayloads
{
    const string Key = "SpaceAgency.CustomPayloads";
    public static List<CustomPayload> All()
    {
        try { return JsonUtility.FromJson<CustomPayloadList>(PlayerPrefs.GetString(Key, "{}"))?.items ?? new List<CustomPayload>(); }
        catch { return new List<CustomPayload>(); }
    }
    static void Write(List<CustomPayload> l) => PlayerPrefs.SetString(Key, JsonUtility.ToJson(new CustomPayloadList { items = l }));
    public static CustomPayload Get(string name) => All().FirstOrDefault(x => x.name == name);
    public static void Upsert(CustomPayload c)
    {
        var l = All(); int i = l.FindIndex(x => x.name == c.name);
        if (i >= 0) l[i] = c; else l.Add(c);
        Write(l);
    }
    public static void Delete(string name) { var l = All(); l.RemoveAll(x => x.name == name); Write(l); }
}

public static class SimpleBuilder
{
    public const int MaxTanks = 4, MaxExtras = 8, MaxOuter = 6;

    /// <summary>Regenerates the stack, boosters, fins, payload and equipment of the design from its simple build.</summary>
    public static void Apply(RocketDesign d, PartLibrary lib)
    {
        var s = d.simple ?? (d.simple = new SimpleBuild());
        var stack = new List<string>();
        string TopCls() => stack.Count > 0 ? lib.Get(stack[stack.Count - 1])?.TopClass : null;
        void Adapt(string toCls)
        {
            string top = TopCls();
            if (top == null || toCls == null || top == toCls) return;
            var a = lib.Get("Adp_" + top + "_" + toCls);
            if (a != null) stack.Add(a.id);                      // S <-> L has no adapter: the checks report the size mismatch
        }
        void Tanks(PartInfo t, int n) { Adapt(t.BottomClass); for (int i = 0; i < Mathf.Clamp(n, 1, MaxTanks); i++) stack.Add(t.id); }

        var eng = lib.Get(s.engineId);
        var tank = lib.Get(s.tankId);
        if (eng != null) stack.Add(eng.id);
        if (tank != null) Tanks(tank, s.tankCount);

        var upE = lib.Get(s.upperEngineId);
        if (upE != null && stack.Count > 0)
        {
            string top = TopCls();
            if (lib.Get("Dec_" + top) != null) stack.Add("Dec_" + top);        // the first stage drops away here
            Adapt(upE.BottomClass);
            stack.Add(upE.id);
            var upT = lib.Get(s.upperTankId);
            if (upT != null) Tanks(upT, s.upperTankCount);
        }

        if (stack.Count > 0 && lib.Get("Avi_" + TopCls()) != null) stack.Add("Avi_" + TopCls());   // avionics: always fitted automatically
        var head = lib.Get(s.headId);
        if (head != null) { Adapt(head.BottomClass); stack.Add(head.id); }

        d.stack = stack;
        var cp = string.IsNullOrEmpty(s.customPayload) ? null : CustomPayloads.Get(s.customPayload);
        d.payloadId = cp != null && lib.Get(cp.payloadId) != null ? cp.payloadId : s.payloadId;
        d.boosterId = s.boosterId; d.boosterCount = Mathf.Clamp(s.boosterCount, 1, MaxOuter);
        d.finId = s.finId; d.finCount = Mathf.Clamp(s.finCount, 2, MaxOuter);
        d.equipment = s.extras.Where(e => lib.Get(e) != null).Take(MaxExtras).ToList();
        if (cp != null) d.equipment.AddRange(cp.equipment.Where(e => lib.Get(e) != null));          // the power / comms built into the custom cargo
    }

    /// <summary>Best-effort simple build for an older design that only has a part stack (a three-stage rocket loses its third stage).</summary>
    public static void Infer(RocketDesign d, PartLibrary lib)
    {
        var s = new SimpleBuild { payloadId = d.payloadId, boosterId = d.boosterId, boosterCount = d.boosterCount, finId = d.finId, finCount = d.finCount };
        s.extras.AddRange(d.equipment);
        var parts = d.stack.Select(lib.Get).Where(p => p != null).ToList();
        int stage = 0;
        foreach (var p in parts)
        {
            if (RocketRules.IsDecoupler(p)) { stage++; continue; }
            if (RocketRules.IsHead(p)) s.headId = p.id;
            else if (RocketRules.IsEngine(p)) { if (stage == 0 && s.engineId == null) s.engineId = p.id; else if (stage >= 1 && s.upperEngineId == null) s.upperEngineId = p.id; }
            else if (p.category == "Tank")
            {
                if (stage == 0) { if (s.tankId == null) s.tankId = p.id; if (p.id == s.tankId) s.tankCount++; }
                else if (stage == 1) { if (s.upperTankId == null) s.upperTankId = p.id; if (p.id == s.upperTankId) s.upperTankCount++; }
            }
        }
        s.tankCount = Mathf.Clamp(s.tankCount - (s.tankId != null ? 1 : 0) , 1, SimpleBuilder.MaxTanks);       // the counter above started at 1
        s.upperTankCount = Mathf.Clamp(s.upperTankCount - (s.upperTankId != null ? 1 : 0), 1, SimpleBuilder.MaxTanks);
        d.simple = s; d.useSimple = true;
    }
}

/// <summary>How well tested each part is (0-100), what that means in words, and the special ability of the engines.</summary>
public static class PartRatings
{
    static readonly Dictionary<string, int> Score = new Dictionary<string, int>
    {
        { "Eng_S_Spark", 96 }, { "Eng_S_Vac", 93 }, { "Eng_S_SolidKick", 90 }, { "Eng_M_Lifter", 95 }, { "Eng_M_Cluster3", 91 }, { "Eng_M_Vac", 93 },
        { "Eng_L_Cluster4", 88 }, { "Eng_L_Vac", 86 },
        { "Booster_S", 96 }, { "Booster_M", 93 }, { "Booster_L", 90 }, { "Fin_S", 98 }, { "Fin_L", 96 },
        { "Tank_S_Short", 98 }, { "Tank_S_Long", 97 }, { "Tank_M_Short", 97 }, { "Tank_M_Long", 96 }, { "Tank_L_Short", 94 }, { "Tank_L_Long", 92 },
        { "Head_Nose_S", 99 }, { "Head_Nose_M", 99 }, { "Head_Nose_L", 98 }, { "Head_Fairing_S", 97 }, { "Head_Fairing_M", 96 }, { "Head_Fairing_L", 94 },
        { "Head_Capsule_M", 84 }, { "Head_Capsule_L", 78 },
        { "Pay_Satellite_S", 94 }, { "Pay_Satellite_M", 91 }, { "Pay_Rover", 86 }, { "Pay_Lander", 80 },
        { "Rec_Parachute_S", 96 }, { "Rec_Parachute_M", 95 }, { "Rec_LandingLeg", 90 }, { "Rec_GridFin", 82 },
        { "Pow_Solar_S", 95 }, { "Pow_Solar_L", 92 }, { "Pow_Battery", 93 }, { "Pow_RTG", 85 },
        { "Com_Whip", 97 }, { "Com_Patch", 95 }, { "Com_Dish_S", 92 }, { "Com_Dish_L", 88 }, { "Com_PhasedArray", 76 },
        { "Thr_RCS_Block", 93 }, { "Thr_Ion", 72 },
    };

    static readonly Dictionary<string, string> Ability = new Dictionary<string, string>
    {
        { "Eng_S_Spark", "Gentle, reliable start" }, { "Eng_S_Vac", "Vacuum tuned: very efficient in space" }, { "Eng_S_SolidKick", "Cheap and simple, cannot be shut down" },
        { "Eng_M_Lifter", "Workhorse: strong liftoff" }, { "Eng_M_Cluster3", "Keeps flying if one engine fails" }, { "Eng_M_Vac", "Vacuum tuned: best fuel economy" },
        { "Eng_L_Cluster4", "Heavy lift, keeps flying with an engine out" }, { "Eng_L_Vac", "Vacuum tuned: huge efficiency for deep space" },
    };

    public static int Rating(PartInfo p) => p != null && Score.TryGetValue(p.id, out var s) ? s : 85;
    public static string Tier(int s) => s >= 95 ? "Flight-proven" : s >= 88 ? "Well tested" : s >= 78 ? "Tested" : "Experimental";
    public static string AbilityOf(PartInfo p) => p != null && Ability.TryGetValue(p.id, out var a) ? a : null;
}

/// <summary>The few numbers the simple builder shows. No extra rocket science.</summary>
public static class SimpleMetrics
{
    const float G0 = 9.81f;
    public const float DvSuborbital = 3500f, DvEarth = 9000f, DvMoon = 10500f, DvMars = 13500f;
    public static readonly string[] OrbitNames = { "Cannot launch", "Suborbital hop", "Earth orbit", "The Moon", "Mars" };

    /// <summary>Boosters (more push, less waste) and fins (less drag) both raise the first stage's efficiency a little.</summary>
    public static float EfficiencyBonus(int bn, PartInfo booster, int fn, PartInfo fin)
    {
        float b = booster != null ? Mathf.Min(0.06f, 0.015f * bn) : 0f;
        float f = fin != null ? Mathf.Min(0.04f, 0.01f * fn) : 0f;
        return b + f;
    }

    /// <summary>Fuel an engine burns per second at sea level (kg/s).</summary>
    public static float BurnKgS(PartInfo e)
    {
        if (e == null) return 0f;
        float thrust = e.thrustKn > 0 ? e.thrustKn : e.thrustVacKn;
        float isp = e.ispSl > 0 ? (e.ispSl + e.ispVac) * 0.5f : e.ispVac;
        return thrust > 0 && isp > 0 ? thrust * 1000f / (isp * G0) : 0f;
    }

    public static string StabilityWord(float s) => s < 40f ? "Low" : s < 70f ? "Medium" : "High";

    public static void Fill(RocketReport r, RocketDesign d, PartLibrary lib, List<PartInfo> stack, PartInfo booster, int bn, PartInfo fin, int fn)
    {
        r.liftKg = r.thrustKn * 1000f / (G0 * 1.2f);                                   // what the first stage can lift at a comfortable 1.2 g

        // fuel burnt per second by the first stage
        var s0 = r.stages.Count > 0 ? r.stages[0] : null;
        r.burnKgS = s0 != null && s0.isp > 0 ? s0.thrustKn * 1000f / (s0.isp * G0) : 0f;

        // payload capacity = what the head can carry
        var head = stack.Count > 0 ? stack[stack.Count - 1] : null;
        r.payloadCapKg = head != null ? head.payloadMaxKg : 0f;

        // stability: fins help a lot, an even set of boosters helps, a very thin tall rocket is wobbly
        float st = 30f;
        if (fin != null && fn > 0) st += Mathf.Min(50f, fn * (fin.size == "L" ? 16f : 10f));
        if (booster != null && bn > 0 && bn % 2 == 0) st += 10f;
        float slender = r.widthM > 0.1f ? r.heightM / r.widthM : 0f;
        if (slender > 14f) st -= (slender - 14f) * 3f;
        r.stability = Mathf.Clamp(st, 0f, 100f);

        // reliability: how well tested all the chosen parts are, on average
        var ids = new List<PartInfo>();
        void Pick(string id) { var p = lib.Get(id); if (p != null) ids.Add(p); }
        var sb = d.simple;
        if (d.useSimple && sb != null)
        {
            Pick(sb.engineId); Pick(sb.upperEngineId); Pick(sb.tankId); Pick(sb.upperTankId); Pick(sb.headId); Pick(sb.payloadId); Pick(sb.boosterId); Pick(sb.finId);
            foreach (var e in sb.extras) Pick(e);
        }
        else
        {
            ids.AddRange(stack); ids.Add(booster); ids.Add(fin); ids.Add(lib.Get(d.payloadId)); ids.AddRange(d.equipment.Select(lib.Get));
            ids.RemoveAll(p => p == null);
        }
        r.reliability = ids.Count > 0 ? (float)ids.Average(PartRatings.Rating) : 0f;

        // how far can it go
        float dv = r.dvTotal;
        r.orbitLevel = !(r.liftoffTwr >= 1f) ? 0 : dv >= DvMars ? 4 : dv >= DvMoon ? 3 : dv >= DvEarth ? 2 : dv >= DvSuborbital ? 1 : 0;
    }
}
