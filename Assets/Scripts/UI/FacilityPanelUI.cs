using System.Globalization;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Facility window: every building, whether it is built, and construction (money + game time) for the ones that are not.</summary>
public class FacilityPanelUI : MonoBehaviour
{
    public const float W = 198f, H = 270f;
    bool built;
    RectTransform list;
    float refreshTimer;
    readonly System.Collections.Generic.List<(BaseJob job, Image fill, TMP_Text label)> live = new System.Collections.Generic.List<(BaseJob, Image, TMP_Text)>();

    public void Init()
    {
        var r = (RectTransform)transform;
        r.anchorMin = r.anchorMax = new Vector2(1f, 1f); r.pivot = new Vector2(1f, 1f);
        r.anchoredPosition = new Vector2(-8f, -32f); r.sizeDelta = new Vector2(W, H);
        UIKit.Glass(r);
        UIKit.TitleBar(r, "Facility", UIKit.T.teal, W, () => gameObject.SetActive(false));
        list = UIKit.ScrollList(r, "List", 6, 28, W - 12, H - 34, null, 5f);
        built = true;
    }

    void OnEnable()
    {
        if (!built) return;
        transform.SetAsLastSibling();
        if (BaseSim.Instance != null) BaseSim.Instance.Changed += Refresh;
        Agency.Changed += Refresh;
        Refresh();
    }

    void OnDisable()
    {
        if (BaseSim.Instance != null) BaseSim.Instance.Changed -= Refresh;
        Agency.Changed -= Refresh;
    }

    void Update()
    {
        refreshTimer += Time.unscaledDeltaTime;
        if (refreshTimer < 0.25f) return;
        refreshTimer = 0f;
        foreach (var (job, fill, label) in live)
        {
            if (fill == null) continue;
            ((RectTransform)fill.transform).anchorMax = new Vector2(Mathf.Clamp01(BaseSim.Progress(job)), 1f);
            label.text = "Building  ·  " + BaseSim.DaysLeft(job) + " days left";
        }
    }

    void Refresh()
    {
        if (!built) return;
        var sim = BaseSim.Instance;
        UIKit.Clear(list);
        live.Clear();
        if (sim == null) return;
        var T = UIKit.T;
        float w = W - 16;
        foreach (var b in BaseData.Buildings)
        {
            bool isBuilt = sim.IsBuilt(b.id);
            var job = sim.Job(b.id);
            float h = isBuilt ? 40 : 56;
            var card = UIKit.At(list, b.id, 0, 0, w, h);
            card.gameObject.AddComponent<LayoutElement>().preferredHeight = h;
            UIKit.Tint(card, new Color(0f, 0f, 0f, 0.18f));
            UIKit.TextAt(card, "Name", 7, 4, w * 0.6f, 11, b.name, 7.8f, TextAlignmentOptions.MidlineLeft, true);
            string state = isBuilt ? "Operational" : job != null ? "Under construction" : "Not built";
            UIKit.TextAt(card, "State", w * 0.4f, 4, w * 0.6f - 7, 11, state, 6.3f, TextAlignmentOptions.MidlineRight, true,
                isBuilt ? new Color(0.62f, 1f, 0.76f, 1f) : job != null ? new Color(1f, 0.75f, 0.4f, 1f) : T.dim);
            var d = UIKit.TextAt(card, "Desc", 7, 15, w - 14, 16, b.description, 6.2f, TextAlignmentOptions.TopLeft, false, T.dim);
            d.textWrappingMode = TextWrappingModes.Normal; d.lineSpacing = -6f;
            if (isBuilt)
            {
                var hosts = BaseData.Programs.Where(p => p.building == b.id).Select(p => p.group == "sci" ? "Scientist training" : p.group == "pilot" ? "Pilot training" : p.name).Distinct();
                var s = string.Join(", ", hosts);
                if (s.Length > 0) UIKit.TextAt(card, "Hosts", 7, 28, w - 14, 9, "Hosts: " + s, 5.8f, TextAlignmentOptions.MidlineLeft, false, T.dim);
            }
            else if (job != null)
            {
                var track = UIKit.At(card, "Progress", 7, 36, w - 14, 14);
                UIKit.Tint(track, T.track);
                var fillRt = UIKit.At(track, "Fill", 0, 0, 10, 14);
                fillRt.anchorMin = new Vector2(0, 0); fillRt.anchorMax = new Vector2(BaseSim.Progress(job), 1); fillRt.pivot = new Vector2(0, 0.5f);
                fillRt.offsetMin = Vector2.zero; fillRt.offsetMax = Vector2.zero;
                var fill = UIKit.Tint(fillRt, T.orange);
                var label = UIKit.Text(track, "", 6.5f, TextAlignmentOptions.Center, true);
                live.Add((job, fill, label));
            }
            else
            {
                string why = sim.WhyNotBuild(b);
                string text = "Build  ·  $" + b.costM.ToString("0.#", CultureInfo.InvariantCulture) + " M  ·  " + Mathf.CeilToInt(b.days) + " days";
                if (why == null) UIKit.Btn(card, text, 7, 36, w - 14, 14, T.teal, () => sim.StartBuild(b), 6.8f);
                else { var bt = UIKit.Btn(card, why, 7, 36, w - 14, 14, T.chip, null, 6.5f, true, T.dim); bt.interactable = false; }
            }
        }
    }
}
