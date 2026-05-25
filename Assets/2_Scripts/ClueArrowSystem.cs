using UnityEngine;
using System.Collections.Generic;
using DG.Tweening;

public class ClueArrowSystem : MonoBehaviour
{
    public static ClueArrowSystem Instance { get; private set; }

    [Header("레퍼런스")]
    [SerializeField] private Transform playerTransform;
    [SerializeField] private Camera nightCamera;
    [SerializeField] private GameObject arrowPrefab;

    [Header("화살표 설정")]
    [SerializeField] private float circleRadius = 120f;
    [SerializeField] private float fadeDuration = 0.25f;

    private readonly List<ClueObject> clues = new List<ClueObject>();
    private readonly Dictionary<ClueObject, ClueArrowElement> arrowMap
        = new Dictionary<ClueObject, ClueArrowElement>();

    private bool wasNightActive = false; // ★

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
        element.ForceHide(); // ★ SetVisible 대신 ForceHide로 확실히 숨김
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

    // ★ 외부에서 강제 숨김 호출용 (NightPhaseManager에서 호출)
    public void ForceHideAll()
    {
        foreach (var pair in arrowMap)
            pair.Value.ForceHide();
        foreach (var clue in clues)
            clue.ShowExclamation(false, 0f);
        wasNightActive = false;
    }

    // ── 매 프레임 갱신 ────────────────────────
    private void Update()
    {
        if (playerTransform == null || nightCamera == null) return;

        bool nightActive = nightCamera.gameObject.activeInHierarchy;

        // ★ 카메라 비활성 감지 → 전부 숨김
        if (!nightActive)
        {
            if (wasNightActive) ForceHideAll();
            return;
        }

        wasNightActive = true;

        Vector2 playerScreen = nightCamera.WorldToScreenPoint(playerTransform.position);

        foreach (var clue in clues)
        {
            if (!arrowMap.TryGetValue(clue, out var element)) continue;

            Vector3 clueWorld = clue.transform.position;
            Vector2 clueScreen = nightCamera.WorldToScreenPoint(clueWorld);

            Vector2 dir = (clueScreen - playerScreen).normalized;
            float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;

            Vector2 arrowPos = playerScreen + dir * circleRadius;
            element.UpdateTransform(arrowPos, angle);

            float dist = Vector2.Distance(playerTransform.position, clueWorld);
            bool inRange = dist <= clue.DetectionRadius;

            element.SetVisible(!inRange, fadeDuration);
            clue.ShowExclamation(inRange, fadeDuration);
        }
    }
}