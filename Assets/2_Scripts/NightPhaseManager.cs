using DG.Tweening;
using Obvious.Soap;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;

public class NightPhaseManager : MonoBehaviour
{
    [Header("카메라")]
    [SerializeField] private Camera dayCameraOrUICamera;
    [SerializeField] private Camera nightTopDownCamera;
    [SerializeField] private NightCameraController nightCameraController;

    [Header("플레이어")]
    [SerializeField] private GameObject playerCharacter;
    [SerializeField] private PlayerController playerController;

    [Header("플레이어 배려 — 증거 카운터")]
    [SerializeField] private TextMeshProUGUI evidenceCountText;

    [Header("퇴근 버튼")]
    [SerializeField] private GameObject goHomeButton;

    [Header("탐색 완료 알림 UI")]
    [SerializeField] private CanvasGroup explorationCompleteBannerCG;
    [SerializeField] private float bannerFadeDuration = 0.35f;
    [SerializeField] private float bannerHoldDuration = 2f;

    [Header("장소-맵 매핑")]
    [SerializeField] private List<LocationMapEntry> locationMaps;

    [Header("폭로루트 — 흑막 등장 (loc_expose_control 최초 입장 시)")]
    [SerializeField] private Transform blackmaskStandPoint;
    [SerializeField] private NPCInteractable blackmaskNpc;
    [SerializeField] private float blackmaskMoveDuration = 0f; // 0 = 순간이동

    [System.Serializable]
    public class LocationMapEntry
    {
        public string locationName;
        [Tooltip("지도 버튼 표시 한글 이름")]
        public string displayName;
        [Tooltip("지도 팝업 한줄 설명 (JSON 있으면 JSON 우선)")]
        public string description;
        public GameObject mapRoot;
        public MapBoundary mapBoundary;
        public Transform spawnPoint;
        public Vector2 mapButtonPosition;
        public List<string> requiredClueIDs = new List<string>();
    }

    [Header("SOAP 연결")]
    [SerializeField] private StringVariable selectedLocation;
    [SerializeField] private ScriptableEventNoParam onLocationSelected;
    [SerializeField] private ScriptableEventNoParam onClueCollected;

    [Header("DOTween 설정")]
    [SerializeField] private float playerSpawnDuration = 0.4f;

    private LocationMapEntry activeEntry = null;
    private bool explorationComplete = false;
    private Vector3 playerOriginalScale;

    // ★ 이번 밤 방문한 장소 추적
    private HashSet<string> visitedLocations = new HashSet<string>();

    public bool IsExplorationComplete => explorationComplete;
    public bool HasRequiredClues =>
        activeEntry != null &&
        activeEntry.requiredClueIDs != null &&
        activeEntry.requiredClueIDs.Count > 0;

    // ── 방문 정보 ─────────────────────────────
    public HashSet<string> GetVisitedLocations() => new HashSet<string>(visitedLocations);

    /// <summary>locationNames 목록 전부 방문했는지 확인</summary>
    public bool HasVisitedAll(List<string> locationNames)
    {
        if (locationNames == null || locationNames.Count == 0) return true;
        foreach (var name in locationNames)
            if (!visitedLocations.Contains(name)) return false;
        return true;
    }

    /// <summary>다음 날 시작 시 방문 기록 초기화</summary>
    public void ResetVisited() => visitedLocations.Clear();

    // ── 초기화 ───────────────────────────────
    private void Awake()
    {
        nightTopDownCamera.gameObject.SetActive(false);
        playerCharacter.SetActive(false);
        if (goHomeButton != null) goHomeButton.SetActive(false);
        foreach (var e in locationMaps)
            if (e.mapRoot != null) e.mapRoot.SetActive(false);
        HideBannerImmediate();
        playerOriginalScale = playerCharacter.transform.localScale;
    }

    public void EnsureHidden()
    {
        if (nightTopDownCamera != null) nightTopDownCamera.gameObject.SetActive(false);
        if (playerCharacter != null) playerCharacter.SetActive(false);
        foreach (var e in locationMaps)
            if (e.mapRoot != null) e.mapRoot.SetActive(false);

        ClueArrowSystem.Instance?.ForceHideAll(); // ★
    }

