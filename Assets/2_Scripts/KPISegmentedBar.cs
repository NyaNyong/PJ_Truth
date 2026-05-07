using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using System.Collections.Generic;

public class KPISegmentedBar : MonoBehaviour
{
    [Header("블록 스프라이트")]
    [SerializeField] private Sprite spriteLeft;   // Image_0
    [SerializeField] private Sprite spriteMiddle; // Image_1
    [SerializeField] private Sprite spriteRight;  // Image_2

    [Header("블록 설정")]
    [SerializeField] private GameObject segmentPrefab;
    [Tooltip("총 블록 수 (왼쪽 1 + 중간 N + 오른쪽 1)")]
    [SerializeField] private int totalSegments = 8;
    [SerializeField] private Color activeColor = Color.white;
    [SerializeField] private Color inactiveColor = new Color(1f, 1f, 1f, 0.15f);

    [Header("애니메이션")]
    [SerializeField] private float segmentDelay = 0.06f;

    private readonly List<Image> segments = new List<Image>();

    private void Awake() => SpawnSegments();

    private void SpawnSegments()
    {
        foreach (Transform child in transform) Destroy(child.gameObject);
        segments.Clear();

        for (int i = 0; i < totalSegments; i++)
        {
            var obj = Instantiate(segmentPrefab, transform);
            var img = obj.GetComponent<Image>();
            if (img == null) continue;

            // ★ 위치에 따라 스프라이트 할당
            if (i == 0) img.sprite = spriteLeft;
            else if (i == totalSegments - 1) img.sprite = spriteRight;
            else img.sprite = spriteMiddle;

            img.color = inactiveColor;
            segments.Add(img);
        }
    }

    public void SetImmediate(float progress)
    {
        int count = Mathf.RoundToInt(Mathf.Clamp01(progress) * totalSegments);
        for (int i = 0; i < segments.Count; i++)
            segments[i].color = i < count ? activeColor : inactiveColor;
    }

    public void AnimateTo(float fromProgress, float toProgress,
                          System.Action onComplete = null)
    {
        SetImmediate(fromProgress);

        int from = Mathf.RoundToInt(Mathf.Clamp01(fromProgress) * totalSegments);
        int target = Mathf.RoundToInt(Mathf.Clamp01(toProgress) * totalSegments);

        if (target <= from) { onComplete?.Invoke(); return; }

        for (int i = from; i < target; i++)
        {
            int idx = i;
            float delay = (idx - from) * segmentDelay;
            DOVirtual.DelayedCall(delay, () =>
            {
                if (idx < segments.Count)
                    segments[idx].DOColor(activeColor, 0.08f);
            });
        }

        float total = (target - from) * segmentDelay + 0.1f;
        DOVirtual.DelayedCall(total, () => onComplete?.Invoke());
    }
}