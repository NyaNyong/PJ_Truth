using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

public class MainMenuUI : MonoBehaviour
{
    [Header("패널")]
    [SerializeField] private CanvasGroup mainMenuPanelCG;

    [Header("버튼")]
    [SerializeField] private Button gameStartButton;

    [Header("연결")]
    [SerializeField] private IDCardUI idCardUI;

    [Header("DOTween 설정")]
    [SerializeField] private float fadeDuration = 0.35f;

    private void Awake()
    {
        gameStartButton?.onClick.AddListener(OnClickGameStart);
    }

    private void Start()
    {
        ShowMainMenu();
    }

    private void ShowMainMenu()
    {
        AudioManager.Instance?.PlayMainMenu();
        mainMenuPanelCG.gameObject.SetActive(true);
        mainMenuPanelCG.alpha = 0f;
        mainMenuPanelCG.interactable = false;
        mainMenuPanelCG.blocksRaycasts = false;
        mainMenuPanelCG.DOFade(1f, fadeDuration).OnComplete(() =>
        {
            mainMenuPanelCG.interactable = true;
            mainMenuPanelCG.blocksRaycasts = true;
        });
    }

    private void OnClickGameStart()
    {
        mainMenuPanelCG.interactable = false;
        mainMenuPanelCG.blocksRaycasts = false;
        mainMenuPanelCG.DOFade(0f, fadeDuration).OnComplete(() =>
        {
            mainMenuPanelCG.gameObject.SetActive(false);
            idCardUI?.Show();
        });
    }
}