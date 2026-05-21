using UnityEngine;
using System.Collections.Generic;
using DG.Tweening;

public class ExitObject : MonoBehaviour, IInteractable
{
    [Header("설정")]
    [TextArea(1, 2)]
    [SerializeField] private string exitPromptText = "퇴근하시겠습니까?";
    [Tooltip("true면 필수 단서 미수집 시 경고 대화 표시")]
    [SerializeField] private bool requireAllClues = true;

    [Header("탐색 완료 판정")]
    [SerializeField] private NightPhaseManager nightPhaseManager;

    [Header("퇴근 확인 팝업")]
    [Tooltip("requireAllLocations 체크용. 없으면 '집으로' 고정")]
    [SerializeField] private GameManager gameManager;

    [Header("연출")]
    [SerializeField] private CanvasGroup fadeOutOverlayCG;
    [SerializeField] private float fadeOutDuration = 0.5f;

    private static readonly List<string> incompleteWarning = new List<string>
    {
        "아직 조사하지 않은 단서가 있습니다.",
        "그래도 퇴근하시겠습니까? (다시 E키)"
    };

    private bool warningShown = false;
    private PlayerController cachedPlayer;

    public void Interact(PlayerController player)
    {
        cachedPlayer = player;

        // 필수 단서 미수집 경고
        if (requireAllClues && nightPhaseManager != null
            && nightPhaseManager.HasRequiredClues
            && !nightPhaseManager.IsExplorationComplete)
        {
            if (!warningShown)
            {
                warningShown = true;
                DialogueUI.Instance?.StartDialogue(
                    "출구", incompleteWarning,
                    () => player.NotifyInteractionEnded());
                return;
            }
        }

        warningShown = false;
        DoExit(player);
    }

    private void DoExit(PlayerController player)
    {
        Debug.Log($"🚪 [ExitObject] {exitPromptText}");

        // 팝업 동안 이동 방지
        player.EnableControl(false);

        if (ConfirmPopupUI.Instance == null)
        {
            ExecuteExit(player);
            return;
        }

        string title = (gameManager != null && gameManager.ShouldReturnToMap())
            ? "다른 곳으로 가시겠습니까?"
            : "집으로 돌아가시겠습니까?";

        ConfirmPopupUI.Instance.Open(
            title,
            "",
            "다시 조사할 수 없습니다",
            onConfirm: () => ExecuteExit(player),
            onCancel: () => player.EnableControl(true), // 아니요 → 조작 재개
            confirmText: "예",
            cancelText: "아니요"
        );
    }

    private void ExecuteExit(PlayerController player)
    {
        if (fadeOutOverlayCG != null)
        {
            fadeOutOverlayCG.gameObject.SetActive(true);
            fadeOutOverlayCG.alpha = 0f;
            fadeOutOverlayCG.DOFade(1f, fadeOutDuration)
                .SetEase(Ease.InQuad)
                .OnComplete(() => player.NotifyExitLocation());
        }
        else
        {
            player.NotifyExitLocation();
        }
    }

    private void OnDisable()
    {
        if (fadeOutOverlayCG != null)
        {
            fadeOutOverlayCG.DOKill();
            fadeOutOverlayCG.alpha = 0f;
            fadeOutOverlayCG.gameObject.SetActive(false);
        }
        warningShown = false;
    }
}