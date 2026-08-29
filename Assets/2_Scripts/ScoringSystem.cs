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

    [Header("성과금 지급 기준")]
    [SerializeField] private int bonusPayS = 2;
    [SerializeField] private int bonusPayA = 1;
    [SerializeField] private int bonusPayB = 1;
    [SerializeField] private int bonusPayC = 0;
    [SerializeField] private int bonusPayF = 0;

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
    public int BonusPay { get; private set; } = 0;
    public int TodayEarned { get; private set; } = 0; // ★ 오늘 획득 성과금


    private DayScore lastScore;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        transform.SetParent(null);        // ★ 추가
        DontDestroyOnLoad(gameObject);
        TotalKPI = kpiMax * 0.5f; // ★ 추가 — Stage4 시작 승급도 50%
    }

    public int TotalProcessedDocuments { get; private set; } = 0;


    public DayScore CalculateAndRecord(float censorScore, float typewriterScore, int day)
    {
        TodayEarned = 0; // ★ 매 호출마다 초기화
        TotalProcessedDocuments++;
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

        // ★ 튜토리얼 페이즈(Day 3)에서는 KPI 수치를 변경하지 않음
        if (day != 3)
        {
            TotalKPI = Mathf.Clamp(TotalKPI + total, 0f, kpiMax);
        }

        // 등급별 성과금 할당
        int earned = grade switch
        {
            Grade.S => bonusPayS,
            Grade.A => bonusPayA,
            Grade.B => bonusPayB,
            Grade.C => bonusPayC,
            _ => bonusPayF
        };

        // ★ UI 결과창 표기를 위해 획득량(TodayEarned)은 할당
        TodayEarned = earned;

        // ★ 실제 성과금 잔고 증가는 튜토리얼(Day 3)이 아닐 때만 적용
        if (earned > 0 && day != 3)
        {
            EarnBonusPay(earned);
        }

        Debug.Log($"[Score] Day{day} — {total:F1}점 / {grade} / KPI: {TotalKPI:F0}/{kpiMax}");
        return lastScore;
    }

    // ★ 성과금 획득
    public void EarnBonusPay(int amount)
    {
        BonusPay += amount;
        Debug.Log($"[성과금] +{amount} → 잔액: {BonusPay}");
    }

    // ★ 성과금 소비 (잔액 부족 시 false 반환)
    public bool SpendBonusPay(int amount)
    {
        if (BonusPay < amount) return false;
        BonusPay -= amount;
        Debug.Log($"[성과금] -{amount} → 잔액: {BonusPay}");
        return true;
    }

    public DayScore GetLastScore() => lastScore;

    // ─────────────────────────────────────────
    // ★ 전체 초기화 (재시도 시 "게임을 켠 상태"로 복원)
    public void ResetAll()
    {
        TotalKPI = kpiMax * 0.5f;
        BonusPay = 0;
        TodayEarned = 0;
        TotalProcessedDocuments = 0;
        lastScore = null;
        Debug.Log("[ScoringSystem] 전체 초기화 완료 (승급도 50%로 시작)");
    }

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