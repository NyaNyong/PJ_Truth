using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using DG.Tweening;
using System.Collections;
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
    [SerializeField] private CanvasGroup curtainCG;
    [SerializeField] private float curtainFadeDuration = 0.6f;

    [Header("해금 즉시 팝업")]
    [SerializeField] private CanvasGroup revealPanelCG;
    [SerializeField] private TextMeshProUGUI revealText;

    [Header("진실 수집 노트")]
    [Tooltip("진실 목록을 보여주는 스크롤 패널 CanvasGroup")]
    [SerializeField] private CanvasGroup truthNotePanelCG;
    [Tooltip("ScrollView의 Content RectTransform")]
    [SerializeField] private RectTransform truthNoteContainer;
    [Tooltip("진실 한 항목 프리팹 (TextMeshProUGUI 포함)")]
    [SerializeField] private GameObject truthEntryPrefab;
    [Tooltip("진실 노트 열기/닫기 버튼")]
    [SerializeField] private Button truthNoteButton;
    [Tooltip("새 진실 수집 시 표시할 뱃지 오브젝트 (! 이미지 등)")]
    [SerializeField] private GameObject truthBadge;   // ★ 추가

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
    private bool isCurtainDown = false;
    private bool isTruthNoteOpen = false;

    // ── 초기화 ───────────────────────────────
    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        HideImmediate();

        if (closeButton != null) closeButton.onClick.AddListener(CloseBoard);
        if (truthNoteButton != null) truthNoteButton.onClick.AddListener(ToggleTruthNote);

        if (previewLineRT != null)
        {
            var img = previewLineRT.GetComponent<Image>();
            if (img != null) img.color = previewLineColor;
            previewLineRT.gameObject.SetActive(false);
        }
        SetCGHidden(truthNotePanelCG);
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
        if (data == null) { Debug.LogWarning("[WhiteboardManager] JSON 화이트보드 데이터 없음"); return; }

        var validCards = data.cards.FindAll(c =>
            string.IsNullOrEmpty(c.requiredClueID) ||
            (GameFlags.Instance != null && GameFlags.Instance.HasClue(c.requiredClueID)));

        for (int i = 0; i < validCards.Count; i++)
        {
            var c = validCards[i];
            AddCard(new ClueCardData
            {
                cardID = c.cardID,
                cardTitle = c.title,
                cardContent = c.content,
                boardPosition = Vector2.zero  // SpawnCards에서 재계산
            });
        }

        if (data.correctConnections != null)
        {
            foreach (var conn in data.correctConnections)
            {
                // cardIDs 있으면 우선, 없으면 fromCardID/toCardID 폴백 (하위 호환)
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
            isOpen = false;
            HidePanel();
        }
    }

    public bool IsOpen() => isOpen;

    // ── 카드 스폰 (한 프레임 딜레이로 rect 확정 후 실행) ─
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
        Debug.Log($"[Whiteboard] 컨테이너 크기: {w} x {h}");

        if (w <= 10f) w = 1600f;
        if (h <= 10f) h = 900f;

        float padX = w * 0.08f;                  // ★ 10% → 8% (좌우)
        float padY = h * 0.08f;
        float usableW = w - padX * 2f;
        float usableH = h - padY * 2f;

        int cols = Mathf.CeilToInt(Mathf.Sqrt(count));
        int rows = Mathf.CeilToInt((float)count / cols);

        float cellW = Mathf.Max(usableW / cols, 280f); // ★ 최소 280px
        float cellH = Mathf.Max(usableH / rows, 220f); // ★ 최소 220px

        // 전체 크기 재계산 (최소값 적용 시 중심 기준 배치)
        float totalW = cellW * cols;
        float totalH = cellH * rows;

        for (int i = 0; i < count; i++)
        {
            int col = i % cols;
            int row = i / cols;

            float x = -totalW * 0.5f + cellW * (col + 0.5f) + Random.Range(-cellW * 0.1f, cellW * 0.1f);
            float y = totalH * 0.5f - cellH * (row + 0.5f) + Random.Range(-cellH * 0.1f, cellH * 0.1f);
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
        // 동일 쌍 재클릭 시 토글 제거
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

        CheckAllCorrectConnections();
    }

    // ── 정답 확인 — Union-Find (3개 이상 지원) ─
    private void CheckAllCorrectConnections()
    {
        // 현재 연결된 실로 Union-Find 그래프 구성
        var parent = new Dictionary<string, string>();
        foreach (var card in spawnedCards)
            parent[card.CardID] = card.CardID;

        foreach (var s in spawnedStrings)
            Union(parent, s.CardA.CardID, s.CardB.CardID);

        foreach (var conn in correctConnections)
        {
            if (conn.isRevealed) continue;
            if (conn.cardIDs == null || conn.cardIDs.Count < 2) continue;

            // 모든 cardIDs가 같은 연결 컴포넌트에 속하는지 확인
            string root = Find(parent, conn.cardIDs[0]);
            bool allConnected = true;
            for (int i = 1; i < conn.cardIDs.Count; i++)
            {
                if (!parent.ContainsKey(conn.cardIDs[i]) ||
                    Find(parent, conn.cardIDs[i]) != root)
                {
                    allConnected = false;
                    break;
                }
            }

            if (allConnected)
            {
                conn.isRevealed = true;
                RevealTruth(conn.revealMessage);
            }
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
        a = Find(parent, a);
        b = Find(parent, b);
        if (a != b) parent[a] = b;
    }

    // ── 진실 해금 ────────────────────────────
    private void RevealTruth(string message)
    {
        GameFlags.Instance?.AddTruth(message);

        // 진실 기록 버튼에 뱃지 표시
        if (truthBadge != null) truthBadge.SetActive(true);   // ★

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

    // ── 진실 수집 노트 토글 ──────────────────
    private void ToggleTruthNote()
    {
        if (isTruthNoteOpen) HideTruthNote();
        else ShowTruthNote();
    }

    private void ShowTruthNote()
    {
        if (truthNotePanelCG == null || truthNoteContainer == null) return;
        isTruthNoteOpen = true;

        if (truthBadge != null) truthBadge.SetActive(false);   // ★

        // 항목 초기화 후 재구성
        foreach (Transform child in truthNoteContainer)
            Destroy(child.gameObject);

        var truths = GameFlags.Instance?.GetAllTruths();
        if (truths != null && truths.Count > 0)
        {
            foreach (var truth in truths)
            {
                var entry = Instantiate(truthEntryPrefab, truthNoteContainer);
                var tmp = entry.GetComponentInChildren<TextMeshProUGUI>();
                if (tmp != null) tmp.text = truth;
            }
        }
        else
        {
            // 수집된 진실 없을 때 안내 텍스트
            var entry = Instantiate(truthEntryPrefab, truthNoteContainer);
            var tmp = entry.GetComponentInChildren<TextMeshProUGUI>();
            if (tmp != null) tmp.text = "아직 수집된 진실이 없습니다.";
        }

        truthNotePanelCG.gameObject.SetActive(true);
        truthNotePanelCG.alpha = 0f;
        truthNotePanelCG.interactable = false;
        truthNotePanelCG.blocksRaycasts = false;
        truthNotePanelCG.DOFade(1f, fadeDuration).OnComplete(() =>
        {
            truthNotePanelCG.interactable = true;
            truthNotePanelCG.blocksRaycasts = true;
        });
    }

    private void HideTruthNote()
    {
        if (truthNotePanelCG == null) return;
        isTruthNoteOpen = false;
        truthNotePanelCG.interactable = false;
        truthNotePanelCG.blocksRaycasts = false;
        truthNotePanelCG.DOFade(0f, fadeDuration)
            .OnComplete(() => truthNotePanelCG.gameObject.SetActive(false));
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

    private void SetCGHidden(CanvasGroup cg)
    {
        if (cg == null) return;
        cg.alpha = 0f;
        cg.interactable = false;
        cg.blocksRaycasts = false;
        cg.gameObject.SetActive(false);
    }

    private void HideImmediate()
    {
        SetCGHidden(whiteboardPanelCG);
        ResetCurtain();
    }

    private void ResetBoard()
    {
        foreach (var c in spawnedCards) if (c != null) Destroy(c.gameObject);
        foreach (var s in spawnedStrings) if (s != null) Destroy(s.gameObject);
        spawnedCards.Clear();
        spawnedStrings.Clear();
        isCurtainDown = false;
        isTruthNoteOpen = false;
        ResetCurtain();
        SetCGHidden(truthNotePanelCG);
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
    public List<string> cardIDs;       // ★ 2개 이상 지원
    public string revealMessage;
    [HideInInspector] public bool isRevealed = false;
}