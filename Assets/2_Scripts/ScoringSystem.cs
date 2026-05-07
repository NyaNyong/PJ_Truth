using UnityEngine;
using Sirenix.OdinInspector;

public class ScoringSystem : MonoBehaviour
{
    public static ScoringSystem Instance { get; private set; }

    [Header("등급 기준 (이상이면 해당 등급)")]
    [SerializeField] private float thresholdS = 90f;
    [SerializeField] private float thresholdA = 75f;
    [SerializeField] private float thresholdB = 60f;
    [SerializeField] private float thresholdC = 40f;

    [Header("KPI 설정")]
    [Tooltip("승급에 필요한 누적 KPI 총량")]
    [SerializeField] private float kpiMax = 500f;

    [Header("디버그 (Play 모드 전용)")]
    [SerializeField] private float debugKPIValue = 0f;

    [Button("KPI 강제 설정")]
    private void DebugSetKPI()
    {
        TotalKPI = Mathf.Clamp(debugKPIValue, 0f, kpiMax);
        Debug.Log($"[Debug] KPI 강제 설정: {TotalKPI}/{kpiMax} ({KPIProgress * 100f:F0}%)");
    }

    [Button("KPI 100%")]
    private void DebugSetKPIFull()
    {
        TotalKPI = kpiMax;
        Debug.Log("[Debug] KPI 100% 설정");
    }

    [Button("KPI 초기화")]
    private void DebugResetKPI()
    {
        TotalKPI = 0f;
        Debug.Log("[Debug] KPI 초기화");
    }


    public float TotalKPI { get; private set; } = 0f;
    public float KPIProgress => Mathf.Clamp01(TotalKPI / kpiMax);

    private DayScore lastScore;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    /// <summary>점수 계산, KPI 누적, DayScore 반환</summary>
    public int TotalProcessedDocuments { get; private set; } = 0;

    public DayScore CalculateAndRecord(float censorScore, float typewriterScore, int day)
    {
        TotalProcessedDocuments++; // ★ 추가
        float total = Mathf.Clamp(censorScore + typewriterScore, 0f, 100f);
        Grade grade = GetGrade(total);

        lastScore = new DayScore
        {
            dayNumber = day,
            censorScore = censorScore,
            typewriterScore = typewriterScore,
            totalScore = total,
            grade = grade
        };

        TotalKPI = Mathf.Clamp(TotalKPI + total, 0f, kpiMax);

        Debug.Log($"[Score] Day{day} — {total:F1}점 / {grade} / KPI: {TotalKPI:F0}/{kpiMax}");
        return lastScore;
    }


    public DayScore GetLastScore() => lastScore;

    private Grade GetGrade(float score)
    {
        if (score >= thresholdS) return Grade.S;
        if (score >= thresholdA) return Grade.A;
        if (score >= thresholdB) return Grade.B;
        if (score >= thresholdC) return Grade.C;
        return Grade.F;
    }
}

public enum Grade { S, A, B, C, F }

[System.Serializable]
public class DayScore
{
    public int dayNumber;
    public float censorScore;
    public float typewriterScore;
    public float totalScore;
    public Grade grade;
}