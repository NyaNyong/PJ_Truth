using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using TMPro;
using UnityEngine.EventSystems;

public class DocumentViewer : MonoBehaviour, IPointerClickHandler
{
    [Header("연결할 UI 텍스트")]
    public TextMeshProUGUI titleText;
    public TextMeshProUGUI contentText;

    [Header("현재 표시할 문서 데이터")]
    public DocumentData currentDocument;

    private bool isAllMaskedCorrectly = false;
    private HashSet<int> maskedWordIndices = new HashSet<int>();

    public void ShowDocument(DocumentData docData)
    {
        currentDocument = docData;
        titleText.text = docData.documentTitle;

        // 🌟 1. 마커로 칠했던 위치 기록과 정답 판정을 싹 지웁니다.
        maskedWordIndices.Clear();
        isAllMaskedCorrectly = false;

        // 🌟 2. 텍스트를 원본 내용으로 깨끗하게 덮어씁니다.
        contentText.text = docData.mainText;
        contentText.ForceMeshUpdate();
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        Camera uiCamera = Camera.main;
        int wordIndex = TMP_TextUtilities.FindIntersectingWord(contentText, eventData.position, uiCamera);

        if (wordIndex != -1)
        {
            string clickedWord = contentText.textInfo.wordInfo[wordIndex].GetWord();
            bool isTargetWord = false;

            if (currentDocument.needsCensorship)
            {
                foreach (string target in currentDocument.targetCensorKeywords)
                {
                    if (clickedWord.Contains(target))
                    {
                        isTargetWord = true;
                        break;
                    }
                }
            }

            if (maskedWordIndices.Contains(wordIndex))
            {
                maskedWordIndices.Remove(wordIndex);
                if (isTargetWord) Debug.Log($"⚠️ [경고] 검열 단어 노출됨: {clickedWord}");
                else Debug.Log($"[안내] 일반 단어 지움: {clickedWord}");
            }
            else
            {
                maskedWordIndices.Add(wordIndex);
                if (isTargetWord) Debug.Log($"🎯 [적중] 검열 단어 가림: {clickedWord}");
                else Debug.Log($"[안내] 일반 단어 칠함: {clickedWord}");
            }

            UpdateTextDisplay();
            CheckAnswer();
        }
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
            if (index < contentText.textInfo.wordCount)
            {
                TMP_WordInfo wInfo = contentText.textInfo.wordInfo[index];
                int startChar = wInfo.firstCharacterIndex;
                int endChar = wInfo.lastCharacterIndex;

                newText = newText.Insert(endChar + 1, "</color></mark>");
                newText = newText.Insert(startChar, "<mark=#000000><color=#000000>");
            }
        }
        contentText.text = newText;
    }

    private void CheckAnswer()
    {
        if (currentDocument.needsCensorship == false) return;

        List<string> currentlyMaskedWords = new List<string>();
        foreach (int index in maskedWordIndices)
        {
            if (index < contentText.textInfo.wordCount)
            {
                currentlyMaskedWords.Add(contentText.textInfo.wordInfo[index].GetWord());
            }
        }

        bool allTargetsFound = true;

        foreach (string target in currentDocument.targetCensorKeywords)
        {
            bool isThisTargetMasked = false;
            foreach (string maskedWord in currentlyMaskedWords)
            {
                if (maskedWord.Contains(target))
                {
                    isThisTargetMasked = true;
                    break;
                }
            }

            if (isThisTargetMasked == false)
            {
                allTargetsFound = false;
                break;
            }
        }

        isAllMaskedCorrectly = allTargetsFound;
    }

    public void OnClickApproveButton()
    {
        Debug.Log("서류 승인 버튼 클릭됨! 채점을 시작합니다...");

        if (currentDocument.needsCensorship == true)
        {
            if (isAllMaskedCorrectly == true) Debug.Log("✅ [채점/정답] 모든 위험한 정보를 완벽히 가리고 승인했습니다!");
            else Debug.Log("❌ [채점/오답] 가려야 할 정보가 남아있는 채로 승인되었습니다!");
        }
        else
        {
            if (maskedWordIndices.Count == 0) Debug.Log("✅ [채점/정답] 정상적인 문서를 훼손 없이 승인했습니다!");
            else Debug.Log("❌ [채점/오답] 멀쩡한 정보를 임의로 훼손(조작)했습니다!");
        }

        // 승인 후 서류를 자동으로 치우고 다음 시간으로 넘깁니다.
        FindObjectOfType<GameManager>().GoToNextPhase();
    }
}