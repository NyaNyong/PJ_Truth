using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;

public class ResultScreenUI : MonoBehaviour
{
    public static ResultScreenUI Instance { get; private set; }

    [Header("패널")]
    [SerializeField] private CanvasGroup resultPanelCG;

    [Header("카드 — 처리문서")]
    [SerializeField] private TextMeshProUGUI documentCountText;

    [Header("카드 — 검열 정확도")]
    [SerializeField] private TextMeshProUGUI accuracyText;

    [Header("카드 — 평가 등급")]
    [SerializeField] private TextMeshProUGUI gradeText;

    // ★ 기존 kpiBarContainer, kpiBarFillRT 제거 후 교체
    [Header("승급 진행도")]
    [SerializeField] private KPISegmentedBar kpiSegmentedBar;
    [SerializeField] private TextMeshProUGUI kpiDeltaText;

    [Header("버튼")]
    [SerializeField] private Button nextPhaseButton;

    [Header("DOTween 설정")]
    [SerializeField] private float fadeDuration = 0.35f;
    [SerializeField] private float cardPopDuration = 0.35f;
    [SerializeField] private float cardStagger = 0.1f;



    private System.Action onNextAction;

    // ── 초기화 ───────────────────────────────
    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        HideImmediate();
        if (nextPhaseButton != null)
            nextPhaseButton.onClick.AddListener(OnClickNext);
    }

    // ── 표시 ────────────────────────────────
    /// <param name="score">오늘 점수 데이터</param>
    /// <param name="kpiProgressAfter">이번 날 포함 KPI 진행도 (0~1)</param>
    /// <param name="totalDocuments">누적 처리 문서 수</param>
    /// <param name="onNext">버튼 클릭 시 콜백</param>
    public void Show(DayScore score, float kpiProgressAfter,
                 int todayEarned, System.Action onNext)
    {
        onNextAction = onNext;

        if (documentCountText != null)
            documentCountText.text = todayEarned > 0 ? $"+{todayEarned}" : "0";
        if (accuracyText != null)
            accuracyText.text = $"{(int)score.totalScore}%";
        if (gradeText != null)
        {
            gradeText.text = score.grade.ToString();
            gradeText.color = Color.black; // ★ 고정
        }

        float deltaRatio = score.totalScore / 500f;
        if (kpiDeltaText != null)
            kpiDeltaText.text = $"+ {(deltaRatio * 100f):F0}%";

        // ★ 이전 값으로 즉시 세팅
        float kpiBefore = Mathf.Clamp01(kpiProgressAfter - deltaRatio);
        kpiSegmentedBar?.SetImmediate(kpiBefore);

        SetCardScales(0f);

        resultPanelCG.gameObject.SetActive(true);
        resultPanelCG.alpha = 0f;
        resultPanelCG.interactable = false;
        resultPanelCG.blocksRaycasts = false;

        resultPanelCG.DOFade(1f, fadeDuration).OnComplete(() =>
        {
            AnimateCards();

            float delay = cardStagger * 3f + cardPopDuration + 0.1f;
            DOVirtual.DelayedCall(delay, () =>
            {
                kpiSegmentedBar?.AnimateTo(kpiBefore, kpiProgressAfter);

                resultPanelCG.interactable = true;
                resultPanelCG.blocksRaycasts = true;

                // ★ 튜토리얼: 결과창
                if (score.dayNumber == 3 && TutorialManager.Instance != null)
                {
                    TutorialManager.Instance.OnResultScreen();
                }
            });
        });
    }

    public void Hide(System.Action onComplete = null)
    {
        resultPanelCG.interactable = false;
        resultPanelCG.blocksRaycasts = false;
        resultPanelCG.DOFade(0f, fadeDuration).OnComplete(() =>
        {
            resultPanelCG.gameObject.SetActive(false);
            onComplete?.Invoke();
        });
    }

    private void HideImmediate()
    {
        if (resultPanelCG == null) return;
        resultPanelCG.alpha = 0f;
        resultPanelCG.interactable = false;
        resultPanelCG.blocksRaycasts = false;
        resultPanelCG.gameObject.SetActive(false);
    }

    // ── 카드 애니메이션 ──────────────────────
    private void SetCardScales(float scale)
    {
        var cards = GetCardTransforms();
        foreach (var t in cards)
            if (t != null) t.localScale = Vector3.one * scale;
    }

    private void AnimateCards()
    {
        var cards = GetCardTransforms();
        for (int i = 0; i < cards.Length; i++)
        {
            if (cards[i] == null) continue;
            cards[i].DOScale(Vector3.one, cardPopDuration)
                    .SetDelay(i * cardStagger)
                    .SetEase(Ease.OutBack);
        }
    }

    private Transform[] GetCardTransforms()
    {
        // 카드 3개의 부모 Transform 반환
        // Inspector에서 직접 연결하거나 아래 자동 탐색 방식 사용
        var list = new System.Collections.Generic.List<Transform>();
        if (documentCountText != null) list.Add(documentCountText.transform.parent);
        if (accuracyText != null) list.Add(accuracyText.transform.parent);
        if (gradeText != null) list.Add(gradeText.transform.parent);
        return list.ToArray();
    }

    // ── 버튼 ────────────────────────────────
    private void OnClickNext() => Hide(() => onNextAction?.Invoke());

    // ── 등급 색상 ────────────────────────────
}