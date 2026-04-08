using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro; // TextMeshPro를 사용하기 위해 반드시 추가해야 하는 줄입니다!

public class DocumentViewer : MonoBehaviour
{
    [Header("연결할 UI 텍스트")]
    public TextMeshProUGUI titleText;
    public TextMeshProUGUI contentText;

    [Header("현재 표시할 문서 데이터")]
    public DocumentData currentDocument;

    // 게임이 시작될 때 문서를 화면에 띄웁니다.
    void Start()
    {
        if (currentDocument != null)
        {
            ShowDocument(currentDocument);
        }
    }

    // 문서 데이터를 받아서 UI에 글씨를 써주는 핵심 함수
    public void ShowDocument(DocumentData docData)
    {
        currentDocument = docData;

        // UI 텍스트의 내용을 데이터에 적힌 글씨로 바꿉니다.
        titleText.text = docData.documentTitle;
        contentText.text = docData.mainText;
    }
}