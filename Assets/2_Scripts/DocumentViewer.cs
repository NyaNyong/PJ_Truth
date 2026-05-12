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

    // ─────────────────────────────────────────
    private DocumentData    currentDocument;
    private bool            isAllMaskedCorrectly = false;
    private HashSet<string> maskedKeywords        = new HashSet<string>();
    private bool            isDocumentActive      = false;
    private ToolMode        currentTool           = ToolMode.None;

    // 3분할 오버레이 풀 (각 항목 = 컨테이너 RT, 자식: Front/Middle/End)
    private readonly List<RectTransform> _overlayPool    = new List<RectTransform>();
    private readonly List<RectTransform> _activeOverlays = new List<RectTransform>();

    private static readonly Regex slotSourceRegex      = new Regex(@"##SLOT(\d+)##");
    private static readonly Regex slotPlaceholderRegex = new Regex(@"\x02(\d+)\x03");
    private static readonly Regex slotContainsRegex    = new Regex(@"SLOT(\d+)");

    // 자식 인덱스 상수
    private const int CHILD_FRONT  = 0;
    private const int CHILD_MIDDLE = 1;
    private const int CHILD_END    = 2;

    // ─────────────────────────────────────────
    private void Awake()
    {
        if (blackMarkerButton != null)
            blackMarkerButton.onClick.AddListener(OnClickBlackMarkerButton);
        if (typewriterButton != null)
            typewriterButton.onClick.AddListener(OnClickTypewriterButton);
        RefreshToolButtonUI();
    }

    public void ShowDocument(DocumentData docData)
    {
        currentDocument  = docData;
        titleText.text   = docData.documentTitle;
        isDocumentActive = true;
        currentTool      = ToolMode.None;

        maskedKeywords.Clear();
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

        contentText.text = BuildDetectionText();
        contentText.ForceMeshUpdate();

        int wordIndex = GetWordIndexAt(screenPos);
        string word   = wordIndex >= 0
            ? contentText.textInfo.wordInfo[wordIndex].GetWord()
            : "";

        RenderDocument();

        if (string.IsNullOrWhiteSpace(word)) return;
        if (slotContainsRegex.IsMatch(word))  return;
        if (IsInsertedWord(word))             return;

        bool isTarget = IsTargetKeyword(word);
        if (maskedKeywords.Contains(word))
        {
            maskedKeywords.Remove(word);
            Debug.Log(isTarget ? $"검열 단어 노출: {word}" : $"마커 제거: {word}");
        }
        else
        {
            maskedKeywords.Add(word);
            AudioManager.Instance?.PlaySfxMarkerDraw(); // ★ SFX (긋기만, 제거는 무음)
            Debug.Log(isTarget ? $"[검열] 가림: {word}" : $"마커 칠함: {word}");
        }

        RenderDocument();
        CheckAnswer();
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

        foreach (string kw in maskedKeywords)
        {
            if (string.IsNullOrEmpty(kw)) continue;
            string masked = useSprite
                ? $"<color=#00000000>{kw}</color>"
                : $"<mark=#000000><color=#000000>{kw}</color></mark>";
            text = text.Replace(kw, masked);
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
                return string.IsNullOrEmpty(original) ? $" SLOT{idx} " : $" {original} ";
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

        if (markerFrontSprite  == null) return;
        if (markerMiddleSprite == null) return;
        if (markerEndSprite    == null) return;
        if (maskedKeywords.Count == 0)  return;

        var info = contentText.textInfo;
        if (info == null || info.wordCount == 0) return;

        float capW = markerCapWidth > 0f
            ? markerCapWidth
            : markerFrontSprite.rect.width;

        for (int wi = 0; wi < info.wordCount; wi++)
        {
            var wordInfo = info.wordInfo[wi];
            string word  = wordInfo.GetWord();
            if (!maskedKeywords.Contains(word)) continue;

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

            float   totalW = (maxX - minX) + markerPaddingX * 2f;
            float   totalH = (maxY - minY) + markerPaddingY * 2f;
            Vector2 center = new Vector2((minX + maxX) * 0.5f, (minY + maxY) * 0.5f);

            var container = GetOrCreateOverlayContainer();
            container.SetParent(contentText.transform, false);

            // 컨테이너 위치·크기 (contentText 로컬 좌표)
            container.anchorMin = new Vector2(0.5f, 0.5f);
            container.anchorMax = new Vector2(0.5f, 0.5f);
            container.pivot = new Vector2(0.5f, 0.5f);
            container.sizeDelta = new Vector2(totalW, totalH);
            container.localPosition = new Vector3(center.x, center.y, 0f);

            // ── Front: 왼쪽 끝, 고정 너비, 세로 full ──
            var front = container.GetChild(CHILD_FRONT) as RectTransform;
            front.anchorMin        = new Vector2(0f, 0f);
            front.anchorMax        = new Vector2(0f, 1f);
            front.pivot            = new Vector2(0f, 0.5f);
            front.anchoredPosition = Vector2.zero;
            front.sizeDelta        = new Vector2(capW, 0f);
            front.GetComponent<Image>().sprite = markerFrontSprite;

            // ── End: 오른쪽 끝, 고정 너비, 세로 full ──
            var end = container.GetChild(CHILD_END) as RectTransform;
            end.anchorMin        = new Vector2(1f, 0f);
            end.anchorMax        = new Vector2(1f, 1f);
            end.pivot            = new Vector2(1f, 0.5f);
            end.anchoredPosition = Vector2.zero;
            end.sizeDelta        = new Vector2(capW, 0f);
            end.GetComponent<Image>().sprite = markerEndSprite;

            // ── Middle: 캡 사이를 가로 stretch ─────
            var mid = container.GetChild(CHILD_MIDDLE) as RectTransform;
            mid.anchorMin = new Vector2(0f, 0f);
            mid.anchorMax = new Vector2(1f, 1f);
            mid.offsetMin = new Vector2(capW,  0f);
            mid.offsetMax = new Vector2(-capW, 0f);
            mid.GetComponent<Image>().sprite = markerMiddleSprite;

            container.gameObject.SetActive(true);
            _activeOverlays.Add(container);
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
            maskedKeywords.Any(m => m.Contains(t)));
    }

    private bool CheckTypewriterAnswer()
    {
        if (!currentDocument.needsTypewriter) return true;
        if (currentDocument.typewriterSlots == null) return true;
        return currentDocument.typewriterSlots.All(s => s.insertedWord == s.correctWord);
    }

    public void OnClickApproveButton()
    {
        CheckAnswer();
        Debug.Log("서류 승인 → " + (isAllMaskedCorrectly ? "[정답]" : "[오답]"));
        isDocumentActive = false;
        currentTool      = ToolMode.None;
        typewriterSystem?.Close();
        onApproveClicked?.Raise();
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
                .Count(kw => maskedKeywords.Any(m => m.Contains(kw)));
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
