using UnityEngine;
using System.Collections.Generic;
using DG.Tweening;

/// <summary>
/// 조사 가능한 단서 오브젝트. E키로 조사하면 대화 UI로 설명을 보여줍니다.
/// </summary>
public class ClueObject : MonoBehaviour, IInteractable
{
    [Header("단서 정보")]
    [Tooltip("GameFlags에 등록될 고유 ID")]
    [SerializeField] private string clueID = "clue_001";

    [SerializeField] private string clueTitle = "단서";

    [SerializeField] private List<string> clueDescriptionLines = new List<string>();

    [Header("설정")]
    [SerializeField] private bool hideOnCollect  = true;
    [SerializeField] private bool canReexamine   = false;

    private bool isCollected = false;
    private PlayerController cachedPlayer = null;

    public void Interact(PlayerController player)
    {
        // 이미 대화창이 열려있으면 무시
        if (DialogueUI.Instance != null && DialogueUI.Instance.IsOpen()) return;

        cachedPlayer = player;

        if (isCollected && !canReexamine)
        {
            DialogueUI.Instance?.StartDialogue(clueTitle,
                new List<string> { "(이미 조사한 흔적이다.)" },
                () => cachedPlayer?.NotifyInteractionEnded());
            return;
        }

        DialogueUI.Instance?.StartDialogue(clueTitle, clueDescriptionLines, OnExamineFinished);
    }

    private void OnExamineFinished()
    {
        // ★ 대화 종료 시 쿨다운 시작
        cachedPlayer?.NotifyInteractionEnded();

        if (isCollected) return;
        isCollected = true;

        if (GameFlags.Instance != null)
            GameFlags.Instance.AddClue(clueID);

        cachedPlayer?.NotifyClueCollected();

        if (hideOnCollect)
        {
            transform.DOScale(Vector3.zero, 0.3f)
                .SetEase(Ease.InBack)
                .SetDelay(0.2f)
                .OnComplete(() => gameObject.SetActive(false));
        }
    }
}
