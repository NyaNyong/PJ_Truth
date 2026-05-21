using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;
using System;

/// <summary>
/// 전역 확인 팝업. 제목 / 설명 / 경고문 + 예·아니요 버튼.
/// warningText 오브젝트는 Inspector에서 작은 폰트로 설정할 것.
/// </summary>
public class ConfirmPopupUI : MonoBehaviour
{
    public static ConfirmPopupUI Instance { get; private set; }

    [Header("UI 연결")]
    [SerializeField] private CanvasGroup popupCG;
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private TextMeshProUGUI messageText;
    [SerializeField] private TextMeshProUGUI warningText;
    [SerializeField] private Button confirmButton;
    [SerializeField] private Button cancelButton;
    [SerializeField] private TextMeshProUGUI confirmLabel;
    [SerializeField] private TextMeshProUGUI cancelLabel;

    [Header("DOTween")]
    [SerializeField] private float fadeDuration = 0.2f;

    private Action onConfirm;
    private Action onCancel;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        HideImmediate();
        confirmButton?.onClick.AddListener(OnConfirmClicked);
        cancelButton?.onClick.AddListener(OnCancelClicked);
    }

    /// <param name="title">굵은 제목 텍스트</param>
    /// <param name="message">본문 설명 (빈칸이면 숨김)</param>
    /// <param name="warning">하단 경고 작은 텍스트 (빈칸이면 숨김)</param>
    public void Open(string title, string message, string warning,
                     Action onConfirm, Action onCancel = null,
                     string confirmText = "예", string cancelText = "아니요")
    {
        if (popupCG == null)  // ★ 가드
        {
            Debug.LogError("[ConfirmPopupUI] popupCG 미연결 — 팝업 없이 즉시 실행");
            onConfirm?.Invoke();
            return;
        }
        this.onConfirm = onConfirm;
        this.onCancel = onCancel;

        if (titleText != null) titleText.text = title;

        if (messageText != null)
        {
            messageText.text = message;
            messageText.gameObject.SetActive(!string.IsNullOrEmpty(message));
        }
        if (warningText != null)
        {
            warningText.text = warning;
            warningText.gameObject.SetActive(!string.IsNullOrEmpty(warning));
        }
        if (confirmLabel != null) confirmLabel.text = confirmText;
        if (cancelLabel != null) cancelLabel.text = cancelText;

        popupCG.gameObject.SetActive(true);
        popupCG.alpha = 0f;
        popupCG.interactable = false;
        popupCG.blocksRaycasts = false;
        popupCG.DOFade(1f, fadeDuration).OnComplete(() =>
        {
            popupCG.interactable = true;
            popupCG.blocksRaycasts = true;
        });
    }

    private void OnConfirmClicked() => Close(() => onConfirm?.Invoke());
    private void OnCancelClicked() => Close(() => onCancel?.Invoke());

    private void Close(Action onComplete = null)
    {
        popupCG.interactable = false;
        popupCG.blocksRaycasts = false;
        popupCG.DOFade(0f, fadeDuration)
               .OnComplete(() =>
               {
                   popupCG.gameObject.SetActive(false);
                   onComplete?.Invoke();
               });
    }

    private void HideImmediate()
    {
        if (popupCG == null) return;
        popupCG.alpha = 0f;
        popupCG.interactable = false;
        popupCG.blocksRaycasts = false;
        popupCG.gameObject.SetActive(false);
    }
}