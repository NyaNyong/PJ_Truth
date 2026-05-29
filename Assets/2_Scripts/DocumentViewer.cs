using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;
using Obvious.Soap;

public class DocumentViewer : MonoBehaviour
{
    public enum ToolMode { None, BlackMarker, Typewriter }

    [Header("UI - 문서")]
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private TextMeshProUGUI contentText;

    [Header("UI - 도구 버튼")]
    [SerializeField] private Button blackMarkerButton;
    [SerializeField] private Button typewriterButton;

    // ★ 앞뒤 전환
    [Header("UI - 페이지 전환")]
    [SerializeField] private Button              flipButton;        // 뒤집기 버튼
    [SerializeField] private TextMeshProUGUI     pageIndicatorText; // "앞면" / "뒷면" 표시 (옵션)

    [Header("버튼 색상")]
    [SerializeField] private Color buttonActiveColor   = new Color(0.3f, 0.7f, 0.3f);
    [SerializeField] private Color buttonInactiveColor = Color.white;

    [Header("타자기 시스템")]
    [SerializeField] private TypewriterSystem typewriterSystem;

    [Header("SOAP 연결")]
    [SerializeField] private ScriptableEventNoParam onApproveClicked;

    [Header("블랙마커 재질 - 3분할")]
    [SerializeField] private Sprite markerFrontSprite;
    [SerializeField] private Sprite markerMiddleSprite;
    [SerializeField] private Sprite markerEndSprite;
    [SerializeField] private float  markerCapWidth  = 0f;
    [SerializeField] private float  markerPaddingX  = 6f;
    [SerializeField] private float  markerPaddingY  = 4f;

    [Header("승인 버튼")]
    [SerializeField] private Button approveButton;

    // ─────────────────────────────────────────────────────────────────────
    private DocumentData currentDocument;
    private bool         isAllMaskedCorrectly = false;
    private bool         isDocumentActive     = false;
    private ToolMode     currentTool          = ToolMode.None;

    // ★ 페이지 상태
    private int currentPage = 0; // 0 = 앞면, 1 = 뒷면

    // ★ 페이지별 마스킹 데이터 (2페이지)
    private readonly Dictionary<string, HashSet<int>>[] _maskedPerPage =
    {
        new Dictionary<string, HashSet<int>>(),
        new Dictionary<string, HashSet<int>>()
    };

    /// <summary>현재 페이지의 마스킹 딕셔너리 — 기존 코드가 그대로 동작</summary>
    private Dictionary<string, HashSet<int>> maskedOccurrences => _maskedPerPage[currentPage];

    // 3분할 오버레이 풀
    private readonly List<RectTransform> _overlayPool    = new List<RectTransform>();
    private readonly List<RectTransform> _activeOverlays = new List<RectTransform>();

    private static readonly Regex slotSourceRegex      = new Regex(@"\[SLOT_(\d+)\]");
    private static readonly Regex slotPlaceholderRegex = new Regex(@"\x02(\d+)\x03");
    private static readonly Regex slotContainsRegex    = new Regex(@"SLOT(\d+)");

    private const int CHILD_FRONT  = 0;
    private const int CHILD_MIDDLE = 1;
    private const int CHILD_END    = 2;

    // ─────────────────────────────────────────────────────────────────────
    private void Awake()
    {
        if (blackMarkerButton != null)
            blackMarkerButton.onClick.AddListener(OnClickBlackMarkerButton);
        if (typewriterButton != null)
            typewriterButton.onClick.AddListener(OnClickTypewriterButton);
        if (flipButton != null)
            flipButton.onClick.AddListener(OnClickFlipButton);
        RefreshToolButtonUI();
    }

    // ─────────────────────────────────────────────────────────────────────
    // 문서 로드/해제
    // ─────────────────────────────────────────────────────────────────────

