using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The Objectives button at the left of the screen and its panel. Every running contract is a collapsible block; inside, the steps
/// (training, testing, build and deploy a rocket, launch) are listed in order: finished ones get a tick, the one in progress a bar.
/// </summary>
public class ObjectiveMenuUI : MonoBehaviour
{
    public const float PW = 196f, PanelH = 200f;
    RectTransform canvasRt, panel, content;
    CanvasGroup panelGroup;
    Image buttonBg;
    TMP_Text badge;
    Sprite flag, check;
    bool open;
    public bool IsOpen => open;
    float timer;
    readonly HashSet<string> collapsed = new HashSet<string>();
    readonly List<(MissionObjective o, Image fill, TMP_Text status)> live = new List<(MissionObjective, Image, TMP_Text)>();

    public static ObjectiveMenuUI Create(RectTransform canvas)
    {
        var go = new GameObject("Objectives", typeof(RectTransform));
        go.transform.SetParent(canvas, false);
        var ui = go.AddComponent<ObjectiveMenuUI>();
        ui.Build(canvas);
        return ui;
    }

    void Build(RectTransform canvas)
    {
        canvasRt = canvas;
        flag = Resources.Load<Sprite>("UIIcons/icon_flag"); check = Resources.Load<Sprite>("UIIcons/icon_check");
        var rt = (RectTransform)transform;
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;

        // the button, at the left edge under the top bar
        var btn = new GameObject("Objectives Button", typeof(RectTransform)).GetComponent<RectTransform>();
        btn.SetParent(rt, false);
        btn.anchorMin = btn.anchorMax = new Vector2(0f, 1f); btn.pivot = new Vector2(0f, 1f);
        btn.anchoredPosition = new Vector2(8f, -32f); btn.sizeDelta = new Vector2(66f, 22f);
        UIKit.Glass(btn);
        var inner = UIKit.At(btn, "Hit", 3, 3, 60, 16);
        var b = UIKit.MakeButton(inner, "", UIKit.T.chip, Toggle);
        buttonBg = (Image)b.targetGraphic;
        UIKit.IconAt(inner, flag, 4, 3, 10, 10, UIKit.T.ink);
        UIKit.TextAt(inner, "Label", 16, 0, 42, 16, "Objectives", 6.2f, TextAlignmentOptions.MidlineLeft, true);
        var bdg = UIKit.At(btn, "Badge", 38, -3, 14, 10);
        bdg.anchorMin = bdg.anchorMax = new Vector2(0f, 1f); bdg.pivot = new Vector2(0.5f, 0.5f); bdg.anchoredPosition = new Vector2(60f, -3f);
        UIKit.Tint(bdg, UIKit.T.orange);
        badge = UIKit.Text(bdg, "0", 5.6f, TextAlignmentOptions.Center, true);
        btn.gameObject.AddComponent<TooltipTrigger>().text = "Objectives\nWhat to do for each contract.";

        // the panel
        panel = new GameObject("Objectives Panel", typeof(RectTransform)).GetComponent<RectTransform>();
        panel.SetParent(rt, false);
        panel.anchorMin = panel.anchorMax = new Vector2(0f, 1f); panel.pivot = new Vector2(0f, 1f);
        panel.anchoredPosition = new Vector2(8f, -58f); panel.sizeDelta = new Vector2(PW, PanelH);
        UIKit.Panel(panel);
        UIKit.TitleBar(panel, "Objectives", UIKit.T.orange, PW, () => SetOpen(false));
        content = UIKit.ScrollList(panel, "List", 5, 28, PW - 10, PanelH - 33, null, 4f);
        panelGroup = panel.gameObject.AddComponent<CanvasGroup>();
        panel.gameObject.AddComponent<UIPopIn>();
        panel.gameObject.SetActive(false);

        if (MissionManager.Instance != null) MissionManager.Instance.Changed += Refresh;
        if (BaseSim.Instance != null) BaseSim.Instance.Changed += Refresh;
        if (DeploymentManager.Instance != null) DeploymentManager.Instance.Changed += Refresh;
        Refresh();
    }