    private void OnEnable()
    {
        if (onLocationSelected != null) onLocationSelected.OnRaised += OnLocationSelectedHandler;
        if (onClueCollected != null) onClueCollected.OnRaised += OnClueCollectedHandler;
        if (onClueCollected != null) onClueCollected.OnRaised += RefreshEvidenceCount; // ★ 추가
    }

    private void OnDisable()
    {
        if (onLocationSelected != null) onLocationSelected.OnRaised -= OnLocationSelectedHandler;
        if (onClueCollected != null) onClueCollected.OnRaised -= OnClueCollectedHandler;
        if (onClueCollected != null) onClueCollected.OnRaised -= RefreshEvidenceCount; // ★ 추가
    }

    // ── 버튼 목록 빌드 ───────────────────────
    public List<LocationButtonInfo> BuildButtonInfoList(List<string> locationNames)
    {
        var result = new List<LocationButtonInfo>();
        foreach (string name in locationNames)
        {
            var entry = locationMaps.Find(e => e.locationName == name);

            // description: JSON 우선, Inspector 폴백
            string desc = GameTextLoader.Instance?.GetLocationDescription(name) ?? "";
            if (string.IsNullOrEmpty(desc) && entry != null)
                desc = entry.description;

            result.Add(new LocationButtonInfo
            {
                locationName = name,
                displayName = (entry != null && !string.IsNullOrEmpty(entry.displayName))
                                     ? entry.displayName
                                     : name.Replace('_', ' '),
                description = desc,
                buttonPosition = entry != null ? entry.mapButtonPosition : Vector2.zero,
                isVisited = visitedLocations.Contains(name)
            });
        }
        return result;
    }

    // ── 이벤트 ───────────────────────────────
    private void OnLocationSelectedHandler()
    {
        string loc = selectedLocation != null ? selectedLocation.Value : string.Empty;
        if (string.IsNullOrEmpty(loc)) { Debug.LogWarning("selectedLocation 비어있음"); return; }
        ActivateNightView(loc);
    }

    private void OnClueCollectedHandler()
    {
        if (explorationComplete) return;
        CheckExplorationComplete();
    }

    private void CheckExplorationComplete()
    {
        if (activeEntry == null) return;
        if (activeEntry.requiredClueIDs == null || activeEntry.requiredClueIDs.Count == 0) return;
        if (GameFlags.Instance == null) return;

        foreach (string clueID in activeEntry.requiredClueIDs)
            if (!GameFlags.Instance.HasClue(clueID)) return;

        explorationComplete = true;
        Debug.Log($"✅ [{activeEntry.locationName}] 탐색 완료!");
        ShowCompleteBanner();
    }


    private void RefreshEvidenceCount()
    {
        if (evidenceCountText == null) return;
        var allIDs = GameTextLoader.Instance?.GetClueIDsForLocations(visitedLocations) ?? new List<string>(); // ★ 수정
        int remaining = allIDs.Count(id => !(GameFlags.Instance?.HasClue(id) ?? false));
        evidenceCountText.text = remaining > 0 ? $"남은 증거: {remaining}개" : "모든 증거를 확인했습니다.";
    }

