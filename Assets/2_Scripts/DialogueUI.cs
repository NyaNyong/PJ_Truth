using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;
using System.Collections.Generic;
using System;

/// <summary>
/// NPC 대화 및 단서 설명을 표시하는 UI 매니저.
/// 씬에 하나만 존재하며, NPCInteractable과 ClueObject가 이 UI를 사용합니다.
/// </summary>
public class DialogueUI : MonoBehaviour
{
    public static DialogueUI Instance { get; private set; }

    [Header("UI 연결")]
    [SerializeField] private CanvasGroup dialoguePanelCG;
    [SerializeField] private TextMeshProUGUI speakerNameText;
    [SerializeField] private TextMeshProUGUI dialogueBodyText;
    [SerializeField] private GameObject nextIndicator; // "▼ E키로 다음" 표시

    [Header("DOTween 설정")]
    [SerializeField] private float fadeInDuration  = 0.25f;
    [SerializeField] private float fadeOutDuration = 0.2f;
    [SerializeField] private float textTypeSpeed   = 0.03f; // 타이핑 효과 속도

    private List<string> currentLines = new List<string>();
    private int currentLineIndex = 0;
    private bool isTyping = false;
    private Action onFinished;

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
    }

    private void Update()
    {
        if (!IsOpen()) return;

        if (Input.GetKeyDown(KeyCode.E))
        {
            if (isTyping)
                SkipTyping();       // 타이핑 중이면 즉시 완성
            else
                AdvanceLine();      // 완성된 상태면 다음 줄로
        }
    }

    // ─────────────────────────────────────────
    // 공개 인터페이스
    // ─────────────────────────────────────────

    /// <summary>대화를 시작합니다.</summary>
    public void StartDialogue(string speakerName, List<string> lines, Action onDialogueFinished = null)
    {
        currentLines     = lines;
        currentLineIndex = 0;
        onFinished       = onDialogueFinished;

        if (speakerNameText != null)
            speakerNameText.text = speakerName;

        ShowPanel();
        ShowLine(currentLines[0]);
    }

    public bool IsOpen() => dialoguePanelCG != null &&
                            dialoguePanelCG.gameObject.activeSelf &&
                            dialoguePanelCG.alpha > 0.5f;

    // ─────────────────────────────────────────
    // 내부 로직
    // ─────────────────────────────────────────

    private void AdvanceLine()
    {
        currentLineIndex++;

        if (currentLineIndex >= currentLines.Count)
        {
            CloseDialogue();
            return;
        }

        ShowLine(currentLines[currentLineIndex]);
    }

    private void ShowLine(string line)
    {
        DOTween.Kill("dialogue_type");

        if (nextIndicator != null) nextIndicator.SetActive(false);

        isTyping = true;
        dialogueBodyText.text = "";
        string fullText = line;

        DOTween.To(() => 0, x =>
        {
            dialogueBodyText.text = fullText.Substring(0, Mathf.Min(x, fullText.Length));
        }, fullText.Length, textTypeSpeed * fullText.Length)
        .SetId("dialogue_type")
        .SetEase(Ease.Linear)
        .OnComplete(() =>
        {
            isTyping = false;
            dialogueBodyText.text = fullText;
            if (nextIndicator != null) nextIndicator.SetActive(true);
        });
    }

    private void SkipTyping()
    {
        DOTween.Kill("dialogue_type");
        isTyping = false;
        dialogueBodyText.text = currentLines[currentLineIndex];
        if (nextIndicator != null) nextIndicator.SetActive(true);
    }

    private void ShowPanel()
    {
        dialoguePanelCG.gameObject.SetActive(true);
        dialoguePanelCG.alpha = 0f;
        dialoguePanelCG.DOFade(1f, fadeInDuration)
            .OnComplete(() =>
            {
                dialoguePanelCG.interactable   = true;
                dialoguePanelCG.blocksRaycasts = true;
            });
    }

    private void CloseDialogue()
    {
        DOTween.Kill("dialogue_type");
        dialoguePanelCG.interactable   = false;
        dialoguePanelCG.blocksRaycasts = false;
        dialoguePanelCG.DOFade(0f, fadeOutDuration)
            .OnComplete(() =>
            {
                dialoguePanelCG.gameObject.SetActive(false);
                onFinished?.Invoke();
            });
    }
}
