using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 움직이지 않는 NPC. 플레이어가 E키를 누르면 대화를 시작합니다.
/// </summary>
public class NPCInteractable : MonoBehaviour, IInteractable
{
    [Header("NPC 정보")]
    [SerializeField] private string npcName = "???";

    [Header("첫 대화")]
    [SerializeField] private List<string> dialogueLines = new List<string>();

    [Header("반복 대화 (두 번째 이후)")]
    [Tooltip("비어있으면 첫 대화를 반복합니다")]
    [SerializeField] private List<string> repeatLines = new List<string>();

    [Header("단서/플래그 연동 (선택)")]
    [Tooltip("첫 대화 완료 후 획득할 단서 ID. 빈 칸이면 없음.")]
    [SerializeField] private string grantClueIDOnFinish = "";

    [Tooltip("첫 대화 완료 후 세울 플래그 ID. 빈 칸이면 없음.")]
    [SerializeField] private string setFlagOnFinish = "";

    private bool          hasSpoken    = false;
    private PlayerController cachedPlayer = null;

    public void Interact(PlayerController player)
    {
        if (DialogueUI.Instance == null)
        {
            Debug.LogWarning("[NPCInteractable] DialogueUI가 씬에 없습니다.");
            return;
        }

        // 이미 대화창이 열려있으면 무시 (중복 실행 방지)
        if (DialogueUI.Instance.IsOpen()) return;

        cachedPlayer = player;

        List<string> linesToShow = (hasSpoken && repeatLines.Count > 0)
            ? repeatLines
            : dialogueLines;

        DialogueUI.Instance.StartDialogue(npcName, linesToShow, OnDialogueFinished);
    }

    private void OnDialogueFinished()
    {
        // ★ 대화 종료 시 쿨다운 시작 — E키 즉시 재입력 방지
        cachedPlayer?.NotifyInteractionEnded();

        if (!hasSpoken)
        {
            if (!string.IsNullOrEmpty(grantClueIDOnFinish) && GameFlags.Instance != null)
                GameFlags.Instance.AddClue(grantClueIDOnFinish);

            if (!string.IsNullOrEmpty(setFlagOnFinish) && GameFlags.Instance != null)
                GameFlags.Instance.SetFlag(setFlagOnFinish);
        }

        hasSpoken = true;
    }
}
