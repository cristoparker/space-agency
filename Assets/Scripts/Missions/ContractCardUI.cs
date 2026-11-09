using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>One contract in the grid: shows title and type. Hover previews details, click selects.</summary>
public class ContractCardUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    public Image background;
    public TMP_Text titleText;
    public TMP_Text typeText;
    public Color normalColor = new Color(1f, 1f, 1f, 0.14f);
    public Color selectedColor = new Color(0.25f, 0.8f, 1f, 0.38f);

    public MissionDefinition Mission { get; private set; }

    Action<ContractCardUI> onHover, onExit, onClick;

    public void Bind(MissionDefinition mission, Action<ContractCardUI> hover, Action<ContractCardUI> exit, Action<ContractCardUI> click)
    {
        Mission = mission;
        onHover = hover; onExit = exit; onClick = click;
        titleText.text = mission.displayName;
        typeText.text = mission.missionType;
        SetSelected(false);
    }

    public void SetSelected(bool selected)
    {
        if (background != null) background.color = selected ? selectedColor : normalColor;
    }

    public void OnPointerEnter(PointerEventData e) => onHover?.Invoke(this);
    public void OnPointerExit(PointerEventData e) => onExit?.Invoke(this);
    public void OnPointerClick(PointerEventData e) => onClick?.Invoke(this);
}
