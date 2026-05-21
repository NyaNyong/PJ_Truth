using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "New Daily Data", menuName = "TruthArchive/Daily Data")]
public class DailyData : ScriptableObject
{
    [Header("기본 정보")]
    public int dayNumber;

    [Header("아침 페이즈 - 뉴스")]
    public string officialNewsTitle;
    [TextArea(5, 10)] public string officialNewsContent;

    public bool hasPrivateNews;
    public string privateNewsTitle;
    [TextArea(5, 10)] public string privateNewsContent;

    [Header("낮 페이즈 - 검열 서류")]
    [Tooltip("오늘 처리할 서류")]
    public DocumentData documentToProcess;

    [Header("밤 페이즈 - 항상 열린 장소")]
    [Tooltip("조건 없이 항상 지도에 표시되는 장소")]
    public List<string> availableLocations;

    [Header("밤 페이즈 설정")]
    [Tooltip("true면 모든 장소 방문 후에만 화이트보드로 진행")]
    public bool requireAllLocations = false;

    [Header("밤 페이즈 - 조건부 장소")]
    [Tooltip("조건을 충족해야만 지도에 표시되는 장소")]
    public List<ConditionalLocation> conditionalLocations;
}