    public void ShowDocument(DocumentData docData)
    {
        currentDocument      = docData;
        titleText.text       = docData.documentTitle;
        isDocumentActive     = true;
        currentTool          = ToolMode.None;
        currentPage          = 0; // ★ 항상 앞면부터

        // ★ 양면 마스킹 초기화
        _maskedPerPage[0].Clear();
        _maskedPerPage[1].Clear();
        isAllMaskedCorrectly = false;
        ClearAllOverlays();

        // ★ 뒷면 존재 여부에 따라 뒤집기 버튼 표시
        bool hasBack = !string.IsNullOrWhiteSpace(docData.backText);
        if (flipButton != null) flipButton.gameObject.SetActive(hasBack);
        UpdatePageIndicator();

        if (typewriterButton != null)
            typewriterButton.gameObject.SetActive(docData.needsTypewriter);

        if (docData.needsTypewriter && typewriterSystem != null)
        {
            foreach (var slot in docData.typewriterSlots)
                slot.insertedWord = "";
            typewriterSystem.Initialize(docData);
            typewriterSystem.OnWordDropped = OnWordDropped;
            typewriterSystem.OnClosed      = () => SetTool(ToolMode.None);
        }

        typewriterSystem?.Close();
        RefreshToolButtonUI();
        RenderDocument();
        Debug.Log($"[DocumentViewer] 문서 로드: {docData.documentTitle}");
    }

    public void HideDocument()
    {
        isDocumentActive = false;
        currentTool      = ToolMode.None;
        currentPage      = 0;
        typewriterSystem?.Close();
        ClearAllOverlays();
        RefreshToolButtonUI();
        if (flipButton != null) flipButton.gameObject.SetActive(false);
    }

    // ─────────────────────────────────────────────────────────────────────
    // ★ 페이지 전환
    // ─────────────────────────────────────────────────────────────────────

    public void OnClickFlipButton()
    {
        if (!isDocumentActive || currentDocument == null) return;

        // 타자기는 앞면 전용 — 뒷면으로 갈 때 닫기
        if (currentPage == 0)
        {
            typewriterSystem?.Close();
            SetTool(ToolMode.None);
        }

        currentPage = 1 - currentPage; // 0↔1 토글
        ClearAllOverlays();
        UpdatePageIndicator();
        RefreshToolButtonUI();
        RenderDocument();

        AudioManager.Instance?.PlaySfxDayInteraction();
        Debug.Log($"[DocumentViewer] 페이지 전환 → {(currentPage == 0 ? "앞면" : "뒷면")}");
    }

    private void UpdatePageIndicator()
    {
        if (pageIndicatorText == null) return;
        pageIndicatorText.text = currentPage == 0 ? "앞면" : "뒷면";
    }

    // ─────────────────────────────────────────────────────────────────────
    // 도구 버튼
    // ─────────────────────────────────────────────────────────────────────

    public void OnClickBlackMarkerButton()
    {
        AudioManager.Instance?.PlaySfxMarkerToggle();
        if (currentTool == ToolMode.BlackMarker) SetTool(ToolMode.None);
        else { typewriterSystem?.Close(); SetTool(ToolMode.BlackMarker); }
    }

    public void OnClickTypewriterButton()
    {
        if (currentDocument == null || !currentDocument.needsTypewriter) return;
        if (currentPage != 0) return; // ★ 뒷면에서는 타자기 불가
        if (currentTool == ToolMode.Typewriter)
        {
            typewriterSystem?.Close();
            SetTool(ToolMode.None);
        }
        else
        {
            SetTool(ToolMode.Typewriter);
            typewriterSystem?.OpenPanel();
        }
    }

    private void SetTool(ToolMode mode)
    {
        currentTool = mode;
        RefreshToolButtonUI();
    }

    private void RefreshToolButtonUI()
    {
        SetButtonColor(blackMarkerButton, currentTool == ToolMode.BlackMarker);
        SetButtonColor(typewriterButton,  currentTool == ToolMode.Typewriter);

        // ★ 뒷면에서 타자기 버튼 비활성
        if (typewriterButton != null)
        {
            bool showTypewriter = currentDocument != null &&
                                  currentDocument.needsTypewriter &&
                                  currentPage == 0;
            typewriterButton.gameObject.SetActive(showTypewriter);
        }
    }

    private void SetButtonColor(Button btn, bool active)
    {
        if (btn == null) return;
        var img = btn.GetComponent<Image>();
        if (img != null) img.DOColor(active ? buttonActiveColor : buttonInactiveColor, 0.15f);
    }

    // ─────────────────────────────────────────────────────────────────────
    // 블랙마커 클릭 감지
    // ─────────────────────────────────────────────────────────────────────

    private void Update()
    {
        if (!isDocumentActive)                   return;
        if (currentTool != ToolMode.BlackMarker) return;
        if (!Input.GetMouseButtonDown(0))        return;
        HandleMarkerClick(Input.mousePosition);
    }

