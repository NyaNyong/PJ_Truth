using DG.Tweening;
using UnityEngine;

public class InteractPromptHost : MonoBehaviour
{
    [SerializeField] private GameObject promptGO;

    private Vector3 originalScale; // ★

    private void Awake()
    {
        // ★ Inspector에서 설정한 크기를 기준으로 저장
        if (promptGO != null)
            originalScale = promptGO.transform.localScale;
    }

    public void SetVisible(bool visible)
    {
        if (promptGO == null) return;
        promptGO.transform.DOKill();
        if (visible)
        {
            promptGO.SetActive(true);
            promptGO.transform.localScale = Vector3.zero;
            promptGO.transform.DOScale(originalScale, 0.15f).SetEase(Ease.OutBack); // ★
        }
        else
        {
            promptGO.transform.DOScale(Vector3.zero, 0.1f).SetEase(Ease.InBack)
                .OnComplete(() => promptGO.SetActive(false));
        }
    }

    private void OnDisable() => promptGO?.SetActive(false);
}