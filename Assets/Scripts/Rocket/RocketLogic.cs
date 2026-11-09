using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>What the player has built. The stack goes bottom -> top; the rest is attached to it.</summary>
[Serializable]
public class RocketDesign
{
    public List<string> stack = new List<string>();
    public string boosterId;
    public int boosterCount = 2;
    public string finId;
    public int finCount = 4;
    public string payloadId;
    public List<string> equipment = new List<string>();
    public int livery;

    public string ToJson() => JsonUtility.ToJson(this);
    public static RocketDesign FromJson(string json)
    {
        try { return string.IsNullOrEmpty(json) ? new RocketDesign() : JsonUtility.FromJson<RocketDesign>(json) ?? new RocketDesign(); }
        catch { return new RocketDesign(); }
    }

    public void Clear()
    {
        stack.Clear(); equipment.Clear();
        boosterId = finId = payloadId = null;
        boosterCount = 2; finCount = 4;
    }
}

public enum PartSlot { Stack, Booster, Fin, Payload, Equipment }

/// <summary>Where a part can go and whether it fits what is already built.</summary>
public static class RocketRules
{
    public static PartSlot SlotOf(PartInfo p)
    {
        switch (p.category)
        {
            case "Booster": return PartSlot.Booster;
            case "Payload": return PartSlot.Payload;
            case "Comms": case "Power": case "Recovery": case "Thruster": return PartSlot.Equipment;
            case "Structure": return p.id.StartsWith("Fin_") ? PartSlot.Fin : PartSlot.Stack;
            default: return PartSlot.Stack;
        }
    }

    public static bool IsHead(PartInfo p) => p != null && p.category == "Head";
    public static bool IsEngine(PartInfo p) => p != null && p.category == "Engine";
    public static bool IsDecoupler(PartInfo p) => p != null && p.id.StartsWith("Dec_");

    /// <summary>Index at which a stack part would be inserted, or -1 when it does not fit.</summary>
    public static int StackInsertIndex(RocketDesign d, PartInfo p, PartLibrary lib, out string reason)
    {
        reason = "";
        var stack = d.stack.Select(lib.Get).Where(x => x != null).ToList();
        bool hasHead = stack.Count > 0 && IsHead(stack[stack.Count - 1]);
        int top = stack.Count - (hasHead ? 1 : 0);            // number of parts below the head
        PartInfo last = top > 0 ? stack[top - 1] : null;

        if (IsEngine(p) && (stack.Count == 0 || !IsEngine(stack[0])))
        {
            // first-stage engine goes at the bottom
            if (stack.Count > 0 && stack[0].BottomClass != p.TopClass)
            {
                reason = "Engine is size " + p.size + " but the stack starts with size " + stack[0].BottomClass + ".";
                return -1;
            }
            return 0;
        }
        if (IsHead(p))
        {
            if (last != null && last.TopClass != p.BottomClass) { reason = "Head is size " + p.size + " but the stack ends in size " + last.TopClass + ". Add an adapter."; return -1; }
            return hasHead ? stack.Count - 1 : stack.Count;   // replaces the current head
        }
        if (last != null && last.TopClass != p.BottomClass)
        {
            reason = "Size " + p.BottomClass + " part on top of size " + last.TopClass + ". Add an adapter.";
            return -1;
        }
        return top;                                           // goes just under the head, if there is one
    }

    public static bool CanAdd(RocketDesign d, PartInfo p, PartLibrary lib, out string reason)
    {
        reason = "";
        if (SlotOf(p) == PartSlot.Stack) return StackInsertIndex(d, p, lib, out reason) >= 0;
        return true;
    }

