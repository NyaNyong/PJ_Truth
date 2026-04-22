using UnityEngine;
using System.Collections.Generic;
using TMPro;
using DG.Tweening;
using Obvious.Soap;

public class GameManager : MonoBehaviour
{
    public enum GamePhase { Morning, Day, Night, Whiteboard }

    [Header("게임 진행 상태")]
    public GamePhase currentPhase;
    public int       currentDay = 1;

    [Header("SOAP Variables")]
    [SerializeField] private IntVariable soapCurrentDay;

    [Header("SOAP Events")]
    [SerializeField] private ScriptableEventNoParam onApproveClicked;
    [SerializeField] private ScriptableEventNoParam onLocationSelected;

    [Header("UI - 아침 페이즈")]
    [SerializeField] private CanvasGroup     morningNewsPanelCG;
    [SerializeField] private TextMeshProUGUI officialNewsTitleUI;
    [SerializeField] private TextMeshProUGUI officialNewsContentUI;

    [Header("UI - 낮 페이즈")]
    [SerializeField] private CanvasGroup     documentPanelCG;
    [SerializeField] private CanvasGroup     guidelinePanelCG;
    [SerializeField] private GameObject      guidelineButton;
    [SerializeField] private TextMeshProUGUI guidelineTextUI;
    [SerializeField] private DocumentViewer  documentViewer;

    [Header("UI - 밤 페이즈")]
    [SerializeField] private NightMapUI nightMapUI;

    [Header("매니저 연결")]
    [SerializeField] private NightPhaseManager  nightPhaseManager;
    [SerializeField] private WhiteboardManager  whiteboardManager;

    [Header("스테이지 데이터")]
    [SerializeField] private List<DailyData> dailyDataList;

    [Header("DOTween 설정")]
    [SerializeField] private float panelFadeDuration = 0.35f;

    private DailyData todaysData;

    private void OnEnable()
    {
        if (onApproveClicked  != null) onApproveClicked.OnRaised  += GoToNextPhase;
        if (onLocationSelected != null) onLocationSelected.OnRaised += OnLocationSelectedHandler;
    }

    private void OnDisable()
    {
        if (onApproveClicked  != null) onApproveClicked.OnRaised  -= GoToNextPhase;
        if (onLocationSelected != null) onLocationSelected.OnRaised -= OnLocationSelectedHandler;
    }

    private void Start()
    {
        InitializePanels();
        StartDay(1);
    }

    private void InitializePanels()
    {
        HideCanvasGroup(documentPanelCG);
        HideCanvasGroup(guidelinePanelCG);
        HideCanvasGroup(morningNewsPanelCG);
        if (guidelineButton != null) guidelineButton.SetActive(false);
    }

    private void HideCanvasGroup(CanvasGroup cg)
    {
        if (cg == null) return;
        cg.alpha          = 0f;
        cg.interactable   = false;
        cg.blocksRaycasts = false;
    }

    public void StartDay(int day)
    {
        currentDay = day;
        if (soapCurrentDay != null) soapCurrentDay.Value = day;
        Debug.Log($"===== [ Stage {currentDay} 시작 ] =====");
        ChangePhase(GamePhase.Morning);
    }

    public void ChangePhase(GamePhase newPhase)
    {
        currentPhase = newPhase;
        LoadTodaysData();
        HideAllPanels();
        ActivatePhase(newPhase);
    }

    private void HideAllPanels()
    {
        HidePanelImmediate(documentPanelCG);
        HidePanelImmediate(guidelinePanelCG);
        HidePanelImmediate(morningNewsPanelCG);
        if (guidelineButton != null) guidelineButton.SetActive(false);
        documentViewer?.HideDocument();
    }

    private void HidePanelImmediate(CanvasGroup cg)
    {
        if (cg == null) return;
        cg.DOKill();
        cg.alpha          = 0f;
        cg.interactable   = false;
        cg.blocksRaycasts = false;
    }

    private void LoadTodaysData()
    {
        int index  = currentDay - 1;
        todaysData = (index >= 0 && index < dailyDataList.Count) ? dailyDataList[index] : null;
    }

    private void ActivatePhase(GamePhase phase)
    {
        switch (phase)
        {
            case GamePhase.Morning:     ActivateMorningPhase();     break;
            case GamePhase.Day:         ActivateDayPhase();         break;
            case GamePhase.Night:       ActivateNightPhase();       break;
            case GamePhase.Whiteboard:  ActivateWhiteboardPhase();  break;
        }
    }

