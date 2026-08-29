using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

/// <summary>
/// 엔딩 완료 후 표시되는 크레딧 화면.
/// 순서: 팀 로고(2초) → 텍스트 크레딧(5초) → 자동 페이드아웃 → onClosed 콜백.
/// 클릭 입력 없이 시간 지나면 자동으로 넘어갑니다.
/// </summary>
public class CreditsUI : MonoBehaviour
{
    public static CreditsUI Instance { get; private set; }

    [Header("로고")]
    [SerializeField] private CanvasGroup logoCG;
    [SerializeField] private float logoHoldDuration = 2f;

    [Header("텍스트 크레딧")]
    [SerializeField] private CanvasGroup panelCG;
    [SerializeField] private float creditsHoldDuration = 5f;

    [Header("DOTween")]
    [SerializeField] private float fadeDuration = 0.5f;

    private System.Action onClosed;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        HideImmediate();
    }

    // ─── 외부 호출 ──────────────────────────────────────────────────────────
    public void Show(System.Action onClosed)
    {
        this.onClosed = onClosed;
        ShowLogo(ShowTextPanel);
    }

    // ─── 로고 (2초) ─────────────────────────────────────────────────────────
    private void ShowLogo(System.Action onDone)
    {
        if (logoCG == null) { onDone?.Invoke(); return; }

        logoCG.gameObject.SetActive(true);
        logoCG.alpha = 0f;
        logoCG.DOFade(1f, fadeDuration).OnComplete(() =>
        {
            DOVirtual.DelayedCall(logoHoldDuration, () =>
            {
                logoCG.DOFade(0f, fadeDuration).OnComplete(() =>
                {
                    logoCG.gameObject.SetActive(false);
                    onDone?.Invoke();
                });
            });
        });
    }

    // ─── 텍스트 크레딧 (5초) → 자동 종료 ───────────────────────────────────
    private void ShowTextPanel()
    {
        if (panelCG == null) { onClosed?.Invoke(); return; }

        panelCG.gameObject.SetActive(true);
        panelCG.alpha = 0f;
        panelCG.DOFade(1f, fadeDuration).OnComplete(() =>
        {
            DOVirtual.DelayedCall(creditsHoldDuration, () =>
            {
                panelCG.DOFade(0f, fadeDuration).OnComplete(() =>
                {
                    panelCG.gameObject.SetActive(false);
                    onClosed?.Invoke();
                });
            });
        });
    }

    // ─── 초기화 ─────────────────────────────────────────────────────────────
    private void HideImmediate()
    {
        if (logoCG != null)
        {
            logoCG.alpha = 0f;
            logoCG.gameObject.SetActive(false);
        }
        if (panelCG != null)
        {
            panelCG.alpha = 0f;
            panelCG.gameObject.SetActive(false);
        }
    }
}