    private void HandleMarkerClick(Vector2 screenPos)
    {
        if (!RectTransformUtility.RectangleContainsScreenPoint(
                contentText.rectTransform, screenPos, null)) return;

        contentText.text = BuildDetectionText();
        contentText.ForceMeshUpdate();

        int wordIndex = GetWordIndexAt(screenPos);
        if (wordIndex < 0) { RenderDocument(); return; }

        string word          = contentText.textInfo.wordInfo[wordIndex].GetWord();
        int    firstCharIdx  = contentText.textInfo.wordInfo[wordIndex].firstCharacterIndex;
        string detText       = contentText.text;

        RenderDocument();

        if (string.IsNullOrWhiteSpace(word)) return;
        if (slotContainsRegex.IsMatch(word)) return;
        if (IsInsertedWord(word)) return;

        int occurrenceIdx = CountWordOccurrencesBefore(detText, word, firstCharIdx);

        if (!maskedOccurrences.ContainsKey(word))
            maskedOccurrences[word] = new HashSet<int>();

        if (maskedOccurrences[word].Contains(occurrenceIdx))
        {
            maskedOccurrences[word].Remove(occurrenceIdx);
            Debug.Log($"마커 제거: '{word}'[{occurrenceIdx}] (페이지{currentPage})");
        }
        else
        {
            maskedOccurrences[word].Add(occurrenceIdx);
            AudioManager.Instance?.PlaySfxMarkerDraw();
            Debug.Log($"마커 칠함: '{word}'[{occurrenceIdx}] (페이지{currentPage})");
        }

        RenderDocument();
        CheckAnswer();
    }

    private int CountWordOccurrencesBefore(string text, string word, int beforeCharIdx)
    {
        int count = 0, searchFrom = 0;
        while (searchFrom < beforeCharIdx)
        {
            int found = text.IndexOf(word, searchFrom, System.StringComparison.Ordinal);
            if (found < 0 || found >= beforeCharIdx) break;
            count++;
            searchFrom = found + word.Length;
        }
        return count;
    }

    // ─────────────────────────────────────────────────────────────────────
    // 드랍 위치 슬롯 감지 (앞면 전용)
    // ─────────────────────────────────────────────────────────────────────

    public int GetSlotIndexAtScreenPos(Vector2 screenPos)
    {
        if (!isDocumentActive || currentDocument == null) return -1;
        if (currentPage != 0) return -1; // ★ 뒷면 드랍 무시

        if (!RectTransformUtility.RectangleContainsScreenPoint(
                contentText.rectTransform, screenPos, null)) return -1;

        contentText.text = BuildDetectionText();
        contentText.ForceMeshUpdate();

        int    wordIndex = GetWordIndexAt(screenPos);
        string word      = wordIndex >= 0
            ? contentText.textInfo.wordInfo[wordIndex].GetWord()
            : "";

        RenderDocument();

        if (string.IsNullOrWhiteSpace(word)) return -1;

        var m = slotContainsRegex.Match(word);
        if (m.Success) return int.Parse(m.Groups[1].Value);

        if (currentDocument.needsTypewriter && currentDocument.typewriterSlots != null)
            for (int i = 0; i < currentDocument.typewriterSlots.Count; i++)
            {
                var slot = currentDocument.typewriterSlots[i];
                if (slot.insertedWord == word || slot.originalWord == word) return i;
            }

        return -1;
    }

    // ─────────────────────────────────────────────────────────────────────
    // 렌더링
    // ─────────────────────────────────────────────────────────────────────

    private void RenderDocument()
    {
        contentText.text = BuildDisplayText();
        contentText.ForceMeshUpdate();
        ApplyMarkerOverlays();
    }

    /// <summary>★ 현재 페이지에 맞는 표시 텍스트 생성</summary>
    private string BuildDisplayText()
    {
        if (currentDocument == null) return "";

        // ★ 뒷면
        if (currentPage == 1)
            return BuildDisplayTextFromRaw(currentDocument.backText ?? "", false);

        // 앞면
        return BuildDisplayTextFromRaw(currentDocument.mainText ?? "", true);
    }

