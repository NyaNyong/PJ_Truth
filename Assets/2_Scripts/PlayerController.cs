using UnityEngine;
using DG.Tweening;
using Sirenix.OdinInspector;
using Obvious.Soap;

/// <summary>
/// 밤 페이즈 탑다운 뷰의 플레이어 이동 및 상호작용 컨트롤러.
/// Rigidbody2D 기반 WASD 이동 + E키 상호작용 프롬프트를 처리합니다.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Animator))]
public class PlayerController : MonoBehaviour
{
    // ─────────────────────────────────────────
    // 이동 설정
    // ─────────────────────────────────────────
    [BoxGroup("이동 설정")]
    [Range(2f, 8f)]
    [SerializeField] private float moveSpeed = 4f;

    // ─────────────────────────────────────────
    // 상호작용 UI
    // ─────────────────────────────────────────
    [BoxGroup("상호작용 UI")]
    [Tooltip("E키 프롬프트 오브젝트 — 상호작용 가능한 오브젝트 근처에 표시됩니다")]
    [SerializeField] private GameObject interactPromptUI;

    // ─────────────────────────────────────────
    // SOAP 연결
    // ─────────────────────────────────────────
    [BoxGroup("SOAP 연결")]
    [Tooltip("단서 획득 시 Raise — WhiteboardManager 등이 구독합니다")]
    [SerializeField] private ScriptableEventNoParam onClueCollected;

    [BoxGroup("SOAP 연결")]
    [Tooltip("탐색 종료(퇴장) 시 Raise — NightPhaseManager가 구독합니다")]
    [SerializeField] private ScriptableEventNoParam onPhaseTransitionRequest;

    // ─────────────────────────────────────────
    // 애니메이터 파라미터 이름 (Odin으로 편집 가능)
    // ─────────────────────────────────────────
    [BoxGroup("애니메이터")]
    [SerializeField] private string animParamMoveX    = "MoveX";
    [SerializeField] private string animParamMoveY    = "MoveY";
    [SerializeField] private string animParamIsMoving = "IsMoving";

    // ─────────────────────────────────────────
    // 내부 상태
    // ─────────────────────────────────────────
    private Rigidbody2D   rb;
    private Animator      animator;
    private Vector2       moveInput;
    private bool          isControllable      = false;
    private IInteractable currentInteractable = null;

    // ─────────────────────────────────────────
    // 라이프사이클
    // ─────────────────────────────────────────
    private void Awake()
    {
        rb       = GetComponent<Rigidbody2D>();
        animator = GetComponent<Animator>();

        // 상호작용 프롬프트 초기 비활성화
        if (interactPromptUI != null)
            interactPromptUI.SetActive(false);
    }

    private void Update()
    {
        if (!isControllable) return;

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
    }

    // ─────────────────────────────────────────
    // 이동 처리
    // ─────────────────────────────────────────
    private void HandleMovementInput()
    {
        moveInput = new Vector2(
            Input.GetAxisRaw("Horizontal"),
            Input.GetAxisRaw("Vertical")
        ).normalized;

        // 애니메이터 업데이트
        if (animator != null)
        {
            animator.SetFloat(animParamMoveX,    moveInput.x);
            animator.SetFloat(animParamMoveY,    moveInput.y);
            animator.SetBool (animParamIsMoving, moveInput.sqrMagnitude > 0.01f);
        }
    }

    // ─────────────────────────────────────────
    // 상호작용 처리
    // ─────────────────────────────────────────
    private void HandleInteractionInput()
    {
        if (Input.GetKeyDown(KeyCode.E) && currentInteractable != null)
        {
            currentInteractable.Interact(this);
        }
    }

    // ─────────────────────────────────────────
    // 트리거 감지 (상호작용 오브젝트 진입/이탈)
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

    // ─────────────────────────────────────────
    // 프롬프트 UI
    // ─────────────────────────────────────────
    private void ShowInteractPrompt(bool show)
    {
        if (interactPromptUI == null) return;

        if (show)
        {
            interactPromptUI.SetActive(true);
            interactPromptUI.transform.localScale = Vector3.zero;
            interactPromptUI.transform
                .DOScale(Vector3.one, 0.2f)
                .SetEase(Ease.OutBack);
        }
        else
        {
            interactPromptUI.transform
                .DOScale(Vector3.zero, 0.15f)
                .SetEase(Ease.InBack)
                .OnComplete(() => interactPromptUI.SetActive(false));
        }
    }

    // ─────────────────────────────────────────
    // 공개 인터페이스 — NightPhaseManager가 호출
    // ─────────────────────────────────────────
    public void EnableControl(bool enable)
    {
        isControllable = enable;

        if (!enable)
        {
            rb.velocity = Vector2.zero;
            moveInput   = Vector2.zero;
            animator?.SetBool(animParamIsMoving, false);
            ShowInteractPrompt(false);
        }
    }

    /// <summary>
    /// 단서 오브젝트가 상호작용 완료 후 이 메서드를 호출합니다.
    /// </summary>
    public void NotifyClueCollected()
    {
        Debug.Log("🔍 [PlayerController] 단서 획득 이벤트 발행");
        onClueCollected?.Raise();
    }

    /// <summary>
    /// 탈출 오브젝트(엘리베이터, 출구 등)가 이 메서드를 호출합니다.
    /// </summary>
    public void NotifyExitLocation()
    {
        Debug.Log("🚪 [PlayerController] 장소 탐색 종료 이벤트 발행");
        EnableControl(false);
        onPhaseTransitionRequest?.Raise();
    }
}

// ─────────────────────────────────────────────────────────────
// 상호작용 인터페이스
// 단서 오브젝트, NPC, 출구 등 모든 상호작용 오브젝트가 구현합니다.
// ─────────────────────────────────────────────────────────────
public interface IInteractable
{
    /// <summary>플레이어가 E키를 눌렀을 때 호출됩니다.</summary>
    void Interact(PlayerController player);
}
