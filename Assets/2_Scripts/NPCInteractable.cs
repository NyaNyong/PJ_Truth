using System;
using System.Collections.Generic;
using UnityEngine;

public class NPCInteractable : MonoBehaviour, IInteractable
{
    [Header("NPC 정보")]
    [SerializeField] private string npcName = "???";

    [Header("JSON 연동")]
    [Tooltip("day_XX.json 의 npcs[].id 와 일치. 비워두면 Inspector 값 사용")]
    [SerializeField] private string npcID = "";

    [Header("Inspector 폴백 대사 (JSON 미사용 시)")]
    [SerializeField] private List<string> dialogueLines = new List<string>();
    [Tooltip("비어있으면 첫 대화를 반복합니다")]
    [SerializeField] private List<string> repeatLines = new List<string>();

    [Header("단서/플래그 연동 — 선택지 없을 때만 적용")]
    [SerializeField] private string grantClueIDOnFinish = "";
    [SerializeField] private string setFlagOnFinish = "";

    private bool hasSpoken = false;
    private bool choicesWereUsed = false; // ★ 선택지 사용 여부 추적
    private PlayerController cachedPlayer = null;

    public void Interact(PlayerController player)
    {
        if (DialogueUI.Instance == null)
        {
            Debug.LogWarning("[NPCInteractable] DialogueUI가 씬에 없습니다."); return;
        }
        if (DialogueUI.Instance.IsOpen()) return;

        cachedPlayer = player;

        var (resolvedName, firstLines, resolvedRepeat, jsonClueID, jsonFlag, choices) = ResolveTextData();

        // 반복 대화에서는 선택지 없음
        List<string> linesToShow = (hasSpoken && resolvedRepeat.Count > 0)
            ? resolvedRepeat : firstLines;

        bool useChoices = !hasSpoken && choices != null && choices.Count > 0;
        choicesWereUsed = useChoices;

        DialogueUI.Instance.StartDialogue(
            resolvedName,
            linesToShow,
            useChoices ? choices : null,
            useChoices ? (Action<DialogueChoiceData>)OnChoiceMade : null,
            () => OnDialogueFinished(jsonClueID, jsonFlag)
        );
    }

    // 선택지 클릭 시 즉시 호출 — 선택지별 단서/플래그 처리
    private void OnChoiceMade(DialogueChoiceData choice)
    {
        if (!string.IsNullOrEmpty(choice.grantClueID) && GameFlags.Instance != null)
            GameFlags.Instance.AddClue(choice.grantClueID);
        if (!string.IsNullOrEmpty(choice.setFlag) && GameFlags.Instance != null)
            GameFlags.Instance.SetFlag(choice.setFlag);
    }

    // 대화 전체 종료 후 호출
    private void OnDialogueFinished(string clueID, string flagID)
    {
        cachedPlayer?.NotifyInteractionEnded();

        // 선택지가 없었던 경우에만 루트 단서/플래그 처리
        if (!hasSpoken && !choicesWereUsed)
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

    private (string name, List<string> first, List<string> repeat,
             string clueID, string flag, List<DialogueChoiceData> choices)
        ResolveTextData()
    {
        if (!string.IsNullOrEmpty(npcID) && GameTextLoader.Instance != null)
        {
            var data = GameTextLoader.Instance.GetNpc(npcID);
            if (data != null)
                return (data.npcName, data.firstLines, data.repeatLines,
                        data.grantClueID, data.setFlag, data.choices);
        }
        return (npcName, dialogueLines, repeatLines, "", "", null);
    }
}