    // ── 탑다운 뷰 활성/비활성 ────────────────
    private void ActivateNightView(string locationName)
    {
        Debug.Log($"🌙 탑다운 뷰 활성화 → {locationName}");
        explorationComplete = false;
        activeEntry = null;

        visitedLocations.Add(locationName); // ★ 방문 기록

        foreach (var entry in locationMaps)
        {
            bool isTarget = entry.locationName == locationName;
            if (entry.mapRoot != null) entry.mapRoot.SetActive(isTarget);
            if (isTarget) activeEntry = entry;
        }

        if (dayCameraOrUICamera != null) dayCameraOrUICamera.gameObject.SetActive(false);
        nightTopDownCamera.gameObject.SetActive(true);

        nightCameraController?.SetBoundary(activeEntry?.mapBoundary);
        playerController.SetMapBoundary(activeEntry?.mapBoundary);

        if (!HasRequiredClues) explorationComplete = true;

        Vector3 spawnPos = activeEntry?.spawnPoint != null
            ? activeEntry.spawnPoint.position : Vector3.zero;

        playerCharacter.transform.position = spawnPos;
        playerCharacter.SetActive(true);
        playerCharacter.transform.localScale = Vector3.zero;
        playerCharacter.transform
            .DOScale(playerOriginalScale, playerSpawnDuration)
            .SetEase(Ease.OutBack)
            .OnComplete(() =>
            {
                if (goHomeButton != null) goHomeButton.SetActive(true);
                if (evidenceCountText != null) evidenceCountText.gameObject.SetActive(true); // ★ 추가

                // ★ 튜토리얼: 밤 페이즈
                var gm = FindObjectOfType<GameManager>();
                if (gm != null && gm.currentDay == 3 && TutorialManager.Instance != null)
                {
                    TutorialManager.Instance.OnNightPhaseStarted();
                }
                
                               
                if (locationName == "loc_upper_archive" &&
                    GameFlags.Instance?.HasFlag("seen_archive_intro") != true)
                {
                    GameFlags.Instance?.SetFlag("seen_archive_intro");
                    MidCutsceneUI.Instance?.Play("stage7_archive_intro", () => playerController.EnableControl(true));
                }
                else if (locationName == "loc_expose_control" && // ★ 추가
                         GameFlags.Instance?.HasFlag("seen_blackmask_intro") != true)
                {
                    GameFlags.Instance?.SetFlag("seen_blackmask_intro");
                    if (blackmaskStandPoint != null)
                    {
                        playerController.ForceMoveTo(blackmaskStandPoint.position, blackmaskMoveDuration, () =>
                        {
                            blackmaskNpc?.Interact(playerController);
                        });
                    }
                    else
                    {
                        playerController.EnableControl(true);
                    }
                }
                else
                {
                    playerController.EnableControl(true);
                }
            });

    }

    public void DeactivateNightView(System.Action onComplete)
    {
        if (goHomeButton != null) goHomeButton.SetActive(false);
        if (evidenceCountText != null) evidenceCountText.gameObject.SetActive(false); // ★ 추가
        playerController.EnableControl(false);
        HideBannerImmediate();
        ClueArrowSystem.Instance?.ForceHideAll();

        playerCharacter.transform
            .DOScale(Vector3.zero, 0.25f)
            .SetEase(Ease.InBack)
            .OnComplete(() =>
            {
                playerCharacter.SetActive(false);
                nightTopDownCamera.gameObject.SetActive(false);
                if (dayCameraOrUICamera != null) dayCameraOrUICamera.gameObject.SetActive(true);
                foreach (var entry in locationMaps)
                    if (entry.mapRoot != null) entry.mapRoot.SetActive(false);
                activeEntry = null;
                explorationComplete = false;
                onComplete?.Invoke();
            });
    }

    // ── 배너 ─────────────────────────────────
    private void ShowCompleteBanner()
    {
        if (explorationCompleteBannerCG == null) return;
        explorationCompleteBannerCG.gameObject.SetActive(true);
        explorationCompleteBannerCG.DOKill();
        explorationCompleteBannerCG.alpha = 0f;
        explorationCompleteBannerCG.blocksRaycasts = false;

        DOTween.Sequence()
            .Append(explorationCompleteBannerCG.DOFade(1f, bannerFadeDuration))
            .AppendInterval(bannerHoldDuration)
            .Append(explorationCompleteBannerCG.DOFade(0f, bannerFadeDuration))
            .OnComplete(() => explorationCompleteBannerCG.gameObject.SetActive(false));
    }

    private void HideBannerImmediate()
    {
        if (explorationCompleteBannerCG == null) return;
        explorationCompleteBannerCG.DOKill();
        explorationCompleteBannerCG.alpha = 0f;
        explorationCompleteBannerCG.blocksRaycasts = false;
        explorationCompleteBannerCG.gameObject.SetActive(false);
    }
}