    // ── 아침 ──────────────────────────────────
    private void ActivateMorningPhase()
    {
        Debug.Log($"[아침] Stage {currentDay}");
        if (morningNewsPanelCG == null || todaysData == null) return;
        if (officialNewsTitleUI   != null) officialNewsTitleUI.text   = todaysData.officialNewsTitle;
        if (officialNewsContentUI != null) officialNewsContentUI.text = todaysData.officialNewsContent;
        ShowPanel(morningNewsPanelCG);
    }

    public void OnClickMorningNewsConfirm()
    {
        Debug.Log("[GameManager] 아침 확인 클릭");
        GoToNextPhase();
    }

    // ── 낮 ───────────────────────────────────
    private void ActivateDayPhase()
    {
        Debug.Log($"[낮] Stage {currentDay}");
        if (todaysData == null || todaysData.documentToProcess == null)
        {
            Debug.LogWarning("documentToProcess null");
            return;
        }
        documentViewer?.ShowDocument(todaysData.documentToProcess);
        if (guidelineTextUI != null) guidelineTextUI.text = todaysData.documentToProcess.guidelineText;
        ShowPanel(documentPanelCG);
        if (guidelineButton != null) guidelineButton.SetActive(true);
    }

    public void OnClickGuidelineToggle()
    {
        if (guidelinePanelCG == null) return;
        bool isVisible = guidelinePanelCG.alpha > 0.5f;
        if (isVisible)
        {
            guidelinePanelCG.DOFade(0f, panelFadeDuration).SetEase(Ease.OutQuad)
                .OnComplete(() =>
                {
                    guidelinePanelCG.interactable   = false;
                    guidelinePanelCG.blocksRaycasts = false;
                });
        }
        else ShowPanel(guidelinePanelCG);
    }

    // ── 밤 ───────────────────────────────────
    private void ActivateNightPhase()
    {
        Debug.Log("[밤] 지도 UI 오픈");
        if (nightMapUI == null || nightPhaseManager == null || todaysData == null) return;

        List<string> locationNames = LocationUnlockManager.Instance != null
            ? LocationUnlockManager.Instance.GetAvailableLocations(todaysData, currentDay)
            : todaysData.availableLocations;

        nightMapUI.ShowMap(nightPhaseManager.BuildButtonInfoList(locationNames));
    }

    private void OnLocationSelectedHandler() { }

    // ── 화이트보드 ───────────────────────────
    private void ActivateWhiteboardPhase()
    {
        Debug.Log("[화이트보드] 오픈");
        if (whiteboardManager != null)
        {
            whiteboardManager.OpenBoard();
        }
        else
        {
            // 화이트보드 없으면 바로 다음 날
            Debug.LogWarning("WhiteboardManager 미연결 — 다음 날로 건너뜁니다");
            StartDay(currentDay + 1);
        }
    }

    // ── 페이즈 전환 ───────────────────────────
    public void GoToNextPhase()
    {
        Debug.Log($"[GameManager] GoToNextPhase — 현재: {currentPhase}");
        switch (currentPhase)
        {
            case GamePhase.Morning:
                ChangePhase(GamePhase.Day);
                break;

            case GamePhase.Day:
                ChangePhase(GamePhase.Night);
                break;

            case GamePhase.Night:
                // 밤 탐색 종료 → 화이트보드
                if (nightPhaseManager != null)
                    nightPhaseManager.DeactivateNightView(() => ChangePhase(GamePhase.Whiteboard));
                else
                    ChangePhase(GamePhase.Whiteboard);
                break;

            case GamePhase.Whiteboard:
                // 화이트보드 닫기 → 다음 날
                StartDay(currentDay + 1);
                break;
        }
    }

    private void ShowPanel(CanvasGroup cg, float duration = -1f)
    {
        if (cg == null) return;
        float d = duration < 0f ? panelFadeDuration : duration;
        cg.alpha          = 0f;
        cg.interactable   = false;
        cg.blocksRaycasts = false;
        cg.DOFade(1f, d).SetEase(Ease.InQuad).OnComplete(() =>
        {
            cg.interactable   = true;
            cg.blocksRaycasts = true;
        });
    }
}
