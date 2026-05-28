using UnityEngine;

public class DirectionalSpriteRenderer : MonoBehaviour
{
    // ── 방향별 5프레임 묶음 ────────────────────────────────────────────────
    [System.Serializable]
    public class DirectionalFrames
    {
        [Tooltip("0: 정지  /  1~4: 걷기 프레임")]
        public Sprite[] frames = new Sprite[5];

        public Sprite GetFrame(int index)
        {
            if (frames == null || frames.Length == 0) return null;
            return frames[Mathf.Clamp(index, 0, frames.Length - 1)];
        }
    }

    public enum Direction { N, NE, E, SE, S, SW, W, NW }

    // ── 남성 스프라이트 (8방향 × 5프레임) ──────────────────────────────────
    [Header("남성 — 각 방향 5프레임 (0=정지, 1~4=걷기)")]
    [SerializeField] private DirectionalFrames maleS;
    [SerializeField] private DirectionalFrames maleSW;
    [SerializeField] private DirectionalFrames maleW;
    [SerializeField] private DirectionalFrames maleNW;
    [SerializeField] private DirectionalFrames maleN;
    [SerializeField] private DirectionalFrames maleNE;
    [SerializeField] private DirectionalFrames maleE;
    [SerializeField] private DirectionalFrames maleSE;

    // ── 여성 스프라이트 (8방향 × 5프레임) ──────────────────────────────────
    [Header("여성 — 각 방향 5프레임 (0=정지, 1~4=걷기)")]
    [SerializeField] private DirectionalFrames femaleS;
    [SerializeField] private DirectionalFrames femaleSW;
    [SerializeField] private DirectionalFrames femaleW;
    [SerializeField] private DirectionalFrames femaleNW;
    [SerializeField] private DirectionalFrames femaleN;
    [SerializeField] private DirectionalFrames femaleNE;
    [SerializeField] private DirectionalFrames femaleE;
    [SerializeField] private DirectionalFrames femaleSE;

    // ── 애니메이션 설정 ───────────────────────────────────────────────────
    [Header("애니메이션 설정")]
    [SerializeField] private float walkFPS = 8f;
    [SerializeField] private Direction defaultDirection = Direction.S;

    // ── 런타임 상태 ───────────────────────────────────────────────────────
    private SpriteRenderer sr;
    private Direction currentDirection;
    private bool isMale = true;
    private bool isMoving = false;

    // 걷기 프레임 (1~4) 순환용
    private int walkFrameIndex = 1;
    private float frameTimer = 0f;

    // ─────────────────────────────────────────────────────────────────────
    private void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
    }

    private void Start()
    {
        isMale = PlayerData.Instance == null || PlayerData.Instance.IsMale;
        currentDirection = defaultDirection;
        ApplyFrame(0);
    }

    private void Update()
    {
        if (!isMoving)
        {
            // 정지 상태: 항상 0번 프레임
            walkFrameIndex = 1;
            frameTimer = 0f;
            ApplyFrame(0);
            return;
        }

        // 걷기 애니메이션: 1~4 프레임 순환
        frameTimer += Time.deltaTime;
        float interval = 1f / Mathf.Max(walkFPS, 1f);
        if (frameTimer >= interval)
        {
            frameTimer -= interval;
            walkFrameIndex++;
            if (walkFrameIndex > 4) walkFrameIndex = 1;
            ApplyFrame(walkFrameIndex);
        }
    }

    /// <summary>PlayerController가 매 프레임 호출 — input이 zero면 정지 처리</summary>
    public void UpdateDirection(Vector2 input)
    {
        bool moving = input.sqrMagnitude > 0.01f;

        if (moving)
        {
            Direction dir = InputToDirection(input);
            if (dir != currentDirection)
            {
                currentDirection = dir;
                // 방향 바뀌면 걷기 프레임 리셋
                walkFrameIndex = 1;
                frameTimer = 0f;
                ApplyFrame(walkFrameIndex); // ★ 방향 전환 즉시 반영
            }
        }

        isMoving = moving;
    }

    // ── 내부 헬퍼 ────────────────────────────────────────────────────────
    private void ApplyFrame(int frameIndex)
    {
        if (sr == null) return;
        DirectionalFrames df = GetDirectionalFrames(currentDirection);
        if (df == null) return;
        Sprite s = df.GetFrame(frameIndex);
        if (s != null) sr.sprite = s;
    }

    private DirectionalFrames GetDirectionalFrames(Direction dir)
    {
        if (isMale)
        {
            return dir switch
            {
                Direction.N => maleN,
                Direction.NE => maleNE,
                Direction.E => maleE,
                Direction.SE => maleSE,
                Direction.S => maleS,
                Direction.SW => maleSW,
                Direction.W => maleW,
                Direction.NW => maleNW,
                _ => maleS
            };
        }
        else
        {
            return dir switch
            {
                Direction.N => femaleN,
                Direction.NE => femaleNE,
                Direction.E => femaleE,
                Direction.SE => femaleSE,
                Direction.S => femaleS,
                Direction.SW => femaleSW,
                Direction.W => femaleW,
                Direction.NW => femaleNW,
                _ => femaleS
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
            else return input.y > 0 ? Direction.NW : Direction.SW;
        }
        if (hasX) return input.x > 0 ? Direction.E : Direction.W;
        return input.y > 0 ? Direction.N : Direction.S;
    }
}