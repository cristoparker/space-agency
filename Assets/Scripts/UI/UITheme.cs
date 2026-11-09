using TMPro;
using UnityEngine;

/// <summary>
/// The Space Agency UI design system: one asset (Resources/UITheme) that points at the existing glass materials,
/// rounded sprite and fonts, plus the accent colours used by the reference layouts.
/// It never changes the glass shader or the materials - it only reuses them.
/// </summary>
[CreateAssetMenu(menuName = "Space Agency/UI Theme", fileName = "UITheme")]
public class UITheme : ScriptableObject
{
    [Header("Existing glass look (not modified)")]
    public Material glass;          // UI-Glass: blur panel, top-level only
    public Material flat;           // UI-GlassFlat: translucent fill for anything inside a panel
    public Sprite round;            // ui-glass-round
    public TMP_FontAsset body, head;
    public Sprite closeIcon;

    [Header("Palette")]
    public Color dark = new Color(0.04f, 0.07f, 0.12f, 1f);
    public Color ink = new Color(0.93f, 0.96f, 1f, 1f);
    public Color dim = new Color(0.93f, 0.96f, 1f, 0.55f);
    public Color well = new Color(1f, 1f, 1f, 0.10f);              // sunken area inside a panel
    public Color chip = new Color(1f, 1f, 1f, 0.16f);              // neutral button / chip
    public Color teal = new Color(0.30f, 0.76f, 0.88f, 0.95f);     // contracts, nav
    public Color orange = new Color(1f, 0.62f, 0.14f, 0.95f);      // rocket designer
    public Color purple = new Color(0.72f, 0.48f, 1f, 0.95f);      // payload / satellite designer
    public Color green = new Color(0.14f, 0.82f, 0.42f, 0.95f);    // accept / equip / confirm
    public Color priceText = new Color(0.55f, 1f, 0.7f, 1f);      // prices on parts
    public Color red = new Color(1f, 0.30f, 0.40f, 0.95f);         // decline / abandon / delete
    public Color blue = new Color(0.27f, 0.58f, 0.95f, 0.95f);     // blueprints

    static UITheme cur;
    public static UITheme Current
    {
        get
        {
            if (cur == null) cur = Resources.Load<UITheme>("UITheme");
            return cur;
        }
    }
}
