using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// UV 퍼즐 오브젝트.
/// 플레이어가 E키를 누르면 UVPuzzleUI를 열어 퍼즐을 시작합니다.
/// 퍼즐을 풀면 GameFlags에 단서를 등록합니다.
/// </summary>
public class UVPuzzleInteractable : MonoBehaviour, IInteractable
{
    [Header("퍼즐 내용")]
    [Tooltip("UV 라이트로 드러날 숨겨진 텍스트 (애너그램 또는 단서 문장)")]
    [SerializeField] private string hiddenContent = "숨겨진 단서 텍스트";

    [Tooltip("정답 단어 (대소문자 무시)")]
    [SerializeField] private string correctAnswer = "정답";

    [Header("단서 연동")]
    [Tooltip("퍼즐 완료 시 획득할 단서 ID")]
    [SerializeField] private string clueIDOnSolve = "";

    [Tooltip("퍼즐 완료 시 세울 플래그 ID")]
    [SerializeField] private string flagIDOnSolve = "";

    [Header("설정")]
    [Tooltip("이미 풀었어도 다시 조사 가능 여부")]
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
            // 이미 풀었으면 DialogueUI로 짧은 메시지
            DialogueUI.Instance?.StartDialogue("단서",
                new System.Collections.Generic.List<string> { "(이미 조사한 흔적이다.)" },
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

        cachedPlayer?.NotifyInteractionEnded();
        Debug.Log($"🔦 [UV 퍼즐 완료] 단서 ID: {clueIDOnSolve}");
    }
}
