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
    [SerializeField] private string animParamMoveX = "MoveX";
    [SerializeField] private string animParamMoveY = "MoveY";
    [SerializeField] private string animParamIsMoving = "IsMoving";

    [Header("상호작용 쿨다운")]
    [Tooltip("대화 종료 후 E키 재입력 방지 시간 (초)")]
    [SerializeField] private float interactionCooldown = 0.4f;

    [Header("방향 스프라이트")]
    [SerializeField] private DirectionalSpriteRenderer directionalSprite;

    private Rigidbody2D rb;
    private Animator animator;
    private Vector2 moveInput;
    private bool isControllable = false;
    private IInteractable currentInteractable = null;
    private float lastInteractionTime = -99f;

    // ── 맵 경계 ───────────────────────────────
    private bool hasBounds = false;
    private Bounds mapBounds;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        animator = GetComponent<Animator>();
        if (interactPromptUI != null) interactPromptUI.SetActive(false);
    }

    private void Update()
    {
        if (!isControllable) return;

        bool dialogueOpen = DialogueUI.Instance != null && DialogueUI.Instance.IsOpen();
        if (dialogueOpen)
        {
            moveInput = Vector2.zero;
            UpdateAnimator();
            return;
        }

        HandleMovementInput();
        HandleInteractionInput();
    }

    private void FixedUpdate()
    {
        if (!isControllable)
        {
            rb.velocity = Vector2.zero;
            return;
        }

        rb.velocity = moveInput * moveSpeed;

        // ★ 맵 경계 클램프 — 벽 콜라이더 없어도 맵 밖으로 나가지 않음
        if (hasBounds)
        {
            rb.position = new Vector2(
                Mathf.Clamp(rb.position.x, mapBounds.min.x, mapBounds.max.x),
                Mathf.Clamp(rb.position.y, mapBounds.min.y, mapBounds.max.y)
            );
        }
    }

    // ─────────────────────────────────────────
    private void HandleMovementInput()
    {
        moveInput = new Vector2(
            Input.GetAxisRaw("Horizontal"),
            Input.GetAxisRaw("Vertical")
        ).normalized;

        directionalSprite?.UpdateDirection(moveInput); // ★ 추가
        UpdateAnimator();
    }

    private void UpdateAnimator()
    {
        if (animator == null) return;
        animator.SetFloat(animParamMoveX, moveInput.x);
        animator.SetFloat(animParamMoveY, moveInput.y);
        animator.SetBool(animParamIsMoving, moveInput.sqrMagnitude > 0.01f);
    }

    private void HandleInteractionInput()
    {
        if (!Input.GetKeyDown(KeyCode.E)) return;
        if (currentInteractable == null) return;
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
            moveInput = Vector2.zero;
            UpdateAnimator();
            ShowInteractPrompt(false);
        }
    }

    /// <summary>NightPhaseManager가 장소 진입 시 호출 — 맵 경계 설정</summary>
    public void SetMapBoundary(MapBoundary boundary)
    {
        if (boundary != null)
        {
            mapBounds = boundary.GetBounds();
            hasBounds = true;
        }
        else
        {
            hasBounds = false;
        }
    }

    public void NotifyInteractionEnded() => lastInteractionTime = Time.time;
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