using UnityEngine;
using System.Collections.Generic;

public class LocationUnlockManager : MonoBehaviour
{
    public static LocationUnlockManager Instance { get; private set; }

    private List<string> runtimeUnlockedLocations = new List<string>();

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    public List<string> GetAvailableLocations(DailyData todaysData, int currentDay)
    {
        var result = new List<string>();
        if (todaysData == null) return result;

        if (todaysData.availableLocations != null)
            result.AddRange(todaysData.availableLocations);

        if (todaysData.conditionalLocations != null)
            foreach (var entry in todaysData.conditionalLocations)
                if (IsConditionMet(entry, currentDay))
                    result.Add(entry.locationName);

        foreach (var loc in runtimeUnlockedLocations)
            if (!result.Contains(loc))
                result.Add(loc);

        return result;
    }

    private bool IsConditionMet(ConditionalLocation entry, int currentDay)
    {
        switch (entry.condition)
        {
            case UnlockCondition.Always:       return true;
            case UnlockCondition.AfterDay:     return currentDay >= entry.requiredDay;
            case UnlockCondition.RequiresClue: return GameFlags.Instance != null && GameFlags.Instance.HasClue(entry.requiredClueID);
            case UnlockCondition.RequiresFlag: return GameFlags.Instance != null && GameFlags.Instance.HasFlag(entry.requiredFlagID);
            default: return false;
        }
    }

    public void UnlockLocationNow(string locationName)
    {
        if (!runtimeUnlockedLocations.Contains(locationName))
        {
            runtimeUnlockedLocations.Add(locationName);
            Debug.Log($"🗺️ 장소 즉시 해금: {locationName}");
        }
    }

    public void ClearRuntimeLocations() => runtimeUnlockedLocations.Clear();
}

// ─────────────────────────────────────────────────
// 조건 타입 열거형
// ─────────────────────────────────────────────────
public enum UnlockCondition
{
    Always,
    AfterDay,
    RequiresClue,
    RequiresFlag
}

// ─────────────────────────────────────────────────
// 조건부 장소 데이터 클래스
// DailyData.conditionalLocations 리스트에서 사용됩니다
// ─────────────────────────────────────────────────
[System.Serializable]
public class ConditionalLocation
{
    [Tooltip("장소 이름 — NightPhaseManager의 LocationMaps와 일치해야 합니다")]
    public string locationName;

    public UnlockCondition condition;

    [Tooltip("AfterDay 조건일 때 사용")]
    public int requiredDay;

    [Tooltip("RequiresClue 조건일 때 사용 — ClueObject의 Clue ID와 일치해야 합니다")]
    public string requiredClueID;

    [Tooltip("RequiresFlag 조건일 때 사용")]
    public string requiredFlagID;
}
