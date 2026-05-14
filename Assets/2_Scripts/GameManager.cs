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
    [Tooltip("Play 전 여기서 시작 스테이지 설정. IDCard 없을 때도 이 값으로 시작")]
    [SerializeField] private int startDay = 4; // ★ 신규

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
    [Tooltip("연결하면 IDCard 확인 후 게임 시작. 비워두면 즉시 시작(테스트용)")]
    [SerializeField] private IDCardUI idCardUI;

    private DailyData todaysData;

    // ── 이벤트 ───────────────────────────────
    private void OnEnable()
    {
        if (onApproveClicked != null)        onApproveClicked.OnRaised        += GoToNextPhase;
        if (onLocationSelected != null)      onLocationSelected.OnRaised      += OnLocationSelectedHandler;
        if (onPhaseTransitionRequest != null) onPhaseTransitionRequest.OnRaised += GoToNextPhase;
    }

    private void OnDisable()
    {
        if (onApproveClicked != null)        onApproveClicked.OnRaised        -= GoToNextPhase;
        if (onLocationSelected != null)      onLocationSelected.OnRaised      -= OnLocationSelectedHandler;
        if (onPhaseTransitionRequest != null) onPhaseTransitionRequest.OnRaised -= GoToNextPhase;
    }

    // ── 시작 ─────────────────────────────────
    private void Start()
    {
        InitializePanels();
        if (idCardUI == null)
            StartDay(startDay); // ★ 1 → startDay
    }

    private void InitializePanels()
    {
        nightPhaseManager?.EnsureHidden(); // ★ 추가
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

        LoadTodaysData();
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
        cg.alpha = 0f;
        cg.interactable = false;
        cg.blocksRaycasts = false;
    }

    private void HideCanvasGroup(CanvasGroup cg)
    {
        if (cg == null) return;
        cg.alpha = 0f; cg.interactable = false; cg.blocksRaycasts = false;
    }

    private void LoadTodaysData()
    {
        todaysData = dailyDataList?.Find(d => d.dayNumber == currentDay); // ★ 인덱스 제거
    }

    private void ActivatePhase(GamePhase phase)
    {
        switch (phase)
        {
            case GamePhase.Morning:    ActivateMorningPhase();    break;
            case GamePhase.Day:        ActivateDayPhase();        break;
            case GamePhase.Night:      ActivateNightPhase();      break;
            case GamePhase.Whiteboard: ActivateWhiteboardPhase(); break;
        }
    }

    /// <summary>IDCardUI 확인 시 호출 — Inspector의 startDay로 시작</summary>
    public void StartFromBeginning() => StartDay(startDay); // ★ 신규

    // ── 아침 ─────────────────────────────────
    private void ActivateMorningPhase()
    {
        AudioManager.Instance?.PlayDayPhase();
        Debug.Log($"[아침] Stage {currentDay}");
        if (todaysData == null) return;

        if (dayPhaseBG != null) dayPhaseBG.SetActive(true);

        if (officialNewsTitleUI != null)   officialNewsTitleUI.text   = todaysData.officialNewsTitle;
        if (officialNewsContentUI != null) officialNewsContentUI.text = todaysData.officialNewsContent;

        if (morningNewsButton != null)
        {
            morningNewsButton.SetActive(true);
            morningNewsButton.transform.localScale = Vector3.zero;
            morningNewsButton.transform
                .DOScale(Vector3.one, 0.35f)
                .SetEase(Ease.OutBack);
        }
    }

    /// <summary>뉴스 버튼 클릭 → Panel_Morning 표시</summary>
    public void OnClickMorningNewsButton()
    {
        AudioManager.Instance?.PlaySfxDayInteraction(); // ★ SFX

        if (morningNewsButton != null)
        {
            morningNewsButton.transform
                .DOScale(Vector3.zero, 0.15f)
                .SetEase(Ease.InBack)
                .OnComplete(() => morningNewsButton.SetActive(false));
        }

        if (morningNewsPanelCG != null)
            morningNewsPanelCG.gameObject.SetActive(true);

        ShowPanel(morningNewsPanelCG);
    }

    public void OnClickMorningNewsConfirm()
    {
        AudioManager.Instance?.PlaySfxDayInteraction(); // ★ SFX
        Debug.Log("[GameManager] 아침 확인 클릭");
        GoToNextPhase();
    }

    // ── 낮 ───────────────────────────────────
    private void ActivateDayPhase()
    {
        AudioManager.Instance?.PlayDayPhase();
        Debug.Log($"[낮] Stage {currentDay}");
        if (todaysData == null || todaysData.documentToProcess == null)
        {
            Debug.LogWarning("documentToProcess null"); return;
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
        AudioManager.Instance?.PlaySfxCasebookToggle(); // ★ SFX
        if (guidelinePanelCG == null) return;
        bool isVisible = guidelinePanelCG.alpha > 0.5f;
        if (isVisible)
        {
            guidelinePanelCG.DOFade(0f, panelFadeDuration).SetEase(Ease.OutQuad)
                .OnComplete(() =>
                {
                    guidelinePanelCG.interactable = false;
                    guidelinePanelCG.blocksRaycasts = false;
                });
        }
        else ShowPanel(guidelinePanelCG);
    }

    // ── 밤 ───────────────────────────────────
    private void ActivateNightPhase()
    {
        AudioManager.Instance?.PlayNightPhase();
        Debug.Log("[밤] 지도 UI 오픈");
        if (nightMapUI == null || nightPhaseManager == null || todaysData == null) return;

        List<string> locationNames = LocationUnlockManager.Instance != null
            ? LocationUnlockManager.Instance.GetAvailableLocations(todaysData, currentDay)
            : todaysData.availableLocations;

        nightMapUI.ShowMap(nightPhaseManager.BuildButtonInfoList(locationNames));
    }

    private void OnLocationSelectedHandler() { }

    // ── 화이트보드 ────────────────────────────
    private void ActivateWhiteboardPhase()
    {
        AudioManager.Instance?.PlayWhiteboard();
        Debug.Log("[화이트보드] 오픈");
        if (whiteboardManager != null)
        {
            whiteboardManager.LoadFromJson();
            whiteboardManager.OpenBoard();
        }
        else
        {
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
                ShowApprovalStamp(() => ShowResultScreen());
                break;

            case GamePhase.Night:
                if (nightPhaseManager != null)
                    nightPhaseManager.DeactivateNightView(() => ChangePhase(GamePhase.Whiteboard));
                else
                    ChangePhase(GamePhase.Whiteboard);
                break;

            case GamePhase.Whiteboard:
                StartDay(currentDay + 1);
                break;
        }
    }

    // ── 승인 도장 ────────────────────────────
    private void ShowApprovalStamp(System.Action onComplete)
    {
        if (stampCG == null || stampRT == null)
        {
            onComplete?.Invoke(); return;
        }

        AudioManager.Instance?.PlaySfxApproveStamp(); // ★ SFX

        stampCG.DOKill();
        stampRT.DOKill();
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
                if (stampCG != null)
                {
                    stampCG.DOKill();
                    stampCG.alpha = 0f;
                    stampCG.gameObject.SetActive(false);
                }
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

        // ★ 검열 강도 플래그 세팅
        GameFlags.Instance?.SetFlag(GetCensorIntensityFlag(score.grade, currentDay));

        if (resultScreenUI != null)
            resultScreenUI.Show(score, kpiProgress, totalDocs, () => ChangePhase(GamePhase.Night));
        else
            ChangePhase(GamePhase.Night);
    }

    /// <summary>검열 강도 플래그 ID 반환 — 다음날 conditionalOverrides에서 사용</summary>
    private string GetCensorIntensityFlag(Grade grade, int day)
    {
        string intensity = grade switch
        {
            Grade.S or Grade.A => "heavy",
            Grade.B => "mild",
            _ => "weak"   // C, F
        };
        return $"censor_{intensity}_day{day}";
    }

    // ── 공통 패널 표시 ────────────────────────
    private void ShowPanel(CanvasGroup cg, float duration = -1f)
    {
        if (cg == null) return;
        float d = duration < 0f ? panelFadeDuration : duration;
        cg.alpha = 0f;
        cg.interactable = false;
        cg.blocksRaycasts = false;
        cg.DOFade(1f, d).SetEase(Ease.InQuad).OnComplete(() =>
        {
            cg.interactable = true;
            cg.blocksRaycasts = true;
        });
    }
}
