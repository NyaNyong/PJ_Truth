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

    private List<DialogueChoiceData> pendingChoices;
    private Action<DialogueChoiceData> onChoiceSelected;
    private bool isShowingChoices = false;
    private HashSet<int> usedChoiceIndices = new HashSet<int>();
    private bool closeAfterCurrentLines = false;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        SetCGHidden(dialoguePanelCG);
        SetCGHidden(choicePanelCG);
    }

    private void SetCGHidden(CanvasGroup cg)
    {
        if (cg == null) return;
        cg.alpha = 0f;
        cg.interactable = false;
        cg.blocksRaycasts = false;
        cg.gameObject.SetActive(false);
    }

    private void Update()
    {
        if (dialoguePanelCG == null || !dialoguePanelCG.gameObject.activeSelf || dialoguePanelCG.alpha < 0.5f) return;
        if (isShowingChoices) return;

        bool advance = Input.GetKeyDown(KeyCode.Space) ||
                       Input.GetKeyDown(KeyCode.Return) ||
                       Input.GetKeyDown(KeyCode.E) ||
                       Input.GetMouseButtonDown(0);
        if (!advance) return;

        if (isTyping) SkipTyping();
        else AdvanceLine();
    }

    // ─────────────────────────────────────────────────────────────────────
    // 공개 API
    // ─────────────────────────────────────────────────────────────────────

    public void StartDialogue(string speakerName, List<string> lines, Action onFinished = null)
        => StartDialogue(speakerName, lines, null, null, onFinished, null);

    /// <param name="preUsedIndices">이전 세션에서 이미 사용한 선택지 인덱스 (재대화 시 전달)</param>
    public void StartDialogue(
        string speakerName,
        List<string> lines,
        List<DialogueChoiceData> choices,
        Action<DialogueChoiceData> onChoiceSelected,
        Action onFinished = null,
        HashSet<int> preUsedIndices = null)
    {
        currentLines = lines ?? new List<string>();
        currentLineIndex = 0;
        pendingChoices = choices;
        this.onChoiceSelected = onChoiceSelected;
        this.onFinished = onFinished;
        isShowingChoices = false;
        closeAfterCurrentLines = false;

        usedChoiceIndices.Clear();
        if (preUsedIndices != null)
            foreach (var idx in preUsedIndices)
                usedChoiceIndices.Add(idx);

        if (speakerNameText != null) speakerNameText.text = speakerName;

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

    public bool IsOpen() => dialoguePanelCG != null &&
                            dialoguePanelCG.gameObject.activeSelf &&
                            dialoguePanelCG.alpha > 0.5f;

    public HashSet<int> GetUsedChoiceIndices() => new HashSet<int>(usedChoiceIndices);

    // ─────────────────────────────────────────────────────────────────────

    private void AdvanceLine()
    {
        currentLineIndex++;
        if (currentLineIndex >= currentLines.Count)
        {
            if (closeAfterCurrentLines)
            {
                closeAfterCurrentLines = false;
                CloseDialogue();
                return;
            }
            // ★ 표시할 선택지(비종료 미사용 or 종료)가 있으면 선택지 패널, 없으면 종료
            if (pendingChoices != null && HasAvailableChoices())
                ShowChoices();
            else
                CloseDialogue();
            return;
        }
        AudioManager.Instance?.PlaySfxDialogueNext();
        ShowLine(currentLines[currentLineIndex]);
    }

    // ─────────────────────────────────────────────────────────────────────
    // 선택지 가용 여부 판별
    // ─────────────────────────────────────────────────────────────────────

    /// <summary>
    /// 표시할 선택지가 있는지 확인.
    /// - 비종료 선택지 중 미사용 & 미차단 항목이 있으면 true
    /// - 종료 선택지(isExitChoice)가 하나라도 있으면 항상 true
    /// </summary>
    private bool HasAvailableChoices()
    {
        if (pendingChoices == null) return false;

        for (int i = 0; i < pendingChoices.Count; i++)
        {
            var c = pendingChoices[i];

            // 종료 선택지는 항상 표시
            if (c.isExitChoice) return true;

            // 비종료 선택지: 사용 여부 + blockIfFlag 체크
            if (usedChoiceIndices.Contains(i)) continue;

            if (!string.IsNullOrEmpty(c.blockIfFlag) &&
                GameFlags.Instance != null &&
                GameFlags.Instance.HasFlag(c.blockIfFlag)) continue;

            return true;
        }
        return false;
    }

    /// <summary>nextIndicator 표시용 — 현재 라인이 마지막이고 선택지가 뜰 예정이면 false</summary>
    private bool WillShowChoicesNext()
    {
        return !closeAfterCurrentLines &&
               pendingChoices != null &&
               HasAvailableChoices() &&
               currentLineIndex >= currentLines.Count - 1;
    }

    // ─────────────────────────────────────────────────────────────────────

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
        .SetId("dialogue_type")
        .SetEase(Ease.Linear)
        .OnComplete(() =>
        {
            isTyping = false;
            if (dialogueBodyText != null) dialogueBodyText.text = line;
            // ★ WillShowChoicesNext()로 통합
            if (nextIndicator != null) nextIndicator.SetActive(!WillShowChoicesNext());
        });
    }

    private void SkipTyping()
    {
        DOTween.Kill("dialogue_type");
        isTyping = false;
        if (dialogueBodyText != null && currentLineIndex < currentLines.Count)
            dialogueBodyText.text = currentLines[currentLineIndex];
        // ★ WillShowChoicesNext()로 통합
        if (nextIndicator != null) nextIndicator.SetActive(!WillShowChoicesNext());
    }

    // ─────────────────────────────────────────────────────────────────────

    private void ShowChoices()
    {
        if (choicePanelCG == null || choiceContainer == null || choiceButtonPrefab == null)
        {
            Debug.LogWarning("[DialogueUI] 선택지 UI 미연결"); CloseDialogue(); return;
        }

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
                {
                    int bp = ScoringSystem.Instance?.BonusPay ?? 0;
                    labelText += $" [{bp}/{choice.costBonusPay}]";
                }
                label.text = labelText;
            }

            // ★ isUsed: 세션 사용 여부 + blockIfFlag 차단 여부 (종료 선택지 제외)
            bool isBlocked = !string.IsNullOrEmpty(choice.blockIfFlag) &&
                             GameFlags.Instance != null &&
                             GameFlags.Instance.HasFlag(choice.blockIfFlag);
            bool isUsed = !choice.isExitChoice && (usedChoiceIndices.Contains(i) || isBlocked);

            bool canAfford = choice.costBonusPay <= 0 ||
                             (ScoringSystem.Instance?.BonusPay ?? 0) >= choice.costBonusPay;
            btn.interactable = !isUsed && canAfford;

            if (isUsed)
            {
                var img = btn.GetComponent<Image>();
                if (img != null) img.color = usedChoiceColor;
            }

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

        if (!choice.isExitChoice)
            usedChoiceIndices.Add(choiceIndex);

        // ★ isUniqueChoice도 isExitChoice와 동일하게 대화 종료
        bool shouldClose = choice.isExitChoice || choice.isUniqueChoice;

        HideChoices(() =>
        {
            isShowingChoices = false;

            bool hasLines = choice.lines != null && choice.lines.Count > 0;

            if (hasLines)
            {
                currentLines = choice.lines;
                currentLineIndex = 0;
                if (shouldClose) closeAfterCurrentLines = true; // ★
                ShowLine(currentLines[0]);
            }
            else
            {
                if (shouldClose) { CloseDialogue(); return; } // ★
                if (HasAvailableChoices())
                    ShowChoices();
                else
                    CloseDialogue();
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
        dialoguePanelCG.interactable = false;
        dialoguePanelCG.blocksRaycasts = false;
        dialoguePanelCG.DOFade(0f, fadeOutDuration).OnComplete(() =>
        {
            dialoguePanelCG.gameObject.SetActive(false);
            onFinished?.Invoke();
        });
    }

    /// <summary>암시 씬 전용 — NPC 없이 라인만 표시, 완료 후 onComplete 호출</summary>
    public void ShowStandaloneLines(List<string> lines, Action onComplete)
    {
        currentLines = lines ?? new List<string>();
        currentLineIndex = 0;
        pendingChoices = null;
        onChoiceSelected = null;
        onFinished = onComplete;
        isShowingChoices = false;
        closeAfterCurrentLines = false;
        usedChoiceIndices.Clear();

        if (speakerNameText != null) speakerNameText.text = "";

        if (currentLines.Count > 0)
        {
            ShowPanel();
            ShowLine(currentLines[0]);
        }
        else
        {
            onComplete?.Invoke();
        }
    }

    public void Close() => CloseDialogue();
}
