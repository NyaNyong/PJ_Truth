/// <summary>
/// 장소 이름과 지도 위 버튼 위치를 묶는 데이터 클래스.
/// NightPhaseManager → GameManager → NightMapUI 순서로 전달됩니다.
/// </summary>
using UnityEngine; // ★ 추가

public class LocationButtonInfo
{
    public string locationName;
    public string displayName;
    public string description; // ★ 팝업 한줄 설명
    public Vector2 buttonPosition;
    public bool isVisited;     // ★ 방문 여부
}
