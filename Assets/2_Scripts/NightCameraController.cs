using UnityEngine;

/// <summary>
/// 포켓몬 스타일 카메라 컨트롤러.
/// 플레이어를 따라다니되, 맵 경계에서는 카메라가 고정되고 플레이어가 직접 이동합니다.
/// MapBoundary 컴포넌트가 있는 오브젝트를 mapBoundary 슬롯에 연결하세요.
/// </summary>
public class NightCameraController : MonoBehaviour
{
    [Header("추적 대상")]
    [SerializeField] private Transform target; // Player 오브젝트

    [Header("맵 경계")]
    [SerializeField] private MapBoundary mapBoundary;

    [Header("설정")]
    [SerializeField] private float followSpeed = 999f; // 높을수록 딱딱하게 따라옴 (포켓몬은 즉각 추적)
    [SerializeField] private float zOffset = -10f;     // 카메라 Z축 위치

    private Camera cam;

    private void Awake()
    {
        cam = GetComponent<Camera>();
    }

    private void LateUpdate()
    {
        if (target == null) return;

        Vector3 desiredPos = new Vector3(target.position.x, target.position.y, zOffset);

        // 맵 경계가 설정된 경우 카메라 위치를 클램프
        if (mapBoundary != null && cam != null)
        {
            Bounds bounds = mapBoundary.GetBounds();

            float camHalfHeight = cam.orthographicSize;
            float camHalfWidth  = camHalfHeight * cam.aspect;

            // 맵이 카메라보다 작으면 맵 중앙에 고정
            float clampedX = (bounds.size.x < camHalfWidth * 2f)
                ? bounds.center.x
                : Mathf.Clamp(desiredPos.x, bounds.min.x + camHalfWidth, bounds.max.x - camHalfWidth);

            float clampedY = (bounds.size.y < camHalfHeight * 2f)
                ? bounds.center.y
                : Mathf.Clamp(desiredPos.y, bounds.min.y + camHalfHeight, bounds.max.y - camHalfHeight);

            desiredPos = new Vector3(clampedX, clampedY, zOffset);
        }

        // followSpeed가 높으면 사실상 즉각 이동 (포켓몬 스타일)
        transform.position = Vector3.Lerp(transform.position, desiredPos, followSpeed * Time.deltaTime);
    }

    public void SetBoundary(MapBoundary boundary)
    {
        mapBoundary = boundary;
    }
}
