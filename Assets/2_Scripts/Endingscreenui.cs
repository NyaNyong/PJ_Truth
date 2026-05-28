using DG.Tweening;
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// 엔딩 화면 컴포넌트.
/// GameManager.ShowEnding(key) → Show(key, onConfirm) 호출.
/// Inspector: panelCG, endingTitleUI, newsTitleUI, newsContentUI 연결.
/// 확인 버튼 onClick → OnClickConfirm().
/// </summary>
public class EndingScreenUI : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private CanvasGroup panelCG;
    [SerializeField] private TextMeshProUGUI endingTitleUI;   // 엔딩명 (예: "나만의 진실")
    [SerializeField] private TextMeshProUGUI newsTitleUI;     // 공식 뉴스 헤드라인
    [SerializeField] private TextMeshProUGUI newsContentUI;   // 공식 뉴스 본문

    [Header("페이드 설정")]
    [SerializeField] private float fadeDuration = 0.5f;

    private Action pendingCallback;

    // ── 엔딩 데이터 (key → 엔딩명 / 뉴스 제목 / 뉴스 본문) ────────────────
    // EndingScreenUI.cs — EndingTable 딕셔너리 교체
    private static readonly Dictionary<string, (string title, string newsTitle, string newsContent)>
        EndingTable = new Dictionary<string, (string, string, string)>
        {
            // ── 잠입 루트 엔딩 ───────────────────────────────────────────────
            ["ending_stealth_expose"] = (
                "안에서 무너뜨리다",
                "상위기록실 원본 기록 외부 유출… 정보관리 체계 전면 중단",
                "진실보관소 상위기록실에 보관된 원본 기록 다수가 외부로 유출되었다.\n관련 기관은 즉각 정보관리 체계 운영을 전면 중단하고 내부 조사에 착수했다고 밝혔다.\n유출된 기록의 규모와 경위는 아직 확인되지 않았다."
            ),
            ["ending_family_only"] = (
                "나만의 진실",
                "실종 사건 추가 단서 없어… 기존 조사 유지",
                "최근 확산된 여러 제보에도 불구하고\n실종 사건과 관련한 새로운 사실은 확인되지 않았다고 관계 당국은 밝혔다.\n수사는 기존 절차에 따라 진행될 예정이다."
            ),
            ["ending_new_manager"] = (
                "새로운 관리자",
                "정보 안정화 정책 확대 시행… 검증 체계 강화",
                "진실보관소는 정보 안정화 정책을 확대 시행하고 내부 검증 체계를 강화한다고 밝혔다.\n고등급 검열 인력이 충원될 예정이며, 상위 기록 관리 절차도 정비될 예정이다."
            ),

            // ── 폭로 루트 엔딩 (준비 중) ─────────────────────────────────────
            ["route_expose_placeholder"] = (
                "폭로 루트 (준비 중)",
                "이 루트는 추후 공개됩니다",
                "폭로 루트는 현재 개발 중입니다.\n다시 플레이하여 잠입 루트를 선택해 보세요."
            ),
        };

    // ─────────────────────────────────────────────────────────────────────
    private void Awake()
    {
        if (panelCG == null) return;
        panelCG.alpha = 0f;
        panelCG.interactable = false;
        panelCG.blocksRaycasts = false;
        panelCG.gameObject.SetActive(false);
    }

    /// <summary>GameManager에서 호출. 암전 후 이 패널이 표시된다.</summary>
    public void Show(string key, Action onConfirm)
    {
        pendingCallback = onConfirm;

        if (EndingTable.TryGetValue(key, out var data))
        {
            if (endingTitleUI != null) endingTitleUI.text = data.title;
            if (newsTitleUI != null) newsTitleUI.text = data.newsTitle;
            if (newsContentUI != null) newsContentUI.text = data.newsContent;
        }
        else
        {
            Debug.LogWarning($"[EndingScreenUI] 알 수 없는 엔딩 키: {key}");
            if (endingTitleUI != null) endingTitleUI.text = key;
        }

        if (panelCG == null) { onConfirm?.Invoke(); return; }

        panelCG.gameObject.SetActive(true);
        panelCG.alpha = 0f;
        panelCG.DOFade(1f, fadeDuration).OnComplete(() =>
        {
            panelCG.interactable = true;
            panelCG.blocksRaycasts = true;
        });
    }

    /// <summary>확인 버튼 onClick → 패널 페이드아웃 후 콜백</summary>
    public void OnClickConfirm()
    {
        if (panelCG == null)
        {
            pendingCallback?.Invoke();
            pendingCallback = null;
            return;
        }

        panelCG.interactable = false;
        panelCG.blocksRaycasts = false;
        panelCG.DOFade(0f, fadeDuration).OnComplete(() =>
        {
            panelCG.gameObject.SetActive(false);
            pendingCallback?.Invoke();
            pendingCallback = null;
        });
    }
}