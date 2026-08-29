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

    [Header("상호작용 조건")]
    [Tooltip("이 단서 조사에 필요한 플래그 (비어있으면 항상 조사 가능)")]
    [SerializeField] private string requiredFlag = "";
    [SerializeField] private string lockedMessage = "(아직 접근할 수 없다.)";

    // ★ 추가 — 이 단서를 수집하면 다른 오브젝트를 활성화 (예: 다음 단서 공개)
    [Header("수집 시 연동 (선택)")]
    [SerializeField] private GameObject revealOnCollect;

    // ★ 추가 — 조사 완료 후 강제 이동 + 자동 대화 시작 (특수 단서용)
    [Header("조사 후 강제 이동 (선택)")]
    [SerializeField] private Transform forcedMoveTarget;
    [SerializeField] private NPCInteractable forcedMoveTargetNpc;
    [SerializeField] private float forcedMoveDuration = 1.2f;

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

        if (!string.IsNullOrEmpty(requiredFlag) &&
            !(GameFlags.Instance?.HasFlag(requiredFlag) ?? false))
        {
            DialogueUI.Instance?.StartDialogue("???",
                new List<string> { lockedMessage },
                () => cachedPlayer?.NotifyInteractionEnded());
            return;
        }

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
        DialogueUI.Instance?.StartDialogue(title, lines, OnClueLinesFinished); // ★ 수정
    }

    // ★ 추가 — 본문 다 본 뒤, 독백이 있으면 화자를 플레이어로 바꿔서 한 번 더 보여줌
    private void OnClueLinesFinished()
    {
        var data = GameTextLoader.Instance?.GetClue(clueID);
        var monologue = data?.monologueLines;

        if (monologue != null && monologue.Count > 0)
        {
            string playerName = PlayerData.Instance?.PlayerName ?? "";
            DialogueUI.Instance?.StartDialogue(playerName, monologue, OnExamineFinished);
        }
        else
        {
            OnExamineFinished();
        }
    }

    private void OnExamineFinished()
    {
        cachedPlayer?.NotifyInteractionEnded();
        if (isCollected) return;
        Collect();

        if (forcedMoveTarget != null && cachedPlayer != null)
        {
            cachedPlayer.ForceMoveTo(forcedMoveTarget.position, forcedMoveDuration, () =>
            {
                // ★ 수정 — NPC 활성화만 하고, 자동 대화 시작은 제거 (위치 이동만)
                if (forcedMoveTargetNpc != null)
                    forcedMoveTargetNpc.gameObject.SetActive(true);
            });
        }
    }

    // ── UV 퍼즐 ──────────────────────────────
    private void InteractAsPuzzle()
    {
        if (UVPuzzleUI.Instance == null) return;
        if (UVPuzzleUI.Instance.IsOpen()) return;

        var data = GameTextLoader.Instance?.GetClue(clueID);
        string hidden = (data != null && !string.IsNullOrEmpty(data.hiddenContent))
                         ? data.hiddenContent : hiddenContent;
        string answer = (data != null && !string.IsNullOrEmpty(data.correctAnswer))
                         ? data.correctAnswer : correctAnswer;
        string flag = (data != null && !string.IsNullOrEmpty(data.flagIDOnSolve))
                         ? data.flagIDOnSolve : flagIDOnSolve;

        UVPuzzleUI.Instance.OpenPuzzle(hidden, answer, () =>
        {
            if (!string.IsNullOrEmpty(flag)) GameFlags.Instance?.SetFlag(flag);
            DOVirtual.DelayedCall(0.35f, ShowClueDialogue);
        });
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

        if (revealOnCollect != null) revealOnCollect.SetActive(true); // ★ 추가

        var col = GetComponent<Collider2D>();
        if (col != null) col.enabled = false;

        if (hideOnCollect)
        {
            transform.DOKill();
            transform.DOScale(Vector3.zero, 0.3f).SetEase(Ease.InBack).SetDelay(0.2f);
            DOVirtual.DelayedCall(0.55f, () =>
            {
                if (this != null && gameObject != null)
                    gameObject.SetActive(false);
            });
        }
        else
        {
            gameObject.SetActive(false);
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