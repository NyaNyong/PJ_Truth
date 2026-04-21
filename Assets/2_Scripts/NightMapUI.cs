using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;
using System.Collections;
using System.Collections.Generic;
using Obvious.Soap;

public class NightMapUI : MonoBehaviour
{
    [Header("UI 연결")]
    [SerializeField] private CanvasGroup mapPanelCG;

    [Tooltip("버튼들이 놓일 부모. VerticalLayoutGroup 없이 자유 배치로 사용합니다.")]
    [SerializeField] private RectTransform buttonContainer;

    [SerializeField] private GameObject locationButtonPrefab;

    [Header("SOAP 연결")]
    [SerializeField] private StringVariable selectedLocation;
    [SerializeField] private ScriptableEventNoParam onLocationSelected;

    [Header("DOTween 설정")]
    [SerializeField] private float panelFadeInDuration  = 0.35f;
    [SerializeField] private float panelFadeOutDuration = 0.25f;
    [SerializeField] private float buttonStaggerDelay   = 0.1f;
    [SerializeField] private float buttonRiseDistance   = 20f;

    private readonly List<GameObject> spawnedButtons = new List<GameObject>();

    private void Awake()
    {
        if (mapPanelCG != null)
        {
            mapPanelCG.alpha          = 0f;
            mapPanelCG.interactable   = false;
            mapPanelCG.blocksRaycasts = false;
            mapPanelCG.gameObject.SetActive(false);
        }
    }

    /// <summary>위치 정보가 포함된 버튼 목록으로 지도를 표시합니다.</summary>
    public void ShowMap(List<LocationButtonInfo> buttonInfoList)
    {
        if (buttonInfoList == null || buttonInfoList.Count == 0)
        {
            Debug.LogWarning("[NightMapUI] 표시할 장소가 없습니다.");
            return;
        }

        ClearButtons();
        mapPanelCG.gameObject.SetActive(true);
        mapPanelCG.alpha          = 0f;
        mapPanelCG.interactable   = false;
        mapPanelCG.blocksRaycasts = false;

        StartCoroutine(SpawnAndAnimateButtons(buttonInfoList));
    }

    private IEnumerator SpawnAndAnimateButtons(List<LocationButtonInfo> buttonInfoList)
    {
        var spawned = new List<(RectTransform rt, CanvasGroup cg)>();

        foreach (var info in buttonInfoList)
        {
            GameObject btnObj = Instantiate(locationButtonPrefab, buttonContainer);
            spawnedButtons.Add(btnObj);

            // 텍스트 설정
            var label = btnObj.GetComponentInChildren<TextMeshProUGUI>();
            if (label != null) label.text = info.locationName;

            // ★ 지정된 위치로 배치
            var rt = btnObj.GetComponent<RectTransform>();
            rt.anchoredPosition = info.buttonPosition;

            // 초기 투명 처리
            var cg = btnObj.GetComponent<CanvasGroup>();
            if (cg == null) cg = btnObj.AddComponent<CanvasGroup>();
            cg.alpha = 0f;

            // 클릭 이벤트
            string captured = info.locationName;
            btnObj.GetComponent<Button>()?.onClick.AddListener(() => OnLocationButtonClicked(captured));

            spawned.Add((rt, cg));
        }

        // 한 프레임 대기 후 애니메이션
        yield return null;

        // 패널 페이드 인
        mapPanelCG.DOFade(1f, panelFadeInDuration).SetEase(Ease.OutQuad)
                  .OnComplete(() =>
                  {
                      mapPanelCG.interactable   = true;
                      mapPanelCG.blocksRaycasts = true;
                  });

        // 각 버튼 순차 등장
        for (int i = 0; i < spawned.Count; i++)
        {
            var (rt, cg) = spawned[i];
            Vector2 targetPos = rt.anchoredPosition;
            rt.anchoredPosition = targetPos + Vector2.down * buttonRiseDistance;

            DOTween.Sequence()
                   .SetDelay(i * buttonStaggerDelay)
                   .Append(rt.DOAnchorPos(targetPos, 0.35f).SetEase(Ease.OutBack))
                   .Join(cg.DOFade(1f, 0.25f));
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
        mapPanelCG.interactable   = false;
        mapPanelCG.blocksRaycasts = false;
        mapPanelCG.DOFade(0f, panelFadeOutDuration).SetEase(Ease.InQuad)
                  .OnComplete(() =>
                  {
                      mapPanelCG.gameObject.SetActive(false);
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
