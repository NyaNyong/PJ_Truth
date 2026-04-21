/// <summary>
/// 장소 이름과 지도 위 버튼 위치를 묶는 데이터 클래스.
/// NightPhaseManager → GameManager → NightMapUI 순서로 전달됩니다.
/// </summary>
public class LocationButtonInfo
{
    public string  locationName;
    public UnityEngine.Vector2 buttonPosition; // ButtonContainer 기준 앵커 좌표
}
