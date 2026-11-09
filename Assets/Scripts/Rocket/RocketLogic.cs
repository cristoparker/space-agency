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

    /// <summary>
    /// Where a stack part would be inserted. Parts are never refused: if the size does not fit,
    /// `advisory` says so (the rocket checks will show the error until it is fixed, e.g. with an adapter or Auto order).
    /// </summary>
    public static int StackInsertIndex(RocketDesign d, PartInfo p, PartLibrary lib, out string advisory)
    {
        advisory = "";
        var stack = d.stack.Select(lib.Get).Where(x => x != null).ToList();
        bool hasHead = stack.Count > 0 && IsHead(stack[stack.Count - 1]);
        int top = stack.Count - (hasHead ? 1 : 0);            // number of parts below the head
        PartInfo last = top > 0 ? stack[top - 1] : null;

        if (IsEngine(p) && (stack.Count == 0 || !IsEngine(stack[0])))
        {
            // first-stage engine goes at the bottom
            if (stack.Count > 0 && stack[0].BottomClass != p.TopClass)
                advisory = "Engine is size " + p.size + " but the stack starts with size " + stack[0].BottomClass + ".";
            return 0;
        }
        if (IsHead(p))
        {
            if (last != null && last.TopClass != p.BottomClass) advisory = "Head is size " + p.size + " but the stack ends in size " + last.TopClass + ". Add an adapter.";
            return hasHead ? stack.Count - 1 : stack.Count;   // replaces the current head
        }
        if (last != null && last.TopClass != p.BottomClass)
            advisory = "Size " + p.BottomClass + " part on top of size " + last.TopClass + ". Add an adapter.";
        return top;                                           // goes just under the head, if there is one
    }

    /// <summary>Always true. `reason` holds a size warning when the part does not fit well.</summary>
    public static bool CanAdd(RocketDesign d, PartInfo p, PartLibrary lib, out string reason)
    {
        reason = "";
        if (SlotOf(p) == PartSlot.Stack) StackInsertIndex(d, p, lib, out reason);
        return true;
    }

    public static Sel Add(RocketDesign d, PartInfo p, PartLibrary lib)
    {
        switch (SlotOf(p))
        {
            case PartSlot.Stack:
                int i = StackInsertIndex(d, p, lib, out _);
                if (IsHead(p) && d.stack.Count > 0 && IsHead(lib.Get(d.stack[d.stack.Count - 1]))) d.stack.RemoveAt(d.stack.Count - 1);
                i = Mathf.Min(i, d.stack.Count);
                d.stack.Insert(i, p.id);
                return Sel.Of(SelKind.Stack, i);
            case PartSlot.Booster: if (d.boosterId != p.id && d.boosterCount < 2) d.boosterCount = 2; d.boosterId = p.id; return Sel.Of(SelKind.Booster);
            case PartSlot.Fin: d.finId = p.id; if (d.finCount < 3) d.finCount = 4; return Sel.Of(SelKind.Fin);
            case PartSlot.Payload: d.payloadId = p.id; return Sel.Of(SelKind.Payload);
            default:
                if (d.equipment.Count < 10) d.equipment.Add(p.id);
                return Sel.Of(SelKind.Equip, d.equipment.Count - 1);
        }
    }

    static PartInfo Take(List<PartInfo> pool, System.Func<PartInfo, bool> pred)
    {
        var p = pool.FirstOrDefault(pred);
        if (p != null) pool.Remove(p);
        return p;
    }

    class Stg
    {
        public List<PartInfo> eng = new List<PartInfo>(), tanks = new List<PartInfo>();
        public string Cls => eng.Count > 0 ? eng[0].TopClass : tanks.Count > 0 ? tanks[0].BottomClass : "M";
        public string TopCls => tanks.Count > 0 ? tanks[tanks.Count - 1].TopClass : Cls;
    }

    /// <summary>
    /// Puts the stack in a sensible physical order: biggest stage at the bottom, each stage = engines then tanks,
    /// a decoupler between stages (adapters where the size changes), avionics near the top and the head last.
    /// </summary>
    public static string AutoOrder(RocketDesign d, PartLibrary lib)
    {
        var all = d.stack.Select(lib.Get).Where(x => x != null).ToList();
        if (all.Count < 2) return "Nothing to order yet.";
        var heads = all.Where(IsHead).ToList();
        var engines = all.Where(IsEngine).ToList();
        var tanks = all.Where(p => p.category == "Tank").ToList();
        var decs = all.Where(IsDecoupler).ToList();
        var adps = all.Where(p => p.id.StartsWith("Adp_")).ToList();
        var avis = all.Where(p => p.id.StartsWith("Avi_")).ToList();
        var rest = all.Where(p => !IsHead(p) && !IsEngine(p) && p.category != "Tank" && !IsDecoupler(p) && !p.id.StartsWith("Adp_") && !p.id.StartsWith("Avi_")).ToList();

        // sea-level engines first (bigger first); vacuum-only engines go to the upper stages
        engines = engines.OrderByDescending(e => e.thrustKn > 0 ? 1 : 0).ThenByDescending(e => PartInfo.SizeRank(e.TopClass)).ThenByDescending(e => e.thrustKn + e.thrustVacKn).ToList();
        int n = Mathf.Max(1, Mathf.Min(decs.Count + 1, Mathf.Max(1, engines.Count)));
        var st = Enumerable.Range(0, n).Select(_ => new Stg()).ToList();
        for (int i = 0; i < engines.Count; i++)
        {
            int target = i < n ? i : st.FindIndex(s => s.Cls == engines[i].TopClass);
            st[target < 0 ? 0 : target].eng.Add(engines[i]);
        }
        foreach (var t in tanks.OrderByDescending(t => PartInfo.SizeRank(t.TopClass)).ThenByDescending(t => t.fuelKg))
        {
            var match = st.Where(s => s.eng.Count > 0 && s.Cls == t.BottomClass).OrderBy(s => s.tanks.Count).FirstOrDefault()
                        ?? st.Where(s => s.eng.Count == 0).OrderBy(s => s.tanks.Count).FirstOrDefault() ?? st[0];
            match.tanks.Add(t);
        }
        st = st.Where(s => s.eng.Count + s.tanks.Count > 0).OrderByDescending(s => PartInfo.SizeRank(s.Cls)).ToList();

        var result = new List<PartInfo>();
        for (int i = 0; i < st.Count; i++)
        {
            result.AddRange(st[i].eng);
            result.AddRange(st[i].tanks.OrderByDescending(t => t.heightM));
            if (i < st.Count - 1)
            {
                string top = st[i].TopCls, nextBottom = st[i + 1].Cls;
                var dec = Take(decs, x => x.BottomClass == top) ?? Take(decs, x => true);
                if (dec != null) result.Add(dec);
                if (top != nextBottom)
                {
                    var ad = Take(adps, x => x.BottomClass == top && x.TopClass == nextBottom);
                    if (ad != null) result.Add(ad);
                }
            }
        }
        // whatever is left (extra decouplers, adapters, odd parts), then avionics, then the head
        result.AddRange(adps); result.AddRange(decs); result.AddRange(rest);
        string cls = result.Count > 0 ? result[result.Count - 1].TopClass : "M";
        var avi = Take(avis, x => x.BottomClass == cls) ?? Take(avis, x => true);
        while (avi != null) { result.Add(avi); avi = Take(avis, x => true); }
        if (heads.Count > 0) result.Add(heads[heads.Count - 1]);
        d.stack = result.Select(p => p.id).ToList();
        return "Auto order: " + st.Count + " stage" + (st.Count == 1 ? "" : "s") + ", " + result.Count + " parts.";
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
            case "moon": return 10500f;
            case "mars": return 13500f;
            default: return 9000f;                  // Earth orbit
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
                st.engines++;
                if (thrust > 0 && isp > 0) { st.thrustKn += thrust; st.thrustOverIsp += thrust / isp; }
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
                    Add(r, Sev.Error, "Size mismatch: " + stack[i - 1].DisplayName + " (" + stack[i - 1].TopClass + ") under " + stack[i].DisplayName + " (" + stack[i].BottomClass + "). Add an adapter or press Auto order.");
            if (r.fuelKg <= 0) Add(r, Sev.Error, "No fuel. Add a fuel tank.");
            for (int i = 0; i < r.stages.Count; i++)
            {
                var st = r.stages[i];
                if (st.parts.Count == 0) continue;
                if (st.fuel > 0 && st.engines == 0 && !(i == 0 && bn > 0)) Add(r, Sev.Error, "Stage " + (i + 1) + " has fuel but no engine. Put an engine at the bottom of that stage.");
                else if (st.engines > 0 && st.fuel <= 0) Add(r, Sev.Warn, "Stage " + (i + 1) + " has an engine but no fuel.");
                if (i == 0 && st.engines > 0 && st.thrustKn <= 0 && bn == 0) Add(r, Sev.Error, "Stage 1 only has vacuum engines. They cannot start in the atmosphere: use a sea-level engine.");
            }
            if (!stack.Any(p => p.id.StartsWith("Avi_"))) Add(r, Sev.Error, "No avionics. Add one (Structure tab) so the rocket can be steered.");
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
    static readonly Dictionary<int, Material> ghostCache = new Dictionary<int, Material>();

    static Material Ghost(PartLibrary lib, int livery)
    {
        if (lib.ghostMaterial == null) return lib.Livery(livery);
        if (!ghostCache.TryGetValue(livery, out var m) || m == null)
        {
            m = new Material(lib.ghostMaterial) { name = "Ghost cover " + livery };
            m.SetTextureOffset("_BaseMap", new Vector2(livery / 8f, 0f));
            ghostCache[livery] = m;
        }
        return m;
    }

    // An open tube around an upper-stage engine: dark ring at each end, livery colour in between.
    static GameObject MakeCover(Transform parent, float radius, float height, Material mat)
    {
        const int seg = 28;
        float band = Mathf.Min(0.22f, height * 0.25f);
        float[] ys = { 0f, band, height - band, height };
        Vector2[] uv = { new Vector2(0.5f / 8f, 5.5f / 16f), new Vector2(0.5f / 8f, 0.5f / 16f), new Vector2(0.5f / 8f, 5.5f / 16f) };   // dark / livery / dark
        var verts = new List<Vector3>(); var norms = new List<Vector3>(); var uvs = new List<Vector2>(); var tris = new List<int>();
        for (int b = 0; b < 3; b++)
        {
            int start = verts.Count;
            for (int ring = 0; ring < 2; ring++)
                for (int i = 0; i <= seg; i++)
                {
                    float a = i * Mathf.PI * 2f / seg;
                    var dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                    verts.Add(dir * radius + Vector3.up * ys[b + ring]); norms.Add(dir); uvs.Add(uv[b]);
                }
            for (int i = 0; i < seg; i++)
            {
                int a0 = start + i, a1 = start + i + 1, b0 = start + seg + 1 + i, b1 = start + seg + 1 + i + 1;
                tris.AddRange(new[] { a0, b0, a1, a1, b0, b1 });
            }
        }
        var mesh = new Mesh { name = "InterstageCover" };
        mesh.SetVertices(verts); mesh.SetNormals(norms); mesh.SetUVs(0, uvs); mesh.SetTriangles(tris, 0);
        mesh.RecalculateBounds();
        var go = new GameObject("InterstageCover", typeof(MeshFilter), typeof(MeshRenderer));
        go.transform.SetParent(parent, false);
        go.GetComponent<MeshFilter>().sharedMesh = mesh;
        var mr = go.GetComponent<MeshRenderer>();
        mr.sharedMaterial = mat; mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        return go;
    }

    static float ClassRadius(string cls) => cls == "S" ? 0.6f : cls == "L" ? 1.5f : 1.0f;

    /// <param name="ghostCovers">true in the builder: the cover over an upper-stage engine is translucent so you can see the engine.
    /// false everywhere else (launch pad, flight): the cover is solid and hides the engine.</param>
    public static GameObject Build(RocketDesign d, PartLibrary lib, out Bounds bounds, bool ghostCovers = false)
    {
        var root = new GameObject("Rocket");
        var mat = lib.Livery(d.livery);

        GameObject Spawn(PartInfo p, Vector3 pos, Quaternion rot, SelKind kind, int index)
        {
            var prefab = lib.Prefab(p.id);
            if (prefab == null) return null;
            var go = UnityEngine.Object.Instantiate(prefab, root.transform);
            go.transform.localPosition = pos; go.transform.localRotation = rot;
            var pr = go.AddComponent<PartRef>(); pr.kind = kind; pr.index = index;
            if (mat != null) foreach (var rd in go.GetComponentsInChildren<MeshRenderer>()) rd.sharedMaterial = mat;
            return go;
        }

        float y = 0f, firstStageR = 0f, topR = 0.6f, headBase = -1f;
        bool inFirst = true;
        for (int si = 0; si < d.stack.Count; si++)
        {
            var p = lib.Get(d.stack[si]); if (p == null) continue;
            var spawned = Spawn(p, new Vector3(0f, y, 0f), Quaternion.identity, SelKind.Stack, si);
            if (spawned != null && RocketRules.IsEngine(p) && si > 0)         // upper-stage engine: close the gap between the stages
                MakeCover(spawned.transform, ClassRadius(p.TopClass) + 0.01f, p.heightM, ghostCovers ? Ghost(lib, d.livery) : lib.Livery(d.livery));
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
                Spawn(booster, dir * (firstStageR + booster.Radius + 0.08f), Quaternion.identity, SelKind.Booster, 0);
            }

        var fin = lib.Get(d.finId);
        if (fin != null && d.finCount > 0)
            for (int i = 0; i < d.finCount; i++)
            {
                float a = 360f * i / d.finCount + 45f;
                Vector3 dir = Quaternion.Euler(0, a, 0) * Vector3.forward;
                Spawn(fin, dir * (firstStageR * 0.98f) + Vector3.up * 0.4f, Quaternion.FromToRotation(Vector3.left, dir), SelKind.Fin, 0);
            }

        // equipment rides on the ring just under the head
        float ey = headBase >= 0f ? headBase : top;
        for (int i = 0; i < d.equipment.Count; i++)
        {
            var e = lib.Get(d.equipment[i]); if (e == null) continue;
            float a = 360f * i / d.equipment.Count + 20f;
            Vector3 dir = Quaternion.Euler(0, a, 0) * Vector3.forward;
            Spawn(e, dir * (topR + 0.04f) + Vector3.up * Mathf.Max(0f, ey - 0.5f), Quaternion.FromToRotation(Vector3.forward, dir), SelKind.Equip, i);
        }

        var rends = root.GetComponentsInChildren<Renderer>();
        bounds = rends.Length > 0 ? rends[0].bounds : new Bounds(Vector3.up, Vector3.one);
        foreach (var rd in rends) bounds.Encapsulate(rd.bounds);
        return root;
    }

    /// <summary>Selected part: brighter colours plus a cyan outline (an inflated back-face copy of its meshes).</summary>
    public static void Highlight(GameObject rocket, Sel sel, Material outline, float thickness)
    {
        if (sel.IsNone || rocket == null) return;
        var mpb = new MaterialPropertyBlock();
        mpb.SetColor("_BaseColor", new Color(1.45f, 1.6f, 1.75f, 1f));
        foreach (var pr in rocket.GetComponentsInChildren<PartRef>())
        {
            if (!pr.Matches(sel)) continue;
            foreach (var mr in pr.GetComponentsInChildren<MeshRenderer>())
            {
                if (mr.gameObject.name == "InterstageCover") continue;
                mr.SetPropertyBlock(mpb);
                var mf = mr.GetComponent<MeshFilter>();
                if (mf == null || mf.sharedMesh == null || outline == null) continue;
                var go = new GameObject("Outline");
                go.transform.SetParent(mr.transform, false);
                go.AddComponent<MeshFilter>().sharedMesh = mf.sharedMesh;
                var orr = go.AddComponent<MeshRenderer>();
                orr.sharedMaterial = outline;
                orr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                Vector3 ext = mf.sharedMesh.bounds.extents, c = mf.sharedMesh.bounds.center;
                Vector3 scale = new Vector3(1f + thickness / Mathf.Max(0.02f, ext.x), 1f + thickness / Mathf.Max(0.02f, ext.y), 1f + thickness / Mathf.Max(0.02f, ext.z));
                go.transform.localScale = scale;
                go.transform.localPosition = c - Vector3.Scale(c, scale);     // inflate around the mesh centre
            }
        }
    }
}

