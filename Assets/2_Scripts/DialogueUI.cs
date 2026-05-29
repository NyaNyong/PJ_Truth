using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;
using System.Collections.Generic;
using System;

public class DialogueUI : MonoBehaviour
{
    public static DialogueUI Instance { get; private set; }

    [Header("대화 패널")]
    [SerializeField] private CanvasGroup dialoguePanelCG;
    [SerializeField] private TextMeshProUGUI speakerNameText;
    [SerializeField] private TextMeshProUGUI dialogueBodyText;
    [SerializeField] private GameObject nextIndicator;

    // ★ NPC 초상화
    [Header("NPC 초상화")]
    [SerializeField] private CanvasGroup portraitCG;
    [SerializeField] private Image portraitImage;

    [Header("선택지 UI")]
    [SerializeField] private CanvasGroup choicePanelCG;
    [SerializeField] private Transform choiceContainer;
    [SerializeField] private Button choiceButtonPrefab;

    [Header("사용 완료 선택지 색상")]
    [SerializeField] private Color usedChoiceColor = new Color(0.5f, 0.5f, 0.5f, 0.5f);

    [Header("DOTween 설정")]
    [SerializeField] private float fadeInDuration  = 0.25f;
    [SerializeField] private float fadeOutDuration = 0.2f;
    [SerializeField] private float textTypeSpeed   = 0.03f;

    private List<string> currentLines    = new List<string>();
    private int          currentLineIndex = 0;
    private bool         isTyping         = false;
    private Action       onFinished;