    private string BuildDisplayTextFromRaw(string rawText, bool processSlots)
    {
        bool useSprite = markerFrontSprite  != null
                      && markerMiddleSprite != null
                      && markerEndSprite    != null;

        string text = processSlots
            ? slotSourceRegex.Replace(rawText, m => $"\x02{m.Groups[1].Value}\x03")
            : rawText;

        // 현재 페이지의 마스킹 적용
        foreach (var kvp in maskedOccurrences)
        {
            string kw     = kvp.Key;
            var    occSet = kvp.Value;
            if (occSet == null || occSet.Count == 0) continue;

            var ranges = new List<(int start, int len)>();
            int pos = 0, occ = 0;
            while (true)
            {
                int idx = text.IndexOf(kw, pos, System.StringComparison.Ordinal);
                if (idx < 0) break;
                if (occSet.Contains(occ)) ranges.Add((idx, kw.Length));
                pos = idx + kw.Length;
                occ++;
            }

            string masked = useSprite
                ? $"<color=#00000000>{kw}</color>"
                : $"<mark=#000000><color=#000000>{kw}</color></mark>";

            for (int r = ranges.Count - 1; r >= 0; r--)
            {
                var (start, len) = ranges[r];
                text = text.Substring(0, start) + masked + text.Substring(start + len);
            }
        }

        // 슬롯 치환 (앞면 전용)
        if (processSlots)
        {
            text = slotPlaceholderRegex.Replace(text, m =>
            {
                int idx = int.Parse(m.Groups[1].Value);
                if (currentDocument.typewriterSlots != null &&
                    idx < currentDocument.typewriterSlots.Count)
                {
                    string inserted = currentDocument.typewriterSlots[idx].insertedWord;
                    if (!string.IsNullOrEmpty(inserted))
                        return $"<color=#4488FF><u>{inserted}</u></color>";
                    string original = currentDocument.typewriterSlots[idx].originalWord;
                    return string.IsNullOrEmpty(original)
                        ? $" SLOT{idx} "
                        : $"<color=#999999>{original}</color>";
                }
                return m.Value;
            });
        }

        return text;
    }

    private string BuildDetectionText()
    {
        if (currentDocument == null) return "";

        // ★ 뒷면은 plain text 그대로
        if (currentPage == 1)
            return currentDocument.backText ?? "";

        // 앞면: 슬롯 처리
        return slotSourceRegex.Replace(currentDocument.mainText, m =>
        {
            int idx = int.Parse(m.Groups[1].Value);
            if (currentDocument.typewriterSlots != null &&
                idx < currentDocument.typewriterSlots.Count)
            {
                string inserted = currentDocument.typewriterSlots[idx].insertedWord;
                if (!string.IsNullOrEmpty(inserted)) return $" {inserted} ";
                return $" SLOT{idx} ";
            }
            return m.Value;
        });
    }

    // ─────────────────────────────────────────────────────────────────────
    // 3분할 마커 오버레이
    // ─────────────────────────────────────────────────────────────────────

