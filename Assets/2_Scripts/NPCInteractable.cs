using System;
using System.Collections.Generic;
using System.Linq;
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
    private bool choicesWereUsed = false;
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

    private void OnChoiceMade(DialogueChoiceData choice)
    {
        // ★ 성과금 비용 처리
        if (choice.costBonusPay > 0)
        {
            bool success = ScoringSystem.Instance?.SpendBonusPay(choice.costBonusPay) ?? false;
            if (!success) return; // 버튼이 이미 비활성화돼야 하지만 안전 장치
        }

        if (!string.IsNullOrEmpty(choice.grantClueID) && GameFlags.Instance != null)
            GameFlags.Instance.AddClue(choice.grantClueID);
        if (!string.IsNullOrEmpty(choice.setFlag) && GameFlags.Instance != null)
            GameFlags.Instance.SetFlag(choice.setFlag);
    }

    private void OnDialogueFinished(string clueID, string flagID)
    {
        cachedPlayer?.NotifyInteractionEnded();

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
            {
                var firstLines = ResolveLines(data.firstLines, data.conditionalFirstLines);
                var choices = ResolveChoiceLines(data.choices, data.conditionalChoiceLines); // ★
                return (data.npcName, firstLines, data.repeatLines,
                        data.grantClueID, data.setFlag, choices);
            }
        }
        return (npcName, dialogueLines, repeatLines, "", "", null);
    }


    // ─── 헬퍼 ────────────────────────────────────────────────────────────

    /// <summary>conditionals 중 현재 세팅된 플래그와 일치하는 첫 항목의 lines 반환.
    /// 없으면 defaultLines 반환.</summary>
    private static List<string> ResolveLines(
        List<string> defaultLines,
        List<ConditionalLinesData> conditionals)
    {
        if (conditionals != null && GameFlags.Instance != null)
        {
            var match = conditionals.FirstOrDefault(
                c => !string.IsNullOrEmpty(c.requiredFlag) &&
                     GameFlags.Instance.HasFlag(c.requiredFlag));
            if (match != null) return match.lines;
        }
        return defaultLines;
    }

    /// <summary>각 선택지의 lines를 conditionalLines 기준으로 해석한 복사본 반환.</summary>
    private static List<DialogueChoiceData> ResolveChoiceLines(
    List<DialogueChoiceData> choices,
    List<ConditionalChoiceLineData> conditionalChoiceLines) // ★ 시그니처 변경
    {
        if (choices == null) return null;

        var resolved = new List<DialogueChoiceData>(choices.Count);
        for (int i = 0; i < choices.Count; i++)
        {
            var c = choices[i];
            List<string> resolvedLines = c.lines;

            // 해당 인덱스의 조건부 대사 탐색
            if (conditionalChoiceLines != null && GameFlags.Instance != null)
            {
                foreach (var cond in conditionalChoiceLines)
                {
                    if (cond.choiceIndex == i &&
                        !string.IsNullOrEmpty(cond.requiredFlag) &&
                        GameFlags.Instance.HasFlag(cond.requiredFlag))
                    {
                        resolvedLines = cond.lines;
                        break;
                    }
                }
            }

            resolved.Add(new DialogueChoiceData
            {
                label = c.label,
                grantClueID = c.grantClueID,
                setFlag = c.setFlag,
                costBonusPay = c.costBonusPay, // ★
                lines = resolvedLines
            });
        }
        return resolved;
    }
}