using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using DG.Tweening;
using System.Collections.Generic;

public class WhiteboardManager : MonoBehaviour
{
    public static WhiteboardManager Instance { get; private set; }

    [Header("UI 연결")]
    [SerializeField] private CanvasGroup whiteboardPanelCG;
    [SerializeField] private RectTransform cardContainer;
    [SerializeField] private GameObject cardPrefab;

    [Header("실 연결")]
    [SerializeField] private GameObject stringPrefab;
    [SerializeField] private RectTransform stringContainer;

    [Header("프리뷰 실")]
    [SerializeField] private RectTransform previewLineRT;
    [SerializeField] private float previewLineWidth = 2f;
    [SerializeField] private Color previewLineColor = new Color(0.8f, 0.1f, 0.1f, 0.5f);

    [Header("커튼 (닫기 연출)")]
    [Tooltip("WhiteBoard2.png가 붙은 CanvasGroup — Panel_Whiteboard 최하단 자식")]
    [SerializeField] private CanvasGroup curtainCG;
    [SerializeField] private float curtainFadeDuration = 0.6f;

    [Header("해금 텍스트")]
    [SerializeField] private CanvasGroup revealPanelCG;
    [SerializeField] private TextMeshProUGUI revealText;

    [Header("닫기 버튼")]
    [SerializeField] private Button closeButton;

    [Header("레퍼런스")]
    [SerializeField] private GameManager gameManager;

    [Header("DOTween 설정")]
    [SerializeField] private float fadeDuration = 0.35f;
    [SerializeField] private float cardStagger = 0.07f;

    private List<ClueCardData> pendingCards = new List<ClueCardData>();
    private List<CorrectConnection> correctConnections = new List<CorrectConnection>();
    private List<ClueCard> spawnedCards = new List<ClueCard>();
    private List<RedStringRenderer> spawnedStrings = new List<RedStringRenderer>();

    private ClueCard dragSource = null;
    private bool isOpen = false;
    private bool isCurtainDown = false; // ★ 커튼 단계 추적

    // ── 초기화 ───────────────────────────────
    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        HideImmediate();
        if (closeButton != null) closeButton.onClick.AddListener(CloseBoard);

