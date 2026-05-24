using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;
using System.Collections.Generic;

public class TypewriterSystem : MonoBehaviour
{
    [Header("UI 연결")]
    [SerializeField] private CanvasGroup typewriterPanelCG;
    [SerializeField] private RectTransform wordCardContainer;
    [SerializeField] private GameObject wordCardPrefab;

    [Header("드래그 연출")]
    [SerializeField] private Canvas rootCanvas;

    [Header("마우스 휠 스크롤")]
    [Tooltip("wordCardContainer를 감싸는 뷰포트 (Image + Mask 컴포넌트 필요)")]
    [SerializeField] private RectTransform scrollViewport;
    [SerializeField] private float scrollSpeed = 1000f;

    [Header("DOTween 설정")]
    [SerializeField] private float slideDuration = 0.25f;
    [SerializeField] private float cardStagger = 0.05f;

    [Header("패널 위치")]
    [SerializeField] private float hiddenY = -250f;
    [SerializeField] private float visibleY = -80f;

    [Header("닫기 버튼")]
    [SerializeField] private Button closeButton;

    public System.Action OnClosed;
    public System.Action<int, string> OnWordDropped;
    public bool IsOpen { get; private set; } = false;

    private DocumentData currentDocument;
    private List<GameObject> spawnedCards = new List<GameObject>();
    private RectTransform panelRT;
    private float _scrollX = 0f;

    private void Awake()
    {
        panelRT = typewriterPanelCG?.GetComponent<RectTransform>();
        HideImmediate();
        if (closeButton != null)
            closeButton.onClick.AddListener(OnClickClose);
    }

    private void Update()
    {
        if (!IsOpen || wordCardContainer == null || scrollViewport == null) return;
        float wheel = Input.GetAxis("Mouse ScrollWheel");
        if (Mathf.Abs(wheel) < 0.001f) return;

        float contentW = wordCardContainer.rect.width;
        float viewportW = scrollViewport.rect.width;
        float maxOffset = contentW - viewportW;

        // ★ 범위 유효성 확인 (maxOffset 음수면 clamp min>max 오작동)
        if (maxOffset <= 0f) return;

        _scrollX = Mathf.Clamp(_scrollX - wheel * scrollSpeed, -maxOffset, 0f);
        wordCardContainer.anchoredPosition = new Vector2(_scrollX, 0f);
    }

    public void Initialize(DocumentData doc)
    {
        currentDocument = doc;
    }

    public void OpenPanel()
    {
        if (currentDocument == null) return;
        if (currentDocument.typewriterSlots == null ||
            currentDocument.typewriterSlots.Count == 0) return;

        AudioManager.Instance?.PlaySfxTypewriterOpen();
        IsOpen = true;

    

        SpawnAllWordCards();
        wordCardContainer.anchorMin = new Vector2(0f, 0f);
        wordCardContainer.anchorMax = new Vector2(0f, 1f);
        wordCardContainer.pivot = new Vector2(0f, 0.5f);
        wordCardContainer.anchoredPosition = Vector2.zero;

        _scrollX = 0f;
        if (wordCardContainer != null)
            wordCardContainer.anchoredPosition =
                new Vector2(0f, wordCardContainer.anchoredPosition.y);
        ShowPanel();
    }

    public void Close()
    {
        if (!IsOpen) return;
        AudioManager.Instance?.PlaySfxTypewriterClose();
        IsOpen = false;
        OnClosed?.Invoke();
        HidePanel();
    }

    private void OnClickClose() { if (IsOpen) Close(); }

