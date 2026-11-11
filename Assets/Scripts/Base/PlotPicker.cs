using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// "Pick a plot" mode: after choosing a facility in the Facility menu the free plots light up on the map, the player clicks one and
/// confirms in a pop-up. Esc or Cancel goes back. While it runs the normal building selection is switched off.
/// </summary>
public class PlotPicker : MonoBehaviour
{
    public static bool Active { get; private set; }
    static PlotPicker inst;

    public static void ResetStatics() { Active = false; inst = null; }

    BuildingDef def;
    Action<int> picked;
    Action cancelled;
    Camera cam;
    RectTransform canvasRect, banner;
    Canvas canvas;
    readonly List<(Transform t, int index, RectTransform label, TMP_Text text, Image bg)> plots = new List<(Transform, int, RectTransform, TMP_Text, Image)>();
    readonly MaterialPropertyBlock mpb = new MaterialPropertyBlock();
    int hover = -1;

    public static void Begin(BuildingDef def, Action<int> picked, Action cancelled)
    {
        if (inst == null) inst = new GameObject("Plot Picker").AddComponent<PlotPicker>();
        inst.Run(def, picked, cancelled);
    }

    public static void Cancel() { if (inst != null && Active) inst.End(true); }

    void Run(BuildingDef d, Action<int> p, Action c)
    {
        def = d; picked = p; cancelled = c;
        cam = Camera.main;
        var hud = FindAnyObjectByType<HudController>();
        if (hud == null || cam == null) { c?.Invoke(); return; }
        canvasRect = (RectTransform)hud.transform; canvas = hud.GetComponentInParent<Canvas>();
        Active = true; hover = -1;
        ClearUi();

        var root = GameObject.Find("Base Plots");
        var sim = BaseSim.Instance;
        if (root != null)
            for (int i = 0; i < root.transform.childCount; i++)
            {
                if (sim != null && !sim.PlotFree(i)) continue;
                var tr = root.transform.GetChild(i);
                var l = new GameObject("Plot Label " + (i + 1), typeof(RectTransform)).GetComponent<RectTransform>();
                l.SetParent(canvasRect, false);
                l.anchorMin = l.anchorMax = new Vector2(0.5f, 0.5f); l.pivot = new Vector2(0.5f, 0.5f); l.sizeDelta = new Vector2(46f, 14f);
                var bg = UIKit.Tint(l, UIKit.Wash(UIKit.T.teal, 0.85f));
                bg.raycastTarget = false;
                var tx = UIKit.Text(l, "P" + (i + 1) + "  free", 6.6f, TextAlignmentOptions.Center, true);
                plots.Add((tr, i, l, tx, bg));
            }

        // banner under the top bar
        banner = new GameObject("Plot Picker Banner", typeof(RectTransform)).GetComponent<RectTransform>();
        banner.SetParent(canvasRect, false);
        banner.anchorMin = banner.anchorMax = new Vector2(0.5f, 1f); banner.pivot = new Vector2(0.5f, 1f);
        banner.anchoredPosition = new Vector2(0f, -32f); banner.sizeDelta = new Vector2(290f, 24f);
        UIKit.Pill(banner);
        banner.gameObject.AddComponent<CanvasGroup>(); banner.gameObject.AddComponent<UIPopIn>();
        var t = UIKit.TextAt(banner, "Text", 10, 1, 205, 22, "Choose a plot for the <b>" + def.name + "</b>\n<size=80%><color=#9FB4C8>Click a highlighted plot. Esc cancels.</color></size>", 6.8f, TextAlignmentOptions.MidlineLeft);
        t.richText = true; t.textWrappingMode = TextWrappingModes.Normal; t.lineSpacing = -6f;
        UIKit.Btn(banner, "Cancel", 222, 4, 58, 16, UIKit.T.chip, () => End(true), 6.8f);
        if (plots.Count == 0) { Hud.Toast("There is no free plot."); End(true); }
    }

    void ClearUi()
    {
        foreach (var p in plots) if (p.label != null) Destroy(p.label.gameObject);
        plots.Clear();
        if (banner != null) Destroy(banner.gameObject);
    }

    void End(bool cancel)
    {
        foreach (var p in plots) Tint(p.t, false);
        ClearUi();
        Active = false;
        var c = cancelled; cancelled = null; picked = null;
        if (cancel) c?.Invoke();
    }

    void Tint(Transform t, bool on, float amount = 0f)
    {
        if (t == null) return;
        foreach (var r in t.GetComponentsInChildren<Renderer>())
        {
            if (!on) { r.SetPropertyBlock(null); continue; }
            var tint = Color.Lerp(Color.white, new Color(0.35f, 1f, 0.9f, 1f), amount);
            mpb.SetColor("_BaseColor", tint);
            r.SetPropertyBlock(mpb);
        }
    }

    static Bounds BoundsOf(Transform t)
    {
        var rs = t.GetComponentsInChildren<Renderer>();
        if (rs.Length == 0) return new Bounds(t.position, Vector3.one * 10f);
        var b = rs[0].bounds;
        foreach (var r in rs) b.Encapsulate(r.bounds);
        return b;
    }

    void Update()
    {
        if (!Active) return;
        var kb = Keyboard.current; var mouse = Mouse.current;
        if (kb != null && kb.escapeKey.wasPressedThisFrame && !UIConfirm.IsOpen) { End(true); return; }
        if (cam == null) return;

        // which free plot is under the pointer
        hover = -1;
        bool over = UnityEngine.EventSystems.EventSystem.current != null && UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject();
        if (mouse != null && !over && !UIConfirm.IsOpen)
        {
            var ray = cam.ScreenPointToRay(mouse.position.ReadValue());
            float best = float.MaxValue;
            foreach (var p in plots)
                if (BoundsOf(p.t).IntersectRay(ray, out float d) && d < best) { best = d; hover = p.index; }
        }

        float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 4f);
        foreach (var p in plots)
        {
            bool h = p.index == hover;
            Tint(p.t, true, h ? 1f : 0.35f + 0.35f * pulse);
            Vector3 vp = cam.WorldToViewportPoint(p.t.position + Vector3.up * 14f);
            bool vis = vp.z > 0f;
            p.label.gameObject.SetActive(vis);
            if (vis) p.label.anchoredPosition = new Vector2((vp.x - 0.5f) * canvasRect.rect.width, (vp.y - 0.5f) * canvasRect.rect.height);
            p.bg.color = h ? UIKit.T.green : UIKit.Wash(UIKit.T.teal, 0.85f);
            p.label.localScale = Vector3.one * (h ? 1.15f : 1f);
        }

        if (mouse != null && mouse.leftButton.wasPressedThisFrame && hover >= 0 && !UIConfirm.IsOpen)
        {
            int plot = hover; var d = def;
            var sim = BaseSim.Instance;
            string body = "Cost  <b>$" + BaseSim.CostOf(d, 1).ToString("0.#") + " M</b>\nTime  <b>" + Mathf.CeilToInt(BaseSim.DaysOf(d, 1)) + " days</b>\nPower  <b>" + BaseData.PowerOf(d, 1).ToString("0") + " MW</b>";
            UIConfirm.Show("Build the " + d.name + " on P" + (plot + 1) + "?", body, "Build here", () => { var cb = picked; End(false); cb?.Invoke(plot); }, null);
        }
    }
}
