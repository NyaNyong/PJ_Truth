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

    // ─────────────────────────────────────────
    private DocumentData    currentDocument;
    private bool            isAllMaskedCorrectly = false;
    private HashSet<string> maskedKeywords        = new HashSet<string>();
    private bool            isDocumentActive      = false;
    private ToolMode        currentTool           = ToolMode.None;

    // ##SLOT0## 소스 태그
    private static readonly Regex slotSourceRegex   = new Regex(@"##SLOT(\d+)##");
    // 플레이스홀더: \x02N\x03 (STX...ETX) — maskedKeywords와 절대 충돌 안 함
    private static readonly Regex slotPlaceholderRegex = new Regex(@"\x02(\d+)\x03");
    // 감지용: SLOT0 포함 여부
    private static readonly Regex slotContainsRegex = new Regex(@"SLOT(\d+)");

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

        if (typewriterButton != null)
            typewriterButton.gameObject.SetActive(docData.needsTypewriter);

        if (docData.needsTypewriter && typewriterSystem != null)
        {
            foreach (var slot in docData.typewriterSlots)
                slot.insertedWord = "";
            typewriterSystem.Initialize(docData);
            typewriterSystem.OnWordDropped = OnWordDropped;
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
        RefreshToolButtonUI();
    }

    // ─────────────────────────────────────────
    // 도구 버튼
    // ─────────────────────────────────────────
    public void OnClickBlackMarkerButton()
    {
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

        // ★ 감지용 plain text로 전환 후 단어 감지
        contentText.text = BuildDetectionText();
        contentText.ForceMeshUpdate();

        int wordIndex = GetWordIndexAt(screenPos);
        string word   = wordIndex >= 0
            ? contentText.textInfo.wordInfo[wordIndex].GetWord()
            : "";

        // 즉시 화면 텍스트 복원
        RenderDocument();

        if (string.IsNullOrWhiteSpace(word)) return;
        if (slotContainsRegex.IsMatch(word))  return; // 슬롯 토큰 제외
        if (IsInsertedWord(word))             return; // 삽입된 단어 제외

        bool isTarget = IsTargetKeyword(word);
        if (maskedKeywords.Contains(word))
        {
            maskedKeywords.Remove(word);
            Debug.Log(isTarget ? $"검열 단어 노출: {word}" : $"마커 제거: {word}");
        }
        else
        {
            maskedKeywords.Add(word);
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

        // ★ 감지용 plain text로 전환
        contentText.text = BuildDetectionText();
        contentText.ForceMeshUpdate();

        int wordIndex = GetWordIndexAt(screenPos);
        string word   = wordIndex >= 0
            ? contentText.textInfo.wordInfo[wordIndex].GetWord()
            : "";

        // 즉시 화면 텍스트 복원
        RenderDocument();

        Debug.Log($"[DocumentViewer] 드랍 위치 단어: '{word}'");

        if (string.IsNullOrWhiteSpace(word)) return -1;

        // SLOT0, SLOT1 ... 포함 여부 확인
        var m = slotContainsRegex.Match(word);
        if (m.Success) return int.Parse(m.Groups[1].Value);

        // 이미 삽입된 단어 위에 드랍 → 교체
        // originalWord 위에 드랍 → 해당 슬롯
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
    }

    /// <summary>
    /// 화면 표시용 텍스트.
    /// 플레이스홀더 → 마스킹 적용 → 슬롯 색상 적용 순으로 한 번에 처리.
    /// 서로 다른 태그가 겹치지 않아 레이아웃 오류 없음.
    /// </summary>
    private string BuildDisplayText()
    {
        if (currentDocument == null) return "";

        // 1단계: 슬롯 태그 → 플레이스홀더 \x02N\x03
        string text = slotSourceRegex.Replace(currentDocument.mainText, m =>
        {
            int idx = int.Parse(m.Groups[1].Value);
            return $"\x02{idx}\x03";
        });

        // 2단계: maskedKeywords → 마크 태그로 교체 (플레이스홀더는 건드리지 않음)
        foreach (string kw in maskedKeywords)
        {
            if (string.IsNullOrEmpty(kw)) continue;
            text = text.Replace(kw,
                $"<mark=#000000><color=#000000>{kw}</color></mark>");
        }

        // 3단계: 플레이스홀더 → 슬롯 표시 (삽입 단어 or SLOT토큰)
        text = slotPlaceholderRegex.Replace(text, m =>
        {
            int idx = int.Parse(m.Groups[1].Value);
            if (currentDocument.typewriterSlots != null &&
                idx < currentDocument.typewriterSlots.Count)
            {
                string inserted = currentDocument.typewriterSlots[idx].insertedWord;
                // 삽입된 단어가 있으면 파란색, 없으면 originalWord 표시
                if (!string.IsNullOrEmpty(inserted))
                    return $"<color=#4488FF><u>{inserted}</u></color>";

                // originalWord가 있으면 그대로 표시 (회색으로 구분)
                string original = currentDocument.typewriterSlots[idx].originalWord;
                return string.IsNullOrEmpty(original)
                    ? $" SLOT{idx} "
                    : $"<color=#999999>{original}</color>";
            }
            return m.Value;
        });

        return text;
    }

    /// <summary>
    /// 감지용 plain 텍스트 (rich text 태그 없음).
    /// TMP 단어 인덱스 감지에 사용.
    /// </summary>
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
                if (!string.IsNullOrEmpty(inserted))
                    return $" {inserted} ";
                string original = currentDocument.typewriterSlots[idx].originalWord;
                return string.IsNullOrEmpty(original)
                    ? $" SLOT{idx} "
                    : $" {original} ";
            }
            return m.Value;
        });
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
