using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using DG.Tweening;

public class WordCard : MonoBehaviour,
    IBeginDragHandler, IDragHandler, IEndDragHandler
{
    public int    SlotIndex { get; private set; }
    public string Word      { get; private set; }

    private TypewriterSystem typewriterSystem;
    private Canvas           rootCanvas;
    private GameObject       ghostObj;
    private RectTransform    ghostRT;
    private CanvasGroup      myCanvasGroup;
    private RectTransform    myRT;

    public void Setup(int slotIndex, string word,
                      TypewriterSystem system, Canvas canvas)
    {
        SlotIndex        = slotIndex;
        Word             = word;
        typewriterSystem = system;
        rootCanvas       = canvas;
        myRT             = GetComponent<RectTransform>();
        myCanvasGroup    = GetComponent<CanvasGroup>();
        if (myCanvasGroup == null)
            myCanvasGroup = gameObject.AddComponent<CanvasGroup>();
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        myCanvasGroup.alpha = 0.4f;

        // 루트 캔버스 아래에 고스트 생성
        Canvas targetCanvas = rootCanvas != null
            ? rootCanvas
            : GetComponentInParent<Canvas>().rootCanvas;

        ghostObj = new GameObject("WordCard_Ghost");
        ghostObj.transform.SetParent(targetCanvas.transform, false);
        ghostObj.transform.SetAsLastSibling();

        var ghostImg  = ghostObj.AddComponent<Image>();
        ghostImg.color = new Color(0.95f, 0.85f, 0.4f, 0.9f);

        ghostRT           = ghostObj.GetComponent<RectTransform>();
        ghostRT.sizeDelta = myRT != null ? myRT.sizeDelta : new Vector2(120f, 40f);
        ghostRT.pivot     = new Vector2(0.5f, 0.5f);
        // anchorMin/Max 0.5로 맞춰야 position 설정이 정확함
        ghostRT.anchorMin = new Vector2(0.5f, 0.5f);
        ghostRT.anchorMax = new Vector2(0.5f, 0.5f);

        // 텍스트 복사
        var srcText = GetComponentInChildren<TextMeshProUGUI>();
        if (srcText != null)
        {
            var tObj   = new GameObject("Text");
            tObj.transform.SetParent(ghostObj.transform, false);
            var tComp  = tObj.AddComponent<TextMeshProUGUI>();
            tComp.text      = srcText.text;
            tComp.font      = srcText.font;
            tComp.fontSize  = srcText.fontSize;
            tComp.color     = Color.black;
            tComp.alignment = TextAlignmentOptions.Center;
            var tRT         = tObj.GetComponent<RectTransform>();
            tRT.anchorMin   = Vector2.zero;
            tRT.anchorMax   = Vector2.one;
            tRT.offsetMin   = Vector2.zero;
            tRT.offsetMax   = Vector2.zero;
        }

        var ghostCG            = ghostObj.AddComponent<CanvasGroup>();
        ghostCG.blocksRaycasts = false;

        MoveGhostToPointer(eventData.position);
    }

    public void OnDrag(PointerEventData eventData)
    {
        MoveGhostToPointer(eventData.position);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (ghostObj != null) Destroy(ghostObj);
        myCanvasGroup.DOFade(1f, 0.15f);

        var viewer = FindObjectOfType<DocumentViewer>();
        if (viewer == null) return;

        int droppedSlot = viewer.GetSlotIndexAtScreenPos(eventData.position);
        if (droppedSlot >= 0)
        {
            Debug.Log($"[WordCard] 슬롯 {droppedSlot} ← '{Word}'");
            typewriterSystem.NotifyWordDropped(droppedSlot, Word);
        }
        else
        {
            Debug.Log("[WordCard] 슬롯 위치 아님");
        }
    }

    /// <summary>
    /// Screen Space Overlay 전용 위치 설정.
    /// RectTransform.position = 스크린 좌표 (z=0) 로 직접 대입.
    /// </summary>
    private void MoveGhostToPointer(Vector2 screenPos)
    {
        if (ghostRT == null) return;
        // Screen Space - Overlay에서 world position == screen position (z=0)
        ghostRT.position = new Vector3(screenPos.x, screenPos.y, 0f);
    }
}