    private void ApplyMarkerOverlays()
    {
        foreach (var rt in _activeOverlays)
            rt.gameObject.SetActive(false);
        _activeOverlays.Clear();

        if (markerFrontSprite == null || markerMiddleSprite == null || markerEndSprite == null) return;
        if (maskedOccurrences.Count == 0) return;

        contentText.ForceMeshUpdate();
        var info = contentText.textInfo;
        if (info == null || info.wordCount == 0) return;

        float capW = markerCapWidth > 0f ? markerCapWidth : markerFrontSprite.rect.width;
        var wordOccCounter = new Dictionary<string, int>();

        for (int wi = 0; wi < info.wordCount; wi++)
        {
            var wordInfo = info.wordInfo[wi];
            string word = wordInfo.GetWord();

            if (!wordOccCounter.ContainsKey(word)) wordOccCounter[word] = 0;
            int occIdx = wordOccCounter[word];
            wordOccCounter[word]++;

            if (!maskedOccurrences.TryGetValue(word, out var occSet)) continue;
            if (!occSet.Contains(occIdx)) continue;

            // ★ 글자를 줄(lineNumber)별로 그룹핑
            var lineSegs = new Dictionary<int, (float minX, float minY, float maxX, float maxY)>();

            for (int ci = wordInfo.firstCharacterIndex;
                 ci < wordInfo.firstCharacterIndex + wordInfo.characterCount; ci++)
            {
                if (ci >= info.characterCount) break;
                var ch = info.characterInfo[ci];
                if (!ch.isVisible) continue;

                int ln = ch.lineNumber;
                if (!lineSegs.ContainsKey(ln))
                    lineSegs[ln] = (float.MaxValue, float.MaxValue, float.MinValue, float.MinValue);

                var (x0, y0, x1, y1) = lineSegs[ln];
                lineSegs[ln] = (
                    Mathf.Min(x0, ch.bottomLeft.x),
                    Mathf.Min(y0, ch.bottomLeft.y),
                    Mathf.Max(x1, ch.topRight.x),
                    Mathf.Max(y1, ch.topRight.y)
                );
            }

            if (lineSegs.Count == 0) continue;

            // ★ 줄마다 오버레이 하나씩 생성
            foreach (var seg in lineSegs.Values)
            {
                var (lMinX, lMinY, lMaxX, lMaxY) = seg;
                float totalW = (lMaxX - lMinX) + markerPaddingX * 2f;
                float totalH = (lMaxY - lMinY) + markerPaddingY * 2f;
                float posX = (lMinX + lMaxX) * 0.5f;
                float posY = (lMinY + lMaxY) * 0.5f;

                var container = GetOrCreateOverlayContainer();
                container.SetParent(contentText.transform.parent, false);
                container.pivot = new Vector2(0.5f, 0.5f);
                container.anchorMin = new Vector2(0.5f, 0.5f);
                container.anchorMax = new Vector2(0.5f, 0.5f);
                container.sizeDelta = new Vector2(totalW, totalH);
                container.position = contentText.transform.TransformPoint(
                                          new Vector3(posX, posY, 0f));
                container.gameObject.SetActive(true);
                _activeOverlays.Add(container);

                // Front / Middle / End 스프라이트 설정 (기존 코드 그대로)
                var frontRT = container.GetChild(CHILD_FRONT) as RectTransform;
                var middleRT = container.GetChild(CHILD_MIDDLE) as RectTransform;
                var endRT = container.GetChild(CHILD_END) as RectTransform;

                var frontImg = frontRT.GetComponent<Image>();
                var middleImg = middleRT.GetComponent<Image>();
                var endImg = endRT.GetComponent<Image>();

                frontImg.sprite = markerFrontSprite; frontImg.color = Color.black;
                middleImg.sprite = markerMiddleSprite; middleImg.color = Color.black;
                endImg.sprite = markerEndSprite; endImg.color = Color.black;

                frontRT.anchorMin = new Vector2(0f, 0f); frontRT.anchorMax = new Vector2(0f, 1f);
                frontRT.pivot = new Vector2(0f, 0.5f);
                frontRT.anchoredPosition = Vector2.zero;
                frontRT.sizeDelta = new Vector2(capW, 0f);

                middleRT.anchorMin = new Vector2(0f, 0f); middleRT.anchorMax = new Vector2(1f, 1f);
                middleRT.offsetMin = new Vector2(capW, 0f);
                middleRT.offsetMax = new Vector2(-capW, 0f);

                endRT.anchorMin = new Vector2(1f, 0f); endRT.anchorMax = new Vector2(1f, 1f);
                endRT.pivot = new Vector2(1f, 0.5f);
                endRT.anchoredPosition = Vector2.zero;
                endRT.sizeDelta = new Vector2(capW, 0f);
            }
        }
    }