    public static void Add(RocketDesign d, PartInfo p, PartLibrary lib)
    {
        switch (SlotOf(p))
        {
            case PartSlot.Stack:
                int i = StackInsertIndex(d, p, lib, out _);
                if (i < 0) return;
                if (IsHead(p) && d.stack.Count > 0 && IsHead(lib.Get(d.stack[d.stack.Count - 1]))) d.stack.RemoveAt(d.stack.Count - 1);
                d.stack.Insert(Mathf.Min(i, d.stack.Count), p.id);
                break;
            case PartSlot.Booster: if (d.boosterId != p.id && d.boosterCount < 2) d.boosterCount = 2; d.boosterId = p.id; break;
            case PartSlot.Fin: d.finId = p.id; if (d.finCount < 3) d.finCount = 4; break;
            case PartSlot.Payload: d.payloadId = p.id; break;
            case PartSlot.Equipment: if (d.equipment.Count < 10) d.equipment.Add(p.id); break;
        }
    }
}

public enum Sev { Ok, Warn, Error }

public class Check { public string text; public Sev sev; }

public class StageInfo
{
    public float dry, fuel, thrustKn, thrustOverIsp, isp, dv, twr, m0;
    public int engines;
    public List<PartInfo> parts = new List<PartInfo>();
}

public class RocketReport
{
    public List<StageInfo> stages = new List<StageInfo>();
    public List<Check> checks = new List<Check>();
    public float heightM, widthM, dryKg, fuelKg, launchKg, payloadKg, thrustKn, liftoffTwr, dvTotal, dvRequired, costM;
    public float powerSupplyW, powerDemandW, commMbps;
    public bool valid;
    public MissionDefinition mission;
}

/// <summary>Turns a design into numbers (mass, thrust, delta-v, cost, power, comms) and pass/fail checks against a contract.</summary>
public static class RocketStats
{
    const float G0 = 9.81f;

    public static float DvForTarget(string target)
    {
        switch ((target ?? "").ToLowerInvariant())
        {
            case "moon": return 12600f;
            case "mars": return 14500f;
            default: return 9400f;                  // Earth orbit
        }
    }

    static void Add(RocketReport r, Sev s, string text) => r.checks.Add(new Check { sev = s, text = text });

