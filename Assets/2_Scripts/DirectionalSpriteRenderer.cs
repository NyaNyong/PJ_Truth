using UnityEngine;

public class DirectionalSpriteRenderer : MonoBehaviour
{
    // ── 방향별 프레임 묶음 ────────────────────────────────────────────────
    [System.Serializable]
    public class DirectionalFrames
    {
        [Tooltip("Idle 프레임 2장 (정지 시 순환)")]
        public Sprite[] idleFrames = new Sprite[2];

        [Tooltip("Walk 프레임 4장 (이동 시 순환)")]
        public Sprite[] walkFrames = new Sprite[4];

        public Sprite GetIdle(int index)
        {
            if (idleFrames == null || idleFrames.Length == 0) return null;
            return idleFrames[index % idleFrames.Length];
        }

        public Sprite GetWalk(int index)
        {
            if (walkFrames == null || walkFrames.Length == 0) return null;
            return walkFrames[index % walkFrames.Length];
        }
    }

    public enum Direction { N, NE, E, SE, S, SW, W, NW }

    // ── 남성 스프라이트 (8방향) ──────────────────────────────────
    [Header("남성 — 8방향 (Idle 2프레임 + Walk 4프레임)")]
    [SerializeField] private DirectionalFrames maleS;
    [SerializeField] private DirectionalFrames maleSW;
    [SerializeField] private DirectionalFrames maleW;
    [SerializeField] private DirectionalFrames maleNW;
    [SerializeField] private DirectionalFrames maleN;
    [SerializeField] private DirectionalFrames maleNE;
    [SerializeField] private DirectionalFrames maleE;
    [SerializeField] private DirectionalFrames maleSE;

    // ── 여성 스프라이트 (8방향) ──────────────────────────────────
    [Header("여성 — 8방향 (Idle 2프레임 + Walk 4프레임)")]
    [SerializeField] private DirectionalFrames femaleS;
    [SerializeField] private DirectionalFrames femaleSW;
    [SerializeField] private DirectionalFrames femaleW;
    [SerializeField] private DirectionalFrames femaleNW;
    [SerializeField] private DirectionalFrames femaleN;
    [SerializeField] private DirectionalFrames femaleNE;
    [SerializeField] private DirectionalFrames femaleE;
    [SerializeField] private DirectionalFrames femaleSE;

    // ── 애니메이션 설정 ──────────────────────────────────────────
    [Header("애니메이션 설정")]
    [SerializeField] private float idleFPS  = 2f;   // ★ Idle 전환 속도
    [SerializeField] private float walkFPS  = 8f;
    [SerializeField] private Direction defaultDirection = Direction.S;

    // ── 런타임 상태 ──────────────────────────────────────────────
    private SpriteRenderer sr;
    private Direction currentDirection;
    private bool isMale   = true;
    private bool isMoving = false;

    // Idle 순환
    private int   idleFrameIndex = 0;
    private float idleFrameTimer = 0f;

    // Walk 순환
    private int   walkFrameIndex = 0;
    private float walkFrameTimer = 0f;

    // ─────────────────────────────────────────────────────────────
    private void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
    }

    private void Start()
    {
        isMale           = PlayerData.Instance == null || PlayerData.Instance.IsMale;
        currentDirection = defaultDirection;
        idleFrameIndex   = 0;
        ApplyCurrentIdle();
    }

    private void Update()
    {
        if (!isMoving)
        {
            // ── Idle: 2프레임 순환 ──────────────────
            walkFrameIndex = 0;
            walkFrameTimer = 0f;

            idleFrameTimer += Time.deltaTime;
            float idleInterval = 1f / Mathf.Max(idleFPS, 0.1f);
            if (idleFrameTimer >= idleInterval)
            {
                idleFrameTimer -= idleInterval;
                idleFrameIndex++;
                ApplyCurrentIdle();
            }
            return;
        }

        // ── Walk: 4프레임 순환 ──────────────────────
        idleFrameIndex = 0;
        idleFrameTimer = 0f;

        walkFrameTimer += Time.deltaTime;
        float walkInterval = 1f / Mathf.Max(walkFPS, 1f);
        if (walkFrameTimer >= walkInterval)
        {
            walkFrameTimer -= walkInterval;
            walkFrameIndex++;
            ApplyCurrentWalk();
        }
    }

    /// <summary>PlayerController가 매 프레임 호출</summary>
    public void UpdateDirection(Vector2 input)
    {
        bool moving = input.sqrMagnitude > 0.01f;

        if (moving)
        {
            Direction dir = InputToDirection(input);
            if (dir != currentDirection)
            {
                currentDirection = dir;
                walkFrameIndex = 0;
                walkFrameTimer = 0f;
                ApplyCurrentWalk();
            }
        }

        isMoving = moving;
    }

    // ── 내부 헬퍼 ────────────────────────────────────────────────
    private void ApplyCurrentIdle()
    {
        if (sr == null) return;
        DirectionalFrames df = GetDirectionalFrames(currentDirection);
        if (df == null) return;
        Sprite s = df.GetIdle(idleFrameIndex);
        if (s != null) sr.sprite = s;
    }

    private void ApplyCurrentWalk()
    {
        if (sr == null) return;
        DirectionalFrames df = GetDirectionalFrames(currentDirection);
        if (df == null) return;
        Sprite s = df.GetWalk(walkFrameIndex);
        if (s != null) sr.sprite = s;
    }

    private DirectionalFrames GetDirectionalFrames(Direction dir)
    {
        if (isMale)
        {
            return dir switch
            {
                Direction.N  => maleN,
                Direction.NE => maleNE,
                Direction.E  => maleE,
                Direction.SE => maleSE,
                Direction.S  => maleS,
                Direction.SW => maleSW,
                Direction.W  => maleW,
                Direction.NW => maleNW,
                _            => maleS
            };
        }
        else
        {
            return dir switch
            {
                Direction.N  => femaleN,
                Direction.NE => femaleNE,
                Direction.E  => femaleE,
                Direction.SE => femaleSE,
                Direction.S  => femaleS,
                Direction.SW => femaleSW,
                Direction.W  => femaleW,
                Direction.NW => femaleNW,
                _            => femaleS
            };
        }
    }

    private Direction InputToDirection(Vector2 input)
    {
        bool hasX = Mathf.Abs(input.x) > 0.1f;
        bool hasY = Mathf.Abs(input.y) > 0.1f;

        if (hasX && hasY)
        {
            if (input.x > 0) return input.y > 0 ? Direction.NE : Direction.SE;
            else             return input.y > 0 ? Direction.NW : Direction.SW;
        }
        if (hasX) return input.x > 0 ? Direction.E : Direction.W;
        return input.y > 0 ? Direction.N : Direction.S;
    }
}
