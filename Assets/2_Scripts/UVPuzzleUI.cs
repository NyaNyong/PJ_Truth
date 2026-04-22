using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;

public class UVPuzzleUI : MonoBehaviour
{
    public static UVPuzzleUI Instance { get; private set; }

    [Header("UI 연결")]
    [SerializeField] private CanvasGroup     puzzlePanelCG;
    [SerializeField] private RectTransform   uvLightImage;
    [SerializeField] private TextMeshProUGUI hiddenText;
    [SerializeField] private TMP_InputField  answerInput;
    [SerializeField] private Button          submitButton;
    [SerializeField] private TextMeshProUGUI resultText;
    [SerializeField] private Button          closeButton;

    [Header("UV 라이트 설정")]
    [SerializeField] private float uvLightSize    = 150f;
    [SerializeField] private float textVisibility = 1f;

    [Header("DOTween 설정")]
    [SerializeField] private float fadeDuration = 0.3f;

    private string        correctAnswer     = "";
    private bool          isSolved          = false;
    private bool          isOpen            = false;
    private System.Action onSolvedCallback;
    private RectTransform panelRect;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance  = this;
        panelRect = puzzlePanelCG?.GetComponent<RectTransform>();

        HideImmediate();
        if (submitButton != null) submitButton.onClick.AddListener(OnClickSubmit);
        if (closeButton  != null) closeButton.onClick.AddListener(OnClickClose);

        if (hiddenText != null)
            hiddenText.color = new Color(hiddenText.color.r, hiddenText.color.g,
                                         hiddenText.color.b, 0f);
    }

    private void Update()
    {
        if (!isOpen || isSolved) return;
        UpdateUVLight();
    }

    public void OpenPuzzle(string hiddenContent, string answer, System.Action onSolved = null)
    {
        correctAnswer    = answer.Trim().ToLower();
        isSolved         = false;
        onSolvedCallback = onSolved;

        if (hiddenText  != null) hiddenText.text  = hiddenContent;
        if (answerInput != null) answerInput.text = "";
        // ★ 이모지 → 한글 텍스트로 교체
        if (resultText  != null) resultText.text  = "";

        if (hiddenText != null)
            hiddenText.color = new Color(hiddenText.color.r, hiddenText.color.g,
                                         hiddenText.color.b, 0f);
        ShowPanel();
    }

    public bool IsOpen() => isOpen;

    private void UpdateUVLight()
    {
        if (uvLightImage == null || panelRect == null) return;

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            panelRect, Input.mousePosition, null, out Vector2 localPoint);
        uvLightImage.anchoredPosition = localPoint;
        UpdateTextVisibility(localPoint);
    }

    private void UpdateTextVisibility(Vector2 uvCenter)
    {
        if (hiddenText == null) return;
        Vector2 textCenter = hiddenText.rectTransform.anchoredPosition;
        float   distance   = Vector2.Distance(uvCenter, textCenter);
        float   alpha      = Mathf.Clamp01(1f - (distance / (uvLightSize * 0.5f))) * textVisibility;
        var     c          = hiddenText.color;
        hiddenText.color   = new Color(c.r, c.g, c.b, alpha);
    }

    private void OnClickSubmit()
    {
        if (answerInput == null) return;
        string input = answerInput.text.Trim().ToLower();

        if (input == correctAnswer)
        {
            isSolved = true;
            Debug.Log("[UV 퍼즐] 정답!");

            if (hiddenText != null)
                hiddenText.DOColor(new Color(hiddenText.color.r, hiddenText.color.g,
                                              hiddenText.color.b, 1f), 0.5f);
            // ★ 이모지 제거
            if (resultText != null)
            {
                resultText.text  = "[정답]";
                resultText.color = Color.green;
            }

            DOVirtual.DelayedCall(1.5f, () => { onSolvedCallback?.Invoke(); ClosePanel(); });
        }
        else
        {
            Debug.Log("[UV 퍼즐] 오답");
            if (resultText != null)
            {
                resultText.text  = "[오답] 다시 시도하세요";
                resultText.color = Color.red;
            }
            answerInput.GetComponent<RectTransform>()?.DOShakeAnchorPos(0.4f, 8f, 20);
        }
    }

    private void OnClickClose() => ClosePanel();

    private void ShowPanel()
    {
        isOpen = true;
        puzzlePanelCG.gameObject.SetActive(true);
        puzzlePanelCG.alpha          = 0f;
        puzzlePanelCG.interactable   = false;
        puzzlePanelCG.blocksRaycasts = false;
        puzzlePanelCG.DOFade(1f, fadeDuration).OnComplete(() =>
        {
            puzzlePanelCG.interactable   = true;
            puzzlePanelCG.blocksRaycasts = true;
        });
    }

    private void ClosePanel()
    {
        isOpen = false;
        puzzlePanelCG.interactable   = false;
        puzzlePanelCG.blocksRaycasts = false;
        puzzlePanelCG.DOFade(0f, fadeDuration)
                     .OnComplete(() => puzzlePanelCG.gameObject.SetActive(false));
    }

    private void HideImmediate()
    {
        if (puzzlePanelCG == null) return;
        isOpen                       = false;
        puzzlePanelCG.alpha          = 0f;
        puzzlePanelCG.interactable   = false;
        puzzlePanelCG.blocksRaycasts = false;
        puzzlePanelCG.gameObject.SetActive(false);
    }
}
