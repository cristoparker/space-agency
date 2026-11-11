using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Shows the running cost: a small icon popup floats up from the Power Grid every game hour ("-84 MWh, -$0.5M", with the extra load in orange after an
/// upgrade), a warning bar appears when the power is short, and hovering the money in the top bar opens a panel with the budget and the daily cost.
/// </summary>
public class ResourceUI : MonoBehaviour
{
    RectTransform canvasRt, warning, panel;
    TMP_Text warningText, panelText;
    float showAfter, accMWh, accMoney, accDelta; int accHours;
    Camera cam;
    Bounds gridBounds; float boundsAge = 99f;
    bool hoverMoney;

    public static ResourceUI Create(RectTransform canvas, RectTransform moneyRt)
    {
        var go = new GameObject("Resource UI", typeof(RectTransform));
        go.transform.SetParent(canvas, false);
        var ui = go.AddComponent<ResourceUI>();
        ui.Build(canvas, moneyRt);
        return ui;
    }

    void Build(RectTransform canvas, RectTransform moneyRt)
    {
        canvasRt = canvas;
        var rt = (RectTransform)transform;
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;

        // warning bar under the top bar
        warning = new GameObject("Power Warning", typeof(RectTransform)).GetComponent<RectTransform>();
        warning.SetParent(rt, false);
        warning.anchorMin = warning.anchorMax = new Vector2(0.5f, 1f); warning.pivot = new Vector2(0.5f, 1f);
        warning.anchoredPosition = new Vector2(0f, -50f); warning.sizeDelta = new Vector2(250f, 16f);
        UIKit.Pill(warning, new Color(0.35f, 0.08f, 0.07f, 0.96f));
        UIKit.IconAt(warning, Resources.Load<Sprite>("UIIcons/icon_power"), 6, 3, 10, 10, new Color(1f, 0.6f, 0.45f, 1f));
        warningText = UIKit.TextAt(warning, "T", 20, 0, 226, 16, "", 6.2f, TextAlignmentOptions.MidlineLeft, true, new Color(1f, 0.82f, 0.74f, 1f));
        warning.gameObject.SetActive(false);

        // the money hover panel
        panel = new GameObject("Money Panel", typeof(RectTransform)).GetComponent<RectTransform>();
        panel.SetParent(rt, false);
        panel.anchorMin = panel.anchorMax = new Vector2(1f, 1f); panel.pivot = new Vector2(1f, 1f);
        panel.anchoredPosition = new Vector2(-8f, -30f); panel.sizeDelta = new Vector2(168f, 86f);
        UIKit.Panel(panel);
        UIKit.TextAt(panel, "Title", 10, 7, 120, 11, "Budget and running cost", 7.2f, TextAlignmentOptions.MidlineLeft, true).raycastTarget = false;
        panelText = UIKit.TextAt(panel, "Body", 10, 21, 148, 62, "", 6.2f, TextAlignmentOptions.TopLeft);
        panelText.textWrappingMode = TextWrappingModes.Normal; panelText.lineSpacing = -4f; panelText.richText = true; panelText.raycastTarget = false;
        panel.gameObject.SetActive(false);

        if (moneyRt != null)
        {
            var hv = moneyRt.gameObject.GetComponent<HoverEvents>() ?? moneyRt.gameObject.AddComponent<HoverEvents>();
            hv.onEnter = () => { hoverMoney = true; panel.gameObject.SetActive(true); panel.SetAsLastSibling(); RefreshPanel(); };
            hv.onExit = () => { hoverMoney = false; panel.gameObject.SetActive(false); };
        }
        if (ResourceFlow.Instance != null) ResourceFlow.Instance.HourUsed += OnHour;
    }

    void OnDestroy() { if (ResourceFlow.Instance != null) ResourceFlow.Instance.HourUsed -= OnHour; }

    void Update()
    {
        bool shortNow = ResourceFlow.PowerShort;
        if (warning.gameObject.activeSelf != shortNow) warning.gameObject.SetActive(shortNow);
        if (shortNow)
        {
            var sim = BaseSim.Instance;
            warningText.text = "Power short: " + (sim != null ? sim.PowerDemand.ToString("0") + " / " + sim.PowerSupply.ToString("0") + " MW" : "") + ". Work has stopped. Upgrade the Power Grid.";
        }
        if (hoverMoney) RefreshPanel();
    }

    void RefreshPanel()
    {
        var sim = BaseSim.Instance; if (sim == null) return;
        float demand = sim.PowerDemand, supply = sim.PowerSupply, daily = ResourceFlow.DailyCostM;
        float grid = ResourceFlow.GridUpkeepOf(sim.PowerLevel), bld = demand * ResourceFlow.MoneyPerMWDay;
        long money = Agency.Money;
        string runway = daily > 0f ? Mathf.FloorToInt(money / 1e6f / daily) + " days" : "-";
        int load = supply > 0f ? Mathf.RoundToInt(demand / supply * 100f) : 0;
        string loadCol = load >= 100 ? "#FF8C8C" : load >= 85 ? "#FFB878" : "#8FE3B0";
        panelText.text =
            "Budget  <b>$" + UIKit.Money(money) + "</b>\n" +
            "Daily cost  <b>$" + daily.ToString("0.0", CultureInfo.InvariantCulture) + " M</b>\n" +
            "<color=#9FB4C8>  buildings  $" + bld.ToString("0.0", CultureInfo.InvariantCulture) + " M   ·   grid  $" + grid.ToString("0.0", CultureInfo.InvariantCulture) + " M</color>\n" +
            "Lasts about  <b>" + runway + "</b> without income\n" +
            "Power  <b>" + demand.ToString("0") + " / " + supply.ToString("0") + " MW</b>  <color=" + loadCol + ">(" + load + "%)</color>\n" +
            "<color=#9FB4C8>Average use  " + (demand * 24f).ToString("0") + " MWh per day</color>";
    }

    // ---- the hourly popup
    void OnHour(float mwh, float money, float delta, int hours)
    {
        accMWh += mwh; accMoney += money; accDelta += delta; accHours += hours;
        if (Time.unscaledTime < showAfter) return;                               // when the game runs fast, hours are added up into one popup
        showAfter = Time.unscaledTime + 1.6f;
        Popup(accMWh, accMoney, accDelta, accHours);
        accMWh = accMoney = accDelta = 0f; accHours = 0;
    }

    Vector3 GridPoint()
    {
        boundsAge += Time.unscaledDeltaTime;
        if (boundsAge > 2f)
        {
            boundsAge = 0f;
            var root = GameObject.Find("Base Buildings");
            var t = root != null ? root.transform.Find("PowerGrid") : null;
            var rs = t != null ? t.GetComponentsInChildren<Renderer>() : null;
            if (rs != null && rs.Length > 0 && rs[0] != null) { gridBounds = rs[0].bounds; foreach (var r in rs) if (r.enabled && r.gameObject.activeInHierarchy) gridBounds.Encapsulate(r.bounds); }
            else gridBounds = new Bounds(new Vector3(-375f, 20f, 160f), Vector3.one * 10f);
        }
        return new Vector3(gridBounds.center.x, gridBounds.max.y + 4f, gridBounds.center.z);
    }

    void Popup(float mwh, float money, float delta, int hours)
    {
        if (cam == null) cam = Camera.main;
        if (cam == null) return;
        var go = new GameObject("Hour Popup", typeof(RectTransform)).GetComponent<RectTransform>();
        go.SetParent(transform, false);
        go.sizeDelta = new Vector2(76f, delta > 0.5f ? 30f : 21f);
        go.anchorMin = go.anchorMax = new Vector2(0.5f, 0.5f); go.pivot = new Vector2(0.5f, 0f);
        UIKit.IconAt(go, Resources.Load<Sprite>("UIIcons/icon_power"), 4, 3, 8, 8, new Color(1f, 0.85f, 0.4f, 1f));
        Shadowed(go, "E", 14, 1.5f, 60, 10, "-" + mwh.ToString("0") + " MWh" + (hours > 1 ? "  (" + hours + " h)" : ""), 6.2f, TextAlignmentOptions.MidlineLeft, true, new Color(1f, 0.92f, 0.65f, 1f)).raycastTarget = false;
        UIKit.IconAt(go, Resources.Load<Sprite>("UIIcons/icon_money"), 4, 13, 8, 8, new Color(0.62f, 1f, 0.76f, 1f));
        Shadowed(go, "M", 14, 11.5f, 60, 10, "-$" + money.ToString("0.00", CultureInfo.InvariantCulture) + " M", 6.2f, TextAlignmentOptions.MidlineLeft, true, new Color(0.7f, 1f, 0.82f, 1f)).raycastTarget = false;
        if (delta > 0.5f) Shadowed(go, "D", 14, 21.5f, 60, 8, "+" + delta.ToString("0") + " MW (upgrade)", 5.2f, TextAlignmentOptions.MidlineLeft, true, new Color(1f, 0.72f, 0.3f, 1f)).raycastTarget = false;
        var p = go.gameObject.AddComponent<FloatingPopup>();
        p.Init(this, canvasRt, cam, GridPoint());
    }

    /// <summary>Text on the open scene has no backing, so a soft dark copy just behind it keeps it readable on any background.</summary>
    static TMP_Text Shadowed(Transform parent, string name, float x, float y, float w, float h, string text, float size, TextAlignmentOptions al, bool head, Color color)
    {
        foreach (var o in new[] { new Vector2(0.45f, 0.45f), new Vector2(-0.35f, 0.35f) })
            UIKit.TextAt(parent, name + " shadow", x + o.x, y + o.y, w, h, text, size, al, head, new Color(0f, 0f, 0f, 0.7f)).raycastTarget = false;
        return UIKit.TextAt(parent, name, x, y, w, h, text, size, al, head, color);
    }

    class FloatingPopup : MonoBehaviour
    {
        ResourceUI owner; RectTransform canvasRt; Camera cam; Vector3 world; float t; CanvasGroup cg;
        public void Init(ResourceUI o, RectTransform c, Camera cm, Vector3 w) { owner = o; canvasRt = c; cam = cm; world = w; cg = gameObject.AddComponent<CanvasGroup>(); cg.blocksRaycasts = false; Place(); }
        void Place()
        {
            var vp = cam.WorldToViewportPoint(world);
            ((RectTransform)transform).gameObject.SetActive(vp.z > 0f);
            var r = canvasRt.rect;
            ((RectTransform)transform).anchoredPosition = new Vector2((vp.x - 0.5f) * r.width, (vp.y - 0.5f) * r.height + 4f + t * 16f);
        }
        void Update()
        {
            t += Time.unscaledDeltaTime / 3f;                      // rises and fades over about three seconds
            if (t >= 1f) { Destroy(gameObject); return; }
            cg.alpha = t < 0.15f ? t / 0.15f : Mathf.Clamp01((1f - t) / 0.45f);
            Place();
        }
    }
}

/// <summary>Lets a UI element tell something when the pointer enters or leaves it.</summary>
public class HoverEvents : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    public System.Action onEnter, onExit;
    public void OnPointerEnter(PointerEventData e) => onEnter?.Invoke();
    public void OnPointerExit(PointerEventData e) => onExit?.Invoke();
}