    public static RocketReport Evaluate(RocketDesign d, PartLibrary lib, MissionDefinition mission)
    {
        var r = new RocketReport { mission = mission };
        var stack = d.stack.Select(lib.Get).Where(x => x != null).ToList();
        var payload = lib.Get(d.payloadId);
        var equip = d.equipment.Select(lib.Get).Where(x => x != null).ToList();
        var booster = lib.Get(d.boosterId); int bn = booster != null ? Mathf.Max(0, d.boosterCount) : 0;
        var fin = lib.Get(d.finId); int fn = fin != null ? Mathf.Max(0, d.finCount) : 0;

        // ---- stages: a decoupler closes a stage ----
        r.stages.Add(new StageInfo());
        foreach (var p in stack)
        {
            var st = r.stages[r.stages.Count - 1];
            int si = r.stages.Count - 1;
            st.parts.Add(p); st.dry += p.dryMassKg; st.fuel += p.fuelKg; r.costM += p.costM;
            r.heightM += p.heightM; r.widthM = Mathf.Max(r.widthM, p.widthM);
            if (RocketRules.IsEngine(p))
            {
                float thrust = si == 0 ? p.thrustKn : p.thrustVacKn;
                float isp = si == 0 ? (p.ispSl > 0 ? (p.ispSl + p.ispVac) * 0.5f : p.ispVac) : p.ispVac;
                if (thrust > 0 && isp > 0) { st.thrustKn += thrust; st.thrustOverIsp += thrust / isp; st.engines++; }
            }
            if (RocketRules.IsDecoupler(p)) r.stages.Add(new StageInfo());
        }
        if (r.stages.Count > 1 && r.stages[r.stages.Count - 1].parts.Count == 0) r.stages.RemoveAt(r.stages.Count - 1);
        var s0 = r.stages[0];
        if (bn > 0)
        {
            s0.dry += bn * booster.dryMassKg; s0.fuel += bn * booster.fuelKg; r.costM += bn * booster.costM;
            s0.thrustKn += bn * booster.thrustKn; s0.thrustOverIsp += bn * booster.thrustKn / ((booster.ispSl + booster.ispVac) * 0.5f);
        }
        if (fn > 0) { s0.dry += fn * fin.dryMassKg; r.costM += fn * fin.costM; }

        // ---- payload + equipment ----
        r.payloadKg = (payload != null ? payload.massKg : 0f) + equip.Sum(e => e.dryMassKg);
        if (payload != null) r.costM += payload.costM;
        r.costM += equip.Sum(e => e.costM);

        float above = r.payloadKg;
        for (int i = r.stages.Count - 1; i >= 0; i--)
        {
            var st = r.stages[i];
            above += st.dry + st.fuel; st.m0 = above;
            st.isp = st.thrustOverIsp > 0 ? st.thrustKn / st.thrustOverIsp : 0f;
            st.dv = st.fuel > 0 && st.isp > 0 && st.m0 > st.fuel ? st.isp * G0 * Mathf.Log(st.m0 / (st.m0 - st.fuel)) : 0f;
            st.twr = st.thrustKn * 1000f / (st.m0 * G0);
        }
        r.launchKg = r.stages[0].m0;
        r.dvTotal = r.stages.Sum(s => s.dv);
        r.liftoffTwr = s0.twr;
        r.thrustKn = s0.thrustKn;
        r.fuelKg = r.stages.Sum(s => s.fuel);
        r.dryKg = r.launchKg - r.fuelKg - r.payloadKg;

        // ---- power / comms ----
        r.powerSupplyW = equip.Where(e => e.category == "Power").Sum(e => Mathf.Max(0f, e.powerW)) + (payload != null ? Mathf.Max(0f, payload.powerW) : 0f);
        r.powerDemandW = stack.Where(p => p.category == "Structure").Sum(p => Mathf.Max(0f, p.powerW))
                         + equip.Where(e => e.category != "Power").Sum(e => Mathf.Max(0f, e.powerW))
                         + (payload != null ? Mathf.Max(0f, -payload.powerW) : 0f);
        r.commMbps = Mathf.Max(equip.Where(e => e.category == "Comms").Select(e => e.dataMbps).DefaultIfEmpty(0f).Max(), payload != null ? payload.commMbps : 0f);

        // ---- design checks ----
        if (stack.Count == 0) Add(r, Sev.Error, "Empty rocket. Start with an engine and a tank.");
        else
        {
            if (!RocketRules.IsEngine(stack[0])) Add(r, Sev.Error, "No engine at the bottom of the stack.");
            if (!RocketRules.IsHead(stack[stack.Count - 1])) Add(r, Sev.Error, "No head. Add a fairing, nose cone or capsule on top.");
            for (int i = 1; i < stack.Count; i++)
                if (stack[i - 1].TopClass != stack[i].BottomClass)
                    Add(r, Sev.Error, "Size mismatch: " + stack[i - 1].DisplayName + " / " + stack[i].DisplayName + ". Add an adapter.");
            for (int i = 0; i < r.stages.Count; i++)
            {
                var st = r.stages[i];
                if (st.fuel > 0 && st.engines == 0) Add(r, Sev.Error, "Stage " + (i + 1) + " has fuel but no engine.");
                else if (st.engines > 0 && st.fuel <= 0) Add(r, Sev.Warn, "Stage " + (i + 1) + " has an engine but no fuel.");
            }
            if (!stack.Any(p => p.id.StartsWith("Avi_"))) Add(r, Sev.Warn, "No avionics: the rocket cannot be steered.");
            if (r.stages.Count > 1 && !stack.Any(RocketRules.IsDecoupler)) Add(r, Sev.Warn, "Add decouplers to drop empty stages.");
            if (stack.Count > 0 && RocketRules.IsEngine(stack[0]))
            {
                if (r.liftoffTwr < 1f) Add(r, Sev.Error, "Cannot lift off: thrust-to-weight " + r.liftoffTwr.ToString("0.00") + " (needs > 1.0).");
                else if (r.liftoffTwr < 1.2f) Add(r, Sev.Warn, "Weak liftoff: thrust-to-weight " + r.liftoffTwr.ToString("0.00") + ".");
                else Add(r, Sev.Ok, "Liftoff thrust-to-weight " + r.liftoffTwr.ToString("0.00") + ".");
            }
        }
        if (payload != null)
        {
            var head = stack.Count > 0 ? stack[stack.Count - 1] : null;
            if (head == null || !head.id.StartsWith("Head_Fairing")) Add(r, Sev.Error, "Payload needs a fairing on top.");
            else
            {
                if (PartInfo.SizeRank(payload.size) > PartInfo.SizeRank(head.size)) Add(r, Sev.Error, "Payload (" + payload.size + ") does not fit the " + head.size + " fairing.");
                else if (payload.massKg > head.payloadMaxKg) Add(r, Sev.Error, "Payload is heavier than the fairing allows (" + head.payloadMaxKg + " kg).");
                else Add(r, Sev.Ok, "Payload fits the fairing.");
            }
        }
        if (r.powerSupplyW + r.powerDemandW > 0)
        {
            if (r.powerSupplyW >= r.powerDemandW) Add(r, Sev.Ok, "Power: " + r.powerSupplyW.ToString("0") + " W made, " + r.powerDemandW.ToString("0") + " W used.");
            else Add(r, Sev.Warn, "Power short: " + r.powerSupplyW.ToString("0") + " W made, " + r.powerDemandW.ToString("0") + " W used.");
        }

        // ---- contract checks ----
        if (mission != null)
        {
            r.dvRequired = DvForTarget(mission.target);
            string want = mission.payload != null ? mission.payload.type : "";
            if (!string.IsNullOrEmpty(want))
            {
                bool crew = want.IndexOf("crew", StringComparison.OrdinalIgnoreCase) >= 0 || want.IndexOf("human", StringComparison.OrdinalIgnoreCase) >= 0;
                bool ok = crew ? stack.Any(p => p.id.StartsWith("Head_Capsule")) : payload != null && string.Equals(payload.payloadType, want, StringComparison.OrdinalIgnoreCase);
                Add(r, ok ? Sev.Ok : Sev.Error, ok ? "Payload: " + want + "." : "Contract needs a " + want + " payload.");
            }
            bool massOk = r.payloadKg <= mission.massLimit;
            Add(r, massOk ? Sev.Ok : Sev.Error, "Payload mass " + r.payloadKg.ToString("0") + " / " + mission.massLimit.ToString("0") + " kg.");
            bool budgetOk = r.costM * 1e6 <= mission.budget;
            Add(r, budgetOk ? Sev.Ok : Sev.Error, "Cost $" + r.costM.ToString("0.0") + "M / $" + (mission.budget / 1e6).ToString("0") + "M.");
            bool dvOk = r.dvTotal >= r.dvRequired;
            Add(r, dvOk ? Sev.Ok : Sev.Error, "Delta-v " + r.dvTotal.ToString("0") + " / " + r.dvRequired.ToString("0") + " m/s (" + mission.target + ").");

            foreach (var req in mission.specialRequirements)
            {
                switch ((req.type ?? "").ToLowerInvariant())
                {
                    case "power":
                        float net = r.powerSupplyW - (r.powerDemandW - (payload != null ? Mathf.Max(0f, -payload.powerW) : 0f));
                        Add(r, net >= req.minimum ? Sev.Ok : Sev.Error, "Power: " + net.ToString("0") + " / " + req.minimum.ToString("0") + " W.");
                        break;
                    case "communication":
                        Add(r, r.commMbps >= req.minimum ? Sev.Ok : Sev.Error, "Comms: " + r.commMbps.ToString("0.##") + " / " + req.minimum.ToString("0.##") + " Mbps.");
                        break;
                    case "landing":
                        bool soft = equip.Any(e => e.id.StartsWith("Rec_Parachute") || e.id == "Rec_LandingLeg");
                        Add(r, soft ? Sev.Ok : Sev.Error, soft ? "Landing gear fitted (" + req.value + ")." : req.value + " needs a parachute or landing legs.");
                        break;
                    case "orbit":
                        bool orbit = stack.Any(p => p.id.StartsWith("Avi_")) && equip.Any(e => e.id == "Thr_RCS_Block");
                        Add(r, orbit ? Sev.Ok : Sev.Error, orbit ? req.value + ": avionics + RCS fitted." : req.value + " needs avionics and an RCS block.");
                        break;
                    default:
                        Add(r, Sev.Warn, req.ToDisplayString() + " (not checked yet).");
                        break;
                }
            }
        }

        r.valid = !r.checks.Any(c => c.sev == Sev.Error);
        return r;
    }
}

