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

    // ─── 블랙 마커 ───────────────────────────
    [Header("블랙 마커 검열")]
    public bool         needsCensorship;
    public List<string> targetCensorKeywords;

    // ─── 타자기 ───────────────────────────────
    [Header("타자기 시스템")]
    [Tooltip("타자기 시스템을 사용하는 문서인지 여부")]
    public bool needsTypewriter;

    [Tooltip("타자기로 삽입할 슬롯 목록. 순서대로 문서 내 [SLOT] 태그와 매핑됩니다.")]
    public List<TypewriterSlotData> typewriterSlots;
}

// ─────────────────────────────────────────────────
// 타자기 슬롯 하나의 데이터
// 문서 mainText에 [SLOT_0], [SLOT_1] ... 형태로 표시됩니다
// ─────────────────────────────────────────────────
[System.Serializable]
public class TypewriterSlotData
{
    [Tooltip("슬롯 식별 번호 (0부터 시작)")]
    public int slotIndex;

    [Tooltip("플레이어가 선택할 수 있는 단어 카드 목록")]
    public List<string> wordOptions;

    [Tooltip("정답 단어 (wordOptions 중 하나와 일치해야 함)")]
    public string correctWord;

    [HideInInspector]
    public string insertedWord = ""; // 런타임에 채워집니다
}
