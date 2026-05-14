using System.Collections.Generic;

// ═══════════════════════════════════════════
// day_XX.json 전체 구조
// ═══════════════════════════════════════════

[System.Serializable]
public class DayTextData
{
    public int day;
    public NewsData news;
    public List<DocumentTextData> documents;          // ★ 단수→복수
    public List<LocationTextData> locations;          // ★ 맵별 NPC/단서
    public List<string> availableLocations; // ★ SO에서 이동
    public List<ConditionalLocationData> conditionalLocations; // ★ SO에서 이동
    public WhiteboardData whiteboard;         // ★ 신규
}

// ─── 뉴스 ────────────────────────────────────
[System.Serializable]
public class NewsData
{
    public string officialTitle;
    public string officialContent;
    public string privateTitle;
    public string privateContent;
    public List<ConditionalNewsData> conditionalOverrides; // ★ 조건부 뉴스
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
    public string value;  // flagID, clueID, 또는 일수
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
    public string locationID; // NightPhaseManager.locationName과 일치
    public List<NpcTextData> npcs;
    public List<ClueTextData> clues;
}

// NpcTextData 내부에 추가
[System.Serializable]
public class NpcTextData
{
    public string id;
    public string npcName;
    public List<string> firstLines;
    public List<string> repeatLines;
    public string grantClueID;
    public string setFlag;
    public List<DialogueChoiceData> choices; // ★ 신규: null/empty면 기존 방식
}

[System.Serializable]
public class ClueTextData
{
    public string id;
    public string title;
    public List<string> lines;
}

// ─── 대화 선택지 ──────────────────────────────
[System.Serializable]
public class DialogueChoiceData
{
    public string label;           // 버튼에 표시될 텍스트
    public List<string> lines;     // 선택 후 이어지는 대사
    public string grantClueID;     // 선택 시 획득 단서 (없으면 빈칸)
    public string setFlag;         // 선택 시 세울 플래그 (없으면 빈칸)
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
    public string requiredClueID; // 비어있으면 항상 표시, 있으면 단서 수집 후 표시
    public string title;
    public string content;
}

[System.Serializable]
public class WhiteboardConnectionData
{
    public string fromCardID;
    public string toCardID;
    public string revealText; // 정답 연결 시 해금되는 진실 텍스트
}