    private void SpawnAllWordCards()
    {
        ClearCards();

        for (int si = 0; si < currentDocument.typewriterSlots.Count; si++)
        {
            var slot = currentDocument.typewriterSlots[si];
            if (slot.wordOptions == null) continue;

            foreach (string word in slot.wordOptions)
            {
                int capturedSlot = si;
                string capturedWord = word;

                GameObject obj = Instantiate(wordCardPrefab, wordCardContainer);
                spawnedCards.Add(obj);

                var label = obj.GetComponentInChildren<TextMeshProUGUI>();
                if (label != null) label.text = word;

                bool alreadyUsed = slot.insertedWord == word;
                var img = obj.GetComponent<Image>();
                if (img != null)
                    img.color = alreadyUsed ? new Color(0.7f, 0.7f, 0.7f) : Color.white;

                var card = obj.GetComponent<WordCard>();
                if (card == null) card = obj.AddComponent<WordCard>();
                card.Setup(capturedSlot, capturedWord, this, rootCanvas);

                var cg = obj.GetComponent<CanvasGroup>();
                if (cg == null) cg = obj.AddComponent<CanvasGroup>();
                cg.alpha = 0f;
                cg.interactable = true;  // ★ 명시적 설정
                cg.blocksRaycasts = true;  // ★ 명시적 설정
            }
        }

        LayoutRebuilder.ForceRebuildLayoutImmediate(wordCardContainer);

        for (int i = 0; i < spawnedCards.Count; i++)
        {
            var cg = spawnedCards[i]?.GetComponent<CanvasGroup>();
            if (cg == null) continue;
            cg.DOFade(1f, 0.2f).SetDelay(i * cardStagger);
        }
    }

    public void NotifyWordDropped(int slotIndex, string word)
    {
        if (currentDocument == null) return;
        if (slotIndex < 0 || slotIndex >= currentDocument.typewriterSlots.Count) return;

        currentDocument.typewriterSlots[slotIndex].insertedWord = word;
        OnWordDropped?.Invoke(slotIndex, word);
        RefreshCardColors();
    }

    private void RefreshCardColors()
    {
        foreach (var obj in spawnedCards)
        {
            if (obj == null) continue;
            var card = obj.GetComponent<WordCard>();
            var img = obj.GetComponent<Image>();
            if (card == null || img == null) continue;

            bool used = currentDocument.typewriterSlots[card.SlotIndex].insertedWord == card.Word;
            img.DOColor(used ? new Color(0.7f, 0.7f, 0.7f) : Color.white, 0.15f);
        }
    }

    private void ClearCards()
    {
        foreach (var obj in spawnedCards)
            if (obj != null) Destroy(obj);
        spawnedCards.Clear();
    }

    private void ShowPanel()
    {
        typewriterPanelCG.gameObject.SetActive(true);
        typewriterPanelCG.alpha = 0f;

        if (panelRT != null)
        {
            panelRT.anchoredPosition = new Vector2(panelRT.anchoredPosition.x, hiddenY);
            panelRT.DOAnchorPosY(visibleY, slideDuration).SetEase(Ease.OutCubic);
        }

        // ★ 핵심: 페이드 완료 후 blocksRaycasts 복구 → 카드 드래그 가능
        typewriterPanelCG.DOFade(1f, slideDuration).OnComplete(() =>
        {
            typewriterPanelCG.interactable = true;
            typewriterPanelCG.blocksRaycasts = true;
        });
    }

    private void HidePanel()
    {
        typewriterPanelCG.interactable = false;
        typewriterPanelCG.blocksRaycasts = false;

        if (panelRT != null)
            panelRT.DOAnchorPosY(hiddenY, slideDuration).SetEase(Ease.InCubic);

        typewriterPanelCG.DOFade(0f, slideDuration).OnComplete(() =>
        {
            ClearCards();
            typewriterPanelCG.gameObject.SetActive(false);
        });
    }

    private void HideImmediate()
    {
        if (typewriterPanelCG == null) return;
        typewriterPanelCG.alpha = 0f;
        typewriterPanelCG.interactable = false;
        typewriterPanelCG.blocksRaycasts = false;
        typewriterPanelCG.gameObject.SetActive(false);
        if (panelRT != null)
            panelRT.anchoredPosition = new Vector2(panelRT.anchoredPosition.x, hiddenY);
    }
}