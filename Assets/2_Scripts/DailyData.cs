using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "New Daily Data", menuName = "TruthArchive/Daily Data")]
public class DailyData : ScriptableObject
{
    [Header("기본 정보")]
    public int dayNumber; // 며칠 차(스테이지)인가?

    [Header("아침 페이즈 (뉴스 보도)")]
    public string officialNewsTitle;
    [TextArea(5, 10)] public string officialNewsContent;

    public bool hasPrivateNews; // 오늘 사설 뉴스가 등장하는가?
    public string privateNewsTitle;
    [TextArea(5, 10)] public string privateNewsContent;

    [Header("낮 페이즈 (검열 업무)")]
    [Tooltip("오늘 처리해야 할 사건 일지(서류) 데이터")]
    public DocumentData documentToProcess; // (만약 하루에 서류가 여러 개라면 List<DocumentData>로 변경 가능)

    [Header("밤 페이즈 (이동 가능 장소)")]
    [Tooltip("오늘 밤 지도(Map)에 표시될 장소 이름들")]
    public List<string> availableLocations;
}