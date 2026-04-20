using UnityEngine;
using System.Collections.Generic;
using TMPro;
using DG.Tweening;
using Sirenix.OdinInspector;

// SOAP 네임스페이스 — 실제 임포트 경로가 다를 경우 수정하세요
using Obvious.Soap;

public class GameManager : MonoBehaviour
{
    public enum GamePhase { Morning, Day, Night }

    // ─────────────────────────────────────────
    // 게임 진행 상태
    // ─────────────────────────────────────────
    [BoxGroup("게임 진행 상태")]
    public GamePhase currentPhase;

    [BoxGroup("게임 진행 상태")]
    public int currentDay = 1;

    // ─────────────────────────────────────────
    // SOAP 에셋 연결
    // ─────────────────────────────────────────
    [BoxGroup("SOAP / Variables")]
    [SerializeField] private IntVariable soapCurrentDay;

    [BoxGroup("SOAP / Events (구독)")]
    [Tooltip("DocumentViewer의 승인 버튼 → 다음 페이즈 요청")]
    [SerializeField] private ScriptableEventNoParam onApproveClicked;

    [BoxGroup("SOAP / Events (구독)")]
    [Tooltip("NightMapUI의 장소 선택 완료 → 밤 페이즈 탐색 시작")]
    [SerializeField] private ScriptableEventNoParam onLocationSelected;

    // ─────────────────────────────────────────
    // UI 오브젝트 — 낮 페이즈
    // ─────────────────────────────────────────
    [BoxGroup("UI / 낮 페이즈")]
    [SerializeField] private CanvasGroup documentPanelCG;

    [BoxGroup("UI / 낮 페이즈")]
    [SerializeField] private CanvasGroup guidelinePanelCG;

    [BoxGroup("UI / 낮 페이즈")]
    [SerializeField] private GameObject guidelineButton;

    [BoxGroup("UI / 낮 페이즈")]
    [SerializeField] private TextMeshProUGUI guidelineTextUI;

    [BoxGroup("UI / 낮 페이즈")]
    [SerializeField] private DocumentViewer documentViewer;

    // ─────────────────────────────────────────
    // UI 오브젝트 — 아침 페이즈
    // ─────────────────────────────────────────
    [BoxGroup("UI / 아침 페이즈")]
    [SerializeField] private CanvasGroup morningNewsPanelCG;

    [BoxGroup("UI / 아침 페이즈")]
    [SerializeField] private TextMeshProUGUI officialNewsTitleUI;

    [BoxGroup("UI / 아침 페이즈")]
    [SerializeField] private TextMeshProUGUI officialNewsContentUI;

    // ─────────────────────────────────────────
    // UI 오브젝트 — 밤 페이즈 (지도 선택)
    // ─────────────────────────────────────────
    [BoxGroup("UI / 밤 페이즈")]
    [SerializeField] private NightMapUI nightMapUI;

    // ─────────────────────────────────────────
    // 스테이지 데이터
    // ─────────────────────────────────────────
    [BoxGroup("스테이지 데이터")]
    [TableList(ShowIndexLabels = true)]
    [SerializeField] private List<DailyData> dailyDataList;

    // ─────────────────────────────────────────
    // DOTween 설정
    // ─────────────────────────────────────────
    [BoxGroup("DOTween 설정")]
    [SerializeField] private float panelFadeDuration = 0.35f;

    // ─────────────────────────────────────────
    // 내부 상태
    // ─────────────────────────────────────────
    private DailyData todaysData;

    // ─────────────────────────────────────────
    // 라이프사이클
    // ─────────────────────────────────────────
    private void OnEnable()
    {
        // SOAP 이벤트 구독
        if (onApproveClicked != null)
            onApproveClicked.OnRaised += GoToNextPhase;

        if (onLocationSelected != null)
            onLocationSelected.OnRaised += OnLocationSelectedHandler;
    }

    private void OnDisable()
    {
        // SOAP 이벤트 구독 해제 (메모리 누수 방지)
        if (onApproveClicked != null)
            onApproveClicked.OnRaised -= GoToNextPhase;

        if (onLocationSelected != null)
            onLocationSelected.OnRaised -= OnLocationSelectedHandler;
    }

    private void Start()
    {
        // 모든 패널을 처음에 숨김 상태로 초기화
        InitializePanels();
        StartDay(1);
    }

    // ─────────────────────────────────────────
    // 초기화
    // ─────────────────────────────────────────
    private void InitializePanels()
    {
        SetCanvasGroupHidden(documentPanelCG);
        SetCanvasGroupHidden(guidelinePanelCG);
        SetCanvasGroupHidden(morningNewsPanelCG);

        if (guidelineButton != null)
            guidelineButton.SetActive(false);
    }

    private void SetCanvasGroupHidden(CanvasGroup cg)
    {
        if (cg == null) return;
        cg.alpha = 0f;
        cg.interactable = false;
        cg.blocksRaycasts = false;
        cg.gameObject.SetActive(false);
    }

    // ─────────────────────────────────────────
    // 하루 시작
    // ─────────────────────────────────────────
    public void StartDay(int day)
    {
        currentDay = day;

        if (soapCurrentDay != null)
            soapCurrentDay.Value = day;

        Debug.Log($"===== [ Stage {currentDay} 시작 ] =====");
        ChangePhase(GamePhase.Morning);
    }

    // ─────────────────────────────────────────
    // 페이즈 전환 (핵심)
    // ─────────────────────────────────────────
    public void ChangePhase(GamePhase newPhase)
    {
        // 현재 표시 중인 모든 패널을 먼저 페이드 아웃
        FadeOutAllPanels(() =>
        {
            currentPhase = newPhase;
            LoadTodaysData();
            ActivatePhase(newPhase);
        });
    }

