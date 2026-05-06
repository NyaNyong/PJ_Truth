using UnityEngine;
using System.Collections.Generic;

public class NPCInteractable : MonoBehaviour, IInteractable
{
    [Header("NPC 정보")]
    [SerializeField] private string npcName = "???";

    [Header("JSON 연동")]
    [Tooltip("day_XX.json 의 npcs[].id 와 일치해야 합니다. 비워두면 Inspector 값 사용")]
    [SerializeField] private string npcID = "";

    [Header("Inspector 폴백 대사 (JSON 미사용 시)")]
    [SerializeField] private List<string> dialogueLines = new List<string>();

    [Header("반복 대화 (두 번째 이후, 폴백)")]
    [Tooltip("비어있으면 첫 대화를 반복합니다")]
    [SerializeField] private List<string> repeatLines = new List<string>();

    [Header("단서/플래그 연동 (선택)")]
    [Tooltip("첫 대화 완료 후 획득할 단서 ID")]
    [SerializeField] private string grantClueIDOnFinish = "";
    [Tooltip("첫 대화 완료 후 세울 플래그 ID")]
    [SerializeField] private string setFlagOnFinish = "";

    private bool hasSpoken = false;
    private PlayerController cachedPlayer = null;

    public void Interact(PlayerController player)
    {
        if (DialogueUI.Instance == null)
        {
            Debug.LogWarning("[NPCInteractable] DialogueUI가 씬에 없습니다."); return;
        }
        if (DialogueUI.Instance.IsOpen()) return;

        cachedPlayer = player;

        // JSON 우선, 없으면 Inspector 폴백
        var (resolvedName, firstLines, resolvedRepeat, jsonClueID, jsonFlag) = ResolveTextData();

        List<string> linesToShow = (hasSpoken && resolvedRepeat.Count > 0)
            ? resolvedRepeat : firstLines;

        DialogueUI.Instance.StartDialogue(resolvedName, linesToShow,
            () => OnDialogueFinished(jsonClueID, jsonFlag));
    }

    private void OnDialogueFinished(string clueID, string flagID)
    {
        cachedPlayer?.NotifyInteractionEnded();

        if (!hasSpoken)
        {
            string finalClue = !string.IsNullOrEmpty(clueID) ? clueID : grantClueIDOnFinish;
            string finalFlag = !string.IsNullOrEmpty(flagID) ? flagID : setFlagOnFinish;

            if (!string.IsNullOrEmpty(finalClue) && GameFlags.Instance != null)
                GameFlags.Instance.AddClue(finalClue);
            if (!string.IsNullOrEmpty(finalFlag) && GameFlags.Instance != null)
                GameFlags.Instance.SetFlag(finalFlag);
        }

        hasSpoken = true;
    }

    // JSON 데이터 우선 조회, 없으면 Inspector 값 반환
    private (string name, List<string> first, List<string> repeat, string clueID, string flag)
        ResolveTextData()
    {
        if (!string.IsNullOrEmpty(npcID) && GameTextLoader.Instance != null)
        {
            var data = GameTextLoader.Instance.GetNpc(npcID);
            if (data != null)
                return (data.npcName, data.firstLines, data.repeatLines,
                        data.grantClueID, data.setFlag);
        }
        return (npcName, dialogueLines, repeatLines, "", "");
    }
}