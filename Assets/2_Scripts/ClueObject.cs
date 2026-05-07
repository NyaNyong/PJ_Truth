using UnityEngine;
using System.Collections.Generic;
using DG.Tweening;

public class ClueObject : MonoBehaviour, IInteractable
{
    [Header("단서 정보")]
    [Tooltip("GameFlags 등록 ID + day_XX.json clues[].id 와 일치")]
    [SerializeField] private string clueID = "clue_001";

    [Header("Inspector 폴백 (JSON 미사용 시)")]
    [SerializeField] private string clueTitle = "단서";
    [SerializeField] private List<string> clueDescriptionLines = new List<string>();

    [Header("UV 퍼즐 모드")]
    [Tooltip("true면 E키 시 DialogueUI 대신 UVPuzzleUI를 엽니다")]
    [SerializeField] private bool isPuzzle = false;
    [SerializeField] private string hiddenContent = "숨겨진 단서 텍스트";
    [SerializeField] private string correctAnswer = "정답";
    [Tooltip("퍼즐 완료 시 추가로 세울 플래그 (없으면 빈칸)")]
    [SerializeField] private string flagIDOnSolve = "";

    [Header("화살표 / 탐지")]
    [Tooltip("0이면 BoxCollider2D 크기 x2로 자동 계산")]
    [SerializeField] private float detectionRadius = 0f;
    [Tooltip("자식 오브젝트의 느낌표 SpriteRenderer")]
    [SerializeField] private SpriteRenderer exclamationRenderer = null;

    [Header("설정")]
    [SerializeField] private bool hideOnCollect = true;
    [SerializeField] private bool canReexamine = false;

    private bool isCollected = false;
    private PlayerController cachedPlayer = null;
    private SpriteRenderer sr;

    public float DetectionRadius => detectionRadius;

    // ── 초기화 ───────────────────────────────
    private void Awake()
    {
        sr = GetComponent<SpriteRenderer>();

        if (detectionRadius <= 0f)
        {
            var col = GetComponent<BoxCollider2D>();
            detectionRadius = col != null
                ? Mathf.Max(col.bounds.extents.x, col.bounds.extents.y) * 4f
                : 2f;
        }

        if (sr != null)
            sr.color = new Color(sr.color.r, sr.color.g, sr.color.b, 0f);

        if (exclamationRenderer != null)
            exclamationRenderer.color = new Color(
                exclamationRenderer.color.r, exclamationRenderer.color.g,
                exclamationRenderer.color.b, 0f);
    }

    private void Start()
    {
        if (!isCollected)
            ClueArrowSystem.Instance?.Register(this);
    }

    private void OnDisable()
    {
        ClueArrowSystem.Instance?.Unregister(this);
    }

    // ── 상호작용 ─────────────────────────────
    public void Interact(PlayerController player)
    {
        cachedPlayer = player;

        if (isCollected && !canReexamine)
        {
            var (t, _) = ResolveTextData();
            DialogueUI.Instance?.StartDialogue(t,
                new List<string> { "(이미 조사한 흔적이다.)" },
                () => cachedPlayer?.NotifyInteractionEnded());
            return;
        }

        if (isPuzzle)
            InteractAsPuzzle();
        else
            InteractAsClue();
    }

    // ── 일반 단서 ────────────────────────────
    private void InteractAsClue()
    {
        if (DialogueUI.Instance != null && DialogueUI.Instance.IsOpen()) return;
        var (title, lines) = ResolveTextData();
        DialogueUI.Instance?.StartDialogue(title, lines, OnExamineFinished);
    }

    private void OnExamineFinished()
    {
        cachedPlayer?.NotifyInteractionEnded();
        if (isCollected) return;
        Collect();
    }

    // ── UV 퍼즐 ──────────────────────────────
    private void InteractAsPuzzle()
    {
        if (UVPuzzleUI.Instance == null)
        {
            Debug.LogWarning("[ClueObject] UVPuzzleUI가 씬에 없습니다.");
            return;
        }
        if (UVPuzzleUI.Instance.IsOpen()) return;

        UVPuzzleUI.Instance.OpenPuzzle(hiddenContent, correctAnswer, OnPuzzleSolved);
    }

    private void OnPuzzleSolved()
    {
        if (!string.IsNullOrEmpty(flagIDOnSolve))
            GameFlags.Instance?.SetFlag(flagIDOnSolve);

        Debug.Log($"🔦 [UV 퍼즐 완료] clueID: {clueID}");

        // 퍼즐 패널 페이드아웃(0.3s) 후 단서 대화창
        DOVirtual.DelayedCall(0.35f, ShowClueDialogue);
    }

    private void ShowClueDialogue()
    {
        var (title, lines) = ResolveTextData();
        DialogueUI.Instance?.StartDialogue(title, lines, OnClueDialogueFinished);
    }

    private void OnClueDialogueFinished()
    {
        if (!isCollected) Collect();
        cachedPlayer?.NotifyInteractionEnded();
    }

    // ── 공통 수집 처리 ────────────────────────
    private void Collect()
    {
        isCollected = true;
        GameFlags.Instance?.AddClue(clueID);
        cachedPlayer?.NotifyClueCollected();
        ClueArrowSystem.Instance?.Unregister(this);

        if (hideOnCollect)
        {
            transform.DOScale(Vector3.zero, 0.3f)
                .SetEase(Ease.InBack).SetDelay(0.2f)
                .OnComplete(() => gameObject.SetActive(false));
        }
    }

    // ── 화살표 시스템 ─────────────────────────
    public void ShowExclamation(bool show, float duration)
    {
        if (exclamationRenderer == null) return;
        Color target = new Color(
            exclamationRenderer.color.r,
            exclamationRenderer.color.g,
            exclamationRenderer.color.b,
            show ? 1f : 0f);
        exclamationRenderer.DOKill();
        if (duration <= 0f) { exclamationRenderer.color = target; return; }
        exclamationRenderer.DOColor(target, duration);
    }

    private (string title, List<string> lines) ResolveTextData()
    {
        if (GameTextLoader.Instance != null)
        {
            var data = GameTextLoader.Instance.GetClue(clueID);
            if (data != null) return (data.title, data.lines);
        }
        return (clueTitle, clueDescriptionLines);
    }
}