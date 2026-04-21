using UnityEngine;

/// <summary>
/// 맵의 이동 가능 경계를 정의합니다.
/// 이 컴포넌트가 붙은 오브젝트의 BoxCollider2D 크기를 경계로 사용합니다.
/// BoxCollider2D가 없으면 [Boundary Size]로 수동 설정합니다.
/// Scene 뷰에서 초록색 박스로 경계가 보입니다.
/// </summary>
[RequireComponent(typeof(BoxCollider2D))]
public class MapBoundary : MonoBehaviour
{
    private BoxCollider2D boxCollider;

    private void Awake()
    {
        boxCollider = GetComponent<BoxCollider2D>();
        // 플레이어와 충돌하지 않도록 트리거로 설정
        boxCollider.isTrigger = true;
    }

    public Bounds GetBounds()
    {
        return boxCollider.bounds;
    }

    // Scene 뷰에서 경계 시각화
    private void OnDrawGizmos()
    {
        var col = GetComponent<BoxCollider2D>();
        if (col == null) return;

        Gizmos.color = Color.green;
        Gizmos.DrawWireCube(transform.position + (Vector3)col.offset, col.size);
    }
}