    void OnDestroy()
    {
        if (MissionManager.Instance != null) MissionManager.Instance.Changed -= Refresh;
        if (BaseSim.Instance != null) BaseSim.Instance.Changed -= Refresh;
        if (DeploymentManager.Instance != null) DeploymentManager.Instance.Changed -= Refresh;
    }

    public void Toggle() => SetOpen(!open);

    void SetOpen(bool on)
    {
        open = on;
        panel.gameObject.SetActive(on);
        buttonBg.color = on ? UIKit.T.orange : UIKit.T.chip;
        if (on) { panel.SetAsLastSibling(); Refresh(); }
    }

    void Update()
    {
        timer += Time.unscaledDeltaTime;
        if (timer < 0.4f) return;
        timer = 0f;
        var mm = MissionManager.Instance;
        if (badge != null && mm != null) badge.text = (mm.Running.Count + mm.Pending.Count).ToString();
        if (!open) return;
        string sig = Signature();
        if (sig != lastSig) { Refresh(); return; }
        foreach (var (o, fill, status) in live)                       // nothing changed in the list: only move the bars
            if (fill != null) ((RectTransform)fill.transform).anchorMax = new Vector2(Mathf.Clamp01(o.progress), 1f);
    }

    string lastSig = "";

    string Signature()
    {
        var mm = MissionManager.Instance;
        if (mm == null) return "";
        var sb = new System.Text.StringBuilder();
        foreach (var m in mm.Running)
        {
            sb.Append(m.Definition.id);
            foreach (var o in MissionObjectives.For(m)) sb.Append('|').Append((int)o.state).Append(o.status);
        }
        return sb.ToString();
    }

    void Refresh()
    {
        if (content == null) return;
        var mm = MissionManager.Instance;
        if (badge != null && mm != null) badge.text = (mm.Running.Count + mm.Pending.Count).ToString();
        if (!open) return;
        lastSig = Signature();
        UIKit.Clear(content);
        live.Clear();
        if (mm == null || mm.Running.Count == 0)
        {
            var e = UIKit.At(content, "Empty", 0, 0, 100, 64);
            e.gameObject.AddComponent<LayoutElement>().preferredHeight = 64;
            UIKit.Text(e, "No active contract.\nAccept one in Contracts and its steps appear here.", 6.6f, TextAlignmentOptions.Center, false, UIKit.T.dim).textWrappingMode = TextWrappingModes.Normal;
            var go = UIKit.At(content, "Go", 0, 0, 100, 16);
            go.gameObject.AddComponent<LayoutElement>().preferredHeight = 16;
            UIKit.MakeButton(go, "Open Contracts", UIKit.T.teal, () => Panels.Open("contracts"), 6.6f);
            return;
        }
        MissionBlock(mm.Running[0], true);
    }

    void MissionBlock(MissionInstance m, bool tracked)
    {
        var T = UIKit.T;
        var objs = MissionObjectives.For(m);
        int doneCount = 0; foreach (var o in objs) if (o.done) doneCount++;
        string id = m.Definition.id;
        float w = PW - 16;

        // header: the current mission's name and how many steps are done (no expanding: there is only ever one mission)
        var head = UIKit.At(content, "Mission", 0, 0, w, 24);
        head.gameObject.AddComponent<LayoutElement>().preferredHeight = 24;
        UIKit.Tint(head, new Color(1f, 1f, 1f, 0.14f)).raycastTarget = false;
        var nm = UIKit.TextAt(head, "Name", 6, 1, w - 46, 13, m.Definition.displayName, 6.9f, TextAlignmentOptions.MidlineLeft, true);
        nm.overflowMode = TextOverflowModes.Ellipsis; nm.raycastTarget = false;
        UIKit.TextAt(head, "Count", w - 38, 1, 34, 13, doneCount + " / " + objs.Count, 6.2f, TextAlignmentOptions.MidlineRight, true, T.dim).raycastTarget = false;
        var bar = UIKit.At(head, "Bar", 6, 17, w - 12, 3);
        UIKit.Tint(bar, T.track).raycastTarget = false;
        var bf = UIKit.At(bar, "Fill", 0, 0, 10, 3);
        bf.anchorMin = Vector2.zero; bf.anchorMax = new Vector2(Mathf.Clamp01((float)doneCount / Mathf.Max(1, objs.Count)), 1f); bf.offsetMin = bf.offsetMax = Vector2.zero;
        UIKit.Tint(bf, doneCount == objs.Count ? T.green : T.teal).raycastTarget = false;

        foreach (var o in objs) ObjectiveRow(o, w);
    }

