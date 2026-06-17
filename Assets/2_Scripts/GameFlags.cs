using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 게임 전체에서 공유되는 플래그(단서 획득, 이벤트 발생 등)를 관리합니다.
/// 싱글톤 패턴 — 씬에 하나만 존재합니다.
/// </summary>
public class GameFlags : MonoBehaviour
{
    public static GameFlags Instance { get; private set; }

    // 획득한 단서 ID 목록
    private HashSet<string> collectedClues = new HashSet<string>();

    // 발생한 이벤트 플래그 목록
    private HashSet<string> triggeredFlags = new HashSet<string>();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        transform.SetParent(null);        // ★ 추가
        DontDestroyOnLoad(gameObject);
    }

    // ─────────────────────────────────────────
    // 단서 관련
    public void AddClue(string clueID)
    {
        collectedClues.Add(clueID);
        triggeredFlags.Add(clueID); // ★ 추가 — clue 획득 시 동일 ID로 flag도 세팅
        Debug.Log($"🔍 단서 획득: {clueID}");
    }

    public bool HasClue(string clueID) => collectedClues.Contains(clueID);

    // ─────────────────────────────────────────
    // 플래그 관련
    public void SetFlag(string flagID)
    {
        triggeredFlags.Add(flagID);
        Debug.Log($"🚩 플래그 설정: {flagID}");
    }
    public bool HasFlag(string flagID) => triggeredFlags.Contains(flagID);
    public void RemoveFlag(string flagID)
    {
        triggeredFlags.Remove(flagID);
        Debug.Log($"🚩 플래그 해제: {flagID}");
    }

    private List<string> collectedTruths = new List<string>();

    public void AddTruth(string truth)
    {
        if (!collectedTruths.Contains(truth)) collectedTruths.Add(truth);
        Debug.Log($"📖 진실 수집: {truth}");
    }
    public List<string> GetAllTruths() => new List<string>(collectedTruths);
}