    private void FadeOutAllPanels(TweenCallback onComplete)
    {
        // 현재 켜져 있는 패널들을 동시에 페이드 아웃
        var sequence = DOTween.Sequence();

        AppendFadeOut(sequence, documentPanelCG);
        AppendFadeOut(sequence, guidelinePanelCG);
        AppendFadeOut(sequence, morningNewsPanelCG);

        if (guidelineButton != null)
            guidelineButton.SetActive(false);

        sequence.OnComplete(onComplete);

        // 켜진 패널이 없어도 콜백이 즉시 실행되도록
        if (!sequence.IsActive() || sequence.Duration() < 0.01f)
            onComplete?.Invoke();
    }

    private void AppendFadeOut(Sequence seq, CanvasGroup cg)
    {
        if (cg == null || !cg.gameObject.activeSelf) return;

        seq.Join(
            cg.DOFade(0f, panelFadeDuration)
              .SetEase(Ease.OutQuad)
              .OnComplete(() =>
              {
                  cg.interactable = false;
                  cg.blocksRaycasts = false;
                  cg.gameObject.SetActive(false);
              })
        );
    }

    private void LoadTodaysData()
    {
        int index = currentDay - 1;
        todaysData = (index >= 0 && index < dailyDataList.Count)
            ? dailyDataList[index]
            : null;
    }

    private void ActivatePhase(GamePhase phase)
    {
        switch (phase)
        {
            case GamePhase.Morning:
                ActivateMorningPhase();
                break;

            case GamePhase.Day:
                ActivateDayPhase();
                break;

            case GamePhase.Night:
                ActivateNightPhase();
                break;
        }
    }

    // ─────────────────────────────────────────
    // 아침 페이즈
    // ─────────────────────────────────────────
    private void ActivateMorningPhase()
    {
        Debug.Log($"☀️ [아침] Stage {currentDay} 뉴스");

        if (todaysData == null || morningNewsPanelCG == null) return;

        if (officialNewsTitleUI != null)
            officialNewsTitleUI.text = todaysData.officialNewsTitle;

        if (officialNewsContentUI != null)
            officialNewsContentUI.text = todaysData.officialNewsContent;

        ShowPanel(morningNewsPanelCG);
    }

    // 아침 뉴스 확인 버튼 — 인스펙터에서 버튼 OnClick에 연결
    public void OnClickMorningNewsConfirm()
    {
        GoToNextPhase();
    }

    // ─────────────────────────────────────────
    // 낮 페이즈
    // ─────────────────────────────────────────
    private void ActivateDayPhase()
    {
        Debug.Log($"💼 [낮] Stage {currentDay} 업무 시작");

        if (todaysData == null || todaysData.documentToProcess == null) return;

        // 서류 뷰어에 오늘 데이터 전달
        documentViewer?.ShowDocument(todaysData.documentToProcess);

        // 가이드라인 텍스트 설정
        if (guidelineTextUI != null)
            guidelineTextUI.text = todaysData.documentToProcess.guidelineText;

        // 서류 패널 페이드 인
        ShowPanel(documentPanelCG);

        // 가이드라인 버튼 활성화
        if (guidelineButton != null)
            guidelineButton.SetActive(true);
    }

    // 가이드라인 토글 버튼 — 인스펙터에서 버튼 OnClick에 연결
    public void OnClickGuidelineToggle()
    {
        if (guidelinePanelCG == null) return;

        bool isVisible = guidelinePanelCG.gameObject.activeSelf && guidelinePanelCG.alpha > 0.5f;

        if (isVisible)
        {
            // 가이드라인 숨기기
            guidelinePanelCG.DOFade(0f, panelFadeDuration)
                            .SetEase(Ease.OutQuad)
                            .OnComplete(() =>
                            {
                                guidelinePanelCG.interactable = false;
                                guidelinePanelCG.blocksRaycasts = false;
                            });
        }
        else
        {
            // 가이드라인 표시
            ShowPanel(guidelinePanelCG);
        }
    }

    // ─────────────────────────────────────────
    // 밤 페이즈
    // ─────────────────────────────────────────
    private void ActivateNightPhase()
    {
        Debug.Log("🌙 [밤] 지도 UI 오픈");

        if (todaysData == null || nightMapUI == null) return;

        nightMapUI.ShowMap(todaysData.availableLocations);
    }

    // OnLocationSelected 이벤트 수신 → 탐색 시작
    // (실제 씬 전환은 NightPhaseManager가 담당)
    private void OnLocationSelectedHandler()
    {
        Debug.Log("장소 선택 완료 → NightPhaseManager로 신호 전달됨");
        // NightPhaseManager가 OnLocationSelected를 직접 구독하므로
        // GameManager는 별도 처리 불필요
    }

    // ─────────────────────────────────────────
    // 페이즈 순서 진행
    // ─────────────────────────────────────────
    public void GoToNextPhase()
    {
        switch (currentPhase)
        {
            case GamePhase.Morning: ChangePhase(GamePhase.Day);   break;
            case GamePhase.Day:     ChangePhase(GamePhase.Night);  break;
            case GamePhase.Night:   StartDay(currentDay + 1);      break;
        }
    }

    // ─────────────────────────────────────────
    // DOTween 유틸리티
    // ─────────────────────────────────────────
    private void ShowPanel(CanvasGroup cg, float duration = -1f)
    {
        if (cg == null) return;

        float d = duration < 0f ? panelFadeDuration : duration;
        cg.gameObject.SetActive(true);
        cg.alpha = 0f;
        cg.interactable = false;
        cg.blocksRaycasts = false;

        cg.DOFade(1f, d)
          .SetEase(Ease.InQuad)
          .OnComplete(() =>
          {
              cg.interactable = true;
              cg.blocksRaycasts = true;
          });
    }
}