/// <summary>Builds the 3D rocket from a design (stack, boosters, fins and equipment).</summary>
public static class RocketAssembler
{
    public static GameObject Build(RocketDesign d, PartLibrary lib, out Bounds bounds)
    {
        var root = new GameObject("Rocket");
        var mat = lib.Livery(d.livery);

        GameObject Spawn(PartInfo p, Vector3 pos, Quaternion rot)
        {
            var prefab = lib.Prefab(p.id);
            if (prefab == null) return null;
            var go = UnityEngine.Object.Instantiate(prefab, root.transform);
            go.transform.localPosition = pos; go.transform.localRotation = rot;
            if (mat != null) foreach (var rd in go.GetComponentsInChildren<MeshRenderer>()) rd.sharedMaterial = mat;
            return go;
        }

        float y = 0f, firstStageR = 0f, topR = 0.6f, headBase = -1f;
        bool inFirst = true;
        foreach (var id in d.stack)
        {
            var p = lib.Get(id); if (p == null) continue;
            Spawn(p, new Vector3(0f, y, 0f), Quaternion.identity);
            if (inFirst) firstStageR = Mathf.Max(firstStageR, p.Radius);
            if (RocketRules.IsDecoupler(p)) inFirst = false;
            if (RocketRules.IsHead(p)) headBase = y;
            topR = p.Radius;
            y += p.heightM;
        }
        float top = y;

        var booster = lib.Get(d.boosterId);
        if (booster != null && d.boosterCount > 0)
            for (int i = 0; i < d.boosterCount; i++)
            {
                float a = 360f * i / d.boosterCount + 90f;
                Vector3 dir = Quaternion.Euler(0, a, 0) * Vector3.forward;
                Spawn(booster, dir * (firstStageR + booster.Radius + 0.08f), Quaternion.identity);
            }

        var fin = lib.Get(d.finId);
        if (fin != null && d.finCount > 0)
            for (int i = 0; i < d.finCount; i++)
            {
                float a = 360f * i / d.finCount + 45f;
                Vector3 dir = Quaternion.Euler(0, a, 0) * Vector3.forward;
                Spawn(fin, dir * (firstStageR * 0.98f) + Vector3.up * 0.4f, Quaternion.FromToRotation(Vector3.left, dir));
            }

        // equipment rides on the ring just under the head
        float ey = headBase >= 0f ? headBase : top;
        var equip = d.equipment.Select(lib.Get).Where(p => p != null).ToList();
        for (int i = 0; i < equip.Count; i++)
        {
            float a = 360f * i / equip.Count + 20f;
            Vector3 dir = Quaternion.Euler(0, a, 0) * Vector3.forward;
            Spawn(equip[i], dir * (topR + 0.04f) + Vector3.up * Mathf.Max(0f, ey - 0.5f), Quaternion.FromToRotation(Vector3.forward, dir));
        }

        var rends = root.GetComponentsInChildren<Renderer>();
        bounds = rends.Length > 0 ? rends[0].bounds : new Bounds(Vector3.up, Vector3.one);
        foreach (var rd in rends) bounds.Encapsulate(rd.bounds);
        return root;
    }
}
