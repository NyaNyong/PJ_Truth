using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;
using System.Collections.Generic;
using System;

/// <summary>
/// NPC 대화 및 단서 설명 UI. 선택지(Choices) 분기 지원.
/// </summary>
public class DialogueUI : MonoBehaviour
{
    public static DialogueUI Instance { get; private set; }

    [Header("UI 연결")]
    [SerializeField] private CanvasGroup dialoguePanelCG;
    [SerializeField] private TextMeshProUGUI speakerNameText;
    [SerializeField] private TextMeshProUGUI dialogueBodyText;
    [SerializeField] private GameObject nextIndicator;

    [Header("선택지 UI")]
    [Tooltip("선택지 전체를 감싸는 CanvasGroup (대화 패널 위에 오버레이)")]
    [SerializeField] private CanvasGroup choicePanelCG;
    [Tooltip("버튼들이 배치될 컨테이너 (VerticalLayoutGroup 권장)")]
    [SerializeField] private Transform choiceContainer;
    [Tooltip("선택지 버튼 프리팹 — Button + TextMeshProUGUI 자식 포함")]
    [SerializeField] private Button choiceButtonPrefab;

    [Header("DOTween 설정")]
    [SerializeField] private float fadeInDuration = 0.25f;
    [SerializeField] private float fadeOutDuration = 0.2f;
    [SerializeField] private float textTypeSpeed = 0.03f;

    private List<string> currentLines = new List<string>();
    private int currentLineIndex = 0;
    private bool isTyping = false;
    private Action onFinished;

    // ── 선택지 상태 ──────────────────────────
    private List<DialogueChoiceData> pendingChoices;
    private Action<DialogueChoiceData> onChoiceSelected;
    private bool isShowingChoices = false;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        if (dialoguePanelCG != null)
        {
            dialoguePanelCG.alpha = 0f;
            dialoguePanelCG.interactable = false;
            dialoguePanelCG.blocksRaycasts = false;
            dialoguePanelCG.gameObject.SetActive(false);
        }

