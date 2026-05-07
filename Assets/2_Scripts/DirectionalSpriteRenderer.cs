using UnityEngine;

public class DirectionalSpriteRenderer : MonoBehaviour
{
    public enum Direction { N, NE, E, SE, S, SW, W, NW }

    [Header("남성 스프라이트 (8방향)")]
    [SerializeField] private Sprite maleN, maleNE, maleE, maleSE;
    [SerializeField] private Sprite maleS, maleSW, maleW, maleNW;

    [Header("여성 스프라이트 (8방향)")]
    [SerializeField] private Sprite femaleN, femaleNE, femaleE, femaleSE;
    [SerializeField] private Sprite femaleS, femaleSW, femaleW, femaleNW;

    [Header("기본 방향")]
    [SerializeField] private Direction defaultDirection = Direction.S;

    private SpriteRenderer sr;
    private Direction currentDirection;
    private bool isMale = true;

    private void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
    }

    private void Start()
    {
        // PlayerData에서 성별 읽기
        isMale = PlayerData.Instance == null || PlayerData.Instance.IsMale;
        SetDirection(defaultDirection);
    }

    /// <summary>PlayerController가 매 프레임 호출</summary>
    public void UpdateDirection(Vector2 input)
    {
        if (input.sqrMagnitude < 0.01f) return;
        Direction dir = InputToDirection(input);
        if (dir == currentDirection) return;
        SetDirection(dir);
    }

    private void SetDirection(Direction dir)
    {
        currentDirection = dir;
        if (sr == null) return;
        sr.sprite = GetSprite(dir);
    }

    private Sprite GetSprite(Direction dir)
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