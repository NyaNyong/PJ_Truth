using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;
using System.Collections.Generic;

/// <summary>
/// 엔딩 최종 공식뉴스 패널.
/// 컷씬 없이 공식뉴스 1장 표시 → 확인 → 게임 리셋.
/// </summary>
public class EndingScreenUI : MonoBehaviour
{
    [Header("패널")]
    [SerializeField] private CanvasGroup panelCG;

    [Header("공식뉴스 텍스트")]
    [SerializeField] private TextMeshProUGUI newsTitleText;
    [SerializeField] private TextMeshProUGUI newsContentText;

    [Header("확인 버튼")]
    [SerializeField] private Button confirmButton;

    [Header("DOTween")]
    [SerializeField] private float fadeDuration = 0.4f;

    private System.Action onConfirmCallback;

    // ─── 엔딩별 공식뉴스 텍스트 ──────────────────────────────────────────
    private static readonly Dictionary<string, (string title, string content)> EndingNews
        = new Dictionary<string, (string, string)>
    {
        ["ending_stealth_expose"] = (
            "상위기록실 원본 기록 외부 유출… 정보관리 체계 전면 중단",
            "진실보관소 상위기록실에 보관된 원본 기록 다수가 외부로 유출되었다.\n" +
            "관련 기관은 즉각 정보관리 체계 운영을 전면 중단하고 내부 조사에 착수했다고 밝혔다.\n" +
            "유출된 기록의 규모와 경위는 아직 확인되지 않았다."
        ),
        ["ending_family_only"] = (
            "실종 사건 추가 단서 없어… 기존 조사 유지",
            "최근 확산된 여러 제보에도 불구하고\n" +
            "실종 사건과 관련한 새로운 사실은 확인되지 않았다고 관계 당국은 밝혔다.\n" +
            "수사는 기존 절차에 따라 진행될 예정이다."
        ),
        ["ending_new_manager"] = (
            "정보 안정화 정책 확대 시행… 검증 체계 강화",
            "진실보관소는 정보 안정화 정책을 확대 시행하고 내부 검증 체계를 강화한다고 밝혔다.\n" +
            "고등급 검열 인력이 충원될 예정이며, 상위 기록 관리 절차도 정비될 예정이다."
        ),
        ["route_expose_placeholder"] = (
            "이 루트는 추후 공개됩니다",
            "폭로 루트는 현재 개발 중입니다.\n다시 플레이하여 잠입 루트를 선택해 보세요."
        ),
    };

    // ─────────────────────────────────────────────────────────────────────
    private void Awake()
    {
        HideImmediate();
        if (confirmButton != null)
            confirmButton.onClick.AddListener(OnClickConfirm);
    }

    // ─────────────────────────────────────────────────────────────────────
    public void Show(string endingKey, System.Action onConfirm)
    {
        onConfirmCallback = onConfirm;

        if (!EndingNews.TryGetValue(endingKey, out var news))
        {
            Debug.LogWarning($"[EndingScreenUI] 등록되지 않은 엔딩 키: {endingKey}");
            news = ("알 수 없는 결말", "기록이 남아 있지 않습니다.");
        }

        if (newsTitleText  != null) newsTitleText.text  = news.title;
        if (newsContentText != null) newsContentText.text = news.content;

        panelCG.gameObject.SetActive(true);
        panelCG.alpha         = 0f;
        panelCG.interactable  = false;
        panelCG.blocksRaycasts = false;

        panelCG.DOFade(1f, fadeDuration).OnComplete(() =>
        {
            panelCG.interactable  = true;
            panelCG.blocksRaycasts = true;
        });
    }

    private void OnClickConfirm()
    {
        panelCG.interactable  = false;
        panelCG.blocksRaycasts = false;
        panelCG.DOFade(0f, fadeDuration).OnComplete(() =>
        {
            HideImmediate();
            onConfirmCallback?.Invoke(); // → GameManager.StartDay(startDay)
        });
    }

    private void HideImmediate()
    {
        if (panelCG == null) return;
        panelCG.alpha         = 0f;
        panelCG.interactable  = false;
        panelCG.blocksRaycasts = false;
        panelCG.gameObject.SetActive(false);
    }
}
