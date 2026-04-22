using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;
using System.Collections.Generic;
using Obvious.Soap;

public class WhiteboardManager : MonoBehaviour
{
    public static WhiteboardManager Instance { get; private set; }

    [Header("UI 연결")]
    [SerializeField] private CanvasGroup   whiteboardPanelCG;
    [SerializeField] private RectTransform cardContainer;
    [SerializeField] private GameObject    cardPrefab;

    [Header("실 연결")]
    [SerializeField] private GameObject    stringPrefab;
    [SerializeField] private RectTransform stringContainer;

    [Header("해금 텍스트")]
    [SerializeField] private CanvasGroup     revealPanelCG;
    [SerializeField] private TextMeshProUGUI revealText;

    [Header("닫기 버튼")]
    [SerializeField] private Button closeButton;

    [Header("SOAP 연결")]
    [SerializeField] private GameManager gameManager;

    [Header("DOTween 설정")]
    [SerializeField] private float fadeDuration = 0.35f;
    [SerializeField] private float cardStagger  = 0.07f;

    private List<ClueCardData>        pendingCards       = new List<ClueCardData>();
    private List<CorrectConnection>   correctConnections = new List<CorrectConnection>();
    private List<ClueCard>            spawnedCards       = new List<ClueCard>();
    private List<RedStringRenderer>   spawnedStrings     = new List<RedStringRenderer>();
    private ClueCard                  selectedCard       = null;
    private bool                      isOpen             = false;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        HideImmediate();
        if (closeButton != null) closeButton.onClick.AddListener(CloseBoard);
    }

    // ─────────────────────────────────────────
    public void AddCard(ClueCardData cardData)
    {
        if (!pendingCards.Exists(c => c.cardID == cardData.cardID))
            pendingCards.Add(cardData);
    }

    public void AddCorrectConnection(string cardIDA, string cardIDB, string revealMessage)
    {
        correctConnections.Add(new CorrectConnection
        {
            cardIDA = cardIDA, cardIDB = cardIDB, revealMessage = revealMessage
        });
    }

    public void OpenBoard()
    {
        isOpen = true;
        SpawnCards();
        ShowPanel();
    }

    public void CloseBoard()
    {
        isOpen = false;
        HidePanel();
    }

    public bool IsOpen() => isOpen;

    // ─────────────────────────────────────────
    private void SpawnCards()
    {
        foreach (var c in spawnedCards) if (c != null) Destroy(c.gameObject);
        spawnedCards.Clear();

        for (int i = 0; i < pendingCards.Count; i++)
        {
            ClueCardData data = pendingCards[i];
            GameObject   obj  = Instantiate(cardPrefab, cardContainer);
            ClueCard     card = obj.GetComponent<ClueCard>();
            if (card == null) continue;

            card.Initialize(data, this);
            spawnedCards.Add(card);

            var rt = obj.GetComponent<RectTransform>();
            rt.anchoredPosition = data.boardPosition;

            obj.transform.localScale = Vector3.zero;
            obj.transform.DOScale(Vector3.one, 0.4f)
               .SetDelay(i * cardStagger)
               .SetEase(Ease.OutBack);
        }
    }

    // ─────────────────────────────────────────
    public void OnCardClicked(ClueCard card)
    {
        if (selectedCard == null)
        {
            selectedCard = card;
            card.SetHighlight(true);
            Debug.Log($"🔴 실 연결 시작: {card.CardID}");
        }
        else
        {
            if (selectedCard == card)
            {
                selectedCard.SetHighlight(false);
                selectedCard = null;
                return;
            }
            ConnectCards(selectedCard, card);
            selectedCard.SetHighlight(false);
            selectedCard = null;
        }
    }

    private void ConnectCards(ClueCard cardA, ClueCard cardB)
    {
        // 이미 연결된 쌍이면 해제
        RedStringRenderer existing = spawnedStrings.Find(s =>
            (s.CardA == cardA && s.CardB == cardB) ||
            (s.CardA == cardB && s.CardB == cardA));

        if (existing != null)
        {
            spawnedStrings.Remove(existing);
            Destroy(existing.gameObject);
            Debug.Log($"실 연결 해제: {cardA.CardID} ↔ {cardB.CardID}");
            return;
        }

        GameObject        obj = Instantiate(stringPrefab, stringContainer);
        RedStringRenderer rsr = obj.GetComponent<RedStringRenderer>();
        rsr.Setup(cardA, cardB);
        spawnedStrings.Add(rsr);
        Debug.Log($"🔴 실 연결: {cardA.CardID} ↔ {cardB.CardID}");

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
        Debug.Log($"💡 진실 해금: {message}");
        if (revealPanelCG == null || revealText == null) return;

        revealText.text = message;
        revealPanelCG.gameObject.SetActive(true);
        revealPanelCG.alpha = 0f;
        revealPanelCG.DOFade(1f, 0.5f);

        // ★ Bug Fix: [^1] 대신 Count-1 사용
        if (spawnedStrings.Count > 0)
            spawnedStrings[spawnedStrings.Count - 1].PlayCorrectAnimation();
    }

    // ─────────────────────────────────────────
    private void ShowPanel()
    {
        whiteboardPanelCG.gameObject.SetActive(true);
        whiteboardPanelCG.alpha          = 0f;
        whiteboardPanelCG.interactable   = false;
        whiteboardPanelCG.blocksRaycasts = false;
        whiteboardPanelCG.DOFade(1f, fadeDuration).OnComplete(() =>
        {
            whiteboardPanelCG.interactable   = true;
            whiteboardPanelCG.blocksRaycasts = true;
        });
    }

    private void HidePanel()
    {
        whiteboardPanelCG.interactable   = false;
        whiteboardPanelCG.blocksRaycasts = false;
        whiteboardPanelCG.DOFade(0f, fadeDuration).OnComplete(() =>
        {
            whiteboardPanelCG.gameObject.SetActive(false);
            gameManager?.GoToNextPhase();
        });
    }

    private void HideImmediate()
    {
        if (whiteboardPanelCG == null) return;
        whiteboardPanelCG.alpha          = 0f;
        whiteboardPanelCG.interactable   = false;
        whiteboardPanelCG.blocksRaycasts = false;
        whiteboardPanelCG.gameObject.SetActive(false);
    }
}

[System.Serializable]
public class ClueCardData
{
    public string  cardID;
    public string  cardTitle;
    [TextArea(2, 3)]
    public string  cardContent;
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
