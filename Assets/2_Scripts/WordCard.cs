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

        Canvas targetCanvas = rootCanvas != null
            ? rootCanvas
            : GetComponentInParent<Canvas>().rootCanvas;

        // ★ 노란 박스 대신 자신을 복제해서 고스트 생성
        ghostObj = Instantiate(gameObject, targetCanvas.transform);
        ghostObj.name = "WordCard_Ghost";
        ghostObj.transform.SetAsLastSibling();

        // 상호작용 컴포넌트 제거
        var ghostCard = ghostObj.GetComponent<WordCard>();
        if (ghostCard != null) Destroy(ghostCard);
        var layoutElem = ghostObj.GetComponent<LayoutElement>();
        if (layoutElem != null) Destroy(layoutElem);

        // 고스트 스타일
        var ghostCG = ghostObj.GetComponent<CanvasGroup>();
        if (ghostCG == null) ghostCG = ghostObj.AddComponent<CanvasGroup>();
        ghostCG.alpha = 0.75f;
        ghostCG.blocksRaycasts = false;
        ghostCG.interactable = false;

        ghostRT = ghostObj.GetComponent<RectTransform>();
        ghostRT.anchorMin = new Vector2(0.5f, 0.5f);
        ghostRT.anchorMax = new Vector2(0.5f, 0.5f);
        ghostRT.pivot = new Vector2(0.5f, 0.5f);

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
