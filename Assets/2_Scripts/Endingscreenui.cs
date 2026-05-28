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
    private static readonly Dictionary<string, (string title, string newsTitle, string newsContent)>
        EndingTable = new Dictionary<string, (string, string, string)>
        {
            ["ending_expose_stealth"] = (
            "모든 진실은 대가를 요구한다",
            "특별 조사 착수… 정보관리기관 전면 감사",
            "최근 공개된 내부 기록과 실종 사건 관련 자료로 인해\n관계 기관 전반에 대한 특별 조사가 시작되었다.\n다수 책임자가 직무 정지된 것으로 알려졌다."
        ),
            ["ending_family"] = (
            "나만의 진실",
            "실종 사건 추가 단서 없어… 기존 조사 유지",
            "최근 확산된 여러 제보에도 불구하고\n실종 사건과 관련한 새로운 사실은 확인되지 않았다고 관계 당국은 밝혔다.\n수사는 기존 절차에 따라 진행될 예정이다."
        ),
            ["ending_system"] = (
            "기록을 다루는 자",
            "기록관리 체계 고도화… 1급 검열관 충원",
            "진실보관소는 상위 기록 관리 체계를 강화하고\n고등급 검열 인력을 충원할 계획이라고 밝혔다.\n기록의 안정적 관리를 위한 내부 절차도 정비될 예정이다."
        ),
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