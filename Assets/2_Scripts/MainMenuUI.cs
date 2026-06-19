using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

public class MainMenuUI : MonoBehaviour
{
    [Header("패널")]
    [SerializeField] private CanvasGroup mainMenuPanelCG;

    [Header("버튼")]
    [SerializeField] private Button gameStartButton;
    [SerializeField] private Button settingsButton; // ★ 추가

    [Header("연결")]
    [SerializeField] private IDCardUI idCardUI;

    [Header("DOTween 설정")]
    [SerializeField] private float fadeDuration = 0.35f;

    private void Awake()
    {
        gameStartButton?.onClick.AddListener(OnClickGameStart);
        settingsButton?.onClick.AddListener(() => SettingsUI.Instance?.Show()); // ★ 추가
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
            CutsceneManager.Instance?.Play("mp4_1", () => idCardUI?.Show()); // ★ 수정
        });
    }
}