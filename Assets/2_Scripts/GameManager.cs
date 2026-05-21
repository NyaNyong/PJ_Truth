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
    public int currentDay = 1;
    [SerializeField] private int startDay = 4;

    [Header("SOAP Variables")]
    [SerializeField] private IntVariable soapCurrentDay;

    [Header("SOAP Events")]
    [SerializeField] private ScriptableEventNoParam onApproveClicked;
    [SerializeField] private ScriptableEventNoParam onLocationSelected;
    [SerializeField] private ScriptableEventNoParam onPhaseTransitionRequest;

    [Header("UI - 아침 페이즈")]
    [SerializeField] private CanvasGroup morningNewsPanelCG;
    [SerializeField] private TextMeshProUGUI officialNewsTitleUI;
    [SerializeField] private TextMeshProUGUI officialNewsContentUI;
    [SerializeField] private GameObject morningNewsButton;

    [Header("UI - 낮 페이즈")]
    [SerializeField] private CanvasGroup documentPanelCG;
    [SerializeField] private CanvasGroup guidelinePanelCG;
    [SerializeField] private GameObject guidelineButton;
    [SerializeField] private TextMeshProUGUI guidelineTextUI;
    [SerializeField] private DocumentViewer documentViewer;
    [SerializeField] private GameObject dayPhaseBG;

    [Header("승인 도장")]
    [SerializeField] private CanvasGroup stampCG;
    [SerializeField] private RectTransform stampRT;

    [Header("UI - 밤 페이즈")]
    [SerializeField] private NightMapUI nightMapUI;

    [Header("매니저 연결")]
    [SerializeField] private NightPhaseManager nightPhaseManager;
    [SerializeField] private WhiteboardManager whiteboardManager;

    [Header("스테이지 데이터")]
    [SerializeField] private List<DailyData> dailyDataList;

    [Header("DOTween 설정")]
    [SerializeField] private float panelFadeDuration = 0.35f;

    [Header("결과창")]
    [SerializeField] private ResultScreenUI resultScreenUI;

    [Header("타이틀 UI")]
    [SerializeField] private IDCardUI idCardUI;

    private DailyData todaysData;

    // ── 이벤트 ───────────────────────────────
    private void OnEnable()
    {
        if (onApproveClicked != null) onApproveClicked.OnRaised += GoToNextPhase;
        if (onLocationSelected != null) onLocationSelected.OnRaised += OnLocationSelectedHandler;
        if (onPhaseTransitionRequest != null) onPhaseTransitionRequest.OnRaised += GoToNextPhase;
    }

    private void OnDisable()
    {
        if (onApproveClicked != null) onApproveClicked.OnRaised -= GoToNextPhase;
        if (onLocationSelected != null) onLocationSelected.OnRaised -= OnLocationSelectedHandler;
        if (onPhaseTransitionRequest != null) onPhaseTransitionRequest.OnRaised -= GoToNextPhase;
    }

    // ── 시작 ─────────────────────────────────
    private void Start()
    {
        InitializePanels();
        if (idCardUI == null)
            StartDay(startDay);
    }

    private void InitializePanels()
    {
        nightPhaseManager?.EnsureHidden();
        HideCanvasGroup(documentPanelCG);
        HideCanvasGroup(guidelinePanelCG);
        HideCanvasGroup(morningNewsPanelCG);
        if (guidelineButton != null) guidelineButton.SetActive(false);
        if (morningNewsButton != null) morningNewsButton.SetActive(false);
        if (dayPhaseBG != null) dayPhaseBG.SetActive(false);
        if (stampCG != null) { stampCG.alpha = 0f; stampCG.gameObject.SetActive(false); }
    }

    // ── 날짜 시작 ────────────────────────────
    public void StartDay(int day)
    {
        Debug.Log($"[GameManager] StartDay({day}) 호출");
        currentDay = day;
        if (soapCurrentDay != null) soapCurrentDay.Value = day;

        nightPhaseManager?.ResetVisited(); // ★ 방문 기록 초기화

        LoadTodaysData();
        if (todaysData == null) { Debug.LogWarning($"[GameManager] Day{day} DailyData 없음 → 종료"); return; }

        GameTextLoader.Instance?.LoadDay(day);
        GameTextLoader.Instance?.InjectIntoDaily(todaysData);

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

    // ── 패널 초기화 ──────────────────────────
    private void HideAllPanels()
    {
        HidePanelImmediate(documentPanelCG);
        HidePanelImmediate(guidelinePanelCG);
        HidePanelImmediate(morningNewsPanelCG);
        if (guidelineButton != null) guidelineButton.SetActive(false);
        if (dayPhaseBG != null) dayPhaseBG.SetActive(false);
        if (morningNewsButton != null)
        {
            morningNewsButton.transform.DOKill();
            morningNewsButton.SetActive(false);
        }
        documentViewer?.HideDocument();
    }

    private void HidePanelImmediate(CanvasGroup cg)
    {
        if (cg == null) return;
        cg.DOKill();
        cg.alpha = 0f; cg.interactable = false; cg.blocksRaycasts = false;
    }

    private void HideCanvasGroup(CanvasGroup cg)
    {
        if (cg == null) return;
        cg.alpha = 0f; cg.interactable = false; cg.blocksRaycasts = false;
    }

    private void LoadTodaysData()
    {
        todaysData = dailyDataList?.Find(d => d.dayNumber == currentDay);
    }

    private void ActivatePhase(GamePhase phase)
    {
        switch (phase)
        {
            case GamePhase.Morning: ActivateMorningPhase(); break;
            case GamePhase.Day: ActivateDayPhase(); break;
            case GamePhase.Night: ActivateNightPhase(); break;
            case GamePhase.Whiteboard: ActivateWhiteboardPhase(); break;
        }
    }

    public void StartFromBeginning() => StartDay(startDay);

    // ── 아침 ─────────────────────────────────
    private void ActivateMorningPhase()
    {
        AudioManager.Instance?.PlayDayPhase();
        if (todaysData == null) return;
        if (dayPhaseBG != null) dayPhaseBG.SetActive(true);

        if (officialNewsTitleUI != null) officialNewsTitleUI.text = todaysData.officialNewsTitle;
        if (officialNewsContentUI != null) officialNewsContentUI.text = todaysData.officialNewsContent;

        if (morningNewsButton != null)
        {
            morningNewsButton.SetActive(true);
            morningNewsButton.transform.localScale = Vector3.zero;
            morningNewsButton.transform.DOScale(Vector3.one, 0.35f).SetEase(Ease.OutBack);
        }
    }

    public void OnClickMorningNewsButton()
    {
        AudioManager.Instance?.PlaySfxDayInteraction();
        if (morningNewsButton != null)
            morningNewsButton.transform.DOScale(Vector3.zero, 0.15f)
                .SetEase(Ease.InBack)
                .OnComplete(() => morningNewsButton.SetActive(false));

        if (morningNewsPanelCG != null)
            morningNewsPanelCG.gameObject.SetActive(true);
        ShowPanel(morningNewsPanelCG);
    }

    public void OnClickMorningNewsConfirm()
    {
        AudioManager.Instance?.PlaySfxDayInteraction();
        GoToNextPhase();
    }

    // ── 낮 ───────────────────────────────────
    private void ActivateDayPhase()
    {
        AudioManager.Instance?.PlayDayPhase();
        if (todaysData == null || todaysData.documentToProcess == null)
        {
            Debug.LogWarning("[GameManager] documentToProcess null"); return;
        }
        if (dayPhaseBG != null) dayPhaseBG.SetActive(true);

        GameTextLoader.Instance?.InjectIntoDocument(todaysData.documentToProcess);
        documentViewer?.ShowDocument(todaysData.documentToProcess);
        if (guidelineTextUI != null) guidelineTextUI.text = todaysData.documentToProcess.guidelineText;
        ShowPanel(documentPanelCG);
        if (guidelineButton != null) guidelineButton.SetActive(true);
    }

    public void OnClickGuidelineToggle()
    {
        AudioManager.Instance?.PlaySfxCasebookToggle();
        if (guidelinePanelCG == null) return;
        bool isVisible = guidelinePanelCG.alpha > 0.5f;
        if (isVisible)
            guidelinePanelCG.DOFade(0f, panelFadeDuration).SetEase(Ease.OutQuad)
                .OnComplete(() => { guidelinePanelCG.interactable = false; guidelinePanelCG.blocksRaycasts = false; });
        else
            ShowPanel(guidelinePanelCG);
    }

    // ── 밤 ───────────────────────────────────
    private void ActivateNightPhase()
    {
        AudioManager.Instance?.PlayNightPhase();
        if (nightMapUI == null || nightPhaseManager == null || todaysData == null) return;

        List<string> locationNames = LocationUnlockManager.Instance != null
            ? LocationUnlockManager.Instance.GetAvailableLocations(todaysData, currentDay)
            : todaysData.availableLocations;

        // BuildButtonInfoList 내부에서 isVisited가 자동으로 세팅됨
        nightMapUI.ShowMap(nightPhaseManager.BuildButtonInfoList(locationNames));
    }

    private void OnLocationSelectedHandler() { }

    /// <summary>
    /// 퇴근 버튼 onClick에 연결.
    /// requireAllLocations이고 미방문 장소가 있으면 맵으로 복귀, 아니면 화이트보드.
    /// </summary>
    public void OnClickGoHome()
    {
        if (nightPhaseManager == null) return;

        List<string> allLocs = GetCurrentLocationNames();
        bool requireAll = todaysData != null && todaysData.requireAllLocations;
        bool allVisited = nightPhaseManager.HasVisitedAll(allLocs);
        bool returnToMap = requireAll && !allVisited;

        string title = returnToMap
            ? "다른 곳으로 가시겠습니까?"
            : "집으로 돌아가시겠습니까?";

        ConfirmPopupUI.Instance?.Open(
            title: title,
            message: "",
            warning: "다시 조사할 수 없습니다",
            onConfirm: () =>
            {
                if (returnToMap)
                    // 맵 선택으로 복귀 (방문한 장소 버튼은 비활성화됨)
                    nightPhaseManager.DeactivateNightView(() => ActivateNightPhase());
                else
                    nightPhaseManager.DeactivateNightView(() => ChangePhase(GamePhase.Whiteboard));
            },
            confirmText: "예",
            cancelText: "아니요"
        );
    }

    private List<string> GetCurrentLocationNames()
    {
        if (todaysData == null) return new List<string>();
        return LocationUnlockManager.Instance != null
            ? LocationUnlockManager.Instance.GetAvailableLocations(todaysData, currentDay)
            : todaysData.availableLocations ?? new List<string>();
    }
    /// <summary>ExitObject에서 호출 — 맵 복귀 필요 여부 반환</summary>
    public bool ShouldReturnToMap()
    {
        bool requireAll = todaysData != null && todaysData.requireAllLocations;
        bool allVisited = nightPhaseManager?.HasVisitedAll(GetCurrentLocationNames()) ?? true;
        return requireAll && !allVisited;
    }

    // ── 화이트보드 ────────────────────────────
    private void ActivateWhiteboardPhase()
    {
        AudioManager.Instance?.PlayWhiteboard();
        if (whiteboardManager != null)
        {
            whiteboardManager.LoadFromJson();
            whiteboardManager.OpenBoard();
        }
        else
        {
            Debug.LogWarning("[GameManager] WhiteboardManager 미연결");
            StartDay(currentDay + 1);
        }
    }

    // ── 페이즈 전환 ───────────────────────────
    public void GoToNextPhase()
    {
        switch (currentPhase)
        {
            case GamePhase.Morning:
                ChangePhase(GamePhase.Day); break;

            case GamePhase.Day:
                ShowApprovalStamp(() => ShowResultScreen()); break;

            case GamePhase.Night:
                if (nightPhaseManager != null)
                {
                    bool requireAll = todaysData != null && todaysData.requireAllLocations;
                    bool allVisited = nightPhaseManager.HasVisitedAll(GetCurrentLocationNames());

                    if (requireAll && !allVisited)
                        // 아직 방문 안 한 장소 있음 → 맵으로 복귀
                        nightPhaseManager.DeactivateNightView(() => ActivateNightPhase());
                    else
                        // 모두 방문 or requireAll 아님 → 화이트보드
                        nightPhaseManager.DeactivateNightView(() => ChangePhase(GamePhase.Whiteboard));
                }
                else
                    ChangePhase(GamePhase.Whiteboard);
                break;

            case GamePhase.Whiteboard:
                StartDay(currentDay + 1); break;
        }
    }

    // ── 승인 도장 ────────────────────────────
    private void ShowApprovalStamp(System.Action onComplete)
    {
        if (stampCG == null || stampRT == null) { onComplete?.Invoke(); return; }

        AudioManager.Instance?.PlaySfxApproveStamp();
        stampCG.DOKill(); stampRT.DOKill();
        stampCG.gameObject.SetActive(true);
        stampCG.alpha = 0f;
        stampRT.localScale = Vector3.zero;

        DOTween.Sequence()
            .Append(stampCG.DOFade(1f, 0.1f))
            .Join(stampRT.DOScale(1.3f, 0.12f).SetEase(Ease.OutQuad))
            .Append(stampRT.DOScale(1f, 0.08f).SetEase(Ease.InQuad))
            .AppendInterval(1f)
            .OnComplete(() =>
            {
                stampCG?.DOKill();
                if (stampCG != null) { stampCG.alpha = 0f; stampCG.gameObject.SetActive(false); }
                onComplete?.Invoke();
            });
    }

    private void ShowResultScreen()
    {
        if (ScoringSystem.Instance == null || documentViewer == null)
        {
            ChangePhase(GamePhase.Night); return;
        }

        var (censor, typewriter) = documentViewer.CalculateScore();
        var score = ScoringSystem.Instance.CalculateAndRecord(censor, typewriter, currentDay);
        float kpiProgress = ScoringSystem.Instance.KPIProgress;
        int totalDocs = ScoringSystem.Instance.TotalProcessedDocuments;

        GameFlags.Instance?.SetFlag(GetCensorIntensityFlag(score.grade, currentDay));

        if (resultScreenUI != null)
            resultScreenUI.Show(score, kpiProgress, totalDocs, () => ChangePhase(GamePhase.Night));
        else
            ChangePhase(GamePhase.Night);
    }

    private string GetCensorIntensityFlag(Grade grade, int day)
    {
        string intensity = grade switch
        {
            Grade.S or Grade.A => "heavy",
            Grade.B => "mild",
            _ => "weak"
        };
        return $"censor_{intensity}_day{day}";
    }

    // ── 공통 패널 표시 ────────────────────────
    private void ShowPanel(CanvasGroup cg, float duration = -1f)
    {
        if (cg == null) return;
        float d = duration < 0f ? panelFadeDuration : duration;
        cg.alpha = 0f; cg.interactable = false; cg.blocksRaycasts = false;
        cg.DOFade(1f, d).SetEase(Ease.InQuad)
            .OnComplete(() => { cg.interactable = true; cg.blocksRaycasts = true; });
    }
}