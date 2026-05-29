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

    /// <summary>★ isUniqueChoice 선택지 중 하나라도 선택됐으면 true → 나머지 전부 차단</summary>
    private bool hasSelectedUniqueChoice = false;

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
                linesToShow = new List<string>();
                useChoices = choices != null && choices.Count > 0;
                // ★ persistentUsedIndices + isUniqueChoice 차단 인덱스 합산
                preUsed = BuildPreUsed(choices);
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

        // 일반 non-exit 선택지: 영구 사용 처리
        if (!choice.isExitChoice && choice.runtimeIndex >= 0)
            persistentUsedIndices.Add(choice.runtimeIndex);

        // ★ isUniqueChoice: NPC 내 나머지 isUniqueChoice 전부 차단
        if (choice.isUniqueChoice)
            hasSelectedUniqueChoice = true;

        // isUniqueChoice 또는 isExitChoice → 대화 종료
        if (choice.isExitChoice || choice.isUniqueChoice)
            pendingExitClose = true;
    }

    private void OnDialogueFinished(string clueID, string flagID)
    {
        cachedPlayer?.NotifyInteractionEnded();

        if (pendingExitClose)
        {
            pendingExitClose = false;

            // 세션 사용 인덱스 영구 저장소에 병합
            var sessionUsed = DialogueUI.Instance?.GetUsedChoiceIndices();
            if (sessionUsed != null)
                foreach (var idx in sessionUsed)
                    persistentUsedIndices.Add(idx);

            // ★ isUniqueChoice 선택 시 무조건 Done (repeatLines로)
            // 일반 선택지 소진 여부도 Done 조건에 포함
            bool allDone = hasSelectedUniqueChoice ||
                           (totalNonExitChoices > 0 &&
                            persistentUsedIndices.Count >= totalNonExitChoices);
            npcState = allDone ? NpcState.Done : NpcState.ExitedViaChoice;
            return;
        }

        // 자연 종료(exit 선택지 없이 선택지 소진) — 세션 인덱스 병합
        var sessionUsedNatural = DialogueUI.Instance?.GetUsedChoiceIndices();
        if (sessionUsedNatural != null)
            foreach (var idx in sessionUsedNatural)
                persistentUsedIndices.Add(idx);

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

    // ─────────────────────────────────────────────────────────────────────
    // ★ persistentUsedIndices + isUniqueChoice 전체 차단 인덱스 합산
    // ─────────────────────────────────────────────────────────────────────
    private HashSet<int> BuildPreUsed(List<DialogueChoiceData> choices)
    {
        if (!hasSelectedUniqueChoice) return persistentUsedIndices;

        var result = new HashSet<int>(persistentUsedIndices);
        if (choices != null)
            for (int i = 0; i < choices.Count; i++)
                if (choices[i].isUniqueChoice)
                    result.Add(i);
        return result;
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
                label        = c.label,
                grantClueID  = c.grantClueID,
                setFlag      = c.setFlag,
                blockIfFlag  = c.blockIfFlag,
                costBonusPay = c.costBonusPay,
                isExitChoice  = c.isExitChoice,
                isUniqueChoice = c.isUniqueChoice,
                lines        = resolvedLines,
                runtimeIndex = i
            });
        }
        return resolved;
    }
}
