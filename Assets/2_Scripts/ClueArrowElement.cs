using UnityEngine;
using DG.Tweening;

[RequireComponent(typeof(CanvasGroup))]
public class ClueArrowElement : MonoBehaviour
{
    private CanvasGroup cg;
    private RectTransform rt;
    private bool currentVisible = false;

    private void Awake()
    {
        cg = GetComponent<CanvasGroup>();
        rt = GetComponent<RectTransform>();
    }

    public void UpdateTransform(Vector2 screenPos, float angleDeg)
    {
        rt.position = new Vector3(screenPos.x, screenPos.y, 0f);
        rt.rotation = Quaternion.Euler(0f, 0f, angleDeg);
    }

    public void SetVisible(bool visible, float duration)
    {
        if (currentVisible == visible) return;
        currentVisible = visible;
        cg.DOKill();
        if (duration <= 0f) { cg.alpha = visible ? 1f : 0f; return; }
        cg.DOFade(visible ? 1f : 0f, duration);
    }
}