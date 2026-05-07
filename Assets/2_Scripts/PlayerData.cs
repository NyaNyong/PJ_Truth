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
        DontDestroyOnLoad(gameObject);
    }

    public void SetData(string name, bool isMale)
    {
        PlayerName = string.IsNullOrWhiteSpace(name)
            ? (isMale ? "김도준" : "이수아") // 이름 미입력 시 기본값
            : name.Trim();
        IsMale = isMale;
        Debug.Log($"[PlayerData] 이름: {PlayerName}, 성별: {(IsMale ? "남" : "여")}");
    }
}