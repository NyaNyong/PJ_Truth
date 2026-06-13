using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;

public class CodeInputUI : MonoBehaviour
{
    public static CodeInputUI Instance { get; private set; }

    [Header("패널")]
    [SerializeField] private CanvasGroup panelCG;

    [Header("UI 요소")]
    [SerializeField] private TMP_InputField inputField;
    [SerializeField] private Button submitButton;
    [SerializeField] private TextMeshProUGUI promptText;
    [SerializeField] private TextMeshProUGUI feedbackText;

    [Header("설정")]
    [SerializeField] private float fadeDuration = 0.3f;
    [SerializeField] private string defaultPrompt = "송출 경로를 입력하십시오.";
    [SerializeField] private Color successColor = new Color(0.3f, 1f, 0.5f);
    [SerializeField] private Color failColor = new Color(1f, 0.3f, 0.3f);

    private string correctCode;
    private string flagOnSuccess;
    private System.Action onSuccessCallback;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        HideImmediate();
        submitButton?.onClick.AddListener(OnSubmit);
    }

    // ── 외부 호출 ─────────────────────────────────────────────────────────
    public void Show(string correct, string successFlag, System.Action onSuccess = null,
                     string prompt = null)
    {
        correctCode = correct.Trim().ToUpper();
        flagOnSuccess = successFlag;
        onSuccessCallback = onSuccess;

        if (promptText != null) promptText.text = prompt ?? defaultPrompt;
        if (feedbackText != null) feedbackText.text = "";
        if (inputField != null) { inputField.text = ""; }

        panelCG.gameObject.SetActive(true);
        panelCG.alpha = 0f;
        panelCG.interactable = false;
        panelCG.blocksRaycasts = false;
        panelCG.DOFade(1f, fadeDuration).OnComplete(() =>
        {
            panelCG.interactable = true;
            panelCG.blocksRaycasts = true;
            inputField?.Select();
        });
    }

    // ── 제출 ─────────────────────────────────────────────────────────────
    private void OnSubmit()
    {
        string input = inputField != null ? inputField.text.Trim().ToUpper() : "";

        if (input == correctCode)
        {
            OnCorrect();
        }
        else
        {
            OnWrong();
        }
    }

    private void OnCorrect()
    {
        if (!string.IsNullOrEmpty(flagOnSuccess))
            GameFlags.Instance?.SetFlag(flagOnSuccess);

        if (feedbackText != null)
        {
            feedbackText.color = successColor;
            feedbackText.text = "송출 경로 확인\n외부 연결 가능";
        }

        panelCG.interactable = false;
        panelCG.blocksRaycasts = false;

        DOVirtual.DelayedCall(1.2f, () =>
        {
            Hide();
            StageTimerUI.Instance?.StopTimer();
            onSuccessCallback?.Invoke();
        });
    }

    private void OnWrong()
    {
        if (feedbackText != null)
        {
            feedbackText.color = failColor;
            feedbackText.text = "경로 확인 실패";
        }
        inputField?.transform.DOShakePosition(0.35f, 5f, 20);
    }

    public void Hide()
    {
        panelCG.interactable = false;
        panelCG.blocksRaycasts = false;
        panelCG.DOFade(0f, fadeDuration)
               .OnComplete(() => panelCG.gameObject.SetActive(false));
    }

    private void HideImmediate()
    {
        if (panelCG == null) return;
        panelCG.alpha = 0f;
        panelCG.interactable = false;
        panelCG.blocksRaycasts = false;
        panelCG.gameObject.SetActive(false);
    }
}