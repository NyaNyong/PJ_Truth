using DG.Tweening;
using Obvious.Soap;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class GameManager : MonoBehaviour
{
    public enum GamePhase { Morning, Day, Night, Whiteboard }

    [Header("게임 진행 상태")]
    public GamePhase currentPhase;
    public int currentDay = 1;
    [SerializeField] private int startDay = 4;

    [Header("--- 테스트 전용 (빌드 시 반드시 비활성화) ---")]
    [SerializeField] private bool enableTestFlags = false;
    [SerializeField] private List<string> testFlags = new List<string>();
    // 예: "route_stealth", "censor_heavy_day6", "hint_route_stealth"

    [Header("SOAP Variables")]
    [SerializeField] private IntVariable soapCurrentDay;

    [Header("SOAP Events")]
    [SerializeField] private ScriptableEventNoParam onApproveClicked;
    [SerializeField] private ScriptableEventNoParam onLocationSelected;
    [SerializeField] private ScriptableEventNoParam onPhaseTransitionRequest;

    // ── 아침 페이즈 ──────────────────────────────────────────────────────
    [Header("UI - 아침 (공식 뉴스)")]
    [SerializeField] private CanvasGroup morningNewsPanelCG;
    [SerializeField] private TextMeshProUGUI officialNewsTitleUI;
    [SerializeField] private TextMeshProUGUI officialNewsContentUI;
    [SerializeField] private GameObject morningNewsButton;

    [Header("UI - 아침 (사설 뉴스)")]
    [SerializeField] private CanvasGroup privatePanelCG;
    [SerializeField] private TextMeshProUGUI privateNewsTitleUI;
    [SerializeField] private TextMeshProUGUI privateNewsContentUI;

    // ── 낮 페이즈 ────────────────────────────────────────────────────────
    [Header("UI - 낮 페이즈")]
    [SerializeField] private CanvasGroup documentPanelCG;
    [SerializeField] private CanvasGroup guidelinePanelCG;
    [SerializeField] private GameObject guidelineButton;
    [SerializeField] private TextMeshProUGUI guidelineTextUI;
    [SerializeField] private DocumentViewer documentViewer;
    [SerializeField] private GameObject dayPhaseBG;

    [Header("UI - 낮 페이즈 상단 헤더")]
    [SerializeField] private CanvasGroup dayHeaderCG;
    [SerializeField] private TextMeshProUGUI dayHeaderDayText;
    [SerializeField] private TextMeshProUGUI dayHeaderKPIText;

    [Header("승인 도장")]
    [SerializeField] private CanvasGroup stampCG;
    [SerializeField] private RectTransform stampRT;

    // ── 밤 페이즈 ────────────────────────────────────────────────────────
    [Header("UI - 밤 페이즈")]
    [SerializeField] private NightMapUI nightMapUI;

    // ── 매니저 연결 ──────────────────────────────────────────────────────
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

    // ── 화이트보드 → 다음 스테이지 암시 ─────────────────────────────────
    [Header("다음 스테이지 암시")]
    [SerializeField] private CanvasGroup blackoutCG;
    [SerializeField] private CanvasGroup nextStagePanelCG;
    [SerializeField] private TextMeshProUGUI nextStageHintText;
    [SerializeField] private string defaultNextStageHint = "...진실은 더 깊은 곳에 있다.";
    [SerializeField] private float blackoutFadeDuration = 0.8f;

    // ★ 엔딩 ──────────────────────────────────────────────────────────────
    [Header("엔딩")]
    [SerializeField] private EndingScreenUI endingScreenUI;

    private DailyData todaysData;
    private bool privateNewsViewed = false;

    // ── 이벤트 ───────────────────────────────────────────────────────────
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

    // ── 시작 ─────────────────────────────────────────────────────────────
    private void Start()
    {
        InitializePanels();
        if (idCardUI == null) StartDay(startDay);
    }

    private void InitializePanels()
    {
        nightPhaseManager?.EnsureHidden();
        HideImmediate(documentPanelCG);
        HideImmediate(guidelinePanelCG);
        HideImmediate(morningNewsPanelCG);
        HideImmediate(privatePanelCG);
        HideImmediate(dayHeaderCG);
        HideImmediate(blackoutCG);
        HideImmediate(nextStagePanelCG);          // alpha=0으로 숨김
        if (nextStageHintText != null) nextStageHintText.text = "";
        // ★ nextStagePanelCG.SetActive(false) 제거 — SetActive 사용 금지
        if (guidelineButton != null) guidelineButton.SetActive(false);
        if (dayPhaseBG != null) dayPhaseBG.SetActive(false);
        if (stampCG != null) { stampCG.alpha = 0f; stampCG.gameObject.SetActive(false); }
        if (morningNewsButton != null)
        {
            morningNewsButton.SetActive(false);
            var img = morningNewsButton.GetComponent<Image>();
            if (img != null) img.alphaHitTestMinimumThreshold = 0.1f;
        }
    }

    // ── 날짜 시작 ─────────────────────────────────────────────────────────
    public void StartDay(int day)
    {
        Debug.Log($"[GameManager] StartDay({day})");
        currentDay = day;
        if (soapCurrentDay != null) soapCurrentDay.Value = day;

        nightPhaseManager?.ResetVisited();
        privateNewsViewed = false;

#if UNITY_EDITOR
        // ★ 테스트 플래그 주입 — 에디터 전용, 빌드에 포함 안 됨
        if (enableTestFlags && testFlags != null && GameFlags.Instance != null)
        {
            foreach (var flag in testFlags)
                if (!string.IsNullOrEmpty(flag))
                    GameFlags.Instance.SetFlag(flag);
            Debug.Log($"[TEST] 플래그 주입: {string.Join(", ", testFlags)}");
        }
#endif

        LoadTodaysData();
        if (todaysData == null) { Debug.LogWarning($"[GameManager] Day{day} DailyData 없음"); return; }

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

    // ── 패널 정리 ─────────────────────────────────────────────────────────
    private void HideAllPanels()
    {
        HideImmediate(documentPanelCG);
        HideImmediate(guidelinePanelCG);
        HideImmediate(morningNewsPanelCG);
        HideImmediate(privatePanelCG);
        HideImmediate(dayHeaderCG);
        // blackout/nextStage는 암시 연출 완료 후 OnClickNextStageConfirm에서 정리
        if (guidelineButton != null) guidelineButton.SetActive(false);
        if (dayPhaseBG != null) dayPhaseBG.SetActive(false);
        if (morningNewsButton != null)
        {
            morningNewsButton.transform.DOKill();
            morningNewsButton.SetActive(false);
        }
        documentViewer?.HideDocument();
    }

    private void HideImmediate(CanvasGroup cg)
    {
        if (cg == null) return;
        cg.DOKill();
        cg.alpha = 0f; cg.interactable = false; cg.blocksRaycasts = false;
    }

    private void LoadTodaysData()
        => todaysData = dailyDataList?.Find(d => d.dayNumber == currentDay);

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

    // ── 아침 페이즈 ───────────────────────────────────────────────────────
    private void ActivateMorningPhase()
    {
        AudioManager.Instance?.PlayDayPhase();
        if (todaysData == null) return;
        if (dayPhaseBG != null) dayPhaseBG.SetActive(true);

        if (officialNewsTitleUI != null) officialNewsTitleUI.text = todaysData.officialNewsTitle;
        if (officialNewsContentUI != null) officialNewsContentUI.text = todaysData.officialNewsContent;

        if (todaysData.hasPrivateNews)
        {
            if (privateNewsTitleUI != null) privateNewsTitleUI.text = todaysData.privateNewsTitle;
            if (privateNewsContentUI != null) privateNewsContentUI.text = todaysData.privateNewsContent;
            if (privatePanelCG != null)
            {
                privatePanelCG.gameObject.SetActive(true);
                privatePanelCG.alpha = 0f;
                privatePanelCG.interactable = false;
                privatePanelCG.blocksRaycasts = false;
            }
        }

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
            morningNewsButton.transform.DOScale(Vector3.zero, 0.15f).SetEase(Ease.InBack)
                .OnComplete(() => morningNewsButton.SetActive(false));

        ShowPanel(morningNewsPanelCG);
    }

    /// <summary>공식 뉴스 패널 X 버튼 — 사설 뉴스가 있으면 사설 뉴스로, 없으면 낮 페이즈로</summary>
    public void OnClickOfficialNewsClose()
    {
        AudioManager.Instance?.PlaySfxDayInteraction();
        FadeOutPanel(morningNewsPanelCG, () =>
        {
            if (todaysData != null && todaysData.hasPrivateNews && !privateNewsViewed)
                ShowPanel(privatePanelCG);
            else
                GoToNextPhase();
        });
    }

    /// <summary>사설 뉴스 확인 버튼 → 낮 페이즈 진입</summary>
    public void OnClickPrivateNewsConfirm()
    {
        AudioManager.Instance?.PlaySfxDayInteraction();
        privateNewsViewed = true;
        FadeOutPanel(privatePanelCG, () => GoToNextPhase());
    }

    // 하위 호환: 기존 Inspector 연결 유지용
    public void OnClickMorningNewsConfirm() => OnClickOfficialNewsClose();

    // ── 낮 페이즈 ─────────────────────────────────────────────────────────
    private void ActivateDayPhase()
    {
        AudioManager.Instance?.PlayDayPhase();
        if (todaysData == null || todaysData.documentToProcess == null)
        {
            // ★ 문서 없는 Day (Day 7 등) → Night 페이즈로 직행
            Debug.Log("[GameManager] Day 페이즈: 문서 없음 → Night 스킵");
            ChangePhase(GamePhase.Night);
            return;
        }
        if (dayPhaseBG != null) dayPhaseBG.SetActive(true);

        GameTextLoader.Instance?.InjectIntoDocument(todaysData.documentToProcess);
        documentViewer?.ShowDocument(todaysData.documentToProcess);
        if (guidelineTextUI != null) guidelineTextUI.text = todaysData.documentToProcess.guidelineText;
        ShowPanel(documentPanelCG);
        if (guidelineButton != null) guidelineButton.SetActive(true);

        if (dayHeaderCG != null)
        {
            if (dayHeaderDayText != null) dayHeaderDayText.text = $"Day {currentDay}";
            RefreshDayHeaderKPI();
            ShowPanel(dayHeaderCG, 0.2f);
        }
    }

    private void RefreshDayHeaderKPI()
    {
        if (dayHeaderKPIText == null || ScoringSystem.Instance == null) return;
        float kpi = ScoringSystem.Instance.KPIProgress;
        string grade = kpi >= 0.9f ? "S" : kpi >= 0.75f ? "A" :
                       kpi >= 0.55f ? "B" : kpi >= 0.35f ? "C" : "F";
        dayHeaderKPIText.text = $"승급도: {grade} ({(int)(kpi * 100)}%)";
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

    // ── 밤 페이즈 ─────────────────────────────────────────────────────────
    private void ActivateNightPhase()
    {
        AudioManager.Instance?.PlayNightPhase();
        if (nightMapUI == null || nightPhaseManager == null || todaysData == null) return;

        var locationNames = LocationUnlockManager.Instance != null
            ? LocationUnlockManager.Instance.GetAvailableLocations(todaysData, currentDay)
            : todaysData.availableLocations;

        nightMapUI.ShowMap(nightPhaseManager.BuildButtonInfoList(locationNames));
    }

    private void OnLocationSelectedHandler() { }

    public void OnClickGoHome()
    {
        if (nightPhaseManager == null) return;
        var allLocs = GetCurrentLocationNames();
        bool requireAll = todaysData != null && todaysData.requireAllLocations;
        bool allVisited = nightPhaseManager.HasVisitedAll(allLocs);
        bool returnToMap = requireAll && !allVisited;

        ConfirmPopupUI.Instance?.Open(
            title: returnToMap ? "다른 곳으로 가시겠습니까?" : "집으로 돌아가시겠습니까?",
            message: "",
            warning: "다시 조사할 수 없습니다",
            onConfirm: () => GoToNextPhase(),
            confirmText: "예",
            cancelText: "아니요");
    }

    private List<string> GetCurrentLocationNames()
    {
        if (todaysData == null) return new List<string>();
        return LocationUnlockManager.Instance != null
            ? LocationUnlockManager.Instance.GetAvailableLocations(todaysData, currentDay)
            : todaysData.availableLocations ?? new List<string>();
    }

    public bool ShouldReturnToMap()
    {
        bool requireAll = todaysData != null && todaysData.requireAllLocations;
        bool allVisited = nightPhaseManager?.HasVisitedAll(GetCurrentLocationNames()) ?? true;
        return requireAll && !allVisited;
    }

    // ── 화이트보드 ────────────────────────────────────────────────────────
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
            ShowNextStageTransition();
        }
    }

    // ── 페이즈 전환 ───────────────────────────────────────────────────────
    public void GoToNextPhase()
    {
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
                {
                    bool requireAll = todaysData != null && todaysData.requireAllLocations;
                    bool allVisited = nightPhaseManager.HasVisitedAll(GetCurrentLocationNames());
                    if (requireAll && !allVisited)
                    {
                        nightPhaseManager.DeactivateNightView(() => ActivateNightPhase());
                    }
                    else
                    {
                        // ★ skipWhiteboard: true면 화이트보드 건너뛰고 엔딩 판정
                        bool skipWB = GameTextLoader.Instance?.GetSkipWhiteboard() ?? false;
                        nightPhaseManager.DeactivateNightView(() =>
                        {
                            if (skipWB) TryShowEnding();
                            else ChangePhase(GamePhase.Whiteboard);
                        });
                    }
                }
                else
                {
                    bool skipWB = GameTextLoader.Instance?.GetSkipWhiteboard() ?? false;
                    if (skipWB) TryShowEnding();
                    else ChangePhase(GamePhase.Whiteboard);
                }
                break;

            case GamePhase.Whiteboard:
                ShowNextStageTransition();
                break;
        }
    }

    // ★ 엔딩 분기 판정 ─────────────────────────────────────────────────────
    // 기존 코드 (428~437줄) 교체
    private void TryShowEnding()
    {
        if (GameFlags.Instance == null) { ShowNextStageTransition(); return; }

        string key = ResolveEndingKey();
        ShowEnding(key);
    }

    private string ResolveEndingKey()
    {
        var flags = GameFlags.Instance;

        // 잠입 루트: choice 플래그 우선순위 expose > family_only > remain
        if (flags.HasFlag("route_stealth"))
        {
            if (flags.HasFlag("choice_stealth_expose")) return "ending_stealth_expose";
            if (flags.HasFlag("choice_family_only")) return "ending_family_only";
            if (flags.HasFlag("choice_remain")) return "ending_new_manager";
            return "ending_new_manager"; // fallback
        }

        // 폭로 루트 (추후 구현)
        if (flags.HasFlag("route_expose"))
            return "route_expose_placeholder";

        return "ending_new_manager";
    }

    // ★ 엔딩 표시 ──────────────────────────────────────────────────────────
    private void ShowEnding(string key)
    {
        HideAllPanels();
        nightPhaseManager?.EnsureHidden();
        Debug.Log($"[GameManager] ShowEnding: {key}");

        if (blackoutCG != null)
        {
            blackoutCG.gameObject.SetActive(true);
            blackoutCG.alpha = 0f;
            blackoutCG.interactable = false;
            blackoutCG.blocksRaycasts = true;
            blackoutCG.DOFade(1f, blackoutFadeDuration).OnComplete(() =>
            {
                if (endingScreenUI != null)
                    endingScreenUI.Show(key, () => FadeOutBlackout(() => StartDay(startDay)));
                else
                {
                    Debug.LogWarning("[GameManager] EndingScreenUI 미연결 — 메인으로 복귀");
                    FadeOutBlackout(() => StartDay(startDay));
                }
            });
        }
        else
        {
            endingScreenUI?.Show(key, () => StartDay(startDay));
        }
    }

    // ── 스테이지 전환 암시 ────────────────────────────────────────────────
    private void ShowNextStageTransition()
    {
        if (blackoutCG == null) { StartDay(currentDay + 1); return; }

        blackoutCG.gameObject.SetActive(true);
        blackoutCG.alpha = 0f;
        blackoutCG.interactable = false;
        blackoutCG.blocksRaycasts = true;

        blackoutCG.DOFade(1f, blackoutFadeDuration).SetEase(Ease.InQuad).OnComplete(() =>
        {
            var dialogue = GameTextLoader.Instance?.GetNextStageDialogue();
            Debug.Log($"[Transition] dialogue={dialogue?.Count ?? -1} / DialogueUI={DialogueUI.Instance != null}");
            if (dialogue != null && dialogue.Count > 0)
                DialogueUI.Instance?.ShowStandaloneLines(dialogue, ShowNextStageHintPanel); // ★ 대사 먼저
            else
                ShowNextStageHintPanel(); // ★ 대사 없으면 바로 암시
        });
    }

    private void ShowNextStageHintPanel()
    {
        if (nextStagePanelCG == null) { StartDay(currentDay + 1); return; }

        if (nextStageHintText != null)
            nextStageHintText.text = GetNextStageHint();

        // ★ SetActive 없이 alpha만으로 제어 (깜빡임 원천 차단)
        nextStagePanelCG.DOKill();
        nextStagePanelCG.alpha = 0f;
        nextStagePanelCG.interactable = false;
        nextStagePanelCG.blocksRaycasts = false;
        nextStagePanelCG.DOFade(1f, 0.4f).OnComplete(() =>
        {
            nextStagePanelCG.interactable = true;
            nextStagePanelCG.blocksRaycasts = true;
        });
    }

    /// <summary>암시 팝업 확인 버튼 onClick</summary>
    public void OnClickNextStageConfirm()
    {
        nextStagePanelCG.interactable = false;
        nextStagePanelCG.blocksRaycasts = false;
        nextStagePanelCG.DOFade(0f, 0.3f).OnComplete(() =>
        {
            // ★ SetActive(false) 제거
            FadeOutBlackout(() => StartDay(currentDay + 1));
        });
    }

    private void FadeOutBlackout(System.Action onComplete)
    {
        if (blackoutCG == null) { onComplete?.Invoke(); return; }
        blackoutCG.DOFade(0f, blackoutFadeDuration).OnComplete(() =>
        {
            blackoutCG.blocksRaycasts = false;
            blackoutCG.gameObject.SetActive(false);
            onComplete?.Invoke();
        });
    }

    private string GetNextStageHint()
    {
        string jsonHint = GameTextLoader.Instance?.GetNextStageHint();
        return !string.IsNullOrEmpty(jsonHint) ? jsonHint : defaultNextStageHint;
    }

    // ── 승인 도장 ─────────────────────────────────────────────────────────
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
        { ChangePhase(GamePhase.Night); return; }

        var (censor, typewriter) = documentViewer.CalculateScore();
        var score = ScoringSystem.Instance.CalculateAndRecord(censor, typewriter, currentDay);
        float kpiProgress = ScoringSystem.Instance.KPIProgress;
        int totalDocs = ScoringSystem.Instance.TotalProcessedDocuments;

        GameFlags.Instance?.RemoveFlag($"censor_heavy_day{currentDay}");
        GameFlags.Instance?.RemoveFlag($"censor_mild_day{currentDay}");
        GameFlags.Instance?.RemoveFlag($"censor_weak_day{currentDay}");
        GameFlags.Instance?.SetFlag(GetCensorIntensityFlag(score.grade, currentDay, typewriter));

        // ★ Day 6 잠입 루트 분기: censor_heavy_day6이면 route_stealth 확정
        // ★ Day 6 루트 분기 플래그 세팅 (기존 코드 교체)
        if (currentDay == 6)
        {
            if (GameFlags.Instance?.HasFlag("censor_heavy_day6") == true)
            {
                GameFlags.Instance?.SetFlag("route_stealth");
                GameFlags.Instance?.RemoveFlag("route_expose");
            }
            else
            {
                GameFlags.Instance?.RemoveFlag("route_stealth");
                GameFlags.Instance?.SetFlag("route_expose"); // ★ 폭로 루트 구현 시 사용
            }
        }

        if (resultScreenUI != null)
            resultScreenUI.Show(score, kpiProgress, totalDocs, () =>
            {
                if (currentDay >= 7) TryShowEnding();
                else ChangePhase(GamePhase.Night);
            });
        else
        {
            if (currentDay >= 7) TryShowEnding();
            else ChangePhase(GamePhase.Night);
        }
    }

    private string GetCensorIntensityFlag(Grade grade, int day, float typewriterScore = 0f)
    {
        var criticals = todaysData?.documentToProcess?.criticalCensorKeywords;
        if (documentViewer != null && criticals != null && criticals.Count > 0)
        {
            int censored = criticals.Count(k => documentViewer.WasKeywordCensored(k));
            bool typewriterContributes = typewriterScore > 0f;

            string kw;
            if (censored == criticals.Count)
                kw = "heavy";
            else if (censored >= 1 || typewriterContributes)
                kw = "mild";
            else
                kw = "weak";

            Debug.Log($"[Score] 핵심키워드 {censored}/{criticals.Count} 검열, 타자기 {typewriterScore:F1}pt → censor_{kw}_day{day}");
            return $"censor_{kw}_day{day}";
        }

        string intensity;
        if (grade is Grade.S or Grade.A)
            intensity = "heavy";
        else if (grade == Grade.B || typewriterScore > 0f)
            intensity = "mild";
        else
            intensity = "weak";
        return $"censor_{intensity}_day{day}";
    }

    // ── 공통 패널 헬퍼 ────────────────────────────────────────────────────
    private void ShowPanel(CanvasGroup cg, float duration = -1f)
    {
        if (cg == null) return;
        float d = duration < 0f ? panelFadeDuration : duration;
        cg.gameObject.SetActive(true);
        cg.alpha = 0f; cg.interactable = false; cg.blocksRaycasts = false;
        cg.DOFade(1f, d).SetEase(Ease.InQuad)
            .OnComplete(() => { cg.interactable = true; cg.blocksRaycasts = true; });
    }

    private void FadeOutPanel(CanvasGroup cg, System.Action onComplete = null)
    {
        if (cg == null) { onComplete?.Invoke(); return; }
        cg.interactable = false; cg.blocksRaycasts = false;
        cg.DOFade(0f, panelFadeDuration).SetEase(Ease.OutQuad)
            .OnComplete(() => { onComplete?.Invoke(); });
    }

    
}