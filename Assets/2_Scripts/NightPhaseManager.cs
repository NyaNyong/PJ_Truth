using UnityEngine;
using System.Collections.Generic;
using DG.Tweening;
using Sirenix.OdinInspector;
using Obvious.Soap;

/// <summary>
/// 밤 페이즈의 탑다운 뷰를 총괄합니다.
/// OnLocationSelected 이벤트를 구독하여 선택된 장소의 맵을 활성화하고,
/// 카메라를 전환하며 플레이어를 등장시킵니다.
/// </summary>
public class NightPhaseManager : MonoBehaviour
{
    // ─────────────────────────────────────────
    // 카메라
    // ─────────────────────────────────────────
    [BoxGroup("카메라")]
    [Required]
    [Tooltip("낮 페이즈에 사용되는 메인 UI 카메라")]
    [SerializeField] private Camera dayCameraOrUICamera;

    [BoxGroup("카메라")]
    [Required]
    [Tooltip("밤 탑다운 탐색에 사용되는 카메라")]
    [SerializeField] private Camera nightTopDownCamera;

    // ─────────────────────────────────────────
    // 플레이어
    // ─────────────────────────────────────────
    [BoxGroup("플레이어")]
    [Required]
    [SerializeField] private GameObject playerCharacter;

    [BoxGroup("플레이어")]
    [Required]
    [SerializeField] private PlayerController playerController;

    // ─────────────────────────────────────────
    // 장소-맵 매핑
    // ─────────────────────────────────────────
    [BoxGroup("장소-맵 매핑")]
    [TableList(ShowIndexLabels = true, AlwaysExpanded = true)]
    [Tooltip("DailyData.availableLocations의 문자열과 실제 맵 GameObject를 연결합니다")]
    [SerializeField] private List<LocationMapEntry> locationMaps;

    [System.Serializable]
    public class LocationMapEntry
    {
        [TableColumnWidth(140, Resizable = false)]
        public string locationName;

        [TableColumnWidth(200)]
        public GameObject mapRoot;

        [TableColumnWidth(130)]
        [Tooltip("이 장소에서 플레이어가 스폰될 위치")]
        public Transform spawnPoint;
    }

    // ─────────────────────────────────────────
    // SOAP 연결
    // ─────────────────────────────────────────
    [BoxGroup("SOAP 연결")]
    [Required]
    [SerializeField] private StringVariable selectedLocation;

    [BoxGroup("SOAP 연결")]
    [Required]
    [Tooltip("장소 선택 완료 이벤트 — 이 이벤트를 구독해 탑다운 뷰를 활성화합니다")]
    [SerializeField] private ScriptableEventNoParam onLocationSelected;

    [BoxGroup("SOAP 연결")]
    [Required]
    [Tooltip("탐색 종료(엘리베이터/퇴장) 시 Raise — 밤 페이즈를 닫고 다음 페이즈로 이동합니다")]
    [SerializeField] private ScriptableEventNoParam onPhaseTransitionRequest;

    // ─────────────────────────────────────────
    // DOTween 설정
    // ─────────────────────────────────────────
    [BoxGroup("DOTween 설정")]
    [SerializeField] private float playerSpawnDuration = 0.4f;

    [BoxGroup("DOTween 설정")]
    [SerializeField] private float cameraTransitionDuration = 0.5f;

    // ─────────────────────────────────────────
    // 라이프사이클
    // ─────────────────────────────────────────
    private void Awake()
    {
        // 시작 시 밤 뷰 비활성화
        nightTopDownCamera.gameObject.SetActive(false);
        playerCharacter.SetActive(false);

        foreach (var entry in locationMaps)
            if (entry.mapRoot != null)
                entry.mapRoot.SetActive(false);
    }

    private void OnEnable()
    {
        if (onLocationSelected != null)
            onLocationSelected.OnRaised += OnLocationSelectedHandler;

        if (onPhaseTransitionRequest != null)
            onPhaseTransitionRequest.OnRaised += DeactivateNightView;
    }

