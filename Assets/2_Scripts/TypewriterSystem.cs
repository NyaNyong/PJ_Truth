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
    [SerializeField] private TextMeshProUGUI selectedWordPreviewText;
    [SerializeField] private Button insertButton;

    [Header("DOTween 설정")]
    [SerializeField] private float fadeDuration = 0.3f;
    [SerializeField] private float cardStagger  = 0.06f;

    // ★ IsOpen 프로퍼티 — DocumentViewer가 타자기 열림 여부 체크
    public bool IsOpen { get; private set; } = false;

    private DocumentData     currentDocument;
    private int              activeSlotIndex = -1;
    private string           selectedWord    = "";
    private List<GameObject> spawnedCards    = new List<GameObject>();

    public System.Action<int, string> OnWordInserted;

    private void Awake()
    {
        HideImmediate();
        if (insertButton != null)
            insertButton.onClick.AddListener(OnClickInsert);
    }

    public void Initialize(DocumentData doc)
    {
        currentDocument = doc;
    }

    public void OpenForSlot(int slotIndex)
    {
        if (currentDocument == null) return;
        if (slotIndex < 0 || slotIndex >= currentDocument.typewriterSlots.Count) return;

        activeSlotIndex = slotIndex;
        selectedWord    = currentDocument.typewriterSlots[slotIndex].insertedWord ?? "";

        RefreshPreviewText();
        SpawnWordCards(currentDocument.typewriterSlots[slotIndex].wordOptions);
        ShowPanel();
    }

    public void Close()
    {
        HidePanel();
        activeSlotIndex = -1;
        selectedWord    = "";
    }

    private void SpawnWordCards(List<string> words)
    {
        ClearCards();

        for (int i = 0; i < words.Count; i++)
        {
            string     word = words[i];
            GameObject obj  = Instantiate(wordCardPrefab, wordCardContainer);
            spawnedCards.Add(obj);

            var label = obj.GetComponentInChildren<TextMeshProUGUI>();
            if (label != null) label.text = word;

            HighlightCard(obj, word == selectedWord);

            var btn = obj.GetComponent<Button>();
            if (btn != null) btn.onClick.AddListener(() => OnCardClicked(word, obj));

            var cg2 = obj.GetComponent<CanvasGroup>();
            if (cg2 == null) cg2 = obj.AddComponent<CanvasGroup>();
            cg2.alpha = 0f;
            DOTween.Sequence().SetDelay(i * cardStagger).Append(cg2.DOFade(1f, 0.25f));
        }
    }

    private void OnCardClicked(string word, GameObject cardObj)
    {
        selectedWord = word;
        RefreshPreviewText();
        foreach (var card in spawnedCards) HighlightCard(card, false);
        HighlightCard(cardObj, true);
        Debug.Log($"⌨️ 단어 선택: {word}");
    }

    private void OnClickInsert()
    {
        if (string.IsNullOrEmpty(selectedWord))
        {
            Debug.LogWarning("[TypewriterSystem] 삽입할 단어가 선택되지 않았습니다.");
            return;
        }
        if (activeSlotIndex < 0 || activeSlotIndex >= currentDocument.typewriterSlots.Count) return;

        currentDocument.typewriterSlots[activeSlotIndex].insertedWord = selectedWord;
        Debug.Log($"⌨️ 슬롯 {activeSlotIndex}에 '{selectedWord}' 삽입");
        OnWordInserted?.Invoke(activeSlotIndex, selectedWord);
        Close();
    }

    private void RefreshPreviewText()
    {
        if (selectedWordPreviewText == null) return;
        selectedWordPreviewText.text = string.IsNullOrEmpty(selectedWord)
            ? "단어를 선택하세요"
            : $"선택: {selectedWord}";
    }

    private void HighlightCard(GameObject card, bool highlight)
    {
        if (card == null) return;
        var img = card.GetComponent<Image>();
        if (img == null) return;
        img.color = highlight ? new Color(0.9f, 0.85f, 0.5f) : Color.white;
    }

    private void ShowPanel()
    {
        IsOpen = true;
        typewriterPanelCG.gameObject.SetActive(true);
        typewriterPanelCG.alpha          = 0f;
        typewriterPanelCG.interactable   = false;
        typewriterPanelCG.blocksRaycasts = false;
        typewriterPanelCG.DOFade(1f, fadeDuration).OnComplete(() =>
        {
            typewriterPanelCG.interactable   = true;
            typewriterPanelCG.blocksRaycasts = true;
        });
    }

    private void HidePanel()
    {
        IsOpen = false;
        typewriterPanelCG.interactable   = false;
        typewriterPanelCG.blocksRaycasts = false;
        typewriterPanelCG.DOFade(0f, fadeDuration).OnComplete(() =>
        {
            typewriterPanelCG.gameObject.SetActive(false);
            ClearCards();
        });
    }

    private void HideImmediate()
    {
        IsOpen = false;
        if (typewriterPanelCG == null) return;
        typewriterPanelCG.alpha          = 0f;
        typewriterPanelCG.interactable   = false;
        typewriterPanelCG.blocksRaycasts = false;
        typewriterPanelCG.gameObject.SetActive(false);
    }

    private void ClearCards()
    {
        foreach (var c in spawnedCards) if (c != null) Destroy(c);
        spawnedCards.Clear();
    }
}