        if (choicePanelCG != null)
        {
            choicePanelCG.alpha = 0f;
            choicePanelCG.interactable = false;
            choicePanelCG.blocksRaycasts = false;
            choicePanelCG.gameObject.SetActive(false);
        }
    }

    private void Update()
    {
        if (!IsOpen()) return;
        if (isShowingChoices) return; // 선택지 표시 중 E키 차단

        if (Input.GetKeyDown(KeyCode.E))
        {
            if (isTyping) SkipTyping();
            else AdvanceLine();
        }
    }

    // ─────────────────────────────────────────
    // 공개 인터페이스
    // ─────────────────────────────────────────

    /// <summary>선택지 없는 일반 대화</summary>
    public void StartDialogue(string speakerName, List<string> lines, Action onFinished = null)
    {
        StartDialogue(speakerName, lines, null, null, onFinished);
    }

    /// <summary>선택지 포함 대화. 마지막 줄 이후 선택지 버튼 표시.</summary>
    public void StartDialogue(
        string speakerName,
        List<string> lines,
        List<DialogueChoiceData> choices,
        Action<DialogueChoiceData> onChoiceSelected,
        Action onFinished = null)
    {
        currentLines = lines;
        currentLineIndex = 0;
        pendingChoices = (choices != null && choices.Count > 0) ? choices : null;
        this.onChoiceSelected = onChoiceSelected;
        this.onFinished = onFinished;
        isShowingChoices = false;

        if (speakerNameText != null)
            speakerNameText.text = speakerName;

        ShowPanel();
        ShowLine(currentLines[0]);
    }

    public bool IsOpen() => dialoguePanelCG != null &&
                            dialoguePanelCG.gameObject.activeSelf &&
                            dialoguePanelCG.alpha > 0.5f;

    // ─────────────────────────────────────────
    // 내부 로직 — 줄 진행
    // ─────────────────────────────────────────

    private void AdvanceLine()
    {
        currentLineIndex++;
        if (currentLineIndex >= currentLines.Count)
        {
            if (pendingChoices != null)
                ShowChoices();
            else
                CloseDialogue();
            return;
        }
        AudioManager.Instance?.PlaySfxDialogueNext();
        ShowLine(currentLines[currentLineIndex]);
    }

    private void ShowLine(string line)
    {
        DOTween.Kill("dialogue_type");
        if (nextIndicator != null) nextIndicator.SetActive(false);

        isTyping = true;
        dialogueBodyText.text = "";

        DOTween.To(() => 0, x =>
        {
            dialogueBodyText.text = line.Substring(0, Mathf.Min(x, line.Length));
        }, line.Length, textTypeSpeed * line.Length)
        .SetId("dialogue_type")
        .SetEase(Ease.Linear)
        .OnComplete(() =>
        {
            isTyping = false;
            dialogueBodyText.text = line;

            // 선택지가 올 마지막 줄이면 nextIndicator 숨김
            bool isLastWithChoices = pendingChoices != null
                                  && currentLineIndex >= currentLines.Count - 1;
            if (nextIndicator != null)
                nextIndicator.SetActive(!isLastWithChoices);
        });
    }

    private void SkipTyping()
    {
        DOTween.Kill("dialogue_type");
        isTyping = false;
        dialogueBodyText.text = currentLines[currentLineIndex];

        bool isLastWithChoices = pendingChoices != null
                              && currentLineIndex >= currentLines.Count - 1;
        if (nextIndicator != null)
            nextIndicator.SetActive(!isLastWithChoices);
    }

    // ─────────────────────────────────────────
    // 내부 로직 — 선택지
    // ─────────────────────────────────────────

    private void ShowChoices()
    {
        if (choicePanelCG == null || choiceContainer == null || choiceButtonPrefab == null)
        {
            Debug.LogWarning("[DialogueUI] 선택지 UI 미연결 — 그냥 종료합니다.");
            CloseDialogue();
            return;
        }

        isShowingChoices = true;
        if (nextIndicator != null) nextIndicator.SetActive(false);

        // 기존 버튼 정리
        foreach (Transform child in choiceContainer)
            Destroy(child.gameObject);

        // 버튼 생성
        foreach (var choice in pendingChoices)
        {
            var btn = Instantiate(choiceButtonPrefab, choiceContainer);
            var label = btn.GetComponentInChildren<TextMeshProUGUI>();
            if (label != null) label.text = choice.label;

            var captured = choice;
            btn.onClick.AddListener(() => OnChoiceClicked(captured));
        }

        // 패널 페이드인
        choicePanelCG.gameObject.SetActive(true);
        choicePanelCG.alpha = 0f;
        choicePanelCG.interactable = false;
        choicePanelCG.blocksRaycasts = false;
        choicePanelCG.DOFade(1f, fadeInDuration).OnComplete(() =>
        {
            choicePanelCG.interactable = true;
            choicePanelCG.blocksRaycasts = true;
        });
    }

    private void HideChoices(Action onComplete = null)
    {
        if (choicePanelCG == null) { onComplete?.Invoke(); return; }

        choicePanelCG.interactable = false;
        choicePanelCG.blocksRaycasts = false;
        choicePanelCG.DOFade(0f, fadeOutDuration).OnComplete(() =>
        {
            choicePanelCG.gameObject.SetActive(false);
            foreach (Transform child in choiceContainer)
                Destroy(child.gameObject);
            onComplete?.Invoke();
        });
    }

    private void OnChoiceClicked(DialogueChoiceData choice)
    {
        AudioManager.Instance?.PlaySfxDialogueNext();
        isShowingChoices = false;
        onChoiceSelected?.Invoke(choice); // ★ 단서/플래그는 NPCInteractable에서 처리

        HideChoices(() =>
        {
            pendingChoices = null; // 선택지 소비

            if (choice.lines != null && choice.lines.Count > 0)
            {
                // 선택지 대사 재생 → 끝나면 CloseDialogue
                currentLines = choice.lines;
                currentLineIndex = 0;
                ShowLine(currentLines[0]);
            }
            else
            {
                CloseDialogue();
            }
        });
    }

    // ─────────────────────────────────────────
    // 패널 제어
    // ─────────────────────────────────────────

    private void ShowPanel()
    {
        dialoguePanelCG.gameObject.SetActive(true);
        dialoguePanelCG.alpha = 0f;
        dialoguePanelCG.DOFade(1f, fadeInDuration).OnComplete(() =>
        {
            dialoguePanelCG.interactable = true;
            dialoguePanelCG.blocksRaycasts = true;
        });
    }

    private void CloseDialogue()
    {
        AudioManager.Instance?.PlaySfxDialogueClose();
        DOTween.Kill("dialogue_type");
        dialoguePanelCG.interactable = false;
        dialoguePanelCG.blocksRaycasts = false;
        dialoguePanelCG.DOFade(0f, fadeOutDuration).OnComplete(() =>
        {
            dialoguePanelCG.gameObject.SetActive(false);
            onFinished?.Invoke();
        });
    }
}