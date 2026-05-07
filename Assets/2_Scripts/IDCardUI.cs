using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;

public class IDCardUI : MonoBehaviour
{
    [Header("패널")]
    [SerializeField] private CanvasGroup idCardPanelCG;

    [Header("좌측 카드 — 여성")]
    [SerializeField] private Button femaleCardButton;
    [SerializeField] private CanvasGroup femaleCardCG;
    [SerializeField] private GameObject femaleSelectIndicator; // ★ 여성 선택 표시

    [Header("우측 카드 — 남성")]
    [SerializeField] private Button maleCardButton;
    [SerializeField] private CanvasGroup maleCardCG;
    [SerializeField] private GameObject maleSelectIndicator;   // ★ 남성 선택 표시

    [Header("중앙 사원증")]
    [SerializeField] private CanvasGroup portraitCG;            // ★ 초상화 CanvasGroup
    [SerializeField] private Image portraitImage;
    [SerializeField] private Sprite malePortrait;
    [SerializeField] private Sprite femalePortrait;
    [SerializeField] private TMP_InputField nameInputField;

    [Header("확인 버튼")]
    [SerializeField] private Button confirmButton;

    [Header("DOTween 설정")]
    [SerializeField] private float fadeDuration = 0.35f;
    [SerializeField] private float cardFade = 0.2f;

    // ★ 선택 상태 추적
    private bool hasSelectedGender = false;
    private bool isMaleSelected = false;

    // ── 초기화 ───────────────────────────────
    private void Awake()
    {
        maleCardButton?.onClick.AddListener(SelectMale);
        femaleCardButton?.onClick.AddListener(SelectFemale);
        confirmButton?.onClick.AddListener(OnClickConfirm);

        // 이름 입력 변경 시 확인 버튼 상태 갱신
        if (nameInputField != null)
            nameInputField.onValueChanged.AddListener(_ => RefreshConfirmButton());

        HideImmediate();
    }

    // ── 표시 ────────────────────────────────
    public void Show()
    {
        idCardPanelCG.gameObject.SetActive(true);
        idCardPanelCG.alpha = 0f;
        idCardPanelCG.interactable = false;
        idCardPanelCG.blocksRaycasts = false;
        idCardPanelCG.DOFade(1f, fadeDuration).OnComplete(() =>
        {
            idCardPanelCG.interactable = true;
            idCardPanelCG.blocksRaycasts = true;
        });

        // ★ 초기 상태: 초상화 숨김, 선택 없음, 확인 비활성
        hasSelectedGender = false;
        if (portraitCG != null)
        {
            portraitCG.alpha = 0f;
            portraitCG.blocksRaycasts = false;
        }
        if (maleSelectIndicator != null) maleSelectIndicator.SetActive(false);
        if (femaleSelectIndicator != null) femaleSelectIndicator.SetActive(false);

        // 양쪽 카드 동등하게 표시
        maleCardCG?.DOFade(1f, 0f);
        femaleCardCG?.DOFade(1f, 0f);

        if (nameInputField != null) nameInputField.text = "";
        RefreshConfirmButton();
    }

    private void HideImmediate()
    {
        if (idCardPanelCG == null) return;
        idCardPanelCG.alpha = 0f;
        idCardPanelCG.interactable = false;
        idCardPanelCG.blocksRaycasts = false;
        idCardPanelCG.gameObject.SetActive(false);
    }

    // ── 성별 선택 ────────────────────────────
    private void SelectMale()
    {
        isMaleSelected = true;
        hasSelectedGender = true;

        if (portraitImage != null) portraitImage.sprite = malePortrait;
        ShowPortrait();
        UpdateCardVisuals();
        RefreshConfirmButton();
    }

    private void SelectFemale()
    {
        isMaleSelected = false;
        hasSelectedGender = true;

        if (portraitImage != null) portraitImage.sprite = femalePortrait;
        ShowPortrait();
        UpdateCardVisuals();
        RefreshConfirmButton();
    }

    // ★ 초상화 페이드인
    private void ShowPortrait()
    {
        if (portraitCG == null) return;
        portraitCG.DOKill();
        portraitCG.blocksRaycasts = true;
        portraitCG.DOFade(1f, cardFade);
    }

    private void UpdateCardVisuals()
    {
        // 선택된 카드 밝게, 미선택 어둡게
        maleCardCG?.DOFade(isMaleSelected ? 1f : 0.35f, cardFade);
        femaleCardCG?.DOFade(!isMaleSelected ? 1f : 0.35f, cardFade);

        // ★ SelectIndicator: 선택된 쪽만 활성화
        if (maleSelectIndicator != null) maleSelectIndicator.SetActive(isMaleSelected);
        if (femaleSelectIndicator != null) femaleSelectIndicator.SetActive(!isMaleSelected);
    }

    // ★ 확인 버튼 활성화 조건: 성별 선택 + 이름 입력
    private void RefreshConfirmButton()
    {
        if (confirmButton == null) return;
        bool nameEntered = nameInputField != null &&
                           !string.IsNullOrWhiteSpace(nameInputField.text);
        confirmButton.interactable = hasSelectedGender && nameEntered;
    }

    // ── 확인 ─────────────────────────────────
    private void OnClickConfirm()
    {
        if (!hasSelectedGender) return;

        string inputName = nameInputField != null ? nameInputField.text : "";
        PlayerData.Instance?.SetData(inputName, isMaleSelected);

        idCardPanelCG.interactable = false;
        idCardPanelCG.blocksRaycasts = false;
        idCardPanelCG.DOFade(0f, fadeDuration).OnComplete(() =>
        {
            idCardPanelCG.gameObject.SetActive(false);
            FindObjectOfType<GameManager>()?.StartDay(1);
        });
    }
}