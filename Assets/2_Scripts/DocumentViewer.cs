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

    [Header("버튼 색상")]
    [SerializeField] private Color buttonActiveColor   = new Color(0.3f, 0.7f, 0.3f);
    [SerializeField] private Color buttonInactiveColor = Color.white;

    [Header("타자기 시스템")]
    [SerializeField] private TypewriterSystem typewriterSystem;

    [Header("SOAP 연결")]
    [SerializeField] private ScriptableEventNoParam onApproveClicked;

    [Header("블랙마커 재질 - 3분할")]
    [Tooltip("왼쪽 끝 캡 스프라이트 (BlackMarkerFront)")]
    [SerializeField] private Sprite markerFrontSprite;
    [Tooltip("가운데 늘어나는 스프라이트 (BlackMarkerMiddle)")]
    [SerializeField] private Sprite markerMiddleSprite;
    [Tooltip("오른쪽 끝 캡 스프라이트 (BlackMarkerEnd)")]
    [SerializeField] private Sprite markerEndSprite;
    [Tooltip("좌우 캡 고정 너비(px). 0이면 스프라이트 원본 너비 사용.")]
    [SerializeField] private float markerCapWidth = 0f;
    [Tooltip("단어 경계에 추가할 여백(px)")]
    [SerializeField] private float markerPaddingX = 6f;
    [SerializeField] private float markerPaddingY = 4f;

    [Header("승인 버튼")]
    [SerializeField] private Button approveButton; // ★ 추가

    // ─────────────────────────────────────────
    private DocumentData    currentDocument;
    private bool            isAllMaskedCorrectly = false;
    private HashSet<string> maskedKeywords        = new HashSet<string>();
    private bool            isDocumentActive      = false;
    private ToolMode        currentTool           = ToolMode.None;

    // 3분할 오버레이 풀 (각 항목 = 컨테이너 RT, 자식: Front/Middle/End)
    private readonly List<RectTransform> _overlayPool    = new List<RectTransform>();
    private readonly List<RectTransform> _activeOverlays = new List<RectTransform>();

    // ★ [SLOT_N] 형식으로 수정 (기존 ##SLOTN## → [SLOT_N])
    private static readonly Regex slotSourceRegex = new Regex(@"\[SLOT_(\d+)\]");
    private static readonly Regex slotPlaceholderRegex = new Regex(@"\x02(\d+)\x03");
    private static readonly Regex slotContainsRegex = new Regex(@"SLOT(\d+)");

    // ★ 변경: 클릭한 특정 위치의 단어만 검열하도록 (단어텍스트 → 단어별 발생 인덱스 셋)
    private Dictionary<string, HashSet<int>> maskedOccurrences = new Dictionary<string, HashSet<int>>();

    // 자식 인덱스 상수
    private const int CHILD_FRONT  = 0;
    private const int CHILD_MIDDLE = 1;
    private const int CHILD_END    = 2;

    // ─────────────────────────────────────────
    // [SerializeField] private Button approveButton; ← 삭제

    private void Awake()
    {
        if (blackMarkerButton != null)
            blackMarkerButton.onClick.AddListener(OnClickBlackMarkerButton);
        if (typewriterButton != null)
            typewriterButton.onClick.AddListener(OnClickTypewriterButton);
        // approveButton AddListener ← 삭제
        RefreshToolButtonUI();
    }

    public void ShowDocument(DocumentData docData)
    {
        currentDocument  = docData;
        titleText.text   = docData.documentTitle;
        isDocumentActive = true;
        currentTool      = ToolMode.None;

        maskedOccurrences.Clear(); // ★ 기존 maskedKeywords.Clear() 교체
        isAllMaskedCorrectly = false;
        ClearAllOverlays();

        if (typewriterButton != null)
            typewriterButton.gameObject.SetActive(docData.needsTypewriter);

        if (docData.needsTypewriter && typewriterSystem != null)
        {
            foreach (var slot in docData.typewriterSlots)
                slot.insertedWord = "";
            typewriterSystem.Initialize(docData);
            typewriterSystem.OnWordDropped = OnWordDropped;
            typewriterSystem.OnClosed = () => SetTool(ToolMode.None);
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
        typewriterSystem?.Close();
        ClearAllOverlays();
        RefreshToolButtonUI();
    }

    // ─────────────────────────────────────────
    // 도구 버튼
    // ─────────────────────────────────────────
    public void OnClickBlackMarkerButton()
    {
        AudioManager.Instance?.PlaySfxMarkerToggle(); // ★ SFX
        if (currentTool == ToolMode.BlackMarker) SetTool(ToolMode.None);
        else { typewriterSystem?.Close(); SetTool(ToolMode.BlackMarker); }
    }

    public void OnClickTypewriterButton()
    {
        if (currentDocument == null || !currentDocument.needsTypewriter) return;
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
    }

    private void SetButtonColor(Button btn, bool active)
    {
        if (btn == null) return;
        var img = btn.GetComponent<Image>();
        if (img != null) img.DOColor(active ? buttonActiveColor : buttonInactiveColor, 0.15f);
    }

    // ─────────────────────────────────────────
    // 블랙마커 — 클릭 감지
    // ─────────────────────────────────────────
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

        // 감지용 텍스트로 일시 교체
        contentText.text = BuildDetectionText();
        contentText.ForceMeshUpdate();

        int wordIndex = GetWordIndexAt(screenPos);
        if (wordIndex < 0) { RenderDocument(); return; }

        string word = contentText.textInfo.wordInfo[wordIndex].GetWord();
        int firstCharIdx = contentText.textInfo.wordInfo[wordIndex].firstCharacterIndex;
        string detText = contentText.text;

        RenderDocument();

        if (string.IsNullOrWhiteSpace(word)) return;
        if (slotContainsRegex.IsMatch(word)) return;
        if (IsInsertedWord(word)) return;

        // ★ 클릭된 단어가 문서 내 몇 번째 발생인지 계산
        int occurrenceIdx = CountWordOccurrencesBefore(detText, word, firstCharIdx);

        if (!maskedOccurrences.ContainsKey(word))
            maskedOccurrences[word] = new HashSet<int>();

        if (maskedOccurrences[word].Contains(occurrenceIdx))
        {
            maskedOccurrences[word].Remove(occurrenceIdx);
            Debug.Log($"마커 제거: '{word}'[{occurrenceIdx}]");
        }
        else
        {
            maskedOccurrences[word].Add(occurrenceIdx);
            AudioManager.Instance?.PlaySfxMarkerDraw();
            Debug.Log($"마커 칠함: '{word}'[{occurrenceIdx}]");
        }

        RenderDocument();
        CheckAnswer();
    }

    // ★ 신규 헬퍼: beforeCharIdx 이전에 word가 몇 번 등장하는지
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

    // ─────────────────────────────────────────
    // 드랍 위치 슬롯 감지 (WordCard 호출)
    // ─────────────────────────────────────────
    public int GetSlotIndexAtScreenPos(Vector2 screenPos)
    {
        if (!isDocumentActive || currentDocument == null) return -1;
        if (!RectTransformUtility.RectangleContainsScreenPoint(
                contentText.rectTransform, screenPos, null)) return -1;

        contentText.text = BuildDetectionText();
        contentText.ForceMeshUpdate();

        int wordIndex = GetWordIndexAt(screenPos);
        string word   = wordIndex >= 0
            ? contentText.textInfo.wordInfo[wordIndex].GetWord()
            : "";

        RenderDocument();
        Debug.Log($"[DocumentViewer] 드랍 위치 단어: '{word}'");

        if (string.IsNullOrWhiteSpace(word)) return -1;

        var m = slotContainsRegex.Match(word);
        if (m.Success) return int.Parse(m.Groups[1].Value);

        if (currentDocument.needsTypewriter && currentDocument.typewriterSlots != null)
        {
            for (int i = 0; i < currentDocument.typewriterSlots.Count; i++)
            {
                var slot = currentDocument.typewriterSlots[i];
                if (slot.insertedWord == word) return i;
                if (slot.originalWord  == word) return i;
            }
        }

        return -1;
    }

    // ─────────────────────────────────────────
    // 렌더링
    // ─────────────────────────────────────────
    private void RenderDocument()
    {
        contentText.text = BuildDisplayText();
        contentText.ForceMeshUpdate();
        ApplyMarkerOverlays();
    }

    /// <summary>
    /// 화면 표시용 텍스트.
    /// 3분할 스프라이트가 모두 연결된 경우 → 마스킹 단어를 투명(#00000000)으로 처리 (레이아웃 유지, 오버레이가 덮음).
    /// 스프라이트 미연결 시 → mark 태그 폴백.
    /// </summary>
    private string BuildDisplayText()
    {
        if (currentDocument == null) return "";

        bool useSprite = markerFrontSprite  != null
                      && markerMiddleSprite != null
                      && markerEndSprite    != null;

        string text = slotSourceRegex.Replace(currentDocument.mainText, m =>
        {
            int idx = int.Parse(m.Groups[1].Value);
            return $"\x02{idx}\x03";
        });

        // ★ 기존 foreach(string kw in maskedKeywords) 블록 전체를 아래로 교체
        foreach (var kvp in maskedOccurrences)
        {
            string kw = kvp.Key;
            var occSet = kvp.Value;
            if (occSet == null || occSet.Count == 0) continue;

            // 발생 위치를 역순으로 수집 후 교체 (인덱스 보존)
            var ranges = new List<(int start, int len)>();
            int pos = 0, occ = 0;
            while (true)
            {
                int idx = text.IndexOf(kw, pos, System.StringComparison.Ordinal);
                if (idx < 0) break;
                if (occSet.Contains(occ))
                    ranges.Add((idx, kw.Length));
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

        return text;
    }

    /// <summary>감지용 plain 텍스트 (rich text 태그 없음).</summary>
    private string BuildDetectionText()
    {
        if (currentDocument == null) return "";

        return slotSourceRegex.Replace(currentDocument.mainText, m =>
        {
            int idx = int.Parse(m.Groups[1].Value);
            if (currentDocument.typewriterSlots != null &&
                idx < currentDocument.typewriterSlots.Count)
            {
                string inserted = currentDocument.typewriterSlots[idx].insertedWord;
                if (!string.IsNullOrEmpty(inserted)) return $" {inserted} ";
                string original = currentDocument.typewriterSlots[idx].originalWord;
                return $" SLOT{idx} ";
            }
            return m.Value;
        });
    }

    // ─────────────────────────────────────────
    // 3분할 마커 오버레이
    // ─────────────────────────────────────────

    /// <summary>
    /// maskedKeywords에 해당하는 TMP 단어 위치를 읽어
    /// [Front | Middle(stretch) | End] 3분할 Image 조합을 배치한다.
    /// </summary>
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

            // 바운딩 박스
            float minX = float.MaxValue, minY = float.MaxValue;
            float maxX = float.MinValue, maxY = float.MinValue;
            bool hasVisible = false;

            for (int ci = wordInfo.firstCharacterIndex;
                 ci < wordInfo.firstCharacterIndex + wordInfo.characterCount; ci++)
            {
                if (ci >= info.characterCount) break;
                var ch = info.characterInfo[ci];
                if (!ch.isVisible) continue;

                minX = Mathf.Min(minX, ch.bottomLeft.x);
                minY = Mathf.Min(minY, ch.bottomLeft.y);
                maxX = Mathf.Max(maxX, ch.topRight.x);
                maxY = Mathf.Max(maxY, ch.topRight.y);
                hasVisible = true;
            }

            if (!hasVisible) continue;

            float totalW = (maxX - minX) + markerPaddingX * 2f;
            float totalH = (maxY - minY) + markerPaddingY * 2f;
            float posX = (minX + maxX) * 0.5f;
            float posY = (minY + maxY) * 0.5f;

            var container = GetOrCreateOverlayContainer();
            // ★ 교체
            container.SetParent(contentText.transform.parent, false);
            container.pivot = new Vector2(0.5f, 0.5f);
            container.anchorMin = new Vector2(0.5f, 0.5f);
            container.anchorMax = new Vector2(0.5f, 0.5f);
            container.sizeDelta = new Vector2(totalW, totalH);
            // TMP 로컬 좌표 → 월드 좌표로 변환하여 설정 (localPosition 직접 사용 시 부모 피벗 오프셋 발생)
            container.position = contentText.transform.TransformPoint(new Vector3(posX, posY, 0f));
            container.gameObject.SetActive(true);
            _activeOverlays.Add(container);

            // Front
            var frontRT = container.GetChild(CHILD_FRONT) as RectTransform;
            var frontImg = frontRT.GetComponent<Image>();
            frontImg.sprite = markerFrontSprite;
            frontImg.color = Color.black;
            frontRT.anchorMin = new Vector2(0f, 0f);
            frontRT.anchorMax = new Vector2(0f, 1f);
            frontRT.pivot = new Vector2(0f, 0.5f);
            frontRT.anchoredPosition = Vector2.zero;
            frontRT.sizeDelta = new Vector2(capW, 0f);

            // Middle
            var middleRT = container.GetChild(CHILD_MIDDLE) as RectTransform;
            var middleImg = middleRT.GetComponent<Image>();
            middleImg.sprite = markerMiddleSprite;
            middleImg.color = Color.black;
            middleRT.anchorMin = new Vector2(0f, 0f);
            middleRT.anchorMax = new Vector2(1f, 1f);
            middleRT.offsetMin = new Vector2(capW, 0f);
            middleRT.offsetMax = new Vector2(-capW, 0f);

            // End
            var endRT = container.GetChild(CHILD_END) as RectTransform;
            var endImg = endRT.GetComponent<Image>();
            endImg.sprite = markerEndSprite;
            endImg.color = Color.black;
            endRT.anchorMin = new Vector2(1f, 0f);
            endRT.anchorMax = new Vector2(1f, 1f);
            endRT.pivot = new Vector2(1f, 0.5f);
            endRT.anchoredPosition = Vector2.zero;
            endRT.sizeDelta = new Vector2(capW, 0f);
        }
    }

    /// <summary>
    /// 풀에서 컨테이너를 재사용하거나 신규 생성한다.
    /// 자식 구조: [0]=Front  [1]=Middle  [2]=End
    /// </summary>
    private RectTransform GetOrCreateOverlayContainer()
    {
        foreach (var rt in _overlayPool)
            if (!rt.gameObject.activeSelf)
                return rt;

        var container = new GameObject("MarkerOverlay",
            typeof(RectTransform)).GetComponent<RectTransform>();

        string[] childNames = { "Front", "Middle", "End" };
        foreach (string n in childNames)
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

    // ─────────────────────────────────────────
    // 타자기 콜백
    // ─────────────────────────────────────────
    private void OnWordDropped(int slotIndex, string word)
    {
        Debug.Log($"[DocumentViewer] 슬롯 {slotIndex} ← '{word}'");
        RenderDocument();
        CheckAnswer();
    }

    // ─────────────────────────────────────────
    // 채점
    // ─────────────────────────────────────────
    private void CheckAnswer()
    {
        isAllMaskedCorrectly = CheckMarkerAnswer() && CheckTypewriterAnswer();
    }

    private bool CheckMarkerAnswer()
    {
        if (!currentDocument.needsCensorship) return true;
        return currentDocument.targetCensorKeywords.All(t =>
            maskedOccurrences.Any(kvp =>
                kvp.Key.Contains(t) && kvp.Value != null && kvp.Value.Count > 0));
    }

    private bool CheckTypewriterAnswer()
    {
        if (!currentDocument.needsTypewriter) return true;
        if (currentDocument.typewriterSlots == null) return true;
        return currentDocument.typewriterSlots.All(s => s.insertedWord == s.correctWord);
    }

    public void OnClickApproveButton()
    {
        SetTool(ToolMode.None);  // ★ 블랙마커/타자기 즉시 해제
        typewriterSystem?.Close();

        ConfirmPopupUI.Instance?.Open(
            "검열이 완료되었습니까?",
            "",
            "",
            onConfirm: () =>
            {
                CheckAnswer();
                Debug.Log("서류 승인 → " + (isAllMaskedCorrectly ? "[정답]" : "[오답]"));
                isDocumentActive = false;
                currentTool = ToolMode.None;
                typewriterSystem?.Close();
                onApproveClicked?.Raise();
            },
            confirmText: "예", cancelText: "아니요"
        );
    }

    /// <summary>GameManager가 결과창 표시 전 호출 — 검열/타자기 점수 반환</summary>
    public (float censorScore, float typewriterScore) CalculateScore()
    {
        float censor = 50f, typewriter = 50f;

        if (currentDocument != null && currentDocument.needsCensorship &&
            currentDocument.targetCensorKeywords?.Count > 0)
        {
            int total   = currentDocument.targetCensorKeywords.Count;
            int correct = currentDocument.targetCensorKeywords
             .Count(kw => maskedOccurrences.Any(kvp =>
               kvp.Key.Contains(kw) && kvp.Value != null && kvp.Value.Count > 0)); // ★
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

    /// <summary>특정 키워드가 검열됐는지 여부 반환</summary>
    public bool WasKeywordCensored(string keyword)
    {
        return maskedOccurrences.Any(kvp =>
            kvp.Key.Contains(keyword) && kvp.Value != null && kvp.Value.Count > 0);
    }

    // ─────────────────────────────────────────
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
