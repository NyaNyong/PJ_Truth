using UnityEngine;

/// <summary>
/// 씬 전환 시에도 유지되는 플레이어 기본 정보.
/// 대화창 초상화, 밤페이즈 캐릭터 스프라이트 교체에 사용됩니다.
/// </summary>
public class PlayerData : MonoBehaviour
{
    public static PlayerData Instance { get; private set; }

    public string PlayerName { get; private set; } = "";
    public bool IsMale { get; private set; } = true;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        transform.SetParent(null);        // ★ 추가
        DontDestroyOnLoad(gameObject);
    }

    public void SetData(string name, bool isMale)
    {
        PlayerName = string.IsNullOrWhiteSpace(name)
            ? (isMale ? "김도준" : "이수아")
            : name.Trim();
        IsMale = isMale;
        Debug.Log($"[PlayerData] 이름: {PlayerName}, 성별: {(IsMale ? "남" : "여")}");
    }

    // ─────────────────────────────────────────
    // ★ 전체 초기화 (재시도 시 "게임을 켠 상태"로 복원)
    public void ResetData()
    {
        PlayerName = "";
        IsMale = true;
        Debug.Log("[PlayerData] 초기화 완료 (이름/성별 클리어 — IDCardUI에서 재입력)");
    }
}