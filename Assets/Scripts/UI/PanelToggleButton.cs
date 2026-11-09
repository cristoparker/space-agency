using UnityEngine;
using UnityEngine.UI;

/// <summary>Put on a Button: clicking it shows/hides the target panel. Reusable for any window.</summary>
[RequireComponent(typeof(Button))]
public class PanelToggleButton : MonoBehaviour
{
    public GameObject panel;

    void Awake() => GetComponent<Button>().onClick.AddListener(Toggle);

    public void Toggle()
    {
        if (panel != null) panel.SetActive(!panel.activeSelf);
    }
}
