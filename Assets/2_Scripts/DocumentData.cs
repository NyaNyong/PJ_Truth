using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// 이 코드가 있으면 유니티 우클릭 메뉴에서 이 데이터를 붕어빵처럼 찍어낼 수 있습니다.
[CreateAssetMenu(fileName = "New Document", menuName = "TruthArchive/Document Data")]
public class DocumentData : ScriptableObject
{
    [Header("문서 기본 정보")]
    public int documentID;          // 문서 고유 번호 (예: 101)
    public string documentTitle;    // 문서 제목 (예: "이송자 명단", "사설 뉴스 기사")

    [Header("문서 내용")]
    [TextArea(5, 10)] // 에디터에서 글을 길게 쓸 수 있도록 칸을 넓혀줍니다.
    public string mainText;         // 문서의 본문 내용

    [Header("검열 및 판별 시스템")]
    public bool isFakeInfo;         // 허위/조작된 정보인가? (True면 반려해야 함)
    public string requiredKeyword;  // 승인/반려를 결정짓는 핵심 키워드 (예: "실종", "이송")
}