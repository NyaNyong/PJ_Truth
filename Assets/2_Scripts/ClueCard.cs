using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using DG.Tweening;

public class ClueCard : MonoBehaviour,
    IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [Header("UI 연결")]
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private TextMeshProUGUI contentText;
    [SerializeField] private Image cardBackground;

    [Header("색상")]
    [SerializeField] private Color normalColor = Color.white;
    [SerializeField] private Color highlightColor = new Color(1f, 0.9f, 0.4f);

    public string CardID { get; private set; }

    private WhiteboardManager manager;
    private RectTransform rectTransform;

    private void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
    }

    public void Initialize(ClueCardData data, WhiteboardManager mgr)
    {
        CardID = data.cardID;
        manager = mgr;

        if (titleText != null) titleText.text = data.cardTitle;
        if (contentText != null) contentText.text = data.cardContent;
        if (cardBackground != null) cardBackground.color = normalColor;
    }

    // ── 드래그 → 실 연결 ─────────────────────
    public void OnBeginDrag(PointerEventData eventData)
    {
        SetHighlight(true);
        manager?.StartConnectionDrag(this);
    }

    public void OnDrag(PointerEventData eventData)
    {
        manager?.UpdateConnectionDrag(eventData);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        SetHighlight(false);
        manager?.EndConnectionDrag(this, eventData);
    }

    // ── 강조 / 위치 ──────────────────────────
    public void SetHighlight(bool on)
    {
        if (cardBackground == null) return;
        cardBackground.DOColor(on ? highlightColor : normalColor, 0.2f);
    }

    /// <summary>실 연결용 스크린 포지션 (카드 중심)</summary>
    public Vector3 GetWorldCenter() => rectTransform.position;
}