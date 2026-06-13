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
            // route_expose_placeholder 를 아래 5종으로 교체
            ["ending_expose_full"] = (
    "정보관리기관 전면 감사 착수… 상위기록실 이관 기록 조사",
    "최근 공개된 내부 기록을 통해 실종자 관리 대상자 분류와 상위기록실 이관 정황이 드러나며\n" +
    "관계 기관 전반에 대한 특별 조사가 시작되었다.\n" +
    "일부 책임자는 직무 정지된 것으로 알려졌다."
),
            ["ending_expose_partial"] = (
    "실종 기록 관련 내부 자료 유출… 관계 기관 \"절차상 오해\" 해명",
    "일부 내부 자료가 외부에 공개되며 논란이 확산되고 있다.\n" +
    "관계 기관은 해당 기록이 절차상 관리 중인 자료였으며,\n" +
    "실종 사건과 직접 연결짓는 것은 신중해야 한다고 밝혔다."
),
            ["ending_expose_silenced"] = (
    "허위 정보 유포 시도 차단… 내부 직원 조사 중",
    "검증되지 않은 내부 자료가 외부에 유포되려 했으나 관계 기관의 즉각적인 조치로 차단되었다.\n" +
    "관계 당국은 해당 자료가 사실과 다른 내용을 포함하고 있어 추가 확산을 막았다고 밝혔다."
),
            ["ending_expose_closed"] = (
    "내부 혼선 정리… 관련 기록 검토 절차 종료",
    "관계 기관은 최근 제기된 일부 기록 관련 의혹에 대해 절차상 검토를 마쳤으며,\n" +
    "추가 공개가 필요한 사항은 확인되지 않았다고 밝혔다.\n" +
    "관련 기록은 기존 기준에 따라 관리될 예정이다."
),
            ["ending_early_out"] = (
    "기준 이탈 검열관 재배치… 관련 문서 재검토 완료",
    "기준 이탈이 누적된 검열관 1명이 재배치되었다.\n" +
    "관련 문서는 재검토 완료되었으며 외부 확산 위험은 없는 것으로 확인되었다."
),
            ["ending_late_person"] = (
    "내부 보안 사고 수습 완료… 시스템 정상 운영",
    "일부 내부 혼선은 즉시 수습되었으며, 정보관리 시스템은 정상 운영 중이라고 발표했다.\n" +
    "추가 피해는 없는 것으로 알려졌다."
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
