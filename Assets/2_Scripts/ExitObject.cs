using UnityEngine;
using System.Collections.Generic;
using DG.Tweening;

/// <summary>
/// IInteractable 구현 — 탈출/퇴장 오브젝트 (엘리베이터, 출구 등).
/// requireAllClues가 true이면 탐색 완료 전 퇴장 시 경고 대화를 표시합니다.
/// </summary>
public class ExitObject : MonoBehaviour, IInteractable
{
    [Header("설정")]
    [Tooltip("E키 안내 문구")]
    [TextArea(1, 2)]
    [SerializeField] private string exitPromptText = "퇴근하시겠습니까?";

    [Tooltip("true면 필수 단서 미수집 시 경고 대화 표시. false면 언제든 퇴장 가능")]
    [SerializeField] private bool requireAllClues = true;

    [Header("탐색 완료 판정")]
    [Tooltip("NightPhaseManager 참조 — requireAllClues가 true일 때만 필요")]
    [SerializeField] private NightPhaseManager nightPhaseManager;

    [Header("연출")]
    [Tooltip("탈출 시 화면 암전 CanvasGroup (전체화면 검은 패널)")]
    [SerializeField] private CanvasGroup fadeOutOverlayCG;
    [SerializeField] private float fadeOutDuration = 0.5f;

    // 미완료 경고 대화 내용
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

        // 필수 단서 체크
        if (requireAllClues && nightPhaseManager != null
            && nightPhaseManager.HasRequiredClues
            && !nightPhaseManager.IsExplorationComplete)
        {
            if (!warningShown)
            {
                // 첫 시도: 경고 대화 표시
                warningShown = true;
                DialogueUI.Instance?.StartDialogue(
                    "출구",
                    incompleteWarning,
                    () => player.NotifyInteractionEnded()
                );
                return;
            }
            // 두 번째 시도: 경고 무시하고 퇴장
        }

        warningShown = false;
        DoExit(player);
    }

    private void DoExit(PlayerController player)
    {
        Debug.Log($"🚪 [ExitObject] {exitPromptText}");

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
        // 씬 전환/비활성화 시 암전 오버레이 정리
        if (fadeOutOverlayCG != null)
        {
            fadeOutOverlayCG.DOKill();
            fadeOutOverlayCG.alpha = 0f;
            fadeOutOverlayCG.gameObject.SetActive(false);
        }
        warningShown = false;
    }
}