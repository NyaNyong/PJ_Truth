using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;
using System.Collections.Generic;
using Obvious.Soap;

/// <summary>
/// 밤 페이즈 지도 UI.
/// ★ SetActive 완전 제거 — CanvasGroup만으로 show/hide 제어.
///    Panel_NightMap은 항상 활성화 상태. alpha + blocksRaycasts로 가시성 제어.
/// </summary>
public class NightMapUI : MonoBehaviour
{
    [Header("UI 연결")]
    [SerializeField] private CanvasGroup   mapPanelCG;
    [SerializeField] private RectTransform buttonContainer;
    [SerializeField] private GameObject    locationButtonPrefab;

    [Header("SOAP 연결")]
    [SerializeField] private StringVariable         selectedLocation;
    [SerializeField] private ScriptableEventNoParam onLocationSelected;

    [Header("DOTween 설정")]
    [SerializeField] private float panelFadeInDuration  = 0.35f;
    [SerializeField] private float panelFadeOutDuration = 0.25f;
    [SerializeField] private float buttonStaggerDelay   = 0.1f;
    [SerializeField] private float buttonRiseDistance   = 20f;

    private readonly List<GameObject> spawnedButtons = new List<GameObject>();

    private void Awake()
    {
        // ★ SetActive(false) 제거 — CanvasGroup으로만 숨김
        if (mapPanelCG != null)
        {
            mapPanelCG.alpha          = 0f;
            mapPanelCG.interactable   = false;
            mapPanelCG.blocksRaycasts = false;
        }
    }

    public void ShowMap(List<LocationButtonInfo> buttonInfoList)
    {
        if (mapPanelCG == null)
        {
            Debug.LogError("[NightMapUI] mapPanelCG 미연결!");
            return;
        }
        if (buttonContainer == null)
        {
            Debug.LogError("[NightMapUI] buttonContainer 미연결!");
            return;
        }
        if (locationButtonPrefab == null)
        {
            Debug.LogError("[NightMapUI] locationButtonPrefab 미연결!");
            return;
        }
        if (buttonInfoList == null || buttonInfoList.Count == 0)
        {
            Debug.LogWarning("[NightMapUI] 장소 목록 비어있음");
            return;
        }

        Debug.Log($"[NightMapUI] ShowMap — 장소 {buttonInfoList.Count}개");

        ClearButtons();

        // ★ SetActive 없이 CanvasGroup으로만 표시
        mapPanelCG.DOKill();
        mapPanelCG.alpha          = 0f;
        mapPanelCG.interactable   = false;
        mapPanelCG.blocksRaycasts = false;

        SpawnButtons(buttonInfoList);

        mapPanelCG.DOFade(1f, panelFadeInDuration)
                  .SetEase(Ease.OutQuad)
                  .OnComplete(() =>
                  {
                      mapPanelCG.interactable   = true;
                      mapPanelCG.blocksRaycasts = true;
                      Debug.Log("[NightMapUI] 지도 표시 완료");
                  });
    }

    private void SpawnButtons(List<LocationButtonInfo> buttonInfoList)
    {
        for (int i = 0; i < buttonInfoList.Count; i++)
        {
            LocationButtonInfo info   = buttonInfoList[i];
            GameObject         btnObj = Instantiate(locationButtonPrefab, buttonContainer);
            spawnedButtons.Add(btnObj);

            var label = btnObj.GetComponentInChildren<TextMeshProUGUI>();
            if (label != null) label.text = string.IsNullOrEmpty(info.displayName)
    ? info.locationName
    : info.displayName;

            var rt = btnObj.GetComponent<RectTransform>();
            rt.anchoredPosition = info.buttonPosition + Vector2.down * buttonRiseDistance;

            var cg = btnObj.GetComponent<CanvasGroup>();
            if (cg == null) cg = btnObj.AddComponent<CanvasGroup>();
            cg.alpha = 0f;

            Vector2 targetPos = info.buttonPosition;
            DOTween.Sequence()
                   .SetDelay(i * buttonStaggerDelay)
                   .Append(rt.DOAnchorPos(targetPos, 0.35f).SetEase(Ease.OutBack))
                   .Join(cg.DOFade(1f, 0.25f));

            string captured = info.locationName;
            btnObj.GetComponent<Button>()?.onClick.AddListener(() => OnLocationButtonClicked(captured));
        }
    }

    private void OnLocationButtonClicked(string locationName)
    {
        Debug.Log($"🗺️ 장소 선택: {locationName}");
        if (selectedLocation != null) selectedLocation.Value = locationName;
        HideMap(() => onLocationSelected?.Raise());
    }

    private void HideMap(TweenCallback onComplete = null)
    {
        mapPanelCG.DOKill();
        mapPanelCG.interactable   = false;
        mapPanelCG.blocksRaycasts = false;
        mapPanelCG.DOFade(0f, panelFadeOutDuration)
                  .SetEase(Ease.InQuad)
                  .OnComplete(() =>
                  {
                      ClearButtons();
                      onComplete?.Invoke();
                  });
    }

    private void ClearButtons()
    {
        foreach (var btn in spawnedButtons)
            if (btn != null) Destroy(btn);
        spawnedButtons.Clear();
    }
}
