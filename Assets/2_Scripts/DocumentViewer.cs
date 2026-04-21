using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using TMPro;
using UnityEngine.EventSystems;
using Obvious.Soap;

public class DocumentViewer : MonoBehaviour, IPointerClickHandler
{
    [Header("UI 연결")]
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private TextMeshProUGUI contentText;

    [Header("SOAP 연결")]
    [SerializeField] private ScriptableEventNoParam onApproveClicked;

    private DocumentData currentDocument;
    private bool isAllMaskedCorrectly = false;
    private HashSet<int> maskedWordIndices = new HashSet<int>();

    public void ShowDocument(DocumentData docData)
    {
        currentDocument = docData;
        titleText.text = docData.documentTitle;
        maskedWordIndices.Clear();
        isAllMaskedCorrectly = false;
        contentText.text = docData.mainText;
        contentText.ForceMeshUpdate();
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (currentDocument == null) return;
        int wordIndex = TMP_TextUtilities.FindIntersectingWord(contentText, eventData.position, Camera.main);
        if (wordIndex == -1) return;

        string clickedWord = contentText.textInfo.wordInfo[wordIndex].GetWord();
        bool isTargetWord = IsTargetKeyword(clickedWord);

        if (maskedWordIndices.Contains(wordIndex))
        {
            maskedWordIndices.Remove(wordIndex);
            Debug.Log(isTargetWord ? $"⚠️ 검열 단어 노출: {clickedWord}" : $"일반 단어 지움: {clickedWord}");
        }
        else
        {
            maskedWordIndices.Add(wordIndex);
            Debug.Log(isTargetWord ? $"🎯 검열 단어 가림: {clickedWord}" : $"일반 단어 칠함: {clickedWord}");
        }

        UpdateTextDisplay();
        CheckAnswer();
    }

    private void UpdateTextDisplay()
    {
        contentText.text = currentDocument.mainText;
        contentText.ForceMeshUpdate();

        string newText = currentDocument.mainText;
        List<int> sortedIndices = maskedWordIndices.ToList();
        sortedIndices.Sort();
        sortedIndices.Reverse();

        foreach (int index in sortedIndices)
        {
            if (index >= contentText.textInfo.wordCount) continue;
            TMP_WordInfo wInfo = contentText.textInfo.wordInfo[index];
            newText = newText.Insert(wInfo.lastCharacterIndex + 1, "</color></mark>");
            newText = newText.Insert(wInfo.firstCharacterIndex, "<mark=#000000><color=#000000>");
        }
        contentText.text = newText;
    }

    private void CheckAnswer()
    {
        if (!currentDocument.needsCensorship) { isAllMaskedCorrectly = false; return; }
        List<string> maskedWords = new List<string>();
        foreach (int index in maskedWordIndices)
            if (index < contentText.textInfo.wordCount)
                maskedWords.Add(contentText.textInfo.wordInfo[index].GetWord());

        isAllMaskedCorrectly = currentDocument.targetCensorKeywords.All(target =>
            maskedWords.Any(masked => masked.Contains(target)));
    }

    public void OnClickApproveButton()
    {
        Debug.Log("📋 서류 승인");
        if (currentDocument.needsCensorship)
            Debug.Log(isAllMaskedCorrectly ? "✅ 정답!" : "❌ 오답!");
        else
            Debug.Log(maskedWordIndices.Count == 0 ? "✅ 정답!" : "❌ 오답!");

        onApproveClicked?.Raise();
    }

    private bool IsTargetKeyword(string word)
    {
        if (!currentDocument.needsCensorship) return false;
        return currentDocument.targetCensorKeywords.Any(t => word.Contains(t));
    }
}
