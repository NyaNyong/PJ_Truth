using UnityEngine;
using System.Collections.Generic;
using DG.Tweening;

public class ClueObject : MonoBehaviour, IInteractable
{
    [Header("단서 정보")]
    [Tooltip("GameFlags 등록 ID + day_XX.json clues[].id 와 일치")]
    [SerializeField] private string clueID = "clue_001";

    [Header("Inspector 폴백 (JSON 미사용 시)")]
    [SerializeField] private string clueTitle = "단서";
    [SerializeField] private List<string> clueDescriptionLines = new List<string>();

    [Header("설정")]
    [SerializeField] private bool hideOnCollect = true;
    [SerializeField] private bool canReexamine = false;

    private bool isCollected = false;
    private PlayerController cachedPlayer = null;

    public void Interact(PlayerController player)
    {
        if (DialogueUI.Instance != null && DialogueUI.Instance.IsOpen()) return;

        cachedPlayer = player;

        var (title, lines) = ResolveTextData();

        if (isCollected && !canReexamine)
        {
            DialogueUI.Instance?.StartDialogue(title,
                new List<string> { "(이미 조사한 흔적이다.)" },
                () => cachedPlayer?.NotifyInteractionEnded());
            return;
        }

        DialogueUI.Instance?.StartDialogue(title, lines, OnExamineFinished);
    }

    private void OnExamineFinished()
    {
        cachedPlayer?.NotifyInteractionEnded();
        if (isCollected) return;
        isCollected = true;

        GameFlags.Instance?.AddClue(clueID);
        cachedPlayer?.NotifyClueCollected();

        if (hideOnCollect)
        {
            transform.DOScale(Vector3.zero, 0.3f)
                .SetEase(Ease.InBack).SetDelay(0.2f)
                .OnComplete(() => gameObject.SetActive(false));
        }
    }

    // JSON 우선 조회, 없으면 Inspector 폴백
    private (string title, List<string> lines) ResolveTextData()
    {
        if (GameTextLoader.Instance != null)
        {
            var data = GameTextLoader.Instance.GetClue(clueID);
            if (data != null) return (data.title, data.lines);
        }
        return (clueTitle, clueDescriptionLines);
    }
}