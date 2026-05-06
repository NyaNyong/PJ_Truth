using UnityEngine;
using System.Collections.Generic;
using DG.Tweening;

public class UVPuzzleInteractable : MonoBehaviour, IInteractable
{
    [Header("퍼즐 내용")]
    [SerializeField] private string hiddenContent = "숨겨진 단서 텍스트";
    [SerializeField] private string correctAnswer = "정답";

    [Header("단서 연동")]
    [SerializeField] private string clueIDOnSolve = "";
    [SerializeField] private string flagIDOnSolve = "";

    [Header("완료 후 단서 대화 (JSON 미사용 폴백)")]
    [SerializeField] private string clueRevealTitle = "단서 발견";
    [SerializeField] private List<string> clueRevealLines = new List<string>();

    [Header("설정")]
    [SerializeField] private bool canReexamine = false;

    private bool isSolved = false;
    private PlayerController cachedPlayer;

    public void Interact(PlayerController player)
    {
        if (UVPuzzleUI.Instance == null)
        {
            Debug.LogWarning("[UVPuzzleInteractable] UVPuzzleUI가 씬에 없습니다.");
            return;
        }
        if (UVPuzzleUI.Instance.IsOpen()) return;

        cachedPlayer = player;

        if (isSolved && !canReexamine)
        {
            DialogueUI.Instance?.StartDialogue("단서",
                new List<string> { "(이미 조사한 흔적이다.)" },
                () => cachedPlayer?.NotifyInteractionEnded());
            return;
        }

        UVPuzzleUI.Instance.OpenPuzzle(hiddenContent, correctAnswer, OnPuzzleSolved);
    }

    private void OnPuzzleSolved()
    {
        isSolved = true;

        if (!string.IsNullOrEmpty(clueIDOnSolve) && GameFlags.Instance != null)
            GameFlags.Instance.AddClue(clueIDOnSolve);

        if (!string.IsNullOrEmpty(flagIDOnSolve) && GameFlags.Instance != null)
            GameFlags.Instance.SetFlag(flagIDOnSolve);

        Debug.Log($"🔦 [UV 퍼즐 완료] 단서 ID: {clueIDOnSolve}");

        // ★ 퍼즐 패널 페이드아웃(0.3s) 완료 후 단서 대화창 표시
        DOVirtual.DelayedCall(0.35f, ShowClueDialogue);
    }

    private void ShowClueDialogue()
    {
        var (title, lines) = ResolveClueText();
        DialogueUI.Instance?.StartDialogue(title, lines, OnClueDialogueFinished);
    }

    private void OnClueDialogueFinished()
    {
        cachedPlayer?.NotifyInteractionEnded();
        cachedPlayer?.NotifyClueCollected(); // ★ 탐색 완료 판정 트리거
    }

    // JSON 우선 조회, 없으면 Inspector 폴백
    private (string title, List<string> lines) ResolveClueText()
    {
        if (!string.IsNullOrEmpty(clueIDOnSolve) && GameTextLoader.Instance != null)
        {
            var data = GameTextLoader.Instance.GetClue(clueIDOnSolve);
            if (data != null) return (data.title, data.lines);
        }
        return (clueRevealTitle, clueRevealLines);
    }
}