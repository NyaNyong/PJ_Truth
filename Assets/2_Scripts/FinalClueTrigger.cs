using UnityEngine;

/// <summary>
/// 상위 기록실의 "마지막 단서" 오브젝트.
/// 상호작용 시 플레이어를 강제로 특정 위치(흑막 단말기 앞)로 이동시키고,
/// 도착하면 자동으로 흑막 NPC와의 대화를 시작한다.
/// </summary>
public class FinalClueTrigger : MonoBehaviour, IInteractable
{
    [Tooltip("이 단서 확인 시 부여할 클루 ID (선택)")]
    [SerializeField] private string clueID = "";

    [Tooltip("강제 이동시킬 목표 위치 (흑막 단말기 앞)")]
    [SerializeField] private Transform forcedMoveTarget;

    [Tooltip("도착 후 자동으로 대화를 시작할 NPC")]
    [SerializeField] private NPCInteractable finalNpc;

    [SerializeField] private float moveDuration = 1.2f;

    private bool used = false;

    public void Interact(PlayerController player)
    {
        if (used) return;
        used = true;

        if (!string.IsNullOrEmpty(clueID))
            GameFlags.Instance?.AddClue(clueID);

        if (forcedMoveTarget == null)
        {
            finalNpc?.Interact(player);
            return;
        }

        player.ForceMoveTo(forcedMoveTarget.position, moveDuration, () =>
        {
            finalNpc?.Interact(player);
        });
    }
}