using UnityEngine;
using System.Collections.Generic;
using TMPro;
using DG.Tweening;
using Obvious.Soap;

public class GameManager : MonoBehaviour
{
    public enum GamePhase { Morning, Day, Night }

    [Header("게임 진행 상태")]
    public GamePhase currentPhase;
    public int currentDay = 1;

    [Header("SOAP Variables")]
    [SerializeField] private IntVariable soapCurrentDay;

    [Header("SOAP Events")]
    [SerializeField] private ScriptableEventNoParam onApproveClicked;
    [SerializeField] private ScriptableEventNoParam onLocationSelected;

    [Header("UI - 아침 페이즈")]
    [SerializeField] private CanvasGroup morningNewsPanelCG;
    [SerializeField] private TextMeshProUGUI officialNewsTitleUI;
    [SerializeField] private TextMeshProUGUI officialNewsContentUI;

    [Header("UI - 낮 페이즈")]
    [SerializeField] private CanvasGroup documentPanelCG;
    [SerializeField] private CanvasGroup guidelinePanelCG;
    [SerializeField] private GameObject guidelineButton;
    [SerializeField] private TextMeshProUGUI guidelineTextUI;
    [SerializeField] private DocumentViewer documentViewer;

    [Header("UI - 밤 페이즈")]
    [SerializeField] private NightMapUI nightMapUI;

    [Header("매니저 연결")]
    [SerializeField] private NightPhaseManager nightPhaseManager;

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
        SetCanvasGroupHidden(documentPanelCG);
        SetCanvasGroupHidden(guidelinePanelCG);
        SetCanvasGroupHidden(morningNewsPanelCG);
        if (guidelineButton != null) guidelineButton.SetActive(false);
    }

    private void SetCanvasGroupHidden(CanvasGroup cg)
    {
        if (cg == null) return;
        cg.alpha = 0f;
        cg.interactable = false;
        cg.blocksRaycasts = false;
        cg.gameObject.SetActive(false);
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
        HideAllPanelsImmediate();
        ActivatePhase(newPhase);
    }

    private void HideAllPanelsImmediate()
    {
        HidePanelImmediate(documentPanelCG);
        HidePanelImmediate(guidelinePanelCG);
        HidePanelImmediate(morningNewsPanelCG);
        if (guidelineButton != null) guidelineButton.SetActive(false);
    }

    private void HidePanelImmediate(CanvasGroup cg)
    {
        if (cg == null) return;
        cg.DOKill();
        cg.alpha = 0f;
        cg.interactable = false;
        cg.blocksRaycasts = false;
        cg.gameObject.SetActive(false);
    }

    private void LoadTodaysData()
    {
        int index = currentDay - 1;
        todaysData = (index >= 0 && index < dailyDataList.Count) ? dailyDataList[index] : null;
    }

    private void ActivatePhase(GamePhase phase)
    {
        switch (phase)
        {
            case GamePhase.Morning: ActivateMorningPhase(); break;
            case GamePhase.Day:     ActivateDayPhase();     break;
            case GamePhase.Night:   ActivateNightPhase();   break;
        }
    }

    private void ActivateMorningPhase()
    {
        Debug.Log($"☀️ [아침] Stage {currentDay} 뉴스");
        if (todaysData == null || morningNewsPanelCG == null) return;
        if (officialNewsTitleUI   != null) officialNewsTitleUI.text   = todaysData.officialNewsTitle;
        if (officialNewsContentUI != null) officialNewsContentUI.text = todaysData.officialNewsContent;
        ShowPanel(morningNewsPanelCG);
    }

    public void OnClickMorningNewsConfirm() => GoToNextPhase();

    private void ActivateDayPhase()
    {
        Debug.Log($"💼 [낮] Stage {currentDay} 업무 시작");
        if (todaysData == null || todaysData.documentToProcess == null)
        {
            Debug.LogWarning("todaysData 또는 documentToProcess가 null입니다.");
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
        bool isVisible = guidelinePanelCG.gameObject.activeSelf && guidelinePanelCG.alpha > 0.5f;
        if (isVisible)
        {
            guidelinePanelCG.DOFade(0f, panelFadeDuration).SetEase(Ease.OutQuad)
                .OnComplete(() =>
                {
                    guidelinePanelCG.interactable   = false;
                    guidelinePanelCG.blocksRaycasts = false;
                    guidelinePanelCG.gameObject.SetActive(false);
                });
        }
        else ShowPanel(guidelinePanelCG);
    }

    private void ActivateNightPhase()
    {
        Debug.Log("🌙 [밤] 지도 UI 오픈");
        if (nightMapUI == null || nightPhaseManager == null)
        {
            Debug.LogWarning("NightMapUI 또는 NightPhaseManager 미연결");
            return;
        }
        if (todaysData == null) { Debug.LogWarning("todaysData null"); return; }

        // 1. 오늘 열린 장소 이름 목록 가져오기
        List<string> locationNames = LocationUnlockManager.Instance != null
            ? LocationUnlockManager.Instance.GetAvailableLocations(todaysData, currentDay)
            : todaysData.availableLocations;

        // 2. 이름 목록 → 위치 정보 포함 목록으로 변환 (NightPhaseManager가 위치 정보 보유)
        List<LocationButtonInfo> buttonInfoList = nightPhaseManager.BuildButtonInfoList(locationNames);

        // 3. 지도 UI 표시
        nightMapUI.ShowMap(buttonInfoList);
    }

    private void OnLocationSelectedHandler() => Debug.Log("장소 선택 완료");

    public void GoToNextPhase()
    {
        switch (currentPhase)
        {
            case GamePhase.Morning: ChangePhase(GamePhase.Day);   break;
            case GamePhase.Day:     ChangePhase(GamePhase.Night);  break;
            case GamePhase.Night:
                if (nightPhaseManager != null)
                    nightPhaseManager.DeactivateNightView(() => StartDay(currentDay + 1));
                else
                    StartDay(currentDay + 1);
                break;
        }
    }

    private void ShowPanel(CanvasGroup cg, float duration = -1f)
    {
        if (cg == null) return;
        float d = duration < 0f ? panelFadeDuration : duration;
        cg.gameObject.SetActive(true);
        cg.alpha = 0f;
        cg.interactable = false;
        cg.blocksRaycasts = false;
        cg.DOFade(1f, d).SetEase(Ease.InQuad).OnComplete(() =>
        {
            cg.interactable   = true;
            cg.blocksRaycasts = true;
        });
    }
}
