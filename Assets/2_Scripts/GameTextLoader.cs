using UnityEngine;
using System.IO;
using System.Collections.Generic;

public class GameTextLoader : MonoBehaviour
{
    public static GameTextLoader Instance { get; private set; }

    private DayTextData currentData;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    // ── 로드 ────────────────────────────────
    public void LoadDay(int day)
    {
        string fileName = $"day_{day:D2}.json";
        string path = Path.Combine(Application.streamingAssetsPath, "GameData", fileName);

        if (!File.Exists(path))
        {
            Debug.LogWarning($"[GameTextLoader] 파일 없음: {path} — Inspector 값을 폴백으로 사용합니다.");
            currentData = null;
            return;
        }

        string json = File.ReadAllText(path, System.Text.Encoding.UTF8);
        currentData = JsonUtility.FromJson<DayTextData>(json);
        Debug.Log($"[GameTextLoader] day_{day:D2}.json 로드 완료");
    }

    // ── 조건 평가 ────────────────────────────
    private bool EvaluateCondition(ConditionData cond)
    {
        if (cond == null) return false;
        switch (cond.type)
        {
            case "always": return true;
            case "hasFlag": return GameFlags.Instance != null && GameFlags.Instance.HasFlag(cond.value);
            case "hasClue": return GameFlags.Instance != null && GameFlags.Instance.HasClue(cond.value);
            case "afterDay": return int.TryParse(cond.value, out int d) && currentData?.day >= d;
            default: return false;
        }
    }

    // ── 조회 ────────────────────────────────
    public NewsData GetNews()
    {
        if (currentData?.news == null) return null;
        var news = currentData.news;

        if (news.conditionalOverrides != null)
        {
            foreach (var ovr in news.conditionalOverrides)
            {
                if (!EvaluateCondition(ovr.condition)) continue;
                if (!string.IsNullOrEmpty(ovr.overrideOfficialTitle))
                    news.officialTitle = ovr.overrideOfficialTitle;
                if (!string.IsNullOrEmpty(ovr.overrideOfficialContent))
                    news.officialContent = ovr.overrideOfficialContent;
                if (!string.IsNullOrEmpty(ovr.overridePrivateTitle))
                    news.privateTitle = ovr.overridePrivateTitle;
                if (!string.IsNullOrEmpty(ovr.overridePrivateContent))
                    news.privateContent = ovr.overridePrivateContent;
            }
        }
        return news;
    }

    public DocumentTextData GetDocument(int documentID) =>
        currentData?.documents?.Find(d => d.id == documentID);

    public NpcTextData GetNpc(string npcID)
    {
        if (currentData?.locations == null) return null;
        foreach (var loc in currentData.locations)
        {
            var found = loc.npcs?.Find(n => n.id == npcID);
            if (found != null) return found;
        }
        return null;
    }

    public ClueTextData GetClue(string clueID)
    {
        if (currentData?.locations == null) return null;
        foreach (var loc in currentData.locations)
        {
            var found = loc.clues?.Find(c => c.id == clueID);
            if (found != null) return found;
        }
        return null;
    }

    public string GetLocationDescription(string locationID)
    {
        var loc = currentData?.locations?.Find(l => l.locationID == locationID);
        return loc?.description ?? "";
    }

    public WhiteboardData GetWhiteboard() => currentData?.whiteboard;
    public bool GetSkipWhiteboard() => currentData?.skipWhiteboard ?? false; // ★
    public string GetNextStageHint()
    {
        if (currentData == null) return "";

        // 조건부 암시 우선 (censor_heavy/mild/weak 등)
        if (currentData.conditionalNextStageHints != null && GameFlags.Instance != null)
        {
            foreach (var c in currentData.conditionalNextStageHints)
            {
                if (!string.IsNullOrEmpty(c.requiredFlag) &&
                    GameFlags.Instance.HasFlag(c.requiredFlag))
                    return c.hint;
            }
        }

        return currentData.nextStageHint ?? "";
    }

    public List<string> GetNextStageDialogue()
    {
        if (currentData == null) return null;
        foreach (var c in currentData.conditionalNextStageHints)
        {
            if (c.dialogue != null && c.dialogue.Count > 0 &&
                GameFlags.Instance?.HasFlag(c.requiredFlag) == true)
                return c.dialogue;
        }
        return currentData.nextStageDialogue?.Count > 0
            ? currentData.nextStageDialogue : null;
    }

    public List<string> GetAvailableLocations() => currentData?.availableLocations;

    public List<ConditionalLocationData> GetConditionalLocations() =>
        currentData?.conditionalLocations;

    // ── 주입 ────────────────────────────────
    public void InjectIntoDaily(DailyData data)
    {
        if (currentData == null || data == null) return;

        var n = GetNews();
        if (n != null)
        {
            data.officialNewsTitle = n.officialTitle;
            data.officialNewsContent = n.officialContent;
            data.hasPrivateNews = !string.IsNullOrEmpty(n.privateTitle);
            data.privateNewsTitle = n.privateTitle;
            data.privateNewsContent = n.privateContent;
        }

        if (currentData.availableLocations?.Count > 0)
            data.availableLocations = currentData.availableLocations;

        // ★ requireAllLocations 주입
        data.requireAllLocations = currentData.requireAllLocations;

        // ★ conditionalLocations 주입 — ConditionalLocationData → ConditionalLocation 변환
        if (currentData.conditionalLocations?.Count > 0)
        {
            data.conditionalLocations = new System.Collections.Generic.List<ConditionalLocation>();
            foreach (var src in currentData.conditionalLocations)
            {
                var entry = new ConditionalLocation { locationName = src.locationName };

                if (src.condition != null)
                {
                    switch (src.condition.type)
                    {
                        case "always":
                            entry.condition = UnlockCondition.Always;
                            break;
                        case "hasFlag":
                            entry.condition = UnlockCondition.RequiresFlag;
                            entry.requiredFlagID = src.condition.value;
                            break;
                        case "hasClue":
                            entry.condition = UnlockCondition.RequiresClue;
                            entry.requiredClueID = src.condition.value;
                            break;
                        case "afterDay":
                            entry.condition = UnlockCondition.AfterDay;
                            int.TryParse(src.condition.value, out entry.requiredDay);
                            break;
                        default:
                            entry.condition = UnlockCondition.Always;
                            break;
                    }
                }

                data.conditionalLocations.Add(entry);
            }
        }
    }

    public void InjectIntoDocument(DocumentData data)
    {
        if (currentData == null || data == null) return;
        var d = GetDocument(data.documentID);
        if (d == null) return;

        data.documentTitle = d.title;
        data.mainText = d.mainText;
        data.guidelineText = d.guidelineText;
        data.needsCensorship = d.needsCensorship;
        data.needsTypewriter = d.needsTypewriter;
        data.targetCensorKeywords = d.censorKeywords ?? new List<string>();
        data.criticalCensorKeywords = d.criticalCensorKeywords ?? new List<string>(); // ★

        if (d.typewriterSlots != null && d.typewriterSlots.Count > 0)
        {
            data.typewriterSlots = new List<TypewriterSlotData>();
            foreach (var s in d.typewriterSlots)
            {
                data.typewriterSlots.Add(new TypewriterSlotData
                {
                    slotIndex = s.slotIndex,
                    originalWord = s.originalWord,
                    wordOptions = new List<string>(s.wordOptions ?? new List<string>()),
                    correctWord = s.correctWord,
                    insertedWord = ""
                });
            }
        }
    }
}