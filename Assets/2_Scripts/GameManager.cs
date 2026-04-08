using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    // 1. 게임의 3가지 주요 상태(페이즈)를 정의합니다.
    public enum GamePhase
    {
        Morning, // 아침: 결과 및 사설 뉴스 확인
        Day,     // 낮: 공식 검열 업무 수행
        Night    // 밤: 단서 탐색 및 행동 선택
    }

    [Header("게임 진행 상태")]
    public GamePhase currentPhase;
    public int currentDay = 1; // 현재 며칠 차(스테이지)인지 추적

    void Start()
    {
        // 게임이 켜지면 1일 차 아침부터 시작합니다.
        StartDay(1);
    }

    // 새로운 날을 시작하는 함수
    public void StartDay(int day)
    {
        currentDay = day;
        Debug.Log($"===== [ {currentDay}일 차 시작 ] =====");

        // 하루의 시작은 무조건 아침입니다.
        ChangePhase(GamePhase.Morning);
    }

    // 상태(아침/낮/밤)를 변경하고 그에 맞는 이벤트를 실행하는 핵심 함수
    public void ChangePhase(GamePhase newPhase)
    {
        currentPhase = newPhase;

        switch (currentPhase)
        {
            case GamePhase.Morning:
                Debug.Log("☀️ [아침]: 전날의 결과와 새로운 사설 뉴스를 출력합니다.");
                // TODO: 아침 UI 켜기 (다른 UI 끄기)
                break;

            case GamePhase.Day:
                Debug.Log("💼 [낮]: 기준표를 받고 정보 검열 업무를 시작합니다.");
                // TODO: 서류 검열 UI 켜기
                break;

            case GamePhase.Night:
                Debug.Log("🌙 [밤]: 퇴근했습니다. 얻은 단서를 바탕으로 행동을 선택합니다.");
                // TODO: 밤 선택지 UI 켜기
                break;
        }
    }

    // 다음 페이즈로 넘어가는 버튼용 함수 (나중에 UI 버튼과 연결)
    public void GoToNextPhase()
    {
        if (currentPhase == GamePhase.Morning)
        {
            ChangePhase(GamePhase.Day);
        }
        else if (currentPhase == GamePhase.Day)
        {
            ChangePhase(GamePhase.Night);
        }
        else if (currentPhase == GamePhase.Night)
        {
            // 밤이 끝나면 다음 날(Day + 1) 아침으로 넘어갑니다.
            StartDay(currentDay + 1);
        }
    }
}