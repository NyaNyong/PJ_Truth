using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;
using System.Collections.Generic;
using System;

public class DialogueUI : MonoBehaviour
{
    public static DialogueUI Instance { get; private set; }

    [Header("UI 연결")]
    [SerializeField] private CanvasGroup dialoguePanelCG;
    [SerializeField] private TextMeshProUGUI speakerNameText;
    [SerializeField] private TextMeshProUGUI dialogueBodyText;
    [SerializeField] private GameObject nextIndicator;

    [Header("선택지 UI")]
    [SerializeField] private CanvasGroup choicePanelCG;
    [SerializeField] private Transform choiceContainer;
    [SerializeField] private Button choiceButtonPrefab;

    [Header("선택 완료 버튼 색상")]
    [SerializeField] private Color usedChoiceColor = new Color(0.5f, 0.5f, 0.5f, 0.5f);

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

    // ★ 사용한 선택지 인덱스 추적
    private HashSet<int> usedChoiceIndices = new HashSet<int>();

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
        if (isShowingChoices) return;

        if (Input.GetKeyDown(KeyCode.E))
        {
            if (isTyping) SkipTyping();
            else AdvanceLine();
        }
    }

    // ─────────────────────────────────────────
    public void StartDialogue(string speakerName, List<string> lines, Action onFinished = null)
    {
        StartDialogue(speakerName, lines, null, null, onFinished);
    }

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
        usedChoiceIndices.Clear(); // ★ 초기화

        if (speakerNameText != null)
            speakerNameText.text = speakerName;

        ShowPanel();
        ShowLine(currentLines[0]);
    }

    public bool IsOpen() => dialoguePanelCG != null &&
                            dialoguePanelCG.gameObject.activeSelf &&
                            dialoguePanelCG.alpha > 0.5f;

    // ─────────────────────────────────────────
    private void AdvanceLine()
    {
        currentLineIndex++;
        if (currentLineIndex >= currentLines.Count)
        {
            // ★ 선택지가 있고 아직 안 쓴 것이 있으면 선택지로 복귀, 아니면 종료
            if (pendingChoices != null && usedChoiceIndices.Count < pendingChoices.Count)
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

            // ★ 아직 남은 선택지가 있으면 nextIndicator 숨김
            bool moreChoices = pendingChoices != null &&
                               usedChoiceIndices.Count < pendingChoices.Count &&
                               currentLineIndex >= currentLines.Count - 1;
            if (nextIndicator != null)
                nextIndicator.SetActive(!moreChoices);
        });
    }

    private void SkipTyping()
    {
        DOTween.Kill("dialogue_type");
        isTyping = false;
        dialogueBodyText.text = currentLines[currentLineIndex];

        bool moreChoices = pendingChoices != null &&
                           usedChoiceIndices.Count < pendingChoices.Count &&
                           currentLineIndex >= currentLines.Count - 1;
        if (nextIndicator != null)
            nextIndicator.SetActive(!moreChoices);
    }

    // ─────────────────────────────────────────
    private void ShowChoices()
    {
        if (choicePanelCG == null || choiceContainer == null || choiceButtonPrefab == null)
        {
            Debug.LogWarning("[DialogueUI] 선택지 UI 미연결 — 종료합니다.");
            CloseDialogue();
            return;
        }

        isShowingChoices = true;
        if (nextIndicator != null) nextIndicator.SetActive(false);

        foreach (Transform child in choiceContainer)
            Destroy(child.gameObject);

        // ★ 인덱스 기반으로 버튼 생성 — 사용한 선택지는 비활성화
        for (int i = 0; i < pendingChoices.Count; i++)
        {
            var choice = pendingChoices[i];
            var btn = Instantiate(choiceButtonPrefab, choiceContainer);
            var label = btn.GetComponentInChildren<TextMeshProUGUI>();
            if (label != null) label.text = choice.label;

            bool used = usedChoiceIndices.Contains(i);
            btn.interactable = !used;

            // 사용한 선택지 색상 처리
            if (used)
            {
                var img = btn.GetComponent<Image>();
                if (img != null) img.color = usedChoiceColor;
                if (label != null) label.color = usedChoiceColor;
            }

            if (!used)
            {
                int capturedIndex = i;
                var capturedChoice = choice;
                btn.onClick.AddListener(() => OnChoiceClicked(capturedIndex, capturedChoice));
            }
        }

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

    // ★ 인덱스 파라미터 추가
    private void OnChoiceClicked(int choiceIndex, DialogueChoiceData choice)
    {
        AudioManager.Instance?.PlaySfxDialogueNext();
        isShowingChoices = false;

        usedChoiceIndices.Add(choiceIndex); // ★ 사용 기록
        onChoiceSelected?.Invoke(choice);   // 단서/플래그 처리 (NPCInteractable)

        HideChoices(() =>
        {
            if (choice.lines != null && choice.lines.Count > 0)
            {
                // 선택지 대사 재생 → 끝나면 AdvanceLine에서 자동 분기
                currentLines = choice.lines;
                currentLineIndex = 0;
                ShowLine(currentLines[0]);
            }
            else
            {
                // 대사 없는 선택지 → 바로 분기 판단
                if (usedChoiceIndices.Count < pendingChoices.Count)
                    ShowChoices();
                else
                    CloseDialogue();
            }
        });
    }

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