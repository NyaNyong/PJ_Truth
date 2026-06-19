using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;
using System.Collections.Generic;
using Obvious.Soap;

public class NightMapUI : MonoBehaviour
{
    [Header("UI 연결")]
    [SerializeField] private CanvasGroup mapPanelCG;
    [SerializeField] private RectTransform buttonContainer;
    [SerializeField] private GameObject locationButtonPrefab;

    [Header("SOAP 연결")]
    [SerializeField] private StringVariable selectedLocation;
    [SerializeField] private ScriptableEventNoParam onLocationSelected;

    [Header("방문 완료 버튼 색상")]
    [SerializeField] private Color visitedColor = new Color(0.5f, 0.5f, 0.5f, 0.55f);

    [Header("DOTween 설정")]
    [SerializeField] private float panelFadeInDuration = 0.35f;
    [SerializeField] private float panelFadeOutDuration = 0.25f;
    [SerializeField] private float buttonStaggerDelay = 0.1f;
    [SerializeField] private float buttonRiseDistance = 20f;

    private readonly List<GameObject> spawnedButtons = new List<GameObject>();

    private void Awake()
    {
        if (mapPanelCG != null)
        {
            mapPanelCG.alpha = 0f;
            mapPanelCG.interactable = false;
            mapPanelCG.blocksRaycasts = false;
        }
    }

    public void ShowMap(List<LocationButtonInfo> buttonInfoList)
    {
        if (mapPanelCG == null || buttonContainer == null || locationButtonPrefab == null) return;
        if (buttonInfoList == null || buttonInfoList.Count == 0)
        {
            Debug.LogWarning("[NightMapUI] 장소 목록 비어있음"); return;
        }

        ClearButtons();

        mapPanelCG.DOKill();
        mapPanelCG.alpha = 0f;
        mapPanelCG.interactable = false;
        mapPanelCG.blocksRaycasts = false;

        SpawnButtons(buttonInfoList);

        mapPanelCG.DOFade(1f, panelFadeInDuration)
                  .SetEase(Ease.OutQuad)
                  .OnComplete(() =>
                  {
                      mapPanelCG.interactable = true;
                      mapPanelCG.blocksRaycasts = true;
                  });
    }

    private void SpawnButtons(List<LocationButtonInfo> infoList)
    {
        for (int i = 0; i < infoList.Count; i++)
        {
            LocationButtonInfo info = infoList[i];
            GameObject btnObj = Instantiate(locationButtonPrefab, buttonContainer);
            spawnedButtons.Add(btnObj);

            string labelText = string.IsNullOrEmpty(info.displayName)
                ? info.locationName : info.displayName;

            var tmp = btnObj.GetComponentInChildren<TextMeshProUGUI>();
            if (tmp != null) tmp.text = labelText;

            var btn = btnObj.GetComponent<Button>();
            var img = btnObj.GetComponent<Image>();

            if (info.isVisited)
            {
                // 방문한 장소 — 비활성화 & 흐리게
                if (btn != null) btn.interactable = false;
                if (img != null) img.color = visitedColor;
                if (tmp != null) tmp.color = new Color(1f, 1f, 1f, 0.4f);
            }
            else
            {
                // 미방문 — 클릭 시 팝업
                LocationButtonInfo captured = info;
                btn?.onClick.AddListener(() => OnLocationButtonClicked(captured));
            }

            // 등장 연출
            var rt = btnObj.GetComponent<RectTransform>();
            rt.anchoredPosition = info.buttonPosition + Vector2.down * buttonRiseDistance;

            var cg = btnObj.GetComponent<CanvasGroup>() ?? btnObj.AddComponent<CanvasGroup>();
            cg.alpha = 0f;

            Vector2 targetPos = info.buttonPosition;
            DOTween.Sequence()
                   .SetDelay(i * buttonStaggerDelay)
                   .Append(rt.DOAnchorPos(targetPos, 0.35f).SetEase(Ease.OutBack))
                   .Join(cg.DOFade(1f, 0.25f));
        }
    }

    // ── 장소 선택 팝업 ────────────────────────
    private void OnLocationButtonClicked(LocationButtonInfo info)
    {
        string displayName = string.IsNullOrEmpty(info.displayName)
            ? info.locationName : info.displayName;

        // ★ 송출 제어실: 이동 전 암호 입력 필요 (정답이어야만 ConfirmMove 진행)
        if (info.locationName == "loc_expose_control")
        {
            var expose = GameTextLoader.Instance?.GetExposeRoute();
            string code = expose?.codeInputCorrect ?? "";
            string prompt = expose?.codeInputPrompt ?? "송출 경로를 입력하십시오.";

            if (string.IsNullOrEmpty(code))
            {
                Debug.LogWarning("[NightMapUI] codeInputCorrect 미설정");
                return;
            }

            CodeInputUI.Instance?.Show(code, "choice_expose_code_correct",
                () => ConfirmMove(info.locationName), prompt);
            return;
        }

        ConfirmPopupUI.Instance?.Open(
            title: displayName,
            message: info.description,
            warning: "",
            onConfirm: () => ConfirmMove(info.locationName),
            onCancel: null,   // 다시 선택 → 팝업만 닫힘
            confirmText: "이동",
            cancelText: "다시 선택"
        );
    }

    private void ConfirmMove(string locationName)
    {
        if (selectedLocation != null) selectedLocation.Value = locationName;
        HideMap(() => onLocationSelected?.Raise());
    }

    // ── 맵 닫기 ──────────────────────────────
    public void HideMap(TweenCallback onComplete = null)
    {
        mapPanelCG.DOKill();
        mapPanelCG.interactable = false;
        mapPanelCG.blocksRaycasts = false;
        mapPanelCG.DOFade(0f, panelFadeOutDuration)
                  .SetEase(Ease.InQuad)
                  .OnComplete(() => { ClearButtons(); onComplete?.Invoke(); });
    }

    private void ClearButtons()
    {
        foreach (var b in spawnedButtons) if (b != null) Destroy(b);
        spawnedButtons.Clear();
    }
}