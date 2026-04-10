using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "New Document", menuName = "TruthArchive/Document Data")]
public class DocumentData : ScriptableObject
{
    [Header("문서 기본 정보")]
    public int documentID;
    public string documentTitle;

    [Header("문서 내용")]
    [TextArea(5, 10)]
    public string mainText;

    [Header("검열(마스킹) 시스템")]
    public bool needsCensorship;
    public List<string> targetCensorKeywords;

    // 🌟 추가된 부분: 이 문서(해당 일차)를 처리할 때 볼 가이드라인 내용
    [Header("업무 지침 (가이드라인)")]
    [TextArea(3, 5)]
    public string guidelineText;
}