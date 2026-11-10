using TMPro;
using UnityEngine;

/// <summary>
/// The Space Agency UI design system, Apple-inspired: graphite glass with a bright top edge, system colours,
/// clean sans-serif type, soft shadows and capsule controls. One asset (Resources/UITheme) points at the glass materials,
/// the rounded sprite and the font; GameUISetup writes the palette below into it.
/// </summary>
[CreateAssetMenu(menuName = "Space Agency/UI Theme", fileName = "UITheme")]
public class UITheme : ScriptableObject
{
    [Header("Glass")]
    public Material glass;          // UI-Glass: blurred backdrop, top-level panels only
    public Material flat;           // UI-GlassFlat: translucent fill for anything inside a panel
    public Material frost;          // UI-GlassFrost: strongly blurred glass for the cards inside a panel (frosted, the panel itself stays clear)
    public Sprite round;            // ui-glass-round
    public TMP_FontAsset body, head;
    public Sprite closeIcon;

    [Header("Palette (Apple system colours, dark appearance)")]
    public Color dark = new Color(0.085f, 0.095f, 0.13f, 1f);      // glass tint: graphite with a hint of blue
    public Color ink = new Color(1f, 1f, 1f, 0.96f);                // label
    public Color dim = new Color(0.92f, 0.94f, 1f, 0.60f);          // secondary label
    public Color card = new Color(0.07f, 0.09f, 0.14f, 1f);          // inner card: tint of the frosted glass (a cool graphite, not black)
    public Color well = new Color(1f, 1f, 1f, 0.07f);              // grouped / sunken area
    public Color chip = new Color(1f, 1f, 1f, 0.13f);              // neutral control fill
    public Color track = new Color(0f, 0f, 0f, 0.28f);             // segmented control track, input fields
    public Color teal = new Color(0.04f, 0.52f, 1f, 1f);           // system blue: primary accent, selection
    public Color orange = new Color(1f, 0.62f, 0.04f, 1f);         // rocket designer
    public Color purple = new Color(0.75f, 0.35f, 0.95f, 1f);      // satellite designer
    public Color green = new Color(0.19f, 0.82f, 0.35f, 1f);       // accept / equip / confirm
    public Color red = new Color(1f, 0.27f, 0.23f, 1f);            // decline / destructive
    public Color blue = new Color(0.37f, 0.36f, 0.9f, 1f);         // indigo: blueprints
    public Color priceText = new Color(0.19f, 0.82f, 0.35f, 1f);   // prices on parts

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