    private RectTransform GetOrCreateOverlayContainer()
    {
        foreach (var rt in _overlayPool)
            if (!rt.gameObject.activeSelf) return rt;

        var container = new GameObject("MarkerOverlay",
            typeof(RectTransform)).GetComponent<RectTransform>();

        foreach (string n in new[] { "Front", "Middle", "End" })
        {
            var child = new GameObject(n,
                typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
            child.SetParent(container, false);
            var img = child.GetComponent<Image>();
            img.raycastTarget = false;
            img.type          = Image.Type.Simple;
        }

        _overlayPool.Add(container);
        return container;
    }

    private void ClearAllOverlays()
    {
        foreach (var rt in _activeOverlays)
            rt.gameObject.SetActive(false);
        _activeOverlays.Clear();
    }

    // ─────────────────────────────────────────────────────────────────────
    // 타자기 콜백
    // ─────────────────────────────────────────────────────────────────────

    private void OnWordDropped(int slotIndex, string word)
    {
        Debug.Log($"[DocumentViewer] 슬롯 {slotIndex} ← '{word}'");
        RenderDocument();
        CheckAnswer();
    }

    // ─────────────────────────────────────────────────────────────────────
    // 채점 — ★ 양면 합산
    // ─────────────────────────────────────────────────────────────────────

    private void CheckAnswer()
    {
        isAllMaskedCorrectly = CheckMarkerAnswer() && CheckTypewriterAnswer();
    }

    private bool CheckMarkerAnswer()
    {
        if (!currentDocument.needsCensorship) return true;
        // ★ 앞면 + 뒷면 합산 검색
        return currentDocument.targetCensorKeywords.All(t =>
            _maskedPerPage[0].Any(kvp => kvp.Key.Contains(t) && kvp.Value?.Count > 0) ||
            _maskedPerPage[1].Any(kvp => kvp.Key.Contains(t) && kvp.Value?.Count > 0));
    }

    private bool CheckTypewriterAnswer()
    {
        if (!currentDocument.needsTypewriter) return true;
        if (currentDocument.typewriterSlots == null) return true;
        return currentDocument.typewriterSlots.All(s => s.insertedWord == s.correctWord);
    }

    // ─────────────────────────────────────────────────────────────────────
    // 승인
    // ─────────────────────────────────────────────────────────────────────

    public void OnClickApproveButton()
    {
        SetTool(ToolMode.None);
        typewriterSystem?.Close();

        ConfirmPopupUI.Instance?.Open(
            "검열이 완료되었습니까?",
            "", "",
            onConfirm: () =>
            {
                CheckAnswer();
                Debug.Log("서류 승인 → " + (isAllMaskedCorrectly ? "[정답]" : "[오답]"));
                isDocumentActive = false;
                currentTool      = ToolMode.None;
                typewriterSystem?.Close();
                onApproveClicked?.Raise();
            },
            confirmText: "예", cancelText: "아니요"
        );
    }

    // ─────────────────────────────────────────────────────────────────────
    // 점수 계산 / 키워드 확인 — ★ 양면 합산
    // ─────────────────────────────────────────────────────────────────────

    public (float censorScore, float typewriterScore) CalculateScore()
    {
        float censor = 50f, typewriter = 50f;

        if (currentDocument != null && currentDocument.needsCensorship &&
            currentDocument.targetCensorKeywords?.Count > 0)
        {
            int total   = currentDocument.targetCensorKeywords.Count;
            int correct = currentDocument.targetCensorKeywords.Count(kw =>
                // ★ 양면 합산
                _maskedPerPage[0].Any(kvp => kvp.Key.Contains(kw) && kvp.Value?.Count > 0) ||
                _maskedPerPage[1].Any(kvp => kvp.Key.Contains(kw) && kvp.Value?.Count > 0));
            censor = ((float)correct / total) * 50f;
        }

        if (currentDocument != null && currentDocument.needsTypewriter &&
            currentDocument.typewriterSlots?.Count > 0)
        {
            int total   = currentDocument.typewriterSlots.Count;
            int correct = currentDocument.typewriterSlots
                .Count(s => s.insertedWord == s.correctWord);
            typewriter = ((float)correct / total) * 50f;
        }

        return (censor, typewriter);
    }

    public bool WasKeywordCensored(string keyword)
    {
        // ★ 양면 합산
        return _maskedPerPage[0].Any(kvp => kvp.Key.Contains(keyword) && kvp.Value?.Count > 0) ||
               _maskedPerPage[1].Any(kvp => kvp.Key.Contains(keyword) && kvp.Value?.Count > 0);
    }

    // ─────────────────────────────────────────────────────────────────────
    // 헬퍼
    // ─────────────────────────────────────────────────────────────────────

    private int GetWordIndexAt(Vector2 screenPos)
    {
        int idx = TMP_TextUtilities.FindIntersectingWord(contentText, screenPos, null);
        if (idx == -1)
            idx = TMP_TextUtilities.FindNearestWord(contentText, screenPos, null);
        if (idx < 0 || idx >= contentText.textInfo.wordCount) return -1;
        return idx;
    }

    private bool IsTargetKeyword(string word)
    {
        if (!currentDocument.needsCensorship) return false;
        return currentDocument.targetCensorKeywords.Any(t => word.Contains(t));
    }

    private bool IsInsertedWord(string word)
    {
        if (!currentDocument.needsTypewriter) return false;
        if (currentDocument.typewriterSlots == null) return false;
        return currentDocument.typewriterSlots.Any(s => s.insertedWord == word);
    }
}
