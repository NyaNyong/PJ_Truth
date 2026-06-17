using DG.Tweening;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

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
    [SerializeField] private CanvasGroup curtainCG;
    [SerializeField] private float curtainFadeDuration = 0.6f;
    [Tooltip("커튼 완전히 내려온 후 자동 닫기 대기(초)")]
    [SerializeField] private float autoCloseDelay = 2f;

    [Header("해금 즉시 팝업")]
    [SerializeField] private CanvasGroup revealPanelCG;
    [SerializeField] private TextMeshProUGUI revealText;

    [Header("카드 상세 보기 팝업")]
    [SerializeField] private CanvasGroup detailPanelCG;
    [SerializeField] private TextMeshProUGUI detailTitleText;
    [SerializeField] private TextMeshProUGUI detailContentText;
    [SerializeField] private Button detailCloseButton;

    [Header("진실 수집 노트")]
    [SerializeField] private CanvasGroup truthNotePanelCG;
    [SerializeField] private RectTransform truthNoteContainer;
    [SerializeField] private GameObject truthEntryPrefab;
    [SerializeField] private Button truthNoteButton;
    [SerializeField] private GameObject truthBadge;

    [Header("닫기 버튼")]
    [SerializeField] private Button closeButton;

    [Header("레퍼런스")]
    [SerializeField] private GameManager gameManager;

    [Header("DOTween 설정")]
    [SerializeField] private float fadeDuration = 0.35f;
    [SerializeField] private float cardStagger  = 0.07f;

    private List<ClueCardData>      pendingCards       = new List<ClueCardData>();
    private List<CorrectConnection> correctConnections = new List<CorrectConnection>();
    private List<ClueCard>          spawnedCards       = new List<ClueCard>();
    private List<RedStringRenderer> spawnedStrings     = new List<RedStringRenderer>();

    private ClueCard dragSource  = null;
    private bool     isOpen      = false;
    private bool     isCurtainDown  = false;
    private bool     isTruthNoteOpen = false;

    // ── 초기화 ───────────────────────────────
    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        HideImmediate();

        if (closeButton       != null) closeButton.onClick.AddListener(CloseBoard);
        if (truthNoteButton   != null) truthNoteButton.onClick.AddListener(ToggleTruthNote);
        if (detailCloseButton != null) detailCloseButton.onClick.AddListener(HideCardDetail);

        if (previewLineRT != null)
        {
            var img = previewLineRT.GetComponent<Image>();
            if (img != null) img.color = previewLineColor;
            previewLineRT.gameObject.SetActive(false);
        }
        SetCGHidden(truthNotePanelCG);
        SetCGHidden(detailPanelCG);
    }

    // ── 데이터 추가 ──────────────────────────
    public void AddCard(ClueCardData cardData)
    {
        if (!pendingCards.Exists(c => c.cardID == cardData.cardID))
            pendingCards.Add(cardData);
    }

    public void AddCorrectConnection(List<string> cardIDs, string revealMessage)
    {
        correctConnections.Add(new CorrectConnection
        {
            cardIDs = new List<string>(cardIDs),
            revealMessage = revealMessage
        });
    }

    // ── JSON 로드 ────────────────────────────
    public void LoadFromJson()
    {
        pendingCards.Clear();
        correctConnections.Clear();

        var data = GameTextLoader.Instance?.GetWhiteboard();
        if (data == null)
        {
            Debug.LogWarning("[WhiteboardManager] JSON 화이트보드 데이터 없음");
            return;
        }

        var validCards = data.cards.FindAll(c =>
            string.IsNullOrEmpty(c.requiredClueID) ||
            (GameFlags.Instance != null && GameFlags.Instance.HasClue(c.requiredClueID)));

        foreach (var c in validCards)
        {
            AddCard(new ClueCardData
            {
                cardID      = c.cardID,
                cardTitle   = c.title,
                cardContent = c.content,
                boardPosition = Vector2.zero
            });
        }

        if (data.correctConnections != null)
        {
            foreach (var conn in data.correctConnections)
            {
                var ids = (conn.cardIDs != null && conn.cardIDs.Count >= 2)
                    ? conn.cardIDs
                    : new List<string> { conn.fromCardID, conn.toCardID };
                AddCorrectConnection(ids, conn.revealText);
            }
        }

        Debug.Log($"[WhiteboardManager] 카드 {validCards.Count}개, 연결 {correctConnections.Count}개 로드");
    }

    // ── 열기 / 닫기 ──────────────────────────
    public void OpenBoard()
    {
        isOpen = true;
        isCurtainDown = false;
        ResetCurtain();
        ShowPanel();
        StartCoroutine(SpawnCardsNextFrame());
    }

    public void CloseBoard()
    {
        if (!isCurtainDown)
        {
            ConfirmPopupUI.Instance?.Open(
                "진실 조사를 종료하시겠습니까?",
                "",
                "⚠ 종료 후에는 다시 되돌릴 수 없습니다",
                onConfirm: () => { isCurtainDown = true; ShowCurtain(); },
                confirmText: "예", cancelText: "아니요"
            );
        }
        else
        {
            if (!isOpen) return;
            isOpen = false;
            HidePanel();
        }
    }

    public bool IsOpen() => isOpen;

    // ── 카드 상세 보기 ───────────────────────
    public void ShowCardDetail(ClueCardData data)
    {
        if (detailPanelCG == null) return;

        if (detailTitleText   != null) detailTitleText.text   = data.cardTitle;
        if (detailContentText != null) detailContentText.text = data.cardContent;

        detailPanelCG.gameObject.SetActive(true);
        detailPanelCG.alpha          = 0f;
        detailPanelCG.interactable   = false;
        detailPanelCG.blocksRaycasts = false;
        detailPanelCG.DOFade(1f, fadeDuration).OnComplete(() =>
        {
            detailPanelCG.interactable   = true;
            detailPanelCG.blocksRaycasts = true;
        });
    }

    private void HideCardDetail()
    {
        if (detailPanelCG == null) return;
        detailPanelCG.interactable   = false;
        detailPanelCG.blocksRaycasts = false;
        detailPanelCG.DOFade(0f, fadeDuration)
            .OnComplete(() => detailPanelCG.gameObject.SetActive(false));
    }

    // ── 드래그 제스처 (ClueCard → 호출) ──────
    public void StartConnectionDrag(ClueCard source)
    {
        dragSource = source;
        if (previewLineRT != null)
            previewLineRT.gameObject.SetActive(true);
    }

    public void UpdateConnectionDrag(PointerEventData eventData)
    {
        if (dragSource == null || previewLineRT == null) return;
        UpdatePreviewLine(dragSource.GetWorldCenter(), eventData.position);
    }

    public void EndConnectionDrag(ClueCard source, PointerEventData eventData)
    {
        if (previewLineRT != null)
            previewLineRT.gameObject.SetActive(false);

        if (dragSource == null) { dragSource = null; return; }

        var results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(eventData, results);

        foreach (var r in results)
        {
            var target = r.gameObject.GetComponent<ClueCard>();
            if (target != null && target != dragSource)
            {
                ConnectCards(dragSource, target);
                break;
            }
        }

        dragSource = null;
    }

    // ── 프리뷰 라인 ──────────────────────────
    private void UpdatePreviewLine(Vector3 from, Vector3 to)
    {
        previewLineRT.position = (from + to) * 0.5f;

        float scale = GetComponentInParent<Canvas>()?.scaleFactor ?? 1f;
        float dist  = Vector3.Distance(from, to) / scale;
        previewLineRT.sizeDelta = new Vector2(dist, previewLineWidth);

        Vector3 dir = to - from;
        previewLineRT.rotation = Quaternion.Euler(0f, 0f,
            Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);
    }

    // ── 실 연결 ──────────────────────────────
    private void ConnectCards(ClueCard cardA, ClueCard cardB)
    {
        var existing = spawnedStrings.Find(s =>
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

        CheckAllCorrectConnections();
    }

    // ── 정답 확인 (Union-Find) ────────────────
    private void CheckAllCorrectConnections()
    {
        var parent = new Dictionary<string, string>();
        foreach (var card in spawnedCards)
            parent[card.CardID] = card.CardID;

        foreach (var s in spawnedStrings)
            Union(parent, s.CardA.CardID, s.CardB.CardID);

        foreach (var conn in correctConnections)
        {
            if (conn.isRevealed) continue;
            if (conn.cardIDs == null || conn.cardIDs.Count < 2) continue;

            string root      = Find(parent, conn.cardIDs[0]);
            bool   allLinked = true;
            for (int i = 1; i < conn.cardIDs.Count; i++)
            {
                if (!parent.ContainsKey(conn.cardIDs[i]) ||
                    Find(parent, conn.cardIDs[i]) != root)
                { allLinked = false; break; }
            }

            if (allLinked) { conn.isRevealed = true; RevealTruth(conn.revealMessage); }
        }
    }

    private string Find(Dictionary<string, string> parent, string x)
    {
        if (!parent.ContainsKey(x)) return x;
        while (parent[x] != x) x = parent[x] = parent[parent[x]];
        return x;
    }

    private void Union(Dictionary<string, string> parent, string a, string b)
    {
        a = Find(parent, a); b = Find(parent, b);
        if (a != b) parent[a] = b;
    }

    // ── 진실 해금 ────────────────────────────
    private void RevealTruth(string message)
    {
        GameFlags.Instance?.AddTruth(message);
        if (truthBadge != null) truthBadge.SetActive(true);

        if (revealPanelCG != null && revealText != null)
        {
            revealText.text = message;
            revealPanelCG.gameObject.SetActive(true);
            revealPanelCG.alpha = 0f;
            revealPanelCG.DOFade(1f, 0.5f);
        }

        if (spawnedStrings.Count > 0)
            spawnedStrings[spawnedStrings.Count - 1].PlayCorrectAnimation();
    }

    // ── 진실 노트 ────────────────────────────
    private void ToggleTruthNote()
    {
        if (isTruthNoteOpen) HideTruthNote();
        else ShowTruthNote();
    }

    private void ShowTruthNote()
    {
        if (truthNotePanelCG == null || truthNoteContainer == null) return;
        isTruthNoteOpen = true;
        if (truthBadge != null) truthBadge.SetActive(false);

        foreach (Transform child in truthNoteContainer)
            Destroy(child.gameObject);

        var truths = GameFlags.Instance?.GetAllTruths();
        var entries = (truths != null && truths.Count > 0)
            ? truths : new List<string> { "아직 수집된 진실이 없습니다." };

        foreach (var t in entries)
        {
            var entry = Instantiate(truthEntryPrefab, truthNoteContainer);
            var tmp   = entry.GetComponentInChildren<TextMeshProUGUI>();
            if (tmp != null) tmp.text = t;
        }

        truthNotePanelCG.gameObject.SetActive(true);
        truthNotePanelCG.alpha          = 0f;
        truthNotePanelCG.interactable   = false;
        truthNotePanelCG.blocksRaycasts = false;
        truthNotePanelCG.DOFade(1f, fadeDuration).OnComplete(() =>
        {
            truthNotePanelCG.interactable   = true;
            truthNotePanelCG.blocksRaycasts = true;
        });
    }

    private void HideTruthNote()
    {
        if (truthNotePanelCG == null) return;
        isTruthNoteOpen = false;
        truthNotePanelCG.interactable   = false;
        truthNotePanelCG.blocksRaycasts = false;
        truthNotePanelCG.DOFade(0f, fadeDuration)
            .OnComplete(() => truthNotePanelCG.gameObject.SetActive(false));
    }

    // ── 커튼 + 자동 닫기 ─────────────────────
    private void ShowCurtain()
    {
        if (curtainCG == null) return;
        curtainCG.DOKill();
        curtainCG.alpha = 0f;
        curtainCG.DOFade(1f, curtainFadeDuration).SetEase(Ease.OutQuad)
            .OnComplete(() =>
            {
                DOVirtual.DelayedCall(autoCloseDelay, () =>
                {
                    if (!isOpen) return;
                    isOpen = false;
                    HidePanel();
                });
            });
    }

    // ── 패널 표시 / 숨김 ─────────────────────
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
        // ★ 화이트보드 연결 수 → 증거 신뢰도 플래그
        if (GameFlags.Instance != null)
        {
            int revealed = correctConnections.Count(c => c.isRevealed);
            GameFlags.Instance.RemoveFlag("evidence_low");
            GameFlags.Instance.RemoveFlag("evidence_mid");
            GameFlags.Instance.RemoveFlag("evidence_high");

            if (revealed >= 8) GameFlags.Instance.SetFlag("evidence_high");
            else if (revealed >= 5) GameFlags.Instance.SetFlag("evidence_mid");
            else GameFlags.Instance.SetFlag("evidence_low");

            Debug.Log($"[Whiteboard] 연결 완료 {revealed}개 → " +
                      (revealed >= 8 ? "evidence_high" : revealed >= 5 ? "evidence_mid" : "evidence_low"));
        }

        whiteboardPanelCG.interactable = false;
        // ... 기존 코드 유지 ...
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
        curtainCG.alpha          = 0f;
        curtainCG.blocksRaycasts = false;
        curtainCG.interactable   = false;
    }

    private void SetCGHidden(CanvasGroup cg)
    {
        if (cg == null) return;
        cg.alpha          = 0f;
        cg.interactable   = false;
        cg.blocksRaycasts = false;
        cg.gameObject.SetActive(false);
    }

    private void HideImmediate()
    {
        SetCGHidden(whiteboardPanelCG);
        SetCGHidden(detailPanelCG);
        ResetCurtain();
    }

    private void ResetBoard()
    {
        foreach (var c in spawnedCards)   if (c != null) Destroy(c.gameObject);
        foreach (var s in spawnedStrings) if (s != null) Destroy(s.gameObject);
        spawnedCards.Clear();
        spawnedStrings.Clear();
        dragSource      = null;
        isCurtainDown   = false;
        isTruthNoteOpen = false;
        ResetCurtain();
        SetCGHidden(truthNotePanelCG);
        SetCGHidden(detailPanelCG);
        if (previewLineRT != null) previewLineRT.gameObject.SetActive(false);
    }

    // ── 카드 스폰 ────────────────────────────
    private IEnumerator SpawnCardsNextFrame()
    {
        yield return null;
        SpawnCards();
    }

    private void SpawnCards()
    {
        foreach (var c in spawnedCards) if (c != null) Destroy(c.gameObject);
        spawnedCards.Clear();
        Canvas.ForceUpdateCanvases();

        var positions = GeneratePositions(pendingCards.Count);

        for (int i = 0; i < pendingCards.Count; i++)
        {
            var data = pendingCards[i];
            var obj  = Instantiate(cardPrefab, cardContainer);
            var card = obj.GetComponent<ClueCard>();
            if (card == null) continue;

            card.Initialize(data, this);
            spawnedCards.Add(card);

            var rt = obj.GetComponent<RectTransform>();
            rt.anchorMin        = new Vector2(0.5f, 0.5f);
            rt.anchorMax        = new Vector2(0.5f, 0.5f);
            rt.pivot            = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = positions[i];

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

        float w = cardContainer.rect.width;
        float h = cardContainer.rect.height;
        if (w <= 10f) w = 1600f;
        if (h <= 10f) h = 900f;

        float padX   = w * 0.08f;
        float padY   = h * 0.08f;
        float usableW = w - padX * 2f;
        float usableH = h - padY * 2f;

        int cols  = Mathf.CeilToInt(Mathf.Sqrt(count));
        int rows  = Mathf.CeilToInt((float)count / cols);
        float cellW = Mathf.Max(usableW / cols, 280f);
        float cellH = Mathf.Max(usableH / rows, 220f);
        float totalW = cellW * cols;
        float totalH = cellH * rows;

        for (int i = 0; i < count; i++)
        {
            int col = i % cols;
            int row = i / cols;
            float x = -totalW * 0.5f + cellW * (col + 0.5f) + Random.Range(-cellW * 0.1f, cellW * 0.1f);
            float y =  totalH * 0.5f - cellH * (row + 0.5f) + Random.Range(-cellH * 0.1f, cellH * 0.1f);
            positions.Add(new Vector2(x, y));
        }
        return positions;
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
    public List<string> cardIDs;
    public string revealMessage;
    [HideInInspector] public bool isRevealed = false;
}
