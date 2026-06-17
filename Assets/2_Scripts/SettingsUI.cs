using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;

public class SettingsUI : MonoBehaviour
{
    public static SettingsUI Instance { get; private set; }

    [Header("패널")]
    [SerializeField] private CanvasGroup panelCG;

    [Header("슬라이더")]
    [SerializeField] private Slider bgmSlider;
    [SerializeField] private Slider sfxSlider;

    [Header("값 텍스트 (선택)")]
    [SerializeField] private TextMeshProUGUI bgmValueText;
    [SerializeField] private TextMeshProUGUI sfxValueText;

    [Header("버튼")]
    [SerializeField] private Button closeButton;

    [Header("설정")]
    [SerializeField] private float fadeDuration = 0.25f;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        HideImmediate();

        closeButton?.onClick.AddListener(Hide);
        bgmSlider?.onValueChanged.AddListener(OnBGMChanged);
        sfxSlider?.onValueChanged.AddListener(OnSFXChanged);
    }

    private void Start()
    {
        // 저장된 볼륨값으로 슬라이더 초기화
        if (AudioManager.Instance == null) return;
        if (bgmSlider != null) bgmSlider.SetValueWithoutNotify(AudioManager.Instance.BGMVolume);
        if (sfxSlider != null) sfxSlider.SetValueWithoutNotify(AudioManager.Instance.SFXVolume);
        UpdateValueTexts();
    }

    // ── 외부 호출 ─────────────────────────────────────────────────────────
    public void Show()
    {
        // 슬라이더를 현재 값으로 동기화
        if (AudioManager.Instance != null)
        {
            bgmSlider?.SetValueWithoutNotify(AudioManager.Instance.BGMVolume);
            sfxSlider?.SetValueWithoutNotify(AudioManager.Instance.SFXVolume);
            UpdateValueTexts();
        }

        panelCG.gameObject.SetActive(true);
        panelCG.alpha = 0f;
        panelCG.interactable = false;
        panelCG.blocksRaycasts = false;
        panelCG.DOFade(1f, fadeDuration).OnComplete(() =>
        {
            panelCG.interactable = true;
            panelCG.blocksRaycasts = true;
        });
    }

    public void Hide()
    {
        PlayerPrefs.Save();
        panelCG.interactable = false;
        panelCG.blocksRaycasts = false;
        panelCG.DOFade(0f, fadeDuration)
               .OnComplete(() => panelCG.gameObject.SetActive(false));
    }

    // ── 슬라이더 콜백 ────────────────────────────────────────────────────
    private void OnBGMChanged(float value)
    {
        AudioManager.Instance?.SetBGMVolume(value);
        if (bgmValueText != null)
            bgmValueText.text = Mathf.RoundToInt(value * 100f) + "%";
    }

    private void OnSFXChanged(float value)
    {
        AudioManager.Instance?.SetSFXVolume(value);
        if (sfxValueText != null)
            sfxValueText.text = Mathf.RoundToInt(value * 100f) + "%";
        // 미리보기 재생
        AudioManager.Instance?.PlaySfxDialogueNext();
    }

    private void UpdateValueTexts()
    {
        if (AudioManager.Instance == null) return;
        if (bgmValueText != null)
            bgmValueText.text = Mathf.RoundToInt(AudioManager.Instance.BGMVolume * 100f) + "%";
        if (sfxValueText != null)
            sfxValueText.text = Mathf.RoundToInt(AudioManager.Instance.SFXVolume * 100f) + "%";
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