using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using Obvious.Soap;

/// <summary>
/// 밤 페이즈 시작 시 지도(맵) UI를 표시하고,
/// DailyData.availableLocations 목록을 버튼으로 동적 생성합니다.
/// 장소 선택 시 StringVariable에 값을 저장하고 ScriptableEventNoParam을 Raise합니다.
/// </summary>
public class NightMapUI : MonoBehaviour
{
    // ─────────────────────────────────────────
    // UI 연결
    // ─────────────────────────────────────────
    [BoxGroup("UI 연결")]
    [Required]
    [SerializeField] private CanvasGroup mapPanelCG;

    [BoxGroup("UI 연결")]
    [Required]
    [Tooltip("버튼들이 생성될 부모 Transform (VerticalLayoutGroup 권장)")]
    [SerializeField] private Transform buttonContainer;

    [BoxGroup("UI 연결")]
    [Required]
    [Tooltip("장소 버튼 프리팹 — Button + TextMeshProUGUI 포함")]
    [SerializeField] private GameObject locationButtonPrefab;

    // ─────────────────────────────────────────
    // SOAP 연결
    // ─────────────────────────────────────────
    [BoxGroup("SOAP 연결")]
    [Required]
    [Tooltip("선택된 장소 이름을 저장. NightPhaseManager가 읽어 해당 맵을 활성화함")]
    [SerializeField] private StringVariable selectedLocation;

    [BoxGroup("SOAP 연결")]
    [Required]
    [Tooltip("장소 선택 완료 신호 — NightPhaseManager와 GameManager가 구독")]
    [SerializeField] private ScriptableEventNoParam onLocationSelected;

    // ─────────────────────────────────────────
    // DOTween 설정
    // ─────────────────────────────────────────
    [BoxGroup("DOTween 설정")]
    [SerializeField] private float panelFadeInDuration   = 0.35f;
    [SerializeField] private float panelFadeOutDuration  = 0.25f;

    [BoxGroup("DOTween 설정")]
    [Tooltip("버튼이 하나씩 순차적으로 등장하는 간격 (초)")]
    [Range(0.04f, 0.2f)]
    [SerializeField] private float buttonStaggerDelay    = 0.08f;

    [BoxGroup("DOTween 설정")]
    [Tooltip("버튼이 아래에서 올라오는 거리 (px)")]
    [SerializeField] private float buttonRiseDistance    = 28f;

    // ─────────────────────────────────────────
    // 생성된 버튼 캐싱 (재사용 방지용)
    // ─────────────────────────────────────────
    private readonly List<GameObject> spawnedButtons = new List<GameObject>();

    // ─────────────────────────────────────────
    // 초기화
    // ─────────────────────────────────────────
    private void Awake()
    {
        // 시작 시 패널 숨김 상태로 초기화
        if (mapPanelCG != null)
        {
            mapPanelCG.alpha          = 0f;
            mapPanelCG.interactable   = false;
            mapPanelCG.blocksRaycasts = false;
            mapPanelCG.gameObject.SetActive(false);
        }
    }

    // ─────────────────────────────────────────
    // 공개 인터페이스 — GameManager가 호출
    // ─────────────────────────────────────────
    public void ShowMap(List<string> locations)
    {
        if (locations == null || locations.Count == 0)
        {
            Debug.LogWarning("[NightMapUI] availableLocations가 비어 있습니다.");
            return;
        }

        ClearButtons();
        SpawnButtons(locations);

        // 패널 페이드 인
        mapPanelCG.gameObject.SetActive(true);
        mapPanelCG.alpha          = 0f;
        mapPanelCG.interactable   = false;
        mapPanelCG.blocksRaycasts = false;

        mapPanelCG.DOFade(1f, panelFadeInDuration)
                  .SetEase(Ease.OutQuad)
                  .OnComplete(() =>
                  {
                      mapPanelCG.interactable   = true;
                      mapPanelCG.blocksRaycasts = true;
                  });
    }

    // ─────────────────────────────────────────
    // 버튼 생성
    // ─────────────────────────────────────────
    private void SpawnButtons(List<string> locations)
    {
        for (int i = 0; i < locations.Count; i++)
        {
            string locationName = locations[i]; // 클로저 캡처용 로컬 변수

            GameObject btnObj = Instantiate(locationButtonPrefab, buttonContainer);
            spawnedButtons.Add(btnObj);

            // 텍스트 설정
            var label = btnObj.GetComponentInChildren<TextMeshProUGUI>();
            if (label != null) label.text = locationName;

            // 등장 애니메이션 — 아래에서 위로 순차적 팝업
            var rt = btnObj.GetComponent<RectTransform>();
            if (rt != null)
            {
                Vector2 targetPos = rt.anchoredPosition;
                rt.anchoredPosition = targetPos + Vector2.down * buttonRiseDistance;

                // 초기 상태 숨김
                var btnCG = btnObj.GetComponent<CanvasGroup>();
                if (btnCG == null) btnCG = btnObj.AddComponent<CanvasGroup>();
                btnCG.alpha = 0f;

                float delay = i * buttonStaggerDelay;

                DOTween.Sequence()
                       .SetDelay(delay)
                       .Append(rt.DOAnchorPos(targetPos, 0.4f).SetEase(Ease.OutBack))
                       .Join(btnCG.DOFade(1f, 0.3f));
            }

            // 클릭 이벤트
            var button = btnObj.GetComponent<Button>();
            if (button != null)
            {
                button.onClick.AddListener(() => OnLocationButtonClicked(locationName));
            }
        }
    }

    // ─────────────────────────────────────────
    // 장소 선택 처리
    // ─────────────────────────────────────────
    private void OnLocationButtonClicked(string locationName)
    {
        Debug.Log($"🗺️ [NightMapUI] 장소 선택: {locationName}");

        // SOAP Variable에 선택된 장소 저장
        if (selectedLocation != null)
            selectedLocation.Value = locationName;

        // 패널 닫기 후 이벤트 발행
        HideMap(() => onLocationSelected?.Raise());
    }

    // ─────────────────────────────────────────
    // 패널 닫기
    // ─────────────────────────────────────────
    private void HideMap(TweenCallback onComplete = null)
    {
        mapPanelCG.interactable   = false;
        mapPanelCG.blocksRaycasts = false;

        mapPanelCG.DOFade(0f, panelFadeOutDuration)
                  .SetEase(Ease.InQuad)
                  .OnComplete(() =>
                  {
                      mapPanelCG.gameObject.SetActive(false);
                      ClearButtons();
                      onComplete?.Invoke();
                  });
    }

    // ─────────────────────────────────────────
    // 버튼 정리
    // ─────────────────────────────────────────
    private void ClearButtons()
    {
        foreach (var btn in spawnedButtons)
        {
            if (btn != null) Destroy(btn);
        }
        spawnedButtons.Clear();
    }
}
