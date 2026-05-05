using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;
using System.Collections.Generic;

/// <summary>
/// 타자기 시스템 (단순화 버전).
/// 타자기 버튼 클릭 → 하단에 단어 카드 팝업
/// 단어 카드를 문서의 [빈칸N] 위에 드래그&드랍 → 슬롯에 삽입
/// </summary>
public class TypewriterSystem : MonoBehaviour
{
    [Header("UI 연결")]
    [SerializeField] private CanvasGroup   typewriterPanelCG;
    [SerializeField] private RectTransform wordCardContainer;
    [SerializeField] private GameObject    wordCardPrefab;

    [Header("드래그 연출")]
    [SerializeField] private Canvas        rootCanvas; // 드래그 고스트가 올라갈 최상위 Canvas

    [Header("DOTween 설정")]
    [SerializeField] private float slideDuration = 0.25f;
    [SerializeField] private float cardStagger   = 0.05f;

    [Header("패널 위치")]
    [SerializeField] private float hiddenY  = -250f;
    [SerializeField] private float visibleY = 0f;

    // ─────────────────────────────────────────
    public bool IsOpen { get; private set; } = false;

    // DocumentViewer가 구독: (slotIndex, word)
    public System.Action<int, string> OnWordDropped;

    private DocumentData     currentDocument;
    private List<GameObject> spawnedCards = new List<GameObject>();
    private RectTransform    panelRT;

    // ─────────────────────────────────────────
    private void Awake()
    {
        panelRT = typewriterPanelCG?.GetComponent<RectTransform>();
        HideImmediate();
    }

    // ─────────────────────────────────────────
    public void Initialize(DocumentData doc)
    {
        currentDocument = doc;
    }

    /// <summary>타자기 버튼 클릭 → 패널 토글 (DocumentViewer가 호출)</summary>
    public void OpenPanel()
    {
        if (currentDocument == null) return;
        if (currentDocument.typewriterSlots == null ||
            currentDocument.typewriterSlots.Count == 0) return;

        IsOpen = true;
        SpawnAllWordCards();
        ShowPanel();
    }

    public void Close()
    {
        if (!IsOpen) return;
        IsOpen = false;
        HidePanel();
    }

    // ─────────────────────────────────────────
    // 모든 슬롯의 단어 카드를 한 번에 생성
    // ─────────────────────────────────────────
    private void SpawnAllWordCards()
    {
        ClearCards();

        // ── 1단계: 카드 전부 생성 (투명 상태) ──────────
        for (int si = 0; si < currentDocument.typewriterSlots.Count; si++)
        {
            var slot = currentDocument.typewriterSlots[si];
            if (slot.wordOptions == null) continue;

            foreach (string word in slot.wordOptions)
            {
                int    capturedSlot = si;
                string capturedWord = word;

                GameObject obj = Instantiate(wordCardPrefab, wordCardContainer);
                spawnedCards.Add(obj);

                var label = obj.GetComponentInChildren<TextMeshProUGUI>();
                if (label != null) label.text = word;

                bool alreadyUsed = slot.insertedWord == word;
                var  img         = obj.GetComponent<Image>();
                if (img != null)
                    img.color = alreadyUsed ? new Color(0.7f, 0.7f, 0.7f) : Color.white;

                var card = obj.GetComponent<WordCard>();
                if (card == null) card = obj.AddComponent<WordCard>();
                card.Setup(capturedSlot, capturedWord, this, rootCanvas);

                // 처음엔 투명 + 약간 아래에 배치
                var cg = obj.GetComponent<CanvasGroup>();
                if (cg == null) cg = obj.AddComponent<CanvasGroup>();
                cg.alpha = 0f;
            }
        }

        // ── 2단계: Layout Group 강제 계산 후 비활성화 ──
        UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(wordCardContainer);

       

        // ── 3단계: 계산된 위치에서 애니메이션 ───────────
        for (int i = 0; i < spawnedCards.Count; i++)
        {
            var cg = spawnedCards[i]?.GetComponent<CanvasGroup>();
            if (cg == null) continue;

            cg.DOFade(1f, 0.2f).SetDelay(i * cardStagger);
        }
    }

    /// <summary>WordCard가 드랍 완료 후 호출</summary>
    public void NotifyWordDropped(int slotIndex, string word)
    {
        if (currentDocument == null) return;
        if (slotIndex < 0 || slotIndex >= currentDocument.typewriterSlots.Count) return;

        currentDocument.typewriterSlots[slotIndex].insertedWord = word;
        Debug.Log($"[TypewriterSystem] 슬롯 {slotIndex} ← '{word}'");

        OnWordDropped?.Invoke(slotIndex, word);

        // 카드 색상 갱신 (사용된 카드 회색 처리)
        RefreshCardColors();
    }

    private void RefreshCardColors()
    {
        foreach (var obj in spawnedCards)
        {
            if (obj == null) continue;
            var card = obj.GetComponent<WordCard>();
            var img  = obj.GetComponent<Image>();
            if (card == null || img == null) continue;

            var slot = currentDocument.typewriterSlots[card.SlotIndex];
            bool used = slot.insertedWord == card.Word;
            img.DOColor(used ? new Color(0.7f, 0.7f, 0.7f) : Color.white, 0.2f);
        }
    }

    // ─────────────────────────────────────────
    // 패널 슬라이드
    // ─────────────────────────────────────────
    private void ShowPanel()
    {
        if (panelRT != null)
            panelRT.anchoredPosition = new Vector2(
                panelRT.anchoredPosition.x, hiddenY);

        typewriterPanelCG.gameObject.SetActive(true);
        typewriterPanelCG.alpha          = 0f;
        typewriterPanelCG.interactable   = false;
        typewriterPanelCG.blocksRaycasts = false;

        var seq = DOTween.Sequence();
        if (panelRT != null)
            seq.Join(panelRT.DOAnchorPosY(visibleY, slideDuration).SetEase(Ease.OutCubic));
        seq.Join(typewriterPanelCG.DOFade(1f, slideDuration));
        seq.OnComplete(() =>
        {
            typewriterPanelCG.interactable   = true;
            typewriterPanelCG.blocksRaycasts = true;
        });
    }

    private void HidePanel()
    {
        typewriterPanelCG.interactable   = false;
        typewriterPanelCG.blocksRaycasts = false;

        var seq = DOTween.Sequence();
        if (panelRT != null)
            seq.Join(panelRT.DOAnchorPosY(hiddenY, slideDuration * 0.8f).SetEase(Ease.InCubic));
        seq.Join(typewriterPanelCG.DOFade(0f, slideDuration * 0.6f));
        seq.OnComplete(() =>
        {
            typewriterPanelCG.gameObject.SetActive(false);
            ClearCards();
        });
    }

    private void HideImmediate()
    {
        if (typewriterPanelCG == null) return;
        IsOpen = false;
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
