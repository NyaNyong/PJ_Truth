using System.Collections.Generic;

// ═══════════════════════════════════════════
// day_XX.json 전체 구조
// ═══════════════════════════════════════════

[System.Serializable]
public class DayTextData
{
    public int day;
    public bool requireAllLocations; // ★ 추가
    public NewsData news;
    public List<DocumentTextData> documents;
    public List<LocationTextData> locations;
    public List<string> availableLocations;
    public List<ConditionalLocationData> conditionalLocations;
    public WhiteboardData whiteboard;
    public string nextStageHint;                                      // ★
    public List<ConditionalNextStageHintData> conditionalNextStageHints; // ★
    public bool skipWhiteboard; // ★ true면 밤 종료 후 화이트보드 건너뜀
    public List<string> nextStageDialogue; // ★ 기본 대사 라인
}

// ─── 뉴스 ────────────────────────────────────
[System.Serializable]
public class NewsData
{
    public string officialTitle;
    public string officialContent;
    public string privateTitle;
    public string privateContent;
    public List<ConditionalNewsData> conditionalOverrides;
}

[System.Serializable]
public class ConditionalNewsData
{
    public ConditionData condition;
    public string overrideOfficialTitle;
    public string overrideOfficialContent;
    public string overridePrivateTitle;
    public string overridePrivateContent;
}

// ─── 조건 ────────────────────────────────────
[System.Serializable]
public class ConditionData
{
    // type: "hasFlag" / "hasClue" / "afterDay" / "always"
    public string type;
    public string value;
}

// ─── 조건부 대사 (NPC firstLines / 선택지 lines 공용) ──────
[System.Serializable]
public class ConditionalLinesData
{
    public string requiredFlag;   // 이 플래그가 세워져 있으면 아래 lines 사용
    public List<string> lines;
}

// ─── 문서 ────────────────────────────────────
[System.Serializable]
public class DocumentTextData
{
    public int id;
    public string title;
    public string mainText;
    public string guidelineText;
    public bool needsCensorship;
    public bool needsTypewriter;
    public List<string> censorKeywords;
    public List<string> criticalCensorKeywords; // ★ 등급 판정용 핵심 키워드
    public List<TypewriterSlotTextData> typewriterSlots;
}

[System.Serializable]
public class TypewriterSlotTextData
{
    public int slotIndex;
    public string originalWord;
    public List<string> wordOptions;
    public string correctWord;
}

// ─── 장소별 NPC/단서 ──────────────────────────
[System.Serializable]
public class LocationTextData
{
    public string locationID;
    public string description;
    public List<NpcTextData> npcs;
    public List<ClueTextData> clues;
}

[System.Serializable]
public class NpcTextData
{
    public string id;
    public string npcName;
    public List<string> firstLines;
    public List<string> repeatLines;
    public string grantClueID;
    public string setFlag;
    public List<DialogueChoiceData> choices;
    public List<ConditionalLinesData> conditionalFirstLines;
    public List<ConditionalChoiceLineData> conditionalChoiceLines; // ★ 교체
}

[System.Serializable]
public class ClueTextData
{
    public string id;
    public string title;
    public List<string> lines;
    public bool isPuzzle;
    public string hiddenContent;
    public string correctAnswer;
    public string flagIDOnSolve;
}

// ─── 대화 선택지 ──────────────────────────────
[System.Serializable]
public class DialogueChoiceData
{
    public string label;
    public List<string> lines;
    public string grantClueID;
    public string setFlag;
    public string blockIfFlag;          // ★ 이 플래그가 세워져 있으면 선택지 비활성
    public int costBonusPay; // ★ 필요 성과금 (0이면 무료)
    public bool isExitChoice; // ★ 대화/조사 종료 선택지
    // conditionalLines 삭제
    public bool isUniqueChoice;                           // ★ 1회 선택 후 영구 비활성
    [System.NonSerialized] public int runtimeIndex = -1;  // ★ 런타임 인덱스 (JSON 비직렬화)
}

[System.Serializable]
public class ConditionalChoiceLineData
{
    public int choiceIndex;  // choices 배열 인덱스
    public string requiredFlag;
    public List<string> lines;
}

// ─── 조건부 장소 해금 ────────────────────────
[System.Serializable]
public class ConditionalLocationData
{
    public string locationName;
    public ConditionData condition;
}

// ─── 화이트보드 ──────────────────────────────
[System.Serializable]
public class WhiteboardData
{
    public List<WhiteboardCardData> cards;
    public List<WhiteboardConnectionData> correctConnections;
}

[System.Serializable]
public class WhiteboardCardData
{
    public string cardID;
    public string requiredClueID;
    public string title;
    public string content;
}

[System.Serializable]
public class WhiteboardConnectionData
{
    public string fromCardID;
    public string toCardID;
    public List<string> cardIDs;
    public string revealText;
}

[System.Serializable]
public class ConditionalNextStageHintData
{
    public string requiredFlag;
    public string hint;
    public List<string> dialogue; // ★ 조건부 대사 라인
}