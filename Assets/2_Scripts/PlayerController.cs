using UnityEngine;
using DG.Tweening;
using Obvious.Soap;

[RequireComponent(typeof(Rigidbody2D))]
public class PlayerController : MonoBehaviour
{
    [Header("이동 설정")]
    [SerializeField] private float moveSpeed = 4f;

    [Header("상호작용 UI")]
    [SerializeField] private GameObject interactPromptUI;

    [Header("SOAP 연결")]
    [SerializeField] private ScriptableEventNoParam onClueCollected;
    [SerializeField] private ScriptableEventNoParam onPhaseTransitionRequest;

    [Header("애니메이터 파라미터")]
    [SerializeField] private string animParamMoveX    = "MoveX";
    [SerializeField] private string animParamMoveY    = "MoveY";
    [SerializeField] private string animParamIsMoving = "IsMoving";

    [Header("상호작용 쿨다운")]
    [Tooltip("대화 종료 후 E키 재입력 방지 시간 (초)")]
    [SerializeField] private float interactionCooldown = 0.4f;

    private Rigidbody2D   rb;
    private Animator      animator;
    private Vector2       moveInput;
    private bool          isControllable      = false;
    private IInteractable currentInteractable = null;
    private float         lastInteractionTime  = -99f; // 마지막 상호작용 시각

    private void Awake()
    {
        rb       = GetComponent<Rigidbody2D>();
        animator = GetComponent<Animator>();
        if (interactPromptUI != null) interactPromptUI.SetActive(false);
    }

    private void Update()
    {
        if (!isControllable) return;

        // ★ 대화창이 열려있으면 이동·상호작용 모두 차단
        bool dialogueOpen = DialogueUI.Instance != null && DialogueUI.Instance.IsOpen();
        if (dialogueOpen)
        {
            // 이동은 멈추되 Rigidbody velocity는 FixedUpdate에서 처리
            moveInput = Vector2.zero;
            UpdateAnimator();
            return;
        }

        HandleMovementInput();
        HandleInteractionInput();
    }

    private void FixedUpdate()
    {
        rb.velocity = isControllable ? moveInput * moveSpeed : Vector2.zero;
    }

    // ─────────────────────────────────────────
    private void HandleMovementInput()
    {
        moveInput = new Vector2(
            Input.GetAxisRaw("Horizontal"),
            Input.GetAxisRaw("Vertical")
        ).normalized;

        UpdateAnimator();
    }

    private void UpdateAnimator()
    {
        if (animator == null) return;
        animator.SetFloat(animParamMoveX,    moveInput.x);
        animator.SetFloat(animParamMoveY,    moveInput.y);
        animator.SetBool (animParamIsMoving, moveInput.sqrMagnitude > 0.01f);
    }

    private void HandleInteractionInput()
    {
        if (!Input.GetKeyDown(KeyCode.E)) return;
        if (currentInteractable == null)  return;

        // ★ 쿨다운 체크 — 대화가 막 닫힌 직후 E키 재입력 방지
        if (Time.time - lastInteractionTime < interactionCooldown) return;

        lastInteractionTime = Time.time;
        currentInteractable.Interact(this);
    }

    // ─────────────────────────────────────────
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.TryGetComponent<IInteractable>(out var interactable)) return;
        currentInteractable = interactable;
        ShowInteractPrompt(true);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (!other.TryGetComponent<IInteractable>(out _)) return;
        currentInteractable = null;
        ShowInteractPrompt(false);
    }

    private void ShowInteractPrompt(bool show)
    {
        if (interactPromptUI == null) return;
        if (show)
        {
            interactPromptUI.SetActive(true);
            interactPromptUI.transform.localScale = Vector3.zero;
            interactPromptUI.transform.DOScale(Vector3.one, 0.2f).SetEase(Ease.OutBack);
        }
        else
        {
            interactPromptUI.transform.DOScale(Vector3.zero, 0.15f).SetEase(Ease.InBack)
                .OnComplete(() => interactPromptUI.SetActive(false));
        }
    }

    // ─────────────────────────────────────────
    public void EnableControl(bool enable)
    {
        isControllable = enable;
        if (!enable)
        {
            rb.velocity = Vector2.zero;
            moveInput   = Vector2.zero;
            UpdateAnimator();
            ShowInteractPrompt(false);
        }
    }

    /// <summary>대화 종료 시 NPCInteractable/ClueObject가 호출 — 쿨다운 시작</summary>
    public void NotifyInteractionEnded()
    {
        lastInteractionTime = Time.time;
    }

    public void NotifyClueCollected() => onClueCollected?.Raise();
    public void NotifyExitLocation()
    {
        EnableControl(false);
        onPhaseTransitionRequest?.Raise();
    }
}

public interface IInteractable
{
    void Interact(PlayerController player);
}
