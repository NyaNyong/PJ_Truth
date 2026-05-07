using UnityEngine;
using System.Collections.Generic;
using DG.Tweening;

/// <summary>
/// 씬의 모든 ClueObject를 추적하여 화살표 UI를 갱신합니다.
/// Night Phase UI Canvas의 자식으로 배치하세요.
/// </summary>
public class ClueArrowSystem : MonoBehaviour
{
    public static ClueArrowSystem Instance { get; private set; }

    [Header("레퍼런스")]
    [Tooltip("플레이어 캐릭터 Transform")]
    [SerializeField] private Transform playerTransform;
    [Tooltip("밤 페이즈 탑다운 카메라")]
    [SerializeField] private Camera nightCamera;
    [Tooltip("ClueArrowElement 컴포넌트가 붙은 화살표 UI 프리팹")]
    [SerializeField] private GameObject arrowPrefab;

    [Header("화살표 설정")]
    [Tooltip("플레이어 스크린 좌표 기준 화살표 원 반경 (픽셀)")]
    [SerializeField] private float circleRadius = 120f;
    [SerializeField] private float fadeDuration = 0.25f;

    private readonly List<ClueObject> clues = new List<ClueObject>();
    private readonly Dictionary<ClueObject, ClueArrowElement> arrowMap = new Dictionary<ClueObject, ClueArrowElement>();

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    // ── 등록 / 해제 ──────────────────────────
    public void Register(ClueObject clue)
    {
        if (clues.Contains(clue)) return;
        clues.Add(clue);

        var go = Instantiate(arrowPrefab, transform);
        var element = go.GetComponent<ClueArrowElement>();
        element.SetVisible(false, 0f); // 처음엔 숨김, Update에서 제어
        arrowMap[clue] = element;
    }

    public void Unregister(ClueObject clue)
    {
        clues.Remove(clue);
        if (!arrowMap.TryGetValue(clue, out var element)) return;
        Destroy(element.gameObject);
        arrowMap.Remove(clue);
        clue.ShowExclamation(false, 0f);
    }

    // ── 매 프레임 갱신 ────────────────────────
    private void Update()
    {
        if (playerTransform == null || nightCamera == null) return;

        Vector2 playerScreen = nightCamera.WorldToScreenPoint(playerTransform.position);

        foreach (var clue in clues)
        {
            if (!arrowMap.TryGetValue(clue, out var element)) continue;

            Vector3 clueWorld = clue.transform.position;
            Vector2 clueScreen = nightCamera.WorldToScreenPoint(clueWorld);

            // 방향 및 각도 계산
            Vector2 dir = (clueScreen - playerScreen).normalized;
            float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;

            // 화살표 위치: 플레이어 스크린 좌표 기준 원 위
            Vector2 arrowPos = playerScreen + dir * circleRadius;
            element.UpdateTransform(arrowPos, angle);

            // 탐지 범위 체크 (월드 단위 2D 거리)
            float dist = Vector2.Distance(playerTransform.position, clueWorld);
            bool inRange = dist <= clue.DetectionRadius;

            element.SetVisible(!inRange, fadeDuration);
            clue.ShowExclamation(inRange, fadeDuration);
        }
    }
}