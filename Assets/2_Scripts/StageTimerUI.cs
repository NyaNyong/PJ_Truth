using UnityEngine;
using TMPro;
using DG.Tweening;
using System.Collections;
using System.Collections.Generic;

public class StageTimerUI : MonoBehaviour
{
    public static StageTimerUI Instance { get; private set; }

    [Header("패널")]
    [SerializeField] private CanvasGroup panelCG;

    [Header("타이머 UI")]
    [SerializeField] private TextMeshProUGUI timerText;
    [SerializeField] private TextMeshProUGUI warningMessageText;

    [Header("설정")]
    [SerializeField] private float warningInterval = 30f;
    [SerializeField] private float urgentThreshold = 60f;
    [SerializeField] private Color urgentColor = new Color(0.9f, 0.2f, 0.2f);
    [SerializeField] private Color normalColor = Color.white;

    [Header("상사 경고 메시지")]
    [SerializeField]
    private List<string> warningMessages = new List<string>
    {
        "위치 확인 중입니다.",
        "지금 돌아오면 절차상 실수로 처리할 수 있습니다.",
        "더 이상 움직이지 마십시오.",
        "마지막으로 경고합니다."
    };

    // ★ 추가 — 시간대별 힌트
    [Header("힌트 메시지 (남은 시간 기준)")]
    [SerializeField] private TextMeshProUGUI hintText;
    [SerializeField]
    private List<HintEntry> hintEntries = new List<HintEntry>
    {
        new HintEntry { atSeconds = 180f, message = "책상을 둘러보자" },
        new HintEntry { atSeconds = 120f, message = "어딘가에 힌트가 적혀있을 것이다" },
        new HintEntry { atSeconds = 60f,  message = "사진이 수상하다" },
    };

    private float remainingTime;
    private bool isRunning;
    private System.Action onTimeoutCallback;
    private Coroutine timerCoroutine;
    private Coroutine warningCoroutine;
    private HashSet<float> shownHints = new HashSet<float>(); // ★ 추가

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        HideImmediate();
    }

    // ── 외부 호출 ─────────────────────────────────────────────────────────
    public void StartTimer(float seconds, System.Action onTimeout)
    {
        remainingTime = seconds;
        onTimeoutCallback = onTimeout;
        isRunning = true;
        shownHints.Clear(); // ★ 추가 — 매 타이머 시작마다 힌트 재사용 가능하도록 초기화

        Show();

        if (timerCoroutine != null) StopCoroutine(timerCoroutine);
        if (warningCoroutine != null) StopCoroutine(warningCoroutine);

        timerCoroutine = StartCoroutine(TimerRoutine());
        warningCoroutine = StartCoroutine(WarningRoutine());
    }

    public void StopTimer()
    {
        if (!isRunning) return;
        isRunning = false;
        if (timerCoroutine != null) StopCoroutine(timerCoroutine);
        if (warningCoroutine != null) StopCoroutine(warningCoroutine);
        Hide();
    }

    // ── 코루틴 ───────────────────────────────────────────────────────────
    private IEnumerator TimerRoutine()
    {
        while (remainingTime > 0f && isRunning)
        {
            UpdateDisplay();
            CheckHints(); // ★ 추가
            yield return new WaitForSeconds(1f);
            remainingTime = Mathf.Max(0f, remainingTime - 1f);
        }

        if (!isRunning) yield break;

        UpdateDisplay(); // 00:00 표시
        yield return new WaitForSeconds(0.8f);
        isRunning = false;
        Hide();
        onTimeoutCallback?.Invoke();
    }

    private IEnumerator WarningRoutine()
    {
        int idx = 0;
        while (isRunning)
        {
            yield return new WaitForSeconds(warningInterval);
            if (!isRunning) break;
            ShowWarning(warningMessages[idx % warningMessages.Count]);
            idx++;
        }
    }

    // ★ 추가 — 남은 시간이 지정 시점 이하로 내려가면 한 번씩 힌트 표시
    private void CheckHints()
    {
        if (hintEntries == null) return;
        foreach (var hint in hintEntries)
        {
            if (shownHints.Contains(hint.atSeconds)) continue;
            if (remainingTime <= hint.atSeconds)
            {
                shownHints.Add(hint.atSeconds);
                ShowHint(hint.message);
            }
        }
    }

    // ★ 추가
    private void ShowHint(string msg)
    {
        if (hintText == null) return;
        hintText.DOKill();
        hintText.transform.DOKill();

        hintText.text = msg;
        hintText.color = new Color(1f, 1f, 1f, 0f);
        hintText.transform.localScale = Vector3.one * 0.7f;

        hintText.DOFade(1f, 0.3f)
            .OnComplete(() => hintText.DOFade(0f, 0.5f).SetDelay(3f));

        hintText.transform.DOScale(1.15f, 0.25f).SetEase(Ease.OutBack)
            .OnComplete(() => hintText.transform.DOScale(1f, 0.15f));
    }

    // ── UI 갱신 ──────────────────────────────────────────────────────────
    private void UpdateDisplay()
    {
        if (timerText == null) return;
        int m = Mathf.FloorToInt(remainingTime / 60f);
        int s = Mathf.FloorToInt(remainingTime % 60f);
        timerText.text = $"제한 시간: {m:00}:{s:00}";

        bool urgent = remainingTime <= urgentThreshold;
        timerText.color = urgent ? urgentColor : normalColor;
        if (urgent)
            timerText.transform.DOShakePosition(0.25f, 2f, 15).SetEase(Ease.OutQuad);
    }

    private void ShowWarning(string msg)
    {
        if (warningMessageText == null) return;
        warningMessageText.DOKill();
        warningMessageText.text = msg;
        warningMessageText.color = new Color(1f, 1f, 1f, 0f);
        warningMessageText.DOFade(1f, 0.3f)
            .OnComplete(() => warningMessageText.DOFade(0f, 0.5f).SetDelay(3f));
    }

    // ── 패널 ─────────────────────────────────────────────────────────────
    private void Show()
    {
        panelCG.gameObject.SetActive(true);
        panelCG.alpha = 0f;
        panelCG.interactable = false;
        panelCG.blocksRaycasts = false;
        panelCG.DOFade(1f, 0.3f).OnComplete(() =>
        {
            panelCG.interactable = true;
            panelCG.blocksRaycasts = true;
        });
    }

    private void Hide()
    {
        panelCG.interactable = false;
        panelCG.blocksRaycasts = false;
        panelCG.DOFade(0f, 0.3f).OnComplete(() => panelCG.gameObject.SetActive(false));
    }

    private void HideImmediate()
    {
        if (panelCG == null) return;
        panelCG.alpha = 0f;
        panelCG.interactable = false;
        panelCG.blocksRaycasts = false;
        panelCG.gameObject.SetActive(false);
    }
}

[System.Serializable]
public class HintEntry
{
    public float atSeconds;
    [TextArea(1, 2)]
    public string message;
}