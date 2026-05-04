using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "New Document", menuName = "TruthArchive/Document Data")]
public class DocumentData : ScriptableObject
{
    [Header("문서 기본 정보")]
    public int    documentID;
    public string documentTitle;

    [Header("문서 내용")]
    [TextArea(5, 10)]
    public string mainText;

    [Header("업무 지침 (가이드라인)")]
    [TextArea(3, 5)]
    public string guidelineText;

    [Header("블랙 마커 검열")]
    public bool         needsCensorship;
    public List<string> targetCensorKeywords;

    [Header("타자기 시스템")]
    public bool                     needsTypewriter;
    public List<TypewriterSlotData> typewriterSlots;
}

[System.Serializable]
public class TypewriterSlotData
{
    [Tooltip("슬롯 번호 (0부터 시작). mainText에서 ##SLOT0## 형태로 사용")]
    public int slotIndex;

    [Tooltip("문서에 원래 있던 단어 — 타자기로 덮어쓰기 전 기본 표시값")]
    public string originalWord;

    [Tooltip("플레이어가 선택할 수 있는 단어 카드 목록")]
    public List<string> wordOptions;

    [Tooltip("정답 단어 (wordOptions 중 하나)")]
    public string correctWord;

    [HideInInspector]
    public string insertedWord = "";
}
