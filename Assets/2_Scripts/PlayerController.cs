using UnityEngine;
using DG.Tweening;
using Obvious.Soap;
using System.Collections; // ★ 추가

[RequireComponent(typeof(Rigidbody2D))]
public class PlayerController : MonoBehaviour
{
    [Header("이동 설정")]
    [SerializeField] private float moveSpeed = 4f;

    [Header("상호작용 UI (플레이어 전역)")]
    [SerializeField] private GameObject interactPromptUI;

    [Header("SOAP 연결")]
    [SerializeField] private ScriptableEventNoParam onClueCollected;
    [SerializeField] private ScriptableEventNoParam onPhaseTransitionRequest;

    [Header("애니메이터 파라미터")]
    [SerializeField] private string animParamMoveX = "MoveX";
    [SerializeField] private string animParamMoveY = "MoveY";
    [SerializeField] private string animParamIsMoving = "IsMoving";

    [Header("상호작용 쿨다운")]
    [SerializeField] private float interactionCooldown = 0.4f;

    [Header("방향 스프라이트")]
    [SerializeField] private DirectionalSpriteRenderer directionalSprite;

    [Header("걷기 효과음")]
    [SerializeField] private float footstepInterval = 0.4f;
    private float _footstepTimer = 0f;

    private Rigidbody2D rb;
    private Animator animator;
    private Vector2 moveInput;
    private bool isControllable = false;
    private IInteractable currentInteractable = null;
    private InteractPromptHost currentPromptHost = null; // ★
    private float lastInteractionTime = -99f;

    private bool hasBounds = false;
    private Bounds mapBounds;

    private Vector3 promptOriginalScale; // ★

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        animator = GetComponent<Animator>();
        if (interactPromptUI != null)
        {
            promptOriginalScale = interactPromptUI.transform.localScale; // ★
            interactPromptUI.SetActive(false);
        }
    }

    private void Update()
    {
        if (!isControllable) return;

        bool dialogueOpen = DialogueUI.Instance != null && DialogueUI.Instance.IsOpen();
        bool uvPuzzleOpen = UVPuzzleUI.Instance != null && UVPuzzleUI.Instance.IsOpen();

        if (dialogueOpen || uvPuzzleOpen)
        {
            moveInput = Vector2.zero;
            rb.velocity = Vector2.zero;
            UpdateAnimator();
            return;
        }

        HandleMovementInput();
        HandleInteractionInput();
    }

    private void FixedUpdate()
    {
        if (!isControllable) { rb.velocity = Vector2.zero; return; }
        rb.velocity = moveInput * moveSpeed;

        if (hasBounds)
        {
            rb.position = new Vector2(
                Mathf.Clamp(rb.position.x, mapBounds.min.x, mapBounds.max.x),
                Mathf.Clamp(rb.position.y, mapBounds.min.y, mapBounds.max.y));
        }
    }

    private void HandleMovementInput()
    {
        moveInput = new Vector2(
            Input.GetAxisRaw("Horizontal"),
            Input.GetAxisRaw("Vertical")).normalized;

        directionalSprite?.UpdateDirection(moveInput);
        UpdateAnimator();

        if (moveInput.sqrMagnitude > 0.01f)
        {
            _footstepTimer -= Time.deltaTime;
            if (_footstepTimer <= 0f)
            {
                AudioManager.Instance?.PlaySfxFootstep();
                _footstepTimer = footstepInterval;
            }
        }
        else _footstepTimer = 0f;
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

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.TryGetComponent<IInteractable>(out var interactable)) return;
        currentInteractable = interactable;

        // ★ 오브젝트별 E 프롬프트
        currentPromptHost = other.GetComponent<InteractPromptHost>();
        currentPromptHost?.SetVisible(true);
        ShowInteractPrompt(true); // 플레이어 전역 프롬프트(있으면)
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (!other.TryGetComponent<IInteractable>(out _)) return;
        currentInteractable = null;

        // ★ 오브젝트별 E 프롬프트 숨김
        currentPromptHost?.SetVisible(false);
        currentPromptHost = null;
        ShowInteractPrompt(false);
    }

    private void ShowInteractPrompt(bool show)
    {
        if (interactPromptUI == null) return;
        if (show)
        {
            interactPromptUI.SetActive(true);
            interactPromptUI.transform.localScale = Vector3.zero;
            interactPromptUI.transform.DOScale(promptOriginalScale, 0.2f).SetEase(Ease.OutBack); // ★
        }
        else
        {
            interactPromptUI.transform.DOScale(Vector3.zero, 0.15f).SetEase(Ease.InBack)
                .OnComplete(() => interactPromptUI.SetActive(false));
        }
    }

    public void EnableControl(bool enable)
    {
        isControllable = enable;
        if (!enable)
        {
            rb.velocity = Vector2.zero;
            moveInput = Vector2.zero;
            UpdateAnimator();
            // ★ 제어 불가 시 프롬프트 모두 숨김
            currentPromptHost?.SetVisible(false);
            currentPromptHost = null;
            currentInteractable = null;
            ShowInteractPrompt(false);
        }
    }

    public void SetMapBoundary(MapBoundary boundary)
    {
        if (boundary != null) { mapBounds = boundary.GetBounds(); hasBounds = true; }
        else hasBounds = false;
    }

    public void NotifyInteractionEnded() => lastInteractionTime = Time.time;
    public void NotifyClueCollected() => onClueCollected?.Raise();
    public void NotifyExitLocation()
    {
        EnableControl(false);
        onPhaseTransitionRequest?.Raise();
    }

    // ★ 추가 — 특정 위치로 강제 이동 (이동 중 입력 차단, 완료 시 자동 콜백)
    public void ForceMoveTo(Vector3 targetPosition, float duration, System.Action onComplete = null)
    {
        EnableControl(false);
        StartCoroutine(ForceMoveRoutine(targetPosition, duration, onComplete));
    }

    private IEnumerator ForceMoveRoutine(Vector3 target, float duration, System.Action onComplete)
    {
        Vector3 start = transform.position;
        Vector2 dir = ((Vector2)target - (Vector2)start).normalized;
        directionalSprite?.UpdateDirection(dir);
        animator?.SetBool(animParamIsMoving, true);

        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float p = Mathf.Clamp01(t / duration);
            rb.MovePosition(Vector2.Lerp(start, target, p));
            yield return null;
        }

        rb.MovePosition(target);
        animator?.SetBool(animParamIsMoving, false);
        EnableControl(true);
        onComplete?.Invoke();
    }


}

public interface IInteractable
{
    void Interact(PlayerController player);
}

