using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using DG.Tweening;

/// <summary>
/// 좌클릭(클릭) = 카드 상세 보기
/// 좌클릭 드래그 = 연결선 연결
/// Unity EventSystem이 드래그 threshold 초과 시 OnPointerClick을 자동 차단하므로
/// 클릭/드래그가 자연스럽게 분리됩니다.
/// </summary>
public class ClueCard : MonoBehaviour,
    IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerClickHandler
{
    [Header("UI 연결")]
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private TextMeshProUGUI contentText; // 포스트잇에선 비표시
    [SerializeField] private Image cardBackground;

    [Header("색상")]
    [SerializeField] private Color normalColor    = Color.white;
    [SerializeField] private Color highlightColor = new Color(1f, 0.9f, 0.4f);

    public string CardID { get; private set; }

    private ClueCardData      _data;
    private WhiteboardManager manager;
    private RectTransform     rectTransform;

    private void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
    }

    public void Initialize(ClueCardData data, WhiteboardManager mgr)
    {
        CardID  = data.cardID;
        _data   = data;
        manager = mgr;

        if (titleText != null)   titleText.text = data.cardTitle;
        if (contentText != null) contentText.gameObject.SetActive(false); // 제목만 표시
        if (cardBackground != null) cardBackground.color = normalColor;
    }

    // ── 좌클릭 → 상세 보기 ───────────────────
    // 드래그가 시작되면 Unity가 OnPointerClick을 자동 차단
    public void OnPointerClick(PointerEventData eventData)
    {
        manager?.ShowCardDetail(_data);
    }

    // ── 드래그 → 연결선 ──────────────────────
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

    public void ResetColor()
    {
        if (cardBackground == null) return;
        cardBackground.DOColor(normalColor, 0.15f);
    }

    public Vector3 GetWorldCenter() => rectTransform.position;
}
