using UnityEngine;
using DG.Tweening;
using Sirenix.OdinInspector;

/// <summary>
/// IInteractable 구현 예시 — 단서 오브젝트.
/// 플레이어가 E키를 누르면 단서 텍스트를 팝업으로 보여주고
/// PlayerController.NotifyClueCollected()를 호출합니다.
/// </summary>
public class ClueObject : MonoBehaviour, IInteractable
{
    // ─────────────────────────────────────────
    // 단서 정보
    // ─────────────────────────────────────────
    [BoxGroup("단서 정보")]
    [TextArea(2, 4)]
    [SerializeField] private string clueDescription;

    [BoxGroup("단서 정보")]
    [Tooltip("이 단서를 이미 수집했는가?")]
    [ReadOnly]
    [SerializeField] private bool isCollected = false;

    // ─────────────────────────────────────────
    // UI 연결
    // ─────────────────────────────────────────
    [BoxGroup("UI 연결")]
    [Tooltip("단서 내용을 보여줄 팝업 패널 — 없으면 Debug.Log로 대체")]
    [SerializeField] private GameObject cluePopupPanel;

    [BoxGroup("UI 연결")]
    [SerializeField] private TMPro.TextMeshProUGUI cluePopupText;

    // ─────────────────────────────────────────
    // 연출 설정
    // ─────────────────────────────────────────
    [BoxGroup("연출")]
    [Tooltip("수집 후 오브젝트를 씬에서 숨길지 여부")]
    [SerializeField] private bool hideOnCollect = true;

    // ─────────────────────────────────────────
    // IInteractable 구현
    // ─────────────────────────────────────────
    public void Interact(PlayerController player)
    {
        if (isCollected) return;

        isCollected = true;

        Debug.Log($"🔍 [단서 획득] {clueDescription}");

        // 팝업 표시
        ShowPopup();

        // 단서 획득 신호 발행
        player.NotifyClueCollected();

        // 수집 후 오브젝트 처리
        if (hideOnCollect)
        {
            transform.DOScale(Vector3.zero, 0.3f)
                     .SetEase(Ease.InBack)
                     .SetDelay(0.5f)
                     .OnComplete(() => gameObject.SetActive(false));
        }
    }

    private void ShowPopup()
    {
        if (cluePopupPanel == null) return;

        if (cluePopupText != null)
            cluePopupText.text = clueDescription;

        cluePopupPanel.SetActive(true);
        cluePopupPanel.transform.localScale = Vector3.zero;
        cluePopupPanel.transform
            .DOScale(Vector3.one, 0.35f)
            .SetEase(Ease.OutBack);
    }
}
