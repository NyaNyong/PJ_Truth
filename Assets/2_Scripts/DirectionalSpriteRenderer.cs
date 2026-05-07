using UnityEngine;

/// <summary>
/// 이동 방향에 따라 스프라이트를 교체합니다.
/// PlayerController와 같은 오브젝트에 부착하세요.
/// </summary>
public class DirectionalSpriteRenderer : MonoBehaviour
{
    [Header("방향별 스프라이트 (8방향)")]
    [SerializeField] private Sprite dirN;
    [SerializeField] private Sprite dirNE;
    [SerializeField] private Sprite dirE;
    [SerializeField] private Sprite dirSE;
    [SerializeField] private Sprite dirS;
    [SerializeField] private Sprite dirSW;
    [SerializeField] private Sprite dirW;
    [SerializeField] private Sprite dirNW;

    [Header("기본 방향 (시작 및 정지 시)")]
    [SerializeField] private Direction defaultDirection = Direction.S;

    private SpriteRenderer sr;
    private Direction currentDirection;

    public enum Direction { N, NE, E, SE, S, SW, W, NW }

    private void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        SetDirection(defaultDirection);
    }

    /// <summary>PlayerController가 매 프레임 호출</summary>
    public void UpdateDirection(Vector2 input)
    {
        if (input.sqrMagnitude < 0.01f) return; // 정지 시 마지막 방향 유지

        Direction dir = InputToDirection(input);
        if (dir == currentDirection) return;
        SetDirection(dir);
    }

    private void SetDirection(Direction dir)
    {
        currentDirection = dir;
        if (sr == null) return;

        sr.sprite = dir switch
        {
            Direction.N => dirN,
            Direction.NE => dirNE,
            Direction.E => dirE,
            Direction.SE => dirSE,
            Direction.S => dirS,
            Direction.SW => dirSW,
            Direction.W => dirW,
            Direction.NW => dirNW,
            _ => dirS
        };
    }

    private Direction InputToDirection(Vector2 input)
    {
        float x = input.x;
        float y = input.y;

        // 대각선 판정
        bool hasX = Mathf.Abs(x) > 0.1f;
        bool hasY = Mathf.Abs(y) > 0.1f;

        if (hasX && hasY)
        {
            if (x > 0) return y > 0 ? Direction.NE : Direction.SE;
            else return y > 0 ? Direction.NW : Direction.SW;
        }
        if (hasX) return x > 0 ? Direction.E : Direction.W;
        return y > 0 ? Direction.N : Direction.S;
    }
}