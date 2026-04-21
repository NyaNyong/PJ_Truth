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

    [Header("장소-맵 매핑")]
    [SerializeField] private List<LocationMapEntry> locationMaps;

    [System.Serializable]
    public class LocationMapEntry
    {
        [Tooltip("DailyData의 장소 이름과 정확히 일치해야 합니다")]
        public string locationName;

        public GameObject mapRoot;
        public MapBoundary mapBoundary;
        public Transform spawnPoint;

        [Tooltip("지도 UI에서 이 장소 버튼이 표시될 위치 (ButtonContainer 기준 앵커 좌표)")]
        public Vector2 mapButtonPosition;
    }

    [Header("SOAP 연결")]
    [SerializeField] private StringVariable selectedLocation;
    [SerializeField] private ScriptableEventNoParam onLocationSelected;
    [SerializeField] private ScriptableEventNoParam onPhaseTransitionRequest;

    [Header("DOTween 설정")]
    [SerializeField] private float playerSpawnDuration = 0.4f;

    private void Awake()
    {
        nightTopDownCamera.gameObject.SetActive(false);
        playerCharacter.SetActive(false);
        if (goHomeButton != null) goHomeButton.SetActive(false);

        foreach (var entry in locationMaps)
            if (entry.mapRoot != null) entry.mapRoot.SetActive(false);
    }

    private void OnEnable()
    {
        if (onLocationSelected != null)
            onLocationSelected.OnRaised += OnLocationSelectedHandler;
        if (onPhaseTransitionRequest != null)
            onPhaseTransitionRequest.OnRaised += () => DeactivateNightView(null);
    }

    private void OnDisable()
    {
        if (onLocationSelected != null)
            onLocationSelected.OnRaised -= OnLocationSelectedHandler;
        if (onPhaseTransitionRequest != null)
            onPhaseTransitionRequest.OnRaised -= () => DeactivateNightView(null);
    }

    /// <summary>
    /// 장소 이름 목록을 받아 버튼 위치 정보를 포함한 LocationButtonInfo 리스트로 변환합니다.
    /// GameManager가 이 메서드를 호출해 NightMapUI에 전달할 데이터를 만듭니다.
    /// </summary>
    public List<LocationButtonInfo> BuildButtonInfoList(List<string> locationNames)
    {
        var result = new List<LocationButtonInfo>();

        foreach (string name in locationNames)
        {
            var entry = locationMaps.Find(e => e.locationName == name);
            result.Add(new LocationButtonInfo
            {
                locationName   = name,
                // 매핑 테이블에 없는 장소는 화면 중앙 근처에 순서대로 배치
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

    private void ActivateNightView(string locationName)
    {
        Debug.Log($"🌙 탑다운 뷰 활성화 → {locationName}");

        LocationMapEntry targetEntry = null;
        foreach (var entry in locationMaps)
        {
            bool isTarget = entry.locationName == locationName;
            if (entry.mapRoot != null) entry.mapRoot.SetActive(isTarget);
            if (isTarget) targetEntry = entry;
        }

        if (dayCameraOrUICamera != null) dayCameraOrUICamera.gameObject.SetActive(false);
        nightTopDownCamera.gameObject.SetActive(true);

        if (nightCameraController != null && targetEntry?.mapBoundary != null)
        {
            var field = typeof(NightCameraController).GetField("mapBoundary",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            field?.SetValue(nightCameraController, targetEntry.mapBoundary);
        }

        Vector3 spawnPos = targetEntry?.spawnPoint != null
            ? targetEntry.spawnPoint.position
            : Vector3.zero;

        playerCharacter.transform.position = spawnPos;
        playerCharacter.SetActive(true);
        playerCharacter.transform.localScale = Vector3.zero;
        playerCharacter.transform.DOScale(Vector3.one, playerSpawnDuration)
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

        playerCharacter.transform.DOScale(Vector3.zero, 0.25f)
            .SetEase(Ease.InBack)
            .OnComplete(() =>
            {
                playerCharacter.SetActive(false);
                nightTopDownCamera.gameObject.SetActive(false);
                if (dayCameraOrUICamera != null) dayCameraOrUICamera.gameObject.SetActive(true);
                foreach (var entry in locationMaps)
                    if (entry.mapRoot != null) entry.mapRoot.SetActive(false);
                onComplete?.Invoke();
            });
    }
}
