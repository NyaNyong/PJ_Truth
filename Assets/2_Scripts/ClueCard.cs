using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using DG.Tweening;

/// <summary>
/// 화이트보드의 단서 카드.
/// 클릭하면 WhiteboardManager에 실 연결을 요청합니다.
/// 드래그로 위치를 옮길 수 있습니다.
/// </summary>
public class ClueCard : MonoBehaviour,
    IPointerClickHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [Header("UI 연결")]
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private TextMeshProUGUI contentText;
    [SerializeField] private Image           cardBackground;

    [Header("색상")]
    [SerializeField] private Color normalColor    = Color.white;
    [SerializeField] private Color highlightColor = new Color(1f, 0.9f, 0.4f);

    // ─────────────────────────────────────────
    public string CardID { get; private set; }

    private WhiteboardManager manager;
    private RectTransform      rectTransform;
    private Canvas             parentCanvas;
    private bool               isDragging = false;
    private Vector2            dragOffset;

    // ─────────────────────────────────────────
    private void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        parentCanvas  = GetComponentInParent<Canvas>();
    }

    public void Initialize(ClueCardData data, WhiteboardManager mgr)
    {
        CardID  = data.cardID;
        manager = mgr;

        if (titleText   != null) titleText.text   = data.cardTitle;
        if (contentText != null) contentText.text = data.cardContent;
        if (cardBackground != null) cardBackground.color = normalColor;
    }

    // ─────────────────────────────────────────
    // 클릭 → 실 연결
    // ─────────────────────────────────────────
    public void OnPointerClick(PointerEventData eventData)
    {
        if (isDragging) return; // 드래그 후 클릭 무시
        manager?.OnCardClicked(this);
    }

    // ─────────────────────────────────────────
    // 드래그 → 카드 이동
    // ─────────────────────────────────────────
    public void OnBeginDrag(PointerEventData eventData)
    {
        isDragging = true;
        // 드래그 시 카드를 최상단으로
        rectTransform.SetAsLastSibling();

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            rectTransform, eventData.position,
            eventData.pressEventCamera, out dragOffset);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (parentCanvas == null) return;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            parentCanvas.transform as RectTransform,
            eventData.position,
            eventData.pressEventCamera,
            out Vector2 localPoint);

        rectTransform.anchoredPosition = localPoint - dragOffset;
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        isDragging = false;
    }

    // ─────────────────────────────────────────
    // 강조 표시
    // ─────────────────────────────────────────
    public void SetHighlight(bool on)
    {
        if (cardBackground == null) return;
        cardBackground.DOColor(on ? highlightColor : normalColor, 0.2f);
    }

    /// <summary>실 연결용 월드 포지션 반환 (카드 중심)</summary>
    public Vector3 GetWorldCenter()
    {
        return rectTransform.position;
    }
}
