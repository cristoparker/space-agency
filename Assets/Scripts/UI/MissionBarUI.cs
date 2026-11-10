using TMPro;
using UnityEngine;

/// <summary>The small progress bar in the middle of the top bar: how far the tracked contract is (done objectives out of all of them).</summary>
public class MissionBarUI : MonoBehaviour
{
    RectTransform fill;
    TMP_Text pct;
    float timer;

    public static RectTransform Create(RectTransform topBar, Vector2 position)
    {
        var rt = new GameObject("Mission Bar", typeof(RectTransform)).GetComponent<RectTransform>();
        rt.SetParent(topBar, false);
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f); rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = position; rt.sizeDelta = new Vector2(74f, 13f);
        var ui = rt.gameObject.AddComponent<MissionBarUI>();
        var track = UIKit.At(rt, "Track", 0, 4.5f, 54, 4);
        UIKit.Tint(track, UIKit.T.track).raycastTarget = false;
        ui.fill = UIKit.At(track, "Fill", 0, 0, 10, 4);
        ui.fill.anchorMin = Vector2.zero; ui.fill.anchorMax = new Vector2(0f, 1f); ui.fill.offsetMin = ui.fill.offsetMax = Vector2.zero;
        UIKit.Tint(ui.fill, UIKit.T.teal).raycastTarget = false;
        ui.pct = UIKit.TextAt(rt, "Percent", 58, 0, 16, 13, "--", 6.3f, TextAlignmentOptions.MidlineLeft, true, UIKit.T.dim);
        ui.pct.raycastTarget = false;
        return rt;
    }

    void Update()
    {
        timer += Time.unscaledDeltaTime;
        if (timer < 0.3f) return;
        timer = 0f;
        var mm = MissionManager.Instance;
        var m = mm != null ? mm.Tracked : null;
        if (m == null) { fill.anchorMax = new Vector2(0f, 1f); pct.text = "--"; return; }
        var objs = MissionObjectives.For(m);
        int done = 0; foreach (var o in objs) if (o.done) done++;
        float frac = objs.Count > 0 ? (float)done / objs.Count : 0f;
        fill.anchorMax = new Vector2(Mathf.Clamp01(frac), 1f);
        pct.text = Mathf.RoundToInt(frac * 100f) + "%";
    }
}
