using UnityEngine;
using System.Collections.Generic;
using DG.Tweening;
using Obvious.Soap;

public class NightPhaseManager : MonoBehaviour
{
    [Header("카메라")]
    [SerializeField] private Camera dayCameraOrUICamera;
    [SerializeField] private Camera nightTopDownCamera;
    [SerializeField] private NightCameraController nightCameraController;

    [Header("플레이어")]
    [SerializeField] private GameObject playerCharacter;
    [SerializeField] private PlayerController playerController;

    [Header("퇴근 버튼")]
    [SerializeField] private GameObject goHomeButton;

    [Header("탐색 완료 알림 UI")]
    [SerializeField] private CanvasGroup explorationCompleteBannerCG;
    [SerializeField] private float bannerFadeDuration = 0.35f;
    [SerializeField] private float bannerHoldDuration = 2f;

    [Header("장소-맵 매핑")]
    [SerializeField] private List<LocationMapEntry> locationMaps;

    [System.Serializable]
    public class LocationMapEntry
    {
        public string locationName;
        [Tooltip("지도 버튼에 표시할 한글 이름. 비우면 locationName 그대로 표시")]
        public string displayName; // ★ 추가
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



    public bool IsExplorationComplete => explorationComplete;
    public bool HasRequiredClues =>
        activeEntry != null && activeEntry.requiredClueIDs != null && activeEntry.requiredClueIDs.Count > 0;

    private void Awake()
    {
        nightTopDownCamera.gameObject.SetActive(false);
        playerCharacter.SetActive(false);
        if (goHomeButton != null) goHomeButton.SetActive(false);
        foreach (var entry in locationMaps)
            if (entry.mapRoot != null) entry.mapRoot.SetActive(false);
        HideBannerImmediate();
        playerOriginalScale = playerCharacter.transform.localScale; // ★ 원본 스케일 저장
    }

    /// <summary>GameManager 초기화 시 강제 비활성화 (낮 페이즈에 밤 배경 노출 방지)</summary>
    public void EnsureHidden()
    {
        if (nightTopDownCamera != null)
            nightTopDownCamera.gameObject.SetActive(false);
        if (playerCharacter != null)
            playerCharacter.SetActive(false);
        foreach (var entry in locationMaps)
            if (entry.mapRoot != null) entry.mapRoot.SetActive(false);
    }

    private void OnEnable()
    {
        if (onLocationSelected != null) onLocationSelected.OnRaised += OnLocationSelectedHandler;
        if (onClueCollected != null) onClueCollected.OnRaised += OnClueCollectedHandler;
    }

    private void OnDisable()
    {
        if (onLocationSelected != null) onLocationSelected.OnRaised -= OnLocationSelectedHandler;
        if (onClueCollected != null) onClueCollected.OnRaised -= OnClueCollectedHandler;
    }

    public List<LocationButtonInfo> BuildButtonInfoList(List<string> locationNames)
    {
        var result = new List<LocationButtonInfo>();
        foreach (string name in locationNames)
        {
            var entry = locationMaps.Find(e => e.locationName == name);
            result.Add(new LocationButtonInfo
            {
                locationName = name,
                displayName = (entry != null && !string.IsNullOrEmpty(entry.displayName))
                                 ? entry.displayName
                                 : name.Replace('_', ' '), // ★ 폴백: 언더바→공백
                buttonPosition = entry != null ? entry.mapButtonPosition : Vector2.zero
            });
        }
        return result;
    }

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

    private Vector3 playerOriginalScale;
    private void ActivateNightView(string locationName)
    {
        Debug.Log($"🌙 탑다운 뷰 활성화 → {locationName}");
        explorationComplete = false;
        activeEntry = null;

        foreach (var entry in locationMaps)
        {
            bool isTarget = entry.locationName == locationName;
            if (entry.mapRoot != null) entry.mapRoot.SetActive(isTarget);
            if (isTarget) activeEntry = entry;
        }

        if (dayCameraOrUICamera != null) dayCameraOrUICamera.gameObject.SetActive(false);
        nightTopDownCamera.gameObject.SetActive(true);

        if (nightCameraController != null)
            nightCameraController.SetBoundary(activeEntry?.mapBoundary);
        playerController.SetMapBoundary(activeEntry?.mapBoundary);

        if (!HasRequiredClues) explorationComplete = true;

        Vector3 spawnPos = activeEntry?.spawnPoint != null
            ? activeEntry.spawnPoint.position : Vector3.zero;

        playerCharacter.transform.position = spawnPos;
        playerCharacter.SetActive(true);
        // ActivateNightView() 안의 DOScale 부분 교체
        playerCharacter.transform.localScale = Vector3.zero;
        playerCharacter.transform.DOScale(playerOriginalScale, playerSpawnDuration) // ★ Vector3.one → playerOriginalScale
            .SetEase(Ease.OutBack)
            .OnComplete(() =>
            {
                playerController.EnableControl(true);
                if (goHomeButton != null) goHomeButton.SetActive(true);
            });
    }

    public void DeactivateNightView(System.Action onComplete)
    {
        if (goHomeButton != null) goHomeButton.SetActive(false);
        playerController.EnableControl(false);
        HideBannerImmediate();

        playerCharacter.transform.DOScale(Vector3.zero, 0.25f) // 이건 0으로 줄이는 거라 그대로 OK
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