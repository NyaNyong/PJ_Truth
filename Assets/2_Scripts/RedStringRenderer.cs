using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

/// <summary>
/// 두 ClueCard 사이를 잇는 붉은 실.
/// UI Canvas 위에 Image를 늘리고 회전하는 방식으로 선을 그립니다.
/// (LineRenderer 대신 UI 기반으로 구현해 Canvas 위에서 정상 동작)
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class RedStringRenderer : MonoBehaviour
{
    public ClueCard CardA { get; private set; }
    public ClueCard CardB { get; private set; }

    [SerializeField] private Image lineImage;
    [SerializeField] private float lineWidth = 3f;
    [SerializeField] private Color lineColor = new Color(0.8f, 0.1f, 0.1f, 0.85f);

    private RectTransform rt;
    private Canvas        parentCanvas;

    // ─────────────────────────────────────────
    private void Awake()
    {
        rt           = GetComponent<RectTransform>();
        parentCanvas = GetComponentInParent<Canvas>();

        if (lineImage != null)
            lineImage.color = lineColor;
    }

    private void Update()
    {
        // 카드가 드래그로 움직이면 실도 따라감
        if (CardA != null && CardB != null)
            UpdateLine();
    }

    // ─────────────────────────────────────────
    public void Setup(ClueCard a, ClueCard b)
    {
        CardA = a;
        CardB = b;
        UpdateLine();
    }

    private void UpdateLine()
    {
        Vector3 posA = CardA.GetWorldCenter();
        Vector3 posB = CardB.GetWorldCenter();

        // 중점에 배치
        rt.position = (posA + posB) * 0.5f;

        // 거리만큼 너비 조절
        float distance = Vector3.Distance(posA, posB);
        rt.sizeDelta = new Vector2(distance, lineWidth);

        // 방향 회전
        Vector3 dir   = posB - posA;
        float   angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        rt.rotation = Quaternion.Euler(0f, 0f, angle);
    }

    /// <summary>정답 연결 시 실이 팽팽해지는 시각적 연출</summary>
    public void PlayCorrectAnimation()
    {
        if (lineImage == null) return;

        lineImage.DOColor(Color.red, 0.3f)
                 .SetLoops(3, LoopType.Yoyo)
                 .OnComplete(() => lineImage.color = lineColor);

        rt.DOShakeAnchorPos(0.4f, 4f, 10);
    }
}
