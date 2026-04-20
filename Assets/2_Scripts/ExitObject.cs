using UnityEngine;
using DG.Tweening;
using Sirenix.OdinInspector;

/// <summary>
/// IInteractable 구현 — 탈출/퇴장 오브젝트 (엘리베이터, 출구 등).
/// 플레이어가 E키를 누르면 확인 다이얼로그 없이 바로 탐색 종료 신호를 발행합니다.
/// </summary>
public class ExitObject : MonoBehaviour, IInteractable
{
    [BoxGroup("설정")]
    [Tooltip("E키 안내 문구 (NPC 대화 UI가 없을 경우 Debug.Log로 출력)")]
    [TextArea(1, 2)]
    [SerializeField] private string exitPromptText = "퇴근하시겠습니까?";

    [BoxGroup("연출")]
    [Tooltip("탈출 시 화면 암전 연출에 사용할 CanvasGroup (전체화면 검은 패널)")]
    [SerializeField] private CanvasGroup fadeOutOverlayCG;

    [BoxGroup("연출")]
    [SerializeField] private float fadeOutDuration = 0.5f;

    public void Interact(PlayerController player)
    {
        Debug.Log($"🚪 [ExitObject] {exitPromptText}");

        if (fadeOutOverlayCG != null)
        {
            // 화면 암전 후 탐색 종료
            fadeOutOverlayCG.gameObject.SetActive(true);
            fadeOutOverlayCG.alpha = 0f;
            fadeOutOverlayCG.DOFade(1f, fadeOutDuration)
                            .SetEase(Ease.InQuad)
                            .OnComplete(() => player.NotifyExitLocation());
        }
        else
        {
            // 암전 패널 없으면 즉시 종료
            player.NotifyExitLocation();
        }
    }
}
