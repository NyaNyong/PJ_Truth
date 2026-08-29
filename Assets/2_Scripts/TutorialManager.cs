using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using System;
using DG.Tweening;

public class TutorialManager : MonoBehaviour
{
    public static TutorialManager Instance { get; private set; }

    private GameObject tutorialCanvasObj;
    private Image darkOverlayImg;

    private GameObject arrowObj;
    private RectTransform arrowRT;
    private Text arrowText; // ★ 추가: 화살표 방향을 텍스트로 바꾸기 위한 변수

    public bool _markerUsed = false;
    public bool _typewriterUsed = false;
    public bool _truthNoteGuided = false;       // ★ 추가: 진실 노트 안내 여부
    public bool _whiteboardCloseGuided = false; // ★ 추가

    // ★ 추가: 버튼 클릭 이벤트를 직접 추적하기 위한 변수
    private Button currentGuideButton;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        CreateTutorialUI();
    }

    // ★ 추가: 스크립트가 파괴되거나 게임이 종료될 때 무한 루프 애니메이션을 깔끔하게 꺼줍니다.
    private void OnDestroy()
    {
        if (arrowRT != null) arrowRT.DOKill();
    }

    // ─── 튜토리얼 전용 패널 및 화살표 생성 ───────────────────────────────
    private void CreateTutorialUI()
    {
        // 1. 튜토리얼 캔버스
        tutorialCanvasObj = new GameObject("TutorialPanel");
        var canvas = tutorialCanvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 200; // 대화창(DialogueUI)보다 아래에 위치

        // ★ 핵심 추가: 화면 해상도(16:9)에 맞춰 UI 비율을 고정해주는 CanvasScaler 추가
        UnityEngine.UI.CanvasScaler scaler = tutorialCanvasObj.AddComponent<UnityEngine.UI.CanvasScaler>();
        scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080); // 기준 해상도 (보통 1920x1080 사용)
        scaler.screenMatchMode = UnityEngine.UI.CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f; // 가로 세로 비율 균형

        tutorialCanvasObj.AddComponent<GraphicRaycaster>();
        DontDestroyOnLoad(tutorialCanvasObj);

        // 2. 어두운 배경 (클릭 차단)
        var bgObj = new GameObject("DarkBackground");
        bgObj.transform.SetParent(tutorialCanvasObj.transform, false);
        darkOverlayImg = bgObj.AddComponent<Image>();
        darkOverlayImg.color = new Color(0, 0, 0, 0.8f);

        var bgRT = bgObj.GetComponent<RectTransform>();
        bgRT.anchorMin = Vector2.zero;
        bgRT.anchorMax = Vector2.one;
        bgRT.offsetMin = Vector2.zero;
        bgRT.offsetMax = Vector2.zero;

        // 3. 지시용 화살표 생성
        arrowObj = new GameObject("TutorialArrow");
        arrowObj.transform.SetParent(tutorialCanvasObj.transform, false);
        arrowRT = arrowObj.AddComponent<RectTransform>();
        arrowRT.sizeDelta = new Vector2(100, 100);

        arrowText = arrowObj.AddComponent<Text>(); // ★ var를 빼고 클래스 변수에 연결
        arrowText.text = "▼";
        arrowText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (arrowText.font == null) arrowText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");

        arrowText.fontSize = 80;
        arrowText.color = new Color(1f, 0.9f, 0.1f); // 노란색
        arrowText.alignment = TextAnchor.MiddleCenter;
        arrowText.raycastTarget = false;

        arrowObj.SetActive(false);
        tutorialCanvasObj.SetActive(false);
    }

    // ─── 화면 제어 유틸리티 ──────────────────────────────────────────────
    private void SyncCanvasMode()
    {
        if (DialogueUI.Instance != null)
        {
            var mainCanvas = DialogueUI.Instance.GetComponentInParent<Canvas>();
            if (mainCanvas != null)
            {
                var overlayCanvas = tutorialCanvasObj.GetComponent<Canvas>();
                overlayCanvas.renderMode = mainCanvas.renderMode;
                if (mainCanvas.renderMode == RenderMode.ScreenSpaceCamera)
                {
                    overlayCanvas.worldCamera = mainCanvas.worldCamera;
                }
                overlayCanvas.sortingLayerID = mainCanvas.sortingLayerID;
                overlayCanvas.sortingOrder = 200;
            }
        }
    }

    private void ShowDarkOverlay()
    {
        SyncCanvasMode();
        darkOverlayImg.enabled = true;
        tutorialCanvasObj.SetActive(true);
    }

    private void HideDarkOverlay()
    {
        if (darkOverlayImg != null) darkOverlayImg.enabled = false;
    }

    public void PointArrowAt(Component target, float yOffset = 60f)
    {
        if (target == null) { arrowObj.SetActive(false); return; }

        arrowObj.SetActive(true);
        arrowRT.DOKill();

        arrowText.text = "▼"; // ★ 기본(아래) 방향 보장

        RectTransform targetRT = target.GetComponent<RectTransform>();
        arrowRT.position = targetRT.position;

        float baseY = arrowRT.anchoredPosition.y + (targetRT.rect.height * 0.5f) + yOffset;
        arrowRT.anchoredPosition = new Vector2(arrowRT.anchoredPosition.x, baseY);

        arrowRT.DOAnchorPosY(baseY - 20f, 0.6f).SetEase(Ease.InOutSine).SetLoops(-1, LoopType.Yoyo);
    }

    public void PointArrowAtPosition(Vector2 anchoredPos)
    {
        arrowObj.SetActive(true);
        arrowRT.DOKill();

        arrowText.text = "▼"; // ★ 기본(아래) 방향 보장
        arrowRT.anchoredPosition = anchoredPos;

        arrowRT.DOAnchorPosY(anchoredPos.y - 20f, 0.6f).SetEase(Ease.InOutSine).SetLoops(-1, LoopType.Yoyo);
    }

    // ★ 새롭게 추가: 지정된 위치에서 오른쪽을 가리키며 좌우로 움직이는 메서드
    public void PointArrowRightAtPosition(Vector2 anchoredPos)
    {
        arrowObj.SetActive(true);
        arrowRT.DOKill();

        arrowText.text = "▶"; // ★ 오른쪽을 가리키는 텍스트로 변경
        arrowRT.anchoredPosition = anchoredPos;

        // Y축 대신 X축으로 통통 튀는 애니메이션 (왼쪽으로 당겼다 오른쪽으로)
        arrowRT.DOAnchorPosX(anchoredPos.x - 20f, 0.6f).SetEase(Ease.InOutSine).SetLoops(-1, LoopType.Yoyo);
    }

    

    public void HideArrow()
    {
        if (arrowRT != null) arrowRT.DOKill();
        if (arrowObj != null) arrowObj.SetActive(false);
    }

    // ★ TutorialManager.cs 내부의 해당 메서드를 찾아 아래로 통째로 덮어씌우세요.
    public void ShowTutorialDialogue(string text, Action onComplete)
    {
        Debug.Log("[TutorialManager] 대화창 호출 시도: " + text); // 디버그용

        if (DialogueUI.Instance != null)
        {
            var canvas = DialogueUI.Instance.GetComponentInParent<Canvas>();
            if (canvas != null)
            {
                canvas.overrideSorting = true;
                canvas.sortingOrder = 300;
            }

            // ★ 수정: 기존 게임 시스템에 확실하게 존재하는 StartDialogue 사용
            DialogueUI.Instance.StartDialogue("가이드", new List<string> { text }, () =>
            {
                if (canvas != null)
                {
                    canvas.overrideSorting = false;
                    canvas.sortingOrder = 99;
                }
                onComplete?.Invoke();
            });
        }
        else
        {
            Debug.LogWarning("[TutorialManager] DialogueUI.Instance 가 없습니다.");
            onComplete?.Invoke();
        }
    }

    // ─── 튜토리얼 단계별 호출 메서드 ──────────────────────────────────────
    public void OnMorningOfficialNews()
    {
        ShowDarkOverlay();
        ShowTutorialDialogue("아침 뉴스입니다.\n밤사이 발생한 사건의 공식적인 발표를 확인할 수 있습니다.", () => {
            if (tutorialCanvasObj != null) tutorialCanvasObj.SetActive(false);
        });
    }

    public void OnMorningPrivateNews()
    {
        ShowDarkOverlay();
        ShowTutorialDialogue("사설 뉴스입니다.\n공식 뉴스가 아닌 현장의 반응과 의혹이 담긴 사설로 발행되는 뉴스입니다.", () => {
            if (tutorialCanvasObj != null) tutorialCanvasObj.SetActive(false);
        });
    }

    public void OnDocumentViewerOpened(DocumentViewer viewer, CanvasGroup guidelinePanel, CanvasGroup documentPanel)
    {
        StartCoroutine(DocumentTutorialRoutine(viewer, guidelinePanel, documentPanel));
    }

    private IEnumerator DocumentTutorialRoutine(DocumentViewer viewer, CanvasGroup guidelinePanel, CanvasGroup documentPanel)
    {
        yield return new WaitForSeconds(0.5f);

        Button markerBtn = viewer.GetBlackMarkerButton();
        Button typewriterBtn = viewer.GetTypewriterButton();
        Button stampBtn = viewer.GetApproveButton();

        ShowDarkOverlay();
        bool stepDone = false;
        ShowTutorialDialogue("검열 단계입니다.\n우측의 블랙 마커, 타자기, 승인 도장을 이용해 업무를 수행합니다.", () => stepDone = true);
        yield return new WaitUntil(() => stepDone);

        PointArrowAt(guidelinePanel);
        stepDone = false;
        ShowTutorialDialogue("가이드라인입니다.\n문서의 어떤 부분을 어떻게 검열해야 하는지 지침을 확인할 수 있습니다.", () => stepDone = true);
        yield return new WaitUntil(() => stepDone);

        // ★ 수정된 부분: 타겟 추적 대신 사용자가 지정한 정확한 좌표로 화살표 배치
        PointArrowAtPosition(new Vector2(-755f, -110f));
        stepDone = false;
        ShowTutorialDialogue("블랙 마커입니다.\n가이드라인에서 지시한 '금지된 표현'을 클릭하여 검게 지울 수 있습니다.\n직접 블랙 마커를 선택해 단어를 하나 지워보세요.", () => stepDone = true);
        yield return new WaitUntil(() => stepDone);

        HideDarkOverlay();

        _markerUsed = false;
        yield return new WaitUntil(() => _markerUsed);

        // ... (블랙 마커 사용 대기 이후) ...

        // ★ 수정된 부분: 타겟 추적 대신 사용자가 지정한 정확한 좌표로 화살표 배치
        PointArrowAtPosition(new Vector2(575f, -160f));
        stepDone = false;
        ShowTutorialDialogue("타자기입니다.\n타자기를 열면 비워진 [SLOT]에 들어갈 대체 단어 카드가 나타납니다.\n올바른 단어를 드래그하여 슬롯에 덮어씌워 보세요.", () => stepDone = true);
        yield return new WaitUntil(() => stepDone);

        // 사용 대기
        _typewriterUsed = false;
        yield return new WaitUntil(() => _typewriterUsed);

        // ... (이후 승인 도장 설명 단계로 이어짐) ...

        PointArrowAt(stampBtn, 80f);
        stepDone = false;
        ShowTutorialDialogue("승인 도장입니다.\n모든 검열과 수정이 끝났다고 판단되면 도장을 눌러 문서를 최종 승인합니다.", () => stepDone = true);
        yield return new WaitUntil(() => stepDone);

        HideArrow();
        if (tutorialCanvasObj != null) tutorialCanvasObj.SetActive(false);
    }

    public void NotifyMarkerUsed() { _markerUsed = true; }
    public void NotifyTypewriterUsed() { _typewriterUsed = true; }

    public void OnResultScreen()
    {
        ShowDarkOverlay();
        ShowTutorialDialogue("결과창입니다.\n당신의 검열 정확도와 그에 따른 평가 등급, 그리고 승급 진행도(KPI)를 보여줍니다.\n우수한 평가를 받아야 승급에 유리합니다.", () => {
            if (tutorialCanvasObj != null) tutorialCanvasObj.SetActive(false);
        });
    }

    public void OnNightMap()
    {
        ShowDarkOverlay();
        ShowTutorialDialogue("지도입니다.\n업무를 마친 후, 방문할 장소를 선택해 현장을 직접 조사할 수 있습니다.", () => {
            if (tutorialCanvasObj != null) tutorialCanvasObj.SetActive(false);
        });
    }

    public void OnNightPhaseStarted()
    {
        ShowDarkOverlay();
        ShowTutorialDialogue("밤 페이즈입니다.\nWASD 키로 이동하며, 반짝이는 단서나 인물 근처에서 E 키를 눌러 조사 및 상호작용할 수 있습니다..\n조사 종료시에는 파란색 집 아이콘과 상호작용하여 돌아갈 수 있습니다.", () => {
            if (tutorialCanvasObj != null) tutorialCanvasObj.SetActive(false);
        });
    }

    // ★ 실수로 지워진 화이트보드 시작 튜토리얼 (다시 추가)
    public void OnWhiteboardStarted(Action onComplete)
    {
        ShowDarkOverlay();
        ShowTutorialDialogue("화이트보드입니다.\n수집한 단서 카드를 클릭하고 다른 카드에 드래그하여 붉은 실로 연결해보세요.\n사건의 올바른 흐름을 연결하면 감춰진 진실이 해금됩니다.", () => {
            if (tutorialCanvasObj != null) tutorialCanvasObj.SetActive(false);
            onComplete?.Invoke();
        });
    }

    // ★ 직전 단계에서 추가했던 커튼콜(종료) 안내
    // ★ 1. 진실 노트 버튼 안내 (단서 연결 후 호출됨)
    // ★ 1. 진실 노트 버튼 안내 (단서 연결 후 호출됨)
    public void OnTruthNoteGuide(Component truthBtn)
    {
        _truthNoteGuided = true;

        // ★ 추가: 꺼져있던 튜토리얼 캔버스를 켜되 어두운 배경은 없앰
        if (tutorialCanvasObj != null) tutorialCanvasObj.SetActive(true);
        HideDarkOverlay();

        PointArrowAt(truthBtn, 80f);
        ShowTutorialDialogue("연결한 단서에 대한 진실은 오른쪽 아래에서 볼 수 있습니다.", () => {
            // 버튼을 직접 클릭해야 하므로 대화창이 꺼져도 화살표는 숨기지 않고 유지합니다.
        });
    }

    // ★ 2. 커튼콜(종료) 버튼 안내
    public void OnWhiteboardCloseGuide(Component closeBtn)
    {
        _whiteboardCloseGuided = true;

        if (tutorialCanvasObj != null) tutorialCanvasObj.SetActive(true);
        HideDarkOverlay();

        PointArrowRightAtPosition(new Vector2(700f, 0f));

        // ★ 핵심 수정: 버튼에 직접 접근해서 클릭 시 즉시 튜토리얼을 끄도록 이벤트를 달아줍니다.
        currentGuideButton = closeBtn.GetComponent<Button>();
        if (currentGuideButton != null)
        {
            currentGuideButton.onClick.AddListener(ForceCloseTutorial);
        }

        ShowTutorialDialogue("모든 진실을 해금하였다면 커튼을 내려 다음 날로 넘어갈 수 있습니다.", () => {
            // 유저가 직접 버튼을 누르기 전까지 화살표 유지
        });
    }

    // ★ 튜토리얼 강제 종료 (버튼 클릭 시 자동 실행됨)
    public void ForceCloseTutorial()
    {
        // 중복 실행을 막기 위해 등록했던 이벤트를 해제합니다.
        if (currentGuideButton != null)
        {
            currentGuideButton.onClick.RemoveListener(ForceCloseTutorial);
            currentGuideButton = null;
        }

        HideArrow();
        if (tutorialCanvasObj != null) tutorialCanvasObj.SetActive(false);
    }

    // ★ 추가: 튜토리얼 UI 캔버스 자체를 파괴하여 잔여물 제거
    public void DestroyTutorialUI()
    {
        if (tutorialCanvasObj != null)
        {
            Destroy(tutorialCanvasObj);
            tutorialCanvasObj = null;
        }
    }
}