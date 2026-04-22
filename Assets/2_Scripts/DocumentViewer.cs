using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.EventSystems;
using TMPro;
using Obvious.Soap;

public class DocumentViewer : MonoBehaviour
{
    [Header("UI 연결")]
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private TextMeshProUGUI contentText;

    [Header("타자기 시스템")]
    [SerializeField] private TypewriterSystem typewriterSystem;

    [Header("SOAP 연결")]
    [SerializeField] private ScriptableEventNoParam onApproveClicked;

    private DocumentData    currentDocument;
    private bool            isAllMaskedCorrectly = false;
    private HashSet<string> maskedKeywords        = new HashSet<string>();
    private bool            isDocumentActive      = false;

    private static readonly Regex slotRegex = new Regex(@"\[SLOT_(\d+)\]");

    public void ShowDocument(DocumentData docData)
    {
        currentDocument  = docData;
        titleText.text   = docData.documentTitle;
        isDocumentActive = true;

        maskedKeywords.Clear();
        isAllMaskedCorrectly = false;

        if (docData.needsTypewriter && typewriterSystem != null)
        {
            foreach (var slot in docData.typewriterSlots)
                slot.insertedWord = "";
            typewriterSystem.Initialize(docData);
            typewriterSystem.OnWordInserted = OnWordInserted;
        }

        RenderDocument();
        Debug.Log($"[DocumentViewer] 문서 로드: {docData.documentTitle}");
    }

    public void HideDocument()
    {
        isDocumentActive = false;
    }

    // ─────────────────────────────────────────
    // Update 기반 클릭 감지
    // ─────────────────────────────────────────
    private void Update()
    {
        if (!isDocumentActive)      return;
        if (contentText == null)    return;
        if (currentDocument == null) return;
        if (typewriterSystem != null && typewriterSystem.IsOpen) return;
        if (!Input.GetMouseButtonDown(0)) return;

        // ★ IsPointerOverGameObject 제거
        // → RectTransform 범위만으로 판단 (더 안정적)
        HandleTextClick(Input.mousePosition);
    }

    private void HandleTextClick(Vector2 screenPos)
    {
        // contentText 영역 안인지 확인
        if (!RectTransformUtility.RectangleContainsScreenPoint(
                contentText.rectTransform, screenPos, null))
        {
            return;
        }

        contentText.ForceMeshUpdate();

        // 1차: 정확한 위치 감지
        int wordIndex = TMP_TextUtilities.FindIntersectingWord(contentText, screenPos, null);

        // 2차: 정확한 위치에 단어 없으면 가장 가까운 단어 감지
        if (wordIndex == -1)
            wordIndex = TMP_TextUtilities.FindNearestWord(contentText, screenPos, null);

        if (wordIndex == -1)
        {
            Debug.Log("[DocumentViewer] 감지된 단어 없음");
            return;
        }

        // wordIndex 범위 안전 체크
        if (wordIndex >= contentText.textInfo.wordCount) return;

        string clickedWord = contentText.textInfo.wordInfo[wordIndex].GetWord();

        // 빈 문자열 방지
        if (string.IsNullOrWhiteSpace(clickedWord)) return;

        Debug.Log($"[DocumentViewer] 클릭된 단어: '{clickedWord}'");

        // 슬롯 클릭 감지
        if (currentDocument.needsTypewriter && typewriterSystem != null)
        {
            int slotIdx = TryGetSlotIndex(clickedWord);
            if (slotIdx >= 0)
            {
                typewriterSystem.OpenForSlot(slotIdx);
                return;
            }
        }

        // 블랙 마커 토글
        bool isTarget = IsTargetKeyword(clickedWord);

        if (maskedKeywords.Contains(clickedWord))
        {
            maskedKeywords.Remove(clickedWord);
            Debug.Log(isTarget ? $"⚠️ 검열 단어 노출: {clickedWord}" : $"마커 제거: {clickedWord}");
        }
        else
        {
            maskedKeywords.Add(clickedWord);
            Debug.Log(isTarget ? $"🎯 검열 단어 가림: {clickedWord}" : $"마커 칠함: {clickedWord}");
        }

        RenderDocument();
        CheckAnswer();
    }

    // ─────────────────────────────────────────
    // 렌더링
    // ─────────────────────────────────────────
    private void RenderDocument()
    {
        contentText.text = BuildDisplayText();
        contentText.ForceMeshUpdate();
        ApplyMasking();
    }

    private string BuildDisplayText()
    {
        if (currentDocument == null) return "";
        string text = currentDocument.mainText;

        if (!currentDocument.needsTypewriter || currentDocument.typewriterSlots == null)
            return text;

        return slotRegex.Replace(text, match =>
        {
            int idx = int.Parse(match.Groups[1].Value);
            if (idx < currentDocument.typewriterSlots.Count)
            {
                string inserted = currentDocument.typewriterSlots[idx].insertedWord;
                return string.IsNullOrEmpty(inserted) ? $"[ 슬롯{idx} ]" : inserted;
            }
            return match.Value;
        });
    }

    private void ApplyMasking()
    {
        if (maskedKeywords.Count == 0) return;

        contentText.ForceMeshUpdate();
        string     newText       = contentText.text;
        List<int>  indicesToMask = new List<int>();

        for (int i = 0; i < contentText.textInfo.wordCount; i++)
        {
            string word = contentText.textInfo.wordInfo[i].GetWord();
            if (maskedKeywords.Any(kw => word.Contains(kw)))
                indicesToMask.Add(i);
        }

        indicesToMask.Sort();
        indicesToMask.Reverse();

        foreach (int idx in indicesToMask)
        {
            if (idx >= contentText.textInfo.wordCount) continue;
            TMP_WordInfo wInfo = contentText.textInfo.wordInfo[idx];
            newText = newText.Insert(wInfo.lastCharacterIndex  + 1, "</color></mark>");
            newText = newText.Insert(wInfo.firstCharacterIndex,     "<mark=#000000><color=#000000>");
        }

        contentText.text = newText;
    }

    // ─────────────────────────────────────────
    private void OnWordInserted(int slotIndex, string word)
    {
        RenderDocument();
        CheckAnswer();
    }

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
        Debug.Log("📋 서류 승인 → " + (isAllMaskedCorrectly ? "✅ 정답!" : "❌ 오답!"));
        isDocumentActive = false;
        onApproveClicked?.Raise();
    }

    private bool IsTargetKeyword(string word)
    {
        if (!currentDocument.needsCensorship) return false;
        return currentDocument.targetCensorKeywords.Any(t => word.Contains(t));
    }

    private int TryGetSlotIndex(string word)
    {
        var m = Regex.Match(word, @"슬롯(\d+)");
        return m.Success ? int.Parse(m.Groups[1].Value) : -1;
    }
}
