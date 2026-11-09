using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>The floating Mission Manager window: Dashboard (running missions) and Contracts (available missions).</summary>
public class MissionManagerUI : MonoBehaviour
{
    [Header("Window")]
    public Button closeButton;
    public Button dashboardTab;
    public Button contractsTab;
    public GameObject dashboardPage;
    public GameObject contractsPage;
    public Sprite tabNormalSprite;
    public Sprite tabActiveSprite;

    [Header("Dashboard")]
    public Transform runningContent;
    public RunningMissionRowUI runningRowPrefab;
    public GameObject noRunningMissionsGroup;
    public Button discoverButton;

    [Header("Contracts")]
    public Transform contractsContent;
    public ContractCardUI contractCardPrefab;
    public GameObject noContractsText;

    [Header("Details")]
    public TMP_Text detailsTitle;
    public TMP_Text detailsBody;
    public ScrollRect detailsScroll;
    public GameObject detailsHint;
    public GameObject actionButtons;
    public Button startButton;
    public Button discardButton;

    readonly List<ContractCardUI> cards = new List<ContractCardUI>();
    readonly List<RunningMissionRowUI> rows = new List<RunningMissionRowUI>();
    MissionDefinition selected;
    MissionDefinition hovered;
    bool wired;

    void Wire()
    {
        if (wired) return;
        wired = true;
        if (closeButton != null) closeButton.onClick.AddListener(() => gameObject.SetActive(false));
        dashboardTab.onClick.AddListener(ShowDashboard);
        contractsTab.onClick.AddListener(ShowContracts);
        if (discoverButton != null) discoverButton.onClick.AddListener(ShowContracts);
        startButton.onClick.AddListener(StartSelected);
        discardButton.onClick.AddListener(DiscardSelected);
    }

    void OnEnable()
    {
        Wire();
        transform.SetAsLastSibling();
        if (MissionManager.Instance != null) MissionManager.Instance.Changed += Rebuild;
        selected = hovered = null;
        Rebuild();
        ShowDashboard();
    }

    void OnDisable()
    {
        if (MissionManager.Instance != null) MissionManager.Instance.Changed -= Rebuild;
    }

    public void ShowDashboard() => SetPage(true);
    public void ShowContracts() => SetPage(false);

    void SetPage(bool dashboard)
    {
        dashboardPage.SetActive(dashboard);
        contractsPage.SetActive(!dashboard);
        dashboardTab.GetComponent<Image>().sprite = dashboard ? tabActiveSprite : tabNormalSprite;
        contractsTab.GetComponent<Image>().sprite = dashboard ? tabNormalSprite : tabActiveSprite;
        // Dark text on the yellow (active) tab, white on the green one.
        dashboardTab.GetComponentInChildren<TMP_Text>().color = dashboard ? Color.black : Color.white;
        contractsTab.GetComponentInChildren<TMP_Text>().color = dashboard ? Color.white : Color.black;
        if (!dashboard) RefreshDetails();
    }

    void Rebuild()
    {
        var mm = MissionManager.Instance;
        if (mm == null) return;

        foreach (var c in cards) if (c != null) Destroy(c.gameObject);
        foreach (var r in rows) if (r != null) Destroy(r.gameObject);
        cards.Clear(); rows.Clear();

        foreach (var m in mm.Available)
        {
            var card = Instantiate(contractCardPrefab, contractsContent);
            card.Bind(m, c => { hovered = c.Mission; RefreshDetails(); }, c => { if (hovered == c.Mission) hovered = null; RefreshDetails(); }, c => { selected = c.Mission; RefreshDetails(); });
            cards.Add(card);
        }
        if (noContractsText != null) noContractsText.SetActive(mm.Available.Count == 0);

        foreach (var m in mm.Running)
        {
            var row = Instantiate(runningRowPrefab, runningContent);
            row.Bind(m, inst => mm.TrackMission(inst));
            rows.Add(row);
        }
        noRunningMissionsGroup.SetActive(mm.Running.Count == 0);

        if (selected != null && !mm.Available.Contains(selected)) selected = null;
        if (hovered != null && !mm.Available.Contains(hovered)) hovered = null;
        RefreshDetails();
    }

    void RefreshDetails()
    {
        foreach (var c in cards) c.SetSelected(c.Mission == selected);

        var shown = hovered != null ? hovered : selected;
        bool has = shown != null;
        detailsHint.SetActive(!has);
        detailsTitle.gameObject.SetActive(has);
        detailsScroll.gameObject.SetActive(has);
        actionButtons.SetActive(has && shown == selected);
        if (!has) return;

        detailsTitle.text = shown.displayName;
        detailsBody.text = MissionFormat.Details(shown);
        if (detailsScroll.content != null) LayoutRebuilder.ForceRebuildLayoutImmediate(detailsScroll.content);
        detailsScroll.verticalNormalizedPosition = 1f;
    }

    void StartSelected()
    {
        if (selected == null || MissionManager.Instance == null) return;
        var def = selected;
        selected = hovered = null;
        MissionManager.Instance.StartMission(def);
        ShowDashboard();
    }

    void DiscardSelected()
    {
        if (selected == null || MissionManager.Instance == null) return;
        var def = selected;
        selected = hovered = null;
        MissionManager.Instance.DiscardMission(def);
    }
}
