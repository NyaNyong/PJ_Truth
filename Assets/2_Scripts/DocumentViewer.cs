using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using TMPro;
using UnityEngine.EventSystems;
using Sirenix.OdinInspector;
using Obvious.Soap;

public class DocumentViewer : MonoBehaviour, IPointerClickHandler
{
    // ─────────────────────────────────────────
    // UI 연결
    // ─────────────────────────────────────────
    [BoxGroup("UI 연결")]
    [SerializeField] private TextMeshProUGUI titleText;

    [BoxGroup("UI 연결")]
    [SerializeField] private TextMeshProUGUI contentText;

    // ─────────────────────────────────────────
    // SOAP 연결
    // ─────────────────────────────────────────
    [BoxGroup("SOAP 연결")]
    [Tooltip("승인 버튼 클릭 시 Raise → GameManager.GoToNextPhase 호출됨")]
    [SerializeField] private ScriptableEventNoParam onApproveClicked;

    // ─────────────────────────────────────────
    // 내부 상태
    // ─────────────────────────────────────────
    [ReadOnly]
    [BoxGroup("현재 문서 (런타임 확인용)")]
    [SerializeField] private DocumentData currentDocument;

    private bool isAllMaskedCorrectly = false;
    private HashSet<int> maskedWordIndices = new HashSet<int>();

    // ─────────────────────────────────────────
    // 문서 표시
    // ─────────────────────────────────────────
    public void ShowDocument(DocumentData docData)
    {
        currentDocument = docData;
        titleText.text = docData.documentTitle;

        maskedWordIndices.Clear();
        isAllMaskedCorrectly = false;

        contentText.text = docData.mainText;
        contentText.ForceMeshUpdate();
    }

    // ─────────────────────────────────────────
    // 클릭 처리 (단어 마스킹)
    // ─────────────────────────────────────────
    public void OnPointerClick(PointerEventData eventData)
    {
        if (currentDocument == null) return;

        Camera uiCamera = Camera.main;
        int wordIndex = TMP_TextUtilities.FindIntersectingWord(
            contentText, eventData.position, uiCamera);

        if (wordIndex == -1) return;

        string clickedWord = contentText.textInfo.wordInfo[wordIndex].GetWord();
        bool isTargetWord = IsTargetKeyword(clickedWord);

        if (maskedWordIndices.Contains(wordIndex))
        {
            maskedWordIndices.Remove(wordIndex);
            Debug.Log(isTargetWord
                ? $"⚠️ [경고] 검열 단어 노출됨: {clickedWord}"
                : $"[안내] 일반 단어 지움: {clickedWord}");
        }
        else
        {
            maskedWordIndices.Add(wordIndex);
            Debug.Log(isTargetWord
                ? $"🎯 [적중] 검열 단어 가림: {clickedWord}"
                : $"[안내] 일반 단어 칠함: {clickedWord}");
        }

        UpdateTextDisplay();
        CheckAnswer();
    }

    // ─────────────────────────────────────────
    // 텍스트 렌더링 갱신
    // ─────────────────────────────────────────
    private void UpdateTextDisplay()
    {
        // 원본 텍스트로 리셋 후 마스킹 태그 삽입
        contentText.text = currentDocument.mainText;
        contentText.ForceMeshUpdate();

        string newText = currentDocument.mainText;

        // 뒤에서부터 삽입해야 인덱스가 밀리지 않음
        List<int> sortedIndices = maskedWordIndices.ToList();
        sortedIndices.Sort();
        sortedIndices.Reverse();

        foreach (int index in sortedIndices)
        {
            if (index >= contentText.textInfo.wordCount) continue;

            TMP_WordInfo wInfo = contentText.textInfo.wordInfo[index];
            int startChar = wInfo.firstCharacterIndex;
            int endChar   = wInfo.lastCharacterIndex;

            newText = newText.Insert(endChar + 1, "</color></mark>");
            newText = newText.Insert(startChar,   "<mark=#000000><color=#000000>");
        }

        contentText.text = newText;
    }

    // ─────────────────────────────────────────
    // 정답 채점
    // ─────────────────────────────────────────
    private void CheckAnswer()
    {
        if (!currentDocument.needsCensorship)
        {
            isAllMaskedCorrectly = false;
            return;
        }

        // 현재 마스킹된 단어 목록 수집
        List<string> maskedWords = new List<string>();
        foreach (int index in maskedWordIndices)
        {
            if (index < contentText.textInfo.wordCount)
                maskedWords.Add(contentText.textInfo.wordInfo[index].GetWord());
        }

        // 모든 타겟 키워드가 마스킹되었는지 확인
        isAllMaskedCorrectly = currentDocument.targetCensorKeywords.All(target =>
            maskedWords.Any(masked => masked.Contains(target)));
    }

    // ─────────────────────────────────────────
    // 승인 버튼
    // ─────────────────────────────────────────
    public void OnClickApproveButton()
    {
        Debug.Log("📋 서류 승인 버튼 클릭 — 채점 시작");

        if (currentDocument.needsCensorship)
        {
            Debug.Log(isAllMaskedCorrectly
                ? "✅ [정답] 모든 위험 정보를 완벽히 가리고 승인했습니다!"
                : "❌ [오답] 가려야 할 정보가 남아있는 채로 승인되었습니다!");
        }
        else
        {
            Debug.Log(maskedWordIndices.Count == 0
                ? "✅ [정답] 정상 문서를 훼손 없이 승인했습니다!"
                : "❌ [오답] 멀쩡한 정보를 임의로 훼손했습니다!");
        }

        // FindObjectOfType 대신 SOAP 이벤트로 신호 발행
        onApproveClicked?.Raise();
    }

    // ─────────────────────────────────────────
    // 유틸리티
    // ─────────────────────────────────────────
    private bool IsTargetKeyword(string word)
    {
        if (!currentDocument.needsCensorship) return false;

        return currentDocument.targetCensorKeywords
            .Any(target => word.Contains(target));
    }
}
