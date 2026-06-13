using UnityEngine;

/// <summary>
/// 암호 입력 트리거. ClueObject와 동일한 IInteractable 패턴 사용.
/// BoxCollider2D + InteractPromptHost 함께 배치할 것.
/// </summary>
public class CodeInputTrigger : MonoBehaviour, IInteractable
{
    [Header("암호 설정 (비우면 JSON exposeRoute에서 읽음)")]
    [SerializeField] private string correctCode = "";
    [SerializeField] private string promptText = "";
    [SerializeField] private string flagOnSuccess = "choice_expose_code_correct";

    [Header("조건 (비우면 항상 상호작용 가능)")]
    [SerializeField] private string requiredFlag = "";

    [Header("탐지 반경")]
    [SerializeField] private float detectionRadius = 2f;

    public float DetectionRadius => detectionRadius;

    // ── IInteractable ─────────────────────────────────────────────────────
    public void Interact(PlayerController player)
    {
        if (!string.IsNullOrEmpty(requiredFlag) &&
            !(GameFlags.Instance?.HasFlag(requiredFlag) ?? false)) return;

        // 이미 성공했으면 무시
        if (GameFlags.Instance?.HasFlag(flagOnSuccess) ?? false) return;

        var expose = GameTextLoader.Instance?.GetExposeRoute();
        string code = !string.IsNullOrEmpty(correctCode)
                      ? correctCode
                      : expose?.codeInputCorrect ?? "";
        string hint = !string.IsNullOrEmpty(promptText)
                      ? promptText
                      : expose?.codeInputPrompt ?? "송출 경로를 입력하십시오.";

        if (string.IsNullOrEmpty(code))
        {
            Debug.LogWarning("[CodeInputTrigger] 정답 코드 미설정");
            return;
        }

        CodeInputUI.Instance?.Show(code, flagOnSuccess, null, hint);
    }
}