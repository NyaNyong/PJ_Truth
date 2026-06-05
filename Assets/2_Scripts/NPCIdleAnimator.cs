using UnityEngine;

/// <summary>
/// NPC에 붙이면 Idle 프레임을 자동으로 순환시키는 컴포넌트.
/// SpriteRenderer와 같은 GameObject에 추가하거나,
/// SpriteRenderer가 자식에 있을 경우 srTarget에 직접 연결.
/// </summary>
public class NPCIdleAnimator : MonoBehaviour
{
    [Header("Idle 스프라이트")]
    [Tooltip("순환할 Idle 프레임 목록 (1장이면 정지 스프라이트로 동작)")]
    [SerializeField] private Sprite[] idleFrames;

    [Header("설정")]
    [SerializeField] private float fps = 2f;
    [Tooltip("비어있으면 GetComponent<SpriteRenderer>() 자동 탐색")]
    [SerializeField] private SpriteRenderer srTarget;

    [Header("랜덤 오프셋 (여러 NPC의 프레임이 동시에 바뀌는 것 방지)")]
    [SerializeField] private bool randomStartOffset = true;

    private int   frameIndex = 0;
    private float timer      = 0f;

    // ─────────────────────────────────────────────────────────────
    private void Awake()
    {
        if (srTarget == null)
            srTarget = GetComponent<SpriteRenderer>();
    }

    private void Start()
    {
        if (idleFrames == null || idleFrames.Length == 0) return;

        if (randomStartOffset)
            timer = Random.Range(0f, 1f / Mathf.Max(fps, 0.1f));

        ApplyFrame(0);
    }

    private void Update()
    {
        if (idleFrames == null || idleFrames.Length <= 1) return;

        timer += Time.deltaTime;
        float interval = 1f / Mathf.Max(fps, 0.1f);

        if (timer >= interval)
        {
            timer -= interval;
            frameIndex = (frameIndex + 1) % idleFrames.Length;
            ApplyFrame(frameIndex);
        }
    }

    private void ApplyFrame(int index)
    {
        if (srTarget == null) return;
        if (idleFrames == null || idleFrames.Length == 0) return;
        var s = idleFrames[index % idleFrames.Length];
        if (s != null) srTarget.sprite = s;
    }
}