    private List<DialogueChoiceData>    pendingChoices;
    private Action<DialogueChoiceData>  onChoiceSelected;
    private bool                        isShowingChoices       = false;
    private HashSet<int>                usedChoiceIndices      = new HashSet<int>();
    private bool                        closeAfterCurrentLines = false;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        SetCGHidden(dialoguePanelCG);
        SetCGHidden(choicePanelCG);
        SetCGHidden(portraitCG);
    }

    private void SetCGHidden(CanvasGroup cg)
    {
        if (cg == null) return;
        cg.alpha = 0f;
        cg.interactable   = false;
        cg.blocksRaycasts = false;
        cg.gameObject.SetActive(false);
    }

    private void Update()
    {
        if (dialoguePanelCG == null ||
            !dialoguePanelCG.gameObject.activeSelf ||
            dialoguePanelCG.alpha < 0.5f) return;
        if (isShowingChoices) return;

        bool advance = Input.GetKeyDown(KeyCode.Space)  ||
                       Input.GetKeyDown(KeyCode.Return) ||
                       Input.GetKeyDown(KeyCode.E)      ||
                       Input.GetMouseButtonDown(0);
        if (!advance) return;

        if (isTyping) SkipTyping();
        else          AdvanceLine();
    }

    // ─────────────────────────────────────────────────────────────────────
    public void StartDialogue(string speakerName, List<string> lines, Action onFinished = null)
        => StartDialogue(speakerName, lines, null, null, onFinished, null, null);

    public void StartDialogue(
        string                     speakerName,
        List<string>               lines,
        List<DialogueChoiceData>   choices,
        Action<DialogueChoiceData> onChoiceSelected,
        Action                     onFinished     = null,
        HashSet<int>               preUsedIndices = null,
        Sprite                     portrait       = null)
    {
        currentLines           = lines ?? new List<string>();
        currentLineIndex       = 0;
        pendingChoices         = choices;
        this.onChoiceSelected  = onChoiceSelected;
        this.onFinished        = onFinished;
        isShowingChoices       = false;
        closeAfterCurrentLines = false;

        usedChoiceIndices.Clear();
        if (preUsedIndices != null)
            foreach (var idx in preUsedIndices)
                usedChoiceIndices.Add(idx);

        if (speakerNameText != null) speakerNameText.text = speakerName;
        SetPortrait(portrait);

        if (currentLines.Count > 0)
        {
            ShowPanel();
            ShowLine(currentLines[0]);
        }
        else if (pendingChoices != null && HasAvailableChoices())
        {
            ShowPanel(() => ShowChoices());
        }
        else
        {
            ShowPanel(() => CloseDialogue());
        }
    }

    // ★ 암시 전용 — 화자 이름 포함
    public void ShowStandaloneLines(string speakerName, List<string> lines, Action onComplete)
    {
        currentLines           = lines ?? new List<string>();
        currentLineIndex       = 0;
        pendingChoices         = null;
        onChoiceSelected       = null;
        onFinished             = onComplete;
        isShowingChoices       = false;
        closeAfterCurrentLines = false;
        usedChoiceIndices.Clear();

        if (speakerNameText != null) speakerNameText.text = speakerName;
        SetPortrait(null); // 암시는 초상화 없음

        if (currentLines.Count > 0) { ShowPanel(); ShowLine(currentLines[0]); }
        else onComplete?.Invoke();
    }

    // 기존 호환용 (이름 없이 호출)
    public void ShowStandaloneLines(List<string> lines, Action onComplete)
        => ShowStandaloneLines("", lines, onComplete);

    public bool IsOpen() => dialoguePanelCG != null &&
                            dialoguePanelCG.gameObject.activeSelf &&
                            dialoguePanelCG.alpha > 0.5f;

    public HashSet<int> GetUsedChoiceIndices() => new HashSet<int>(usedChoiceIndices);
    public void Close() => CloseDialogue();

    // ─────────────────────────────────────────────────────────────────────
    private void SetPortrait(Sprite sprite)
    {
        if (portraitCG == null || portraitImage == null) return;
        if (sprite != null)
        {
            portraitImage.sprite = sprite;
            portraitCG.gameObject.SetActive(true);
            portraitCG.DOFade(1f, fadeInDuration);
        }
        else
        {
            portraitCG.alpha = 0f;
            portraitCG.gameObject.SetActive(false);
        }
    }

    private bool HasAvailableChoices()
    {
        if (pendingChoices == null) return false;
        for (int i = 0; i < pendingChoices.Count; i++)
        {
            var c = pendingChoices[i];
            if (c.isExitChoice) return true;
            if (usedChoiceIndices.Contains(i)) continue;
            if (!string.IsNullOrEmpty(c.blockIfFlag) &&
                GameFlags.Instance != null &&
                GameFlags.Instance.HasFlag(c.blockIfFlag)) continue;
            return true;
        }
        return false;
    }

    private bool WillShowChoicesNext()
        => !closeAfterCurrentLines &&
           pendingChoices != null &&
           HasAvailableChoices() &&
           currentLineIndex >= currentLines.Count - 1;

    // ─────────────────────────────────────────────────────────────────────
    private void AdvanceLine()
    {
        currentLineIndex++;
        if (currentLineIndex >= currentLines.Count)
        {
            if (closeAfterCurrentLines) { closeAfterCurrentLines = false; CloseDialogue(); return; }
            if (pendingChoices != null && HasAvailableChoices()) ShowChoices();
            else CloseDialogue();
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
        if (dialogueBodyText != null) dialogueBodyText.text = "";

        DOTween.To(() => 0, x =>
        {
            if (dialogueBodyText != null)
                dialogueBodyText.text = line.Substring(0, Mathf.Min(x, line.Length));
        }, line.Length, textTypeSpeed * line.Length)
        .SetId("dialogue_type").SetEase(Ease.Linear)
        .OnComplete(() =>
        {
            isTyping = false;
            if (dialogueBodyText != null) dialogueBodyText.text = line;
            if (nextIndicator != null) nextIndicator.SetActive(!WillShowChoicesNext());
        });
    }

    private void SkipTyping()
    {
        DOTween.Kill("dialogue_type");
        isTyping = false;
        if (dialogueBodyText != null && currentLineIndex < currentLines.Count)
            dialogueBodyText.text = currentLines[currentLineIndex];
        if (nextIndicator != null) nextIndicator.SetActive(!WillShowChoicesNext());
    }

    // ─────────────────────────────────────────────────────────────────────
    private void ShowChoices()
    {
        if (choicePanelCG == null || choiceContainer == null || choiceButtonPrefab == null)
        { Debug.LogWarning("[DialogueUI] 선택지 UI 미연결"); CloseDialogue(); return; }

        DOTween.Kill("dialogue_type");
        isTyping = false;
        if (dialogueBodyText != null) dialogueBodyText.text = "";
        if (nextIndicator != null) nextIndicator.SetActive(false);
        isShowingChoices = true;

        foreach (Transform child in choiceContainer) Destroy(child.gameObject);

        for (int i = 0; i < pendingChoices.Count; i++)
        {
            int capturedIndex = i;
            var choice = pendingChoices[i];
            var btn = Instantiate(choiceButtonPrefab, choiceContainer);

            var label = btn.GetComponentInChildren<TextMeshProUGUI>();
            if (label != null)
            {
                string labelText = choice.label;
                if (choice.costBonusPay > 0)
                    labelText += $" [{ScoringSystem.Instance?.BonusPay ?? 0}/{choice.costBonusPay}]";
                label.text = labelText;
            }

            bool isBlocked = !string.IsNullOrEmpty(choice.blockIfFlag) &&
                             GameFlags.Instance != null &&
                             GameFlags.Instance.HasFlag(choice.blockIfFlag);
            bool isUsed = !choice.isExitChoice && (usedChoiceIndices.Contains(i) || isBlocked);
            bool canAfford = choice.costBonusPay <= 0 ||
                             (ScoringSystem.Instance?.BonusPay ?? 0) >= choice.costBonusPay;

            btn.interactable = !isUsed && canAfford;
            if (isUsed) { var img = btn.GetComponent<Image>(); if (img) img.color = usedChoiceColor; }

            btn.onClick.AddListener(() => OnChoiceClicked(capturedIndex, choice));
        }

        choicePanelCG.gameObject.SetActive(true);
        choicePanelCG.alpha = 0f;
        choicePanelCG.DOFade(1f, fadeInDuration).OnComplete(() =>
        {
            choicePanelCG.interactable = true;
            choicePanelCG.blocksRaycasts = true;
        });
    }

    private void HideChoices(Action onComplete)
    {
        if (choicePanelCG == null || !choicePanelCG.gameObject.activeSelf)
        { onComplete?.Invoke(); return; }

        choicePanelCG.interactable = false;
        choicePanelCG.blocksRaycasts = false;
        choicePanelCG.DOFade(0f, fadeOutDuration).OnComplete(() =>
        {
            choicePanelCG.gameObject.SetActive(false);
            foreach (Transform child in choiceContainer) Destroy(child.gameObject);
            onComplete?.Invoke();
        });
    }

    private void OnChoiceClicked(int choiceIndex, DialogueChoiceData choice)
    {
        AudioManager.Instance?.PlaySfxDialogueNext();
        onChoiceSelected?.Invoke(choice);
        if (!choice.isExitChoice) usedChoiceIndices.Add(choiceIndex);
        bool shouldClose = choice.isExitChoice || choice.isUniqueChoice;

        HideChoices(() =>
        {
            isShowingChoices = false;
            bool hasLines = choice.lines != null && choice.lines.Count > 0;
            if (hasLines)
            {
                currentLines = choice.lines;
                currentLineIndex = 0;
                if (shouldClose) closeAfterCurrentLines = true;
                ShowLine(currentLines[0]);
            }
            else
            {
                if (shouldClose) { CloseDialogue(); return; }
                if (HasAvailableChoices()) ShowChoices();
                else CloseDialogue();
            }
        });
    }

    // ─────────────────────────────────────────────────────────────────────
    private void ShowPanel(Action onShown = null)
    {
        dialoguePanelCG.gameObject.SetActive(true);
        dialoguePanelCG.alpha = 0f;
        dialoguePanelCG.interactable = false;
        dialoguePanelCG.blocksRaycasts = false;
        dialoguePanelCG.DOFade(1f, fadeInDuration).OnComplete(() =>
        {
            dialoguePanelCG.interactable = true;
            dialoguePanelCG.blocksRaycasts = true;
            onShown?.Invoke();
        });
    }

    private void CloseDialogue()
    {
        AudioManager.Instance?.PlaySfxDialogueClose();
        DOTween.Kill("dialogue_type");
        closeAfterCurrentLines = false;

        // ★ 초상화 페이드 아웃
        if (portraitCG != null)
            portraitCG.DOFade(0f, fadeOutDuration)
                      .OnComplete(() => portraitCG.gameObject.SetActive(false));

        dialoguePanelCG.interactable = false;
        dialoguePanelCG.blocksRaycasts = false;
        dialoguePanelCG.DOFade(0f, fadeOutDuration).OnComplete(() =>
        {
            dialoguePanelCG.gameObject.SetActive(false);
            onFinished?.Invoke();
        });
    }
}