        if (previewLineRT != null)
        {
            var img = previewLineRT.GetComponent<Image>();
            if (img != null) img.color = previewLineColor;
            previewLineRT.gameObject.SetActive(false);
        }
    }

    // ── 데이터 추가 ──────────────────────────
    public void AddCard(ClueCardData cardData)
    {
        if (!pendingCards.Exists(c => c.cardID == cardData.cardID))
            pendingCards.Add(cardData);
    }

    public void AddCorrectConnection(string cardIDA, string cardIDB, string revealMessage)
    {
        correctConnections.Add(new CorrectConnection
        {
            cardIDA = cardIDA,
            cardIDB = cardIDB,
            revealMessage = revealMessage
        });
    }

    // ── JSON 로드 ────────────────────────────
    public void LoadFromJson()
    {
        pendingCards.Clear();
        correctConnections.Clear();

        var data = GameTextLoader.Instance?.GetWhiteboard();
        if (data == null) { Debug.LogWarning("[WhiteboardManager] JSON 화이트보드 데이터 없음"); return; }

        var validCards = data.cards.FindAll(c =>
            string.IsNullOrEmpty(c.requiredClueID) ||
            (GameFlags.Instance != null && GameFlags.Instance.HasClue(c.requiredClueID)));

        var positions = GeneratePositions(validCards.Count);
        for (int i = 0; i < validCards.Count; i++)
        {
            var c = validCards[i];
            AddCard(new ClueCardData
            {
                cardID = c.cardID,
                cardTitle = c.title,
                cardContent = c.content,
                boardPosition = positions[i]
            });
        }

        if (data.correctConnections != null)
            foreach (var conn in data.correctConnections)
                AddCorrectConnection(conn.fromCardID, conn.toCardID, conn.revealText);

        Debug.Log($"[WhiteboardManager] 카드 {validCards.Count}개 로드 완료");
    }

    // ── 열기 / 닫기 ──────────────────────────
    public void OpenBoard()
    {
        isOpen = true;
        isCurtainDown = false;
        ResetCurtain(); // ★ 보드 열 때 항상 커튼 초기화
        SpawnCards();
        ShowPanel();
    }

    /// <summary>
    /// 1클릭: 커튼 내리기 / 2클릭: 다음 페이즈로
    /// </summary>
    public void CloseBoard()
    {
        if (!isCurtainDown)
        {
            // ★ 1단계: 커튼 페이드인
            isCurtainDown = true;
            ShowCurtain();
        }
        else
        {
            // ★ 2단계: 다음 페이즈 전환
            isOpen = false;
            HidePanel();
        }
    }

    public bool IsOpen() => isOpen;

    // ── 카드 스폰 ────────────────────────────
    private void SpawnCards()
    {
        foreach (var c in spawnedCards) if (c != null) Destroy(c.gameObject);
        spawnedCards.Clear();

        Canvas.ForceUpdateCanvases();

        for (int i = 0; i < pendingCards.Count; i++)
        {
            ClueCardData data = pendingCards[i];
            GameObject obj = Instantiate(cardPrefab, cardContainer);
            ClueCard card = obj.GetComponent<ClueCard>();
            if (card == null) continue;

            card.Initialize(data, this);
            spawnedCards.Add(card);

            var rt = obj.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = data.boardPosition;

            obj.transform.localScale = Vector3.zero;
            obj.transform.DOScale(Vector3.one, 0.4f)
               .SetDelay(i * cardStagger)
               .SetEase(Ease.OutBack);
        }
    }

    private List<Vector2> GeneratePositions(int count)
    {
        var positions = new List<Vector2>();
        if (count == 0) return positions;

        Canvas.ForceUpdateCanvases();
        float w = cardContainer.rect.width;
        float h = cardContainer.rect.height;
        Debug.Log($"[Whiteboard] 컨테이너 크기: {w} x {h}"); // ★ 임시
        if (w <= 0) w = 1920f;
        if (h <= 0) h = 1080f;

        int cols = Mathf.CeilToInt(Mathf.Sqrt(count));
        int rows = Mathf.CeilToInt((float)count / cols);

        float spacingX = Mathf.Max(w / (cols + 1), 220f);
        float spacingY = Mathf.Max(h / (rows + 1), 170f);

        for (int i = 0; i < count; i++)
        {
            int col = i % cols;
            int row = i / cols;

            float x = -w * 0.5f + spacingX * (col + 1) + Random.Range(-20f, 20f);
            float y = h * 0.5f - spacingY * (row + 1) + Random.Range(-15f, 15f);
            positions.Add(new Vector2(x, y));
        }
        return positions;
    }

    // ── 드래그 연결 ──────────────────────────
    public void StartConnectionDrag(ClueCard source)
    {
        dragSource = source;
        if (previewLineRT != null) previewLineRT.gameObject.SetActive(true);
    }

    public void UpdateConnectionDrag(PointerEventData eventData)
    {
        if (dragSource == null || previewLineRT == null) return;
        UpdatePreviewLine(dragSource.GetWorldCenter(),
                          new Vector3(eventData.position.x, eventData.position.y, 0f));
    }

    public void EndConnectionDrag(ClueCard source, PointerEventData eventData)
    {
        if (previewLineRT != null) previewLineRT.gameObject.SetActive(false);
        if (dragSource == null) return;

        var results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(eventData, results);

        foreach (var result in results)
        {
            var targetCard = result.gameObject.GetComponentInParent<ClueCard>();
            if (targetCard != null && targetCard != source)
            {
                ConnectCards(source, targetCard);
                break;
            }
        }
        dragSource = null;
    }

    private void UpdatePreviewLine(Vector3 from, Vector3 to)
    {
        previewLineRT.position = (from + to) * 0.5f;
        float dist = Vector3.Distance(from, to);
        previewLineRT.sizeDelta = new Vector2(dist, previewLineWidth);
        Vector3 dir = to - from;
        previewLineRT.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);
    }

    // ── 실 연결 ──────────────────────────────
    private void ConnectCards(ClueCard cardA, ClueCard cardB)
    {
        RedStringRenderer existing = spawnedStrings.Find(s =>
            (s.CardA == cardA && s.CardB == cardB) ||
            (s.CardA == cardB && s.CardB == cardA));

        if (existing != null)
        {
            spawnedStrings.Remove(existing);
            Destroy(existing.gameObject);
            return;
        }

        var obj = Instantiate(stringPrefab, stringContainer);
        var rsr = obj.GetComponent<RedStringRenderer>();
        rsr.Setup(cardA, cardB);
        spawnedStrings.Add(rsr);

        CheckCorrectConnection(cardA.CardID, cardB.CardID);
    }

    private void CheckCorrectConnection(string idA, string idB)
    {
        foreach (var conn in correctConnections)
        {
            bool match = (conn.cardIDA == idA && conn.cardIDB == idB) ||
                         (conn.cardIDA == idB && conn.cardIDB == idA);
            if (match && !conn.isRevealed)
            {
                conn.isRevealed = true;
                RevealTruth(conn.revealMessage);
                break;
            }
        }
    }

    private void RevealTruth(string message)
    {
        if (revealPanelCG == null || revealText == null) return;
        revealText.text = message;
        revealPanelCG.gameObject.SetActive(true);
        revealPanelCG.alpha = 0f;
        revealPanelCG.DOFade(1f, 0.5f);

        if (spawnedStrings.Count > 0)
            spawnedStrings[spawnedStrings.Count - 1].PlayCorrectAnimation();
    }

    // ── 커튼 연출 ────────────────────────────
    private void ShowCurtain()
    {
        if (curtainCG == null) return;
        curtainCG.DOKill();
        curtainCG.alpha = 0f;
        curtainCG.DOFade(1f, curtainFadeDuration).SetEase(Ease.OutQuad);
    }

    // ── 패널 표시 ────────────────────────────
    private void ShowPanel()
    {
        whiteboardPanelCG.gameObject.SetActive(true);
        whiteboardPanelCG.alpha = 0f;
        whiteboardPanelCG.interactable = false;
        whiteboardPanelCG.blocksRaycasts = false;
        whiteboardPanelCG.DOFade(1f, fadeDuration).OnComplete(() =>
        {
            whiteboardPanelCG.interactable = true;
            whiteboardPanelCG.blocksRaycasts = true;
        });
    }

    private void HidePanel()
    {
        whiteboardPanelCG.interactable = false;
        whiteboardPanelCG.blocksRaycasts = false;
        whiteboardPanelCG.DOFade(0f, fadeDuration).OnComplete(() =>
        {
            whiteboardPanelCG.gameObject.SetActive(false);
            ResetBoard();
            gameManager?.GoToNextPhase();
        });
    }

    private void ResetCurtain()
    {
        if (curtainCG == null) return;
        curtainCG.DOKill();
        curtainCG.alpha = 0f;
        curtainCG.blocksRaycasts = false;
        curtainCG.interactable = false;
    }

    private void HideImmediate()
    {
        if (whiteboardPanelCG != null)
        {
            whiteboardPanelCG.alpha = 0f;
            whiteboardPanelCG.interactable = false;
            whiteboardPanelCG.blocksRaycasts = false;
            whiteboardPanelCG.gameObject.SetActive(false);
        }
        ResetCurtain(); // ★ SetActive 대신 alpha 초기화
    }

    private void ResetBoard()
    {
        foreach (var c in spawnedCards) if (c != null) Destroy(c.gameObject);
        foreach (var s in spawnedStrings) if (s != null) Destroy(s.gameObject);
        spawnedCards.Clear();
        spawnedStrings.Clear();
        isCurtainDown = false;
        ResetCurtain(); // ★
    }
}

[System.Serializable]
public class ClueCardData
{
    public string cardID;
    public string cardTitle;
    [TextArea(2, 3)]
    public string cardContent;
    public Vector2 boardPosition;
}

[System.Serializable]
public class CorrectConnection
{
    public string cardIDA;
    public string cardIDB;
    public string revealMessage;
    [HideInInspector] public bool isRevealed = false;
}