    void ObjectiveRow(MissionObjective o, float w)
    {
        var T = UIKit.T;
        bool running = o.state == ObjectiveState.Running, available = o.state == ObjectiveState.Available, locked = o.state == ObjectiveState.Locked;
        bool big = running || (available && o.actionLabel != null && o.action != null);
        float h = big ? 36f : 24f;
        var row = UIKit.At(content, "Objective", 0, 0, w, h);
        row.gameObject.AddComponent<LayoutElement>().preferredHeight = h;
        UIKit.Tint(row, running ? new Color(1f, 0.7f, 0.3f, 0.18f) : available ? new Color(1f, 1f, 1f, 0.09f) : new Color(1f, 1f, 1f, 0.04f));

        // tick: green with a check when done, orange when it is being worked on, a hollow dot when it can start, a dim one when locked
        var tick = UIKit.At(row, "Tick", 5, 5, 12, 12);
        var tickImg = UIKit.Tint(tick, o.done ? T.green : running ? T.orange : available ? new Color(1f, 1f, 1f, 0.34f) : new Color(1f, 1f, 1f, 0.12f));
        tickImg.raycastTarget = false;
        if (o.done) UIKit.IconAt(tick, check, 2, 2, 8, 8, new Color(0.05f, 0.15f, 0.1f, 1f));

        Color tc = o.done ? new Color(0.62f, 1f, 0.76f, 1f) : locked ? T.dim : T.ink;
        var t = UIKit.TextAt(row, "Title", 22, 2, w - 70, 11, o.title, 6.6f, TextAlignmentOptions.MidlineLeft, true, tc);
        t.overflowMode = TextOverflowModes.Ellipsis;
        string line = o.state == ObjectiveState.Done || o.state == ObjectiveState.Locked || o.state == ObjectiveState.Running ? o.status : (o.status == "Ready to start" || o.status == o.detail ? o.detail : o.status);
        var d = UIKit.TextAt(row, "Detail", 22, 12, w - 28, 9, line, 5.4f, TextAlignmentOptions.MidlineLeft, false, locked ? new Color(1f, 0.75f, 0.5f, 0.75f) : T.dim);
        d.overflowMode = TextOverflowModes.Ellipsis;
        if (o.done) UIKit.TextAt(row, "Done", w - 40, 2, 36, 11, "Done", 5.8f, TextAlignmentOptions.MidlineRight, true, tc);

        if (running)
        {
            var track = UIKit.At(row, "Progress", 22, 25, w - 28, 5);
            UIKit.Tint(track, T.track);
            var fill = UIKit.At(track, "Fill", 0, 0, 10, 5);
            fill.anchorMin = Vector2.zero; fill.anchorMax = new Vector2(Mathf.Clamp01(o.progress), 1f); fill.offsetMin = fill.offsetMax = Vector2.zero;
            var fi = UIKit.Tint(fill, T.orange);
            live.Add((o, fi, null));
        }
        else if (big)
        {
            var act = o.action;
            UIKit.Btn(row, o.actionLabel, 22, 21, 60, 13, T.teal, () => act(), 6f);
        }
    }
}
