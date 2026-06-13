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

    private float remainingTime;
    private bool isRunning;
    private System.Action onTimeoutCallback;
    private Coroutine timerCoroutine;
    private Coroutine warningCoroutine;

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