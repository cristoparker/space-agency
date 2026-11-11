using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Visual options the player can change: how strong the bloom glow and the distance fog are. Saved between sessions.</summary>
public static class VisualSettings
{
    const string BloomKey = "SpaceAgency.BloomScale", FogKey = "SpaceAgency.FogPercent";
    static readonly Dictionary<Bloom, float> baseBloom = new Dictionary<Bloom, float>();
    public static void ResetStatics() { baseBloom.Clear(); }

    public static float Bloom01 { get => PlayerPrefs.GetFloat(BloomKey, 1f); set { PlayerPrefs.SetFloat(BloomKey, Mathf.Clamp(value, 0f, 2f)); ApplyBloom(); } }
    public static float Fog { get => PlayerPrefs.GetFloat(FogKey, 1f); set => PlayerPrefs.SetFloat(FogKey, Mathf.Clamp(value, 0f, 2f)); }

    /// <summary>Scales the authored bloom intensity of every post-processing volume by the player's setting.</summary>
    public static void ApplyBloom()
    {
        foreach (var v in Object.FindObjectsByType<Volume>(FindObjectsSortMode.None))
        {
            var prof = v.profile != null ? v.profile : null;
            if (prof == null || !prof.TryGet(out Bloom b)) continue;
            if (!baseBloom.TryGetValue(b, out float baseI)) { baseI = b.intensity.value; baseBloom[b] = baseI; }
            b.intensity.Override(baseI * Bloom01);
        }
    }
}

/// <summary>A simple horizontal slider: click or drag along the track.</summary>
public class UISlider : MonoBehaviour, IPointerDownHandler, IDragHandler
{
    public float min, max = 1f, value;
    public System.Action<float> onChange;
    RectTransform track, fill, handle;

    public static UISlider Create(Transform parent, float x, float y, float w, float min, float max, float value, System.Action<float> onChange)
    {
        var r = UIKit.At(parent, "Slider", x, y, w, 12);
        UIKit.Tint(r, Color.clear).raycastTarget = true;
        var s = r.gameObject.AddComponent<UISlider>();
        s.min = min; s.max = max; s.value = value; s.onChange = onChange;
        s.track = UIKit.At(r, "Track", 0, 4, w, 4); UIKit.Tint(s.track, new Color(1f, 1f, 1f, 0.16f)).raycastTarget = false;
        s.fill = UIKit.At(s.track, "Fill", 0, 0, 10, 4); UIKit.Fill(s.fill); UIKit.Tint(s.fill, UIKit.T.teal).raycastTarget = false;
        s.handle = UIKit.At(r, "Handle", 0, 1, 10, 10); UIKit.Tint(s.handle, Color.white).raycastTarget = false;
        s.Refresh();
        return s;
    }

    void Set(PointerEventData e)
    {
        var rt = (RectTransform)transform;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(rt, e.position, e.pressEventCamera, out var local);
        float u = Mathf.Clamp01((local.x - rt.rect.xMin) / Mathf.Max(1f, rt.rect.width));
        value = Mathf.Lerp(min, max, u);
        Refresh();
        onChange?.Invoke(value);
    }

    public void OnPointerDown(PointerEventData e) => Set(e);
    public void OnDrag(PointerEventData e) => Set(e);

    void Refresh()
    {
        float u = Mathf.InverseLerp(min, max, value);
        fill.anchorMax = new Vector2(u, 1f);
        var rt = (RectTransform)transform;
        handle.anchoredPosition = new Vector2(u * (rt.rect.width - 10f), handle.anchoredPosition.y);
    }
}

/// <summary>The settings window at the bottom right: music volume, bloom and fog strength, back to the main menu and exit.</summary>
public class SettingsPanelUI : MonoBehaviour
{
    const float W = 168f;
    RectTransform panel;
    TMP_Text musicVal, bloomVal, fogVal;

    public static SettingsPanelUI Create(RectTransform canvas)
    {
        var go = new GameObject("Settings", typeof(RectTransform));
        go.transform.SetParent(canvas, false);
        var prt = (RectTransform)go.transform;
        prt.anchorMin = Vector2.zero; prt.anchorMax = Vector2.one; prt.offsetMin = prt.offsetMax = Vector2.zero;
        var ui = go.AddComponent<SettingsPanelUI>();
        ui.Build();
        return ui;
    }

    public void Toggle() { panel.gameObject.SetActive(!panel.gameObject.activeSelf); if (panel.gameObject.activeSelf) panel.SetAsLastSibling(); }

    void Build()
    {
        const float h = 176f;
        panel = new GameObject("Settings Panel", typeof(RectTransform)).GetComponent<RectTransform>();
        panel.SetParent(transform, false);
        panel.anchorMin = panel.anchorMax = new Vector2(1f, 0f); panel.pivot = new Vector2(1f, 0f);
        panel.anchoredPosition = new Vector2(-8f, 8f + 21f + 5f); panel.sizeDelta = new Vector2(W, h);
        UIKit.Panel(panel);
        UIKit.TitleBar(panel, "Settings", UIKit.T.teal, W, () => panel.gameObject.SetActive(false));

        float y = 32f;
        UIKit.TextAt(panel, "Audio", 10, y, 80, 10, "AUDIO", 5.4f, TextAlignmentOptions.MidlineLeft, true, UIKit.T.dim).raycastTarget = false; y += 11f;
        var mp = MusicPlayer.Instance;
        UIKit.TextAt(panel, "L1", 10, y, 80, 10, "Music volume", 6.2f, TextAlignmentOptions.MidlineLeft).raycastTarget = false;
        musicVal = UIKit.TextAt(panel, "V1", W - 50, y, 40, 10, "", 6.2f, TextAlignmentOptions.MidlineRight, true); y += 11f;
        float vol = mp != null ? mp.volume : PlayerPrefs.GetFloat("SpaceAgency.MusicVolume", 0.55f);
        UISlider.Create(panel, 10, y, W - 20, 0f, 1f, vol, v => { if (MusicPlayer.Instance != null) MusicPlayer.Instance.SetVolume(v); musicVal.text = Mathf.RoundToInt(v * 100f) + "%"; });
        musicVal.text = Mathf.RoundToInt(vol * 100f) + "%"; y += 18f;

        UIKit.TextAt(panel, "Visual", 10, y, 80, 10, "VISUAL", 5.4f, TextAlignmentOptions.MidlineLeft, true, UIKit.T.dim).raycastTarget = false; y += 11f;
        UIKit.TextAt(panel, "L2", 10, y, 80, 10, "Bloom glow", 6.2f, TextAlignmentOptions.MidlineLeft).raycastTarget = false;
        bloomVal = UIKit.TextAt(panel, "V2", W - 50, y, 40, 10, "", 6.2f, TextAlignmentOptions.MidlineRight, true); y += 11f;
        UISlider.Create(panel, 10, y, W - 20, 0f, 2f, VisualSettings.Bloom01, v => { VisualSettings.Bloom01 = v; bloomVal.text = Mathf.RoundToInt(v * 100f) + "%"; });
        bloomVal.text = Mathf.RoundToInt(VisualSettings.Bloom01 * 100f) + "%"; y += 15f;
        UIKit.TextAt(panel, "L3", 10, y, 80, 10, "Fog intensity", 6.2f, TextAlignmentOptions.MidlineLeft).raycastTarget = false;
        fogVal = UIKit.TextAt(panel, "V3", W - 50, y, 40, 10, "", 6.2f, TextAlignmentOptions.MidlineRight, true); y += 11f;
        UISlider.Create(panel, 10, y, W - 20, 0f, 2f, VisualSettings.Fog, v => { VisualSettings.Fog = v; fogVal.text = Mathf.RoundToInt(v * 100f) + "%"; });
        fogVal.text = Mathf.RoundToInt(VisualSettings.Fog * 100f) + "%"; y += 18f;

        UIKit.Btn(panel, "Main menu", 10, y, (W - 28f) * 0.5f, 16, UIKit.T.chip, () => UIConfirm.Show("Main menu", "Go back to the main menu? Your progress is saved automatically.", "Main menu", ToMenu), 6.6f);
        UIKit.SoftBtn(panel, "Exit game", 10 + (W - 28f) * 0.5f + 8f, y, (W - 28f) * 0.5f, 16, UIKit.T.red, () => UIConfirm.Show("Exit game", "Close the game? Your progress is saved automatically.", "Exit", Quit), 6.6f);
        panel.gameObject.AddComponent<CanvasGroup>(); panel.gameObject.AddComponent<UIPopIn>();
        panel.gameObject.SetActive(false);
    }

    static void ToMenu()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene("1. Main Menu");
    }

    static void Quit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
