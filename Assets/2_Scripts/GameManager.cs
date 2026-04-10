using UnityEngine;
using System.Collections.Generic;
using TMPro; // 🌟 TextMeshPro를 사용하기 위해 추가

public class GameManager : MonoBehaviour
{
    public enum GamePhase { Morning, Day, Night }

    [Header("게임 진행 상태")]
    public GamePhase currentPhase;
    public int currentDay = 1;

    [Header("연결할 UI 오브젝트 (서류)")]
    public GameObject documentPanel;
    public DocumentViewer documentViewer;

    [Header("연결할 UI 오브젝트 (가이드라인)")]
    public GameObject guidelineButton;      // 화면 구석의 [가이드라인] 열기 버튼
    public GameObject guidelinePanel;       // 가이드라인 내용이 뜰 배경 패널
    public TextMeshProUGUI guidelineTextUI; // 가이드라인 글씨

    [Header("문서 데이터베이스")]
    public List<DocumentData> dailyDocuments;

    void Start()
    {
        StartDay(1);
    }

    public void StartDay(int day)
    {
        currentDay = day;
        Debug.Log($"===== [ {currentDay}일 차 시작 ] =====");
        ChangePhase(GamePhase.Morning);
    }

    public void ChangePhase(GamePhase newPhase)
    {
        currentPhase = newPhase;

        // 페이즈가 바뀔 때 기본적으로 서류와 가이드라인 UI를 모두 끕니다.
        if (documentPanel != null) documentPanel.SetActive(false);
        if (guidelineButton != null) guidelineButton.SetActive(false);
        if (guidelinePanel != null) guidelinePanel.SetActive(false);

        switch (currentPhase)
        {
            case GamePhase.Morning:
                break;

            case GamePhase.Day:
                // 🌟 낮이 되면 서류 패널과 가이드라인 '버튼'을 켭니다. (패널은 아직 닫혀있음)
                if (documentPanel != null) documentPanel.SetActive(true);
                if (guidelineButton != null) guidelineButton.SetActive(true);

                int documentIndex = currentDay - 1;
                if (documentIndex < dailyDocuments.Count)
                {
                    DocumentData todaysData = dailyDocuments[documentIndex];

                    // 서류 내용 업데이트
                    if (documentViewer != null) documentViewer.ShowDocument(todaysData);

                    // 🌟 가이드라인 내용 업데이트
                    if (guidelineTextUI != null) guidelineTextUI.text = todaysData.guidelineText;
                }
                break;

            case GamePhase.Night:
                break;
        }
    }

    // 🌟 추가된 기능: 가이드라인 버튼을 누를 때마다 껐다 켰다(Toggle) 해주는 함수
    public void OnClickGuidelineToggle()
    {
        if (guidelinePanel != null)
        {
            // 현재 패널이 켜져 있으면 끄고, 꺼져 있으면 켭니다.
            bool isCurrentlyActive = guidelinePanel.activeSelf;
            guidelinePanel.SetActive(!isCurrentlyActive);
        }
    }

    public void GoToNextPhase()
    {
        if (currentPhase == GamePhase.Morning) ChangePhase(GamePhase.Day);
        else if (currentPhase == GamePhase.Day) ChangePhase(GamePhase.Night);
        else if (currentPhase == GamePhase.Night) StartDay(currentDay + 1);
    }
}