    private void OnDisable()
    {
        if (onLocationSelected != null)
            onLocationSelected.OnRaised -= OnLocationSelectedHandler;

        if (onPhaseTransitionRequest != null)
            onPhaseTransitionRequest.OnRaised -= DeactivateNightView;
    }

    // ─────────────────────────────────────────
    // 이벤트 핸들러
    // ─────────────────────────────────────────
    private void OnLocationSelectedHandler()
    {
        string loc = selectedLocation != null ? selectedLocation.Value : string.Empty;

        if (string.IsNullOrEmpty(loc))
        {
            Debug.LogWarning("[NightPhaseManager] selectedLocation이 비어 있습니다.");
            return;
        }

        ActivateNightView(loc);
    }

    // ─────────────────────────────────────────
    // 밤 뷰 활성화
    // ─────────────────────────────────────────
    private void ActivateNightView(string locationName)
    {
        Debug.Log($"🌙 [NightPhaseManager] 탑다운 뷰 활성화 → {locationName}");

        // 해당 장소 맵만 활성화
        LocationMapEntry targetEntry = null;
        foreach (var entry in locationMaps)
        {
            bool isTarget = entry.locationName == locationName;
            if (entry.mapRoot != null) entry.mapRoot.SetActive(isTarget);
            if (isTarget) targetEntry = entry;
        }

        if (targetEntry == null)
        {
            Debug.LogWarning($"[NightPhaseManager] '{locationName}'에 해당하는 맵이 없습니다. LocationMaps를 확인하세요.");
        }

        // 카메라 전환
        SwitchToNightCamera();

        // 플레이어 스폰
        SpawnPlayer(targetEntry);
    }

    private void SwitchToNightCamera()
    {
        // 낮 카메라 페이드 아웃 후 밤 카메라 활성화
        // (카메라 자체엔 DOFade 불가 → CanvasGroup 검은 오버레이로 연출 가능하나
        //  우선 간단한 즉시 전환으로 구현 후 추후 개선)
        if (dayCameraOrUICamera != null)
            dayCameraOrUICamera.gameObject.SetActive(false);

        nightTopDownCamera.gameObject.SetActive(true);

        // 카메라 AudioListener 중복 방지
        var dayListener = dayCameraOrUICamera?.GetComponent<AudioListener>();
        if (dayListener != null) dayListener.enabled = false;
    }

    private void SpawnPlayer(LocationMapEntry entry)
    {
        // 스폰 위치 결정
        Vector3 spawnPos = (entry?.spawnPoint != null)
            ? entry.spawnPoint.position
            : Vector3.zero;

        playerCharacter.transform.position = spawnPos;
        playerCharacter.SetActive(true);

        // 등장 애니메이션 (스케일 팝업)
        playerCharacter.transform.localScale = Vector3.zero;
        playerCharacter.transform
            .DOScale(Vector3.one, playerSpawnDuration)
            .SetEase(Ease.OutBack)
            .OnComplete(() => playerController.EnableControl(true));
    }

    // ─────────────────────────────────────────
    // 밤 뷰 비활성화 (탐색 종료)
    // ─────────────────────────────────────────
    public void DeactivateNightView()
    {
        Debug.Log("🌙 [NightPhaseManager] 탑다운 뷰 비활성화");

        playerController.EnableControl(false);

        // 플레이어 퇴장 애니메이션
        playerCharacter.transform
            .DOScale(Vector3.zero, 0.25f)
            .SetEase(Ease.InBack)
            .OnComplete(() =>
            {
                playerCharacter.SetActive(false);
                nightTopDownCamera.gameObject.SetActive(false);

                // 낮 카메라 복구
                if (dayCameraOrUICamera != null)
                {
                    dayCameraOrUICamera.gameObject.SetActive(true);
                    var dayListener = dayCameraOrUICamera.GetComponent<AudioListener>();
                    if (dayListener != null) dayListener.enabled = true;
                }

                // 모든 맵 비활성화
                foreach (var entry in locationMaps)
                    if (entry.mapRoot != null)
                        entry.mapRoot.SetActive(false);
            });
    }
}
