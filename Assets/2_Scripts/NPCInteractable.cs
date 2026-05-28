using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class NPCInteractable : MonoBehaviour, IInteractable
{
    [Header("NPC 정보")]
    [SerializeField] private string npcName = "???";

    [Header("JSON 연동")]
    [SerializeField] private string npcID = "";

    [Header("Inspector 폴백 대사")]
    [SerializeField] private List<string> dialogueLines = new List<string>();
    [SerializeField] private List<string> repeatLines = new List<string>();

    [Header("단서/플래그 연동 — 선택지 없을 때만 적용")]
    [SerializeField] private string grantClueIDOnFinish = "";
    [SerializeField] private string setFlagOnFinish = "";

    // ── 상태 ──────────────────────────────────────────────────────────────
    private enum NpcState { Idle, ExitedViaChoice, Done }
    private NpcState npcState = NpcState.Idle;

    private bool pendingExitClose = false;
    private bool firstConvoHadChoices = false;
    private int totalNonExitChoices = 0;

    /// <summary>세션 간 유지되는 사용 완료 선택지 인덱스 (종료 선택지 제외)</summary>
    private HashSet<int> persistentUsedIndices = new HashSet<int>();

    private PlayerController cachedPlayer = null;

    // ─────────────────────────────────────────────────────────────────────

    public void Interact(PlayerController player)
    {
        if (DialogueUI.Instance == null)
        {
            Debug.LogWarning("[NPCInteractable] DialogueUI가 씬에 없습니다."); return;
        }
        if (DialogueUI.Instance.IsOpen()) return;

        cachedPlayer = player;
        var (resolvedName, firstLines, resolvedRepeat, jsonClueID, jsonFlag, choices) = ResolveTextData();

        List<string> linesToShow;
        bool useChoices;
        HashSet<int> preUsed = null;

        switch (npcState)
        {
            case NpcState.Idle:
                linesToShow = firstLines;
                useChoices = choices != null && choices.Count > 0;
                firstConvoHadChoices = useChoices;
                if (useChoices)
                    totalNonExitChoices = choices.Count(c => !c.isExitChoice);
                break;

            case NpcState.ExitedViaChoice:
                // ★ 빈 라인 전달 → DialogueUI가 패널 열자마자 선택지로 진입
                linesToShow = new List<string>();
                useChoices = choices != null && choices.Count > 0;
                preUsed = persistentUsedIndices; // ★ 이전 세션 선택 상태 전달
                break;

            default: // Done
                linesToShow = resolvedRepeat.Count > 0 ? resolvedRepeat : firstLines;
                if (linesToShow.Count == 0) linesToShow = new List<string> { "..." };
                useChoices = false;
                break;
        }

        pendingExitClose = false;

        DialogueUI.Instance.StartDialogue(
            resolvedName,
            linesToShow,
            useChoices ? choices : null,
            useChoices ? (Action<DialogueChoiceData>)OnChoiceMade : null,
            () => OnDialogueFinished(jsonClueID, jsonFlag),
            preUsed
        );
    }

    private void OnChoiceMade(DialogueChoiceData choice)
    {
        if (choice.costBonusPay > 0)
        {
            bool success = ScoringSystem.Instance?.SpendBonusPay(choice.costBonusPay) ?? false;
            if (!success) return;
        }

        if (!string.IsNullOrEmpty(choice.grantClueID))
            GameFlags.Instance?.AddClue(choice.grantClueID);
        if (!string.IsNullOrEmpty(choice.setFlag))
            GameFlags.Instance?.SetFlag(choice.setFlag);
        if (choice.isUniqueChoice && choice.runtimeIndex >= 0)  // ★
            persistentUsedIndices.Add(choice.runtimeIndex);     // ★ 선택 즉시 영구 비활성
        if (choice.isExitChoice)
            pendingExitClose = true;
        // 일반 선택지는 DialogueUI의 usedChoiceIndices가 추적 → 종료 시 병합
    }

    private void OnDialogueFinished(string clueID, string flagID)
    {
        cachedPlayer?.NotifyInteractionEnded();

        if (pendingExitClose)
        {
            pendingExitClose = false;

            // ★ 이번 세션 사용 인덱스를 영구 저장소에 병합
            var sessionUsed = DialogueUI.Instance?.GetUsedChoiceIndices();
            if (sessionUsed != null)
                foreach (var idx in sessionUsed)
                    persistentUsedIndices.Add(idx);

            // 모든 일반 선택지가 소진됐으면 Done (다음엔 repeatLines)
            bool allDone = totalNonExitChoices > 0 &&
                           persistentUsedIndices.Count >= totalNonExitChoices;
            npcState = allDone ? NpcState.Done : NpcState.ExitedViaChoice;
            return;
        }

        // 최초 대화 & 선택지 없을 때만 단서/플래그 지급
        if (npcState == NpcState.Idle && !firstConvoHadChoices)
        {
            string finalClue = !string.IsNullOrEmpty(clueID) ? clueID : grantClueIDOnFinish;
            string finalFlag = !string.IsNullOrEmpty(flagID) ? flagID : setFlagOnFinish;
            if (!string.IsNullOrEmpty(finalClue)) GameFlags.Instance?.AddClue(finalClue);
            if (!string.IsNullOrEmpty(finalFlag)) GameFlags.Instance?.SetFlag(finalFlag);
        }

        npcState = NpcState.Done;
    }

    // ─── 텍스트 리졸브 ────────────────────────────────────────────────────

    private (string name, List<string> first, List<string> repeat,
             string clueID, string flag, List<DialogueChoiceData> choices)
    ResolveTextData()
    {
        if (!string.IsNullOrEmpty(npcID) && GameTextLoader.Instance != null)
        {
            var data = GameTextLoader.Instance.GetNpc(npcID);
            if (data != null)
            {
                var first = ResolveLines(data.firstLines, data.conditionalFirstLines);
                var ch = ResolveChoiceLines(data.choices, data.conditionalChoiceLines);
                return (data.npcName, first, data.repeatLines, data.grantClueID, data.setFlag, ch);
            }
        }
        return (npcName, dialogueLines, repeatLines, "", "", null);
    }

    private static List<string> ResolveLines(
        List<string> defaultLines, List<ConditionalLinesData> conditionals)
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

    private static List<DialogueChoiceData> ResolveChoiceLines(
        List<DialogueChoiceData> choices,
        List<ConditionalChoiceLineData> conditionalChoiceLines)
    {
        if (choices == null) return null;

        var resolved = new List<DialogueChoiceData>(choices.Count);
        for (int i = 0; i < choices.Count; i++)
        {
            var c = choices[i];
            List<string> resolvedLines = c.lines;

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
                costBonusPay = c.costBonusPay,
                isExitChoice = c.isExitChoice,
                isUniqueChoice = c.isUniqueChoice, // ★
                lines = resolvedLines,
                runtimeIndex = i                 // ★
            });
        }
        return resolved;
    }
}