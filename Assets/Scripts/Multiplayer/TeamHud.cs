using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// The in-game side of team play: a Team button at the top right (opposite the Objectives button), the Team Play window,
/// and a short chat feed under the button that fades after a few seconds. Press Enter to open the chat.
/// </summary>
public class TeamHud : MonoBehaviour
{
    MultiplayerUI window;
    RectTransform feed;
    TMP_Text buttonLabel, badge;
    readonly List<(GameObject go, float until)> feedLines = new List<(GameObject, float)>();
    int unread;

    public static TeamHud Create(RectTransform canvas)
    {
        var go = new GameObject("Team HUD", typeof(RectTransform));
        go.transform.SetParent(canvas, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
        var hud = go.AddComponent<TeamHud>();
        hud.Build(canvas);
        return hud;
    }

    void Build(RectTransform canvas)
    {
        var T = UIKit.T;
        var btn = new GameObject("Team Button", typeof(RectTransform)).GetComponent<RectTransform>();
        btn.SetParent(transform, false);
        btn.anchorMin = btn.anchorMax = new Vector2(1f, 1f); btn.pivot = new Vector2(1f, 1f);
        btn.anchoredPosition = new Vector2(-8f, -36f); btn.sizeDelta = new Vector2(66f, 22f);
        var glass = UIKit.Glass(btn, true, true);
        var b = btn.gameObject.AddComponent<Button>();
        b.targetGraphic = glass; b.onClick.AddListener(Toggle);
        UIKit.StyleButton(b);
        UIHover.Attach(btn.gameObject);
        buttonLabel = UIKit.Text(btn, "Team", 8f, TextAlignmentOptions.Center, true);
        btn.gameObject.AddComponent<TooltipTrigger>().text = "Team Play\nFriends, your shared agency and chat. Press Enter to chat.";

        var bd = UIKit.At(btn, "Badge", 54, -4, 14, 10);
        UIKit.Pill(bd, UIKit.Solid(T.red));
        badge = UIKit.Text(bd, "", 6f, TextAlignmentOptions.Center, true);
        bd.gameObject.SetActive(false);

        feed = new GameObject("Chat Feed", typeof(RectTransform)).GetComponent<RectTransform>();
        feed.SetParent(transform, false);
        feed.anchorMin = feed.anchorMax = new Vector2(1f, 1f); feed.pivot = new Vector2(1f, 1f);
        feed.anchoredPosition = new Vector2(-8f, -62f); feed.sizeDelta = new Vector2(180f, 80f);
        var v = feed.gameObject.AddComponent<VerticalLayoutGroup>();
        v.spacing = 2f; v.childControlWidth = true; v.childControlHeight = true; v.childForceExpandHeight = false; v.childAlignment = TextAnchor.UpperRight;

        window = MultiplayerUI.Create(canvas, null);
    }

    void OnEnable() { NetSession.ChatAdded += OnChat; }
    void OnDisable() { NetSession.ChatAdded -= OnChat; }

    void Toggle()
    {
        if (window.gameObject.activeSelf) window.Hide();
        else { window.Show(); unread = 0; }
    }

    void OnChat(ChatLine line)
    {
        if (window != null && window.gameObject.activeSelf) return;
        if (!line.system) unread++;
        var holder = UIKit.At(feed, "Line", 0, 0, 180, 12);
        UIKit.Pill(holder, new Color(0.06f, 0.07f, 0.1f, 0.78f));
        var inner = new GameObject("Inner", typeof(RectTransform)).GetComponent<RectTransform>();
        inner.SetParent(holder, false); UIKit.Fill(inner, 2, 1, 2, 1);
        var v = inner.gameObject.AddComponent<VerticalLayoutGroup>(); v.childControlHeight = true; v.childControlWidth = true;
        MultiplayerUI.ChatRow(inner, line, 176);
        var le = holder.gameObject.AddComponent<LayoutElement>();
        le.preferredHeight = inner.GetComponentInChildren<LayoutElement>().preferredHeight + 2f;
        feedLines.Add((holder.gameObject, Time.unscaledTime + 7f));
        while (feedLines.Count > 4) { Destroy(feedLines[0].go); feedLines.RemoveAt(0); }
    }

    void Update()
    {
        for (int i = feedLines.Count - 1; i >= 0; i--)
            if (feedLines[i].go == null || Time.unscaledTime > feedLines[i].until) { if (feedLines[i].go != null) Destroy(feedLines[i].go); feedLines.RemoveAt(i); }

        var s = NetSession.Instance;
        bool inSession = s != null && s.InSession;
        var me = inSession ? s.Me : null;
        buttonLabel.text = !inSession ? "Team" : me != null && me.role != TeamRole.None ? me.role.ToString() : "Team " + s.Members.Count;
        badge.text = unread > 9 ? "9+" : unread.ToString();
        badge.transform.parent.gameObject.SetActive(unread > 0);

        // Enter opens the chat, unless the player is typing somewhere else
        var kb = Keyboard.current;
        if (kb != null && kb.enterKey.wasPressedThisFrame && !window.gameObject.activeSelf && !Typing())
        {
            window.Show(2); unread = 0;
        }
    }

    static bool Typing()
    {
        var es = UnityEngine.EventSystems.EventSystem.current;
        var sel = es != null ? es.currentSelectedGameObject : null;
        return sel != null && sel.GetComponent<TMP_InputField>() != null;
    }
}