/// <summary>Ready-made rockets that pass all the checks. The three contract demos meet MISSION-001/002/003.</summary>
public static class RocketDemos
{
    public class Demo
    {
        public string name, description, missionId;
        public RocketDesign design;
    }

    static Demo D(string name, string desc, string mission, string[] stack, string payload, string[] equip, string booster = null, int bn = 2, string fin = null, int fn = 3)
    {
        var d = new RocketDesign { boosterId = booster, boosterCount = bn, finId = fin, finCount = fn, payloadId = payload };
        d.stack.AddRange(stack); d.equipment.AddRange(equip);
        return new Demo { name = name, description = desc, missionId = mission, design = d };
    }

    public static readonly List<Demo> All = new List<Demo>
    {
        D("Demo Pathfinder", "Small satellite launcher. Cheap, single stage.", "",
          new[] { "Eng_S_Spark", "Tank_S_Long", "Avi_S", "Head_Fairing_S" }, "Pay_Satellite_S", new[] { "Com_Patch" }, null, 2, "Fin_S", 3),
        D("Demo Crew Ferry", "Two-stage capsule rocket with two side boosters.", "",
          new[] { "Eng_M_Lifter", "Tank_M_Long", "Dec_M", "Eng_M_Vac", "Tank_M_Short", "Avi_M", "Head_Capsule_M" }, null, new[] { "Pow_Solar_S" }, "Booster_S", 2, "Fin_S", 4),
        D("Demo Earth Watch", "For MISSION-002: sun-sync climate satellite. Two medium stages.", "MISSION-002",
          new[] { "Eng_M_Cluster3", "Tank_M_Long", "Tank_M_Long", "Dec_M", "Eng_M_Vac", "Tank_M_Long", "Avi_M", "Head_Fairing_M" },
          "Pay_Satellite_M", new[] { "Thr_RCS_Block", "Com_Patch" }),
        D("Demo Lunar Lander", "For MISSION-003: soft-landing lunar station lander. Three large stages.", "MISSION-003",
          new[] { "Eng_L_Cluster4", "Tank_L_Short", "Tank_L_Short", "Dec_L", "Eng_L_Vac", "Tank_L_Short", "Tank_L_Short", "Tank_L_Short", "Dec_L", "Eng_L_Vac", "Tank_L_Short", "Avi_L", "Head_Fairing_L" },
          "Pay_Lander", new[] { "Rec_Parachute_M", "Rec_LandingLeg", "Pow_Solar_L" }, "Booster_S", 2),
        D("Demo Mars Explorer", "For MISSION-001: Mars rover carrier with solar power. Three stages and two big boosters.", "MISSION-001",
          new[] { "Eng_L_Cluster4", "Tank_L_Short", "Tank_L_Long", "Tank_L_Long", "Dec_L", "Adp_L_M", "Eng_M_Vac", "Tank_M_Long", "Tank_M_Long", "Tank_M_Long", "Dec_M", "Eng_M_Vac", "Tank_M_Long", "Avi_M", "Head_Fairing_M" },
          "Pay_Rover", new[] { "Pow_Solar_L" }, "Booster_L", 2),
    };
}
