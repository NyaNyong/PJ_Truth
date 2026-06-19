using System;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;
using TMPro;
using DG.Tweening;

/// <summary>
/// VideoPlayer 기반 컷신 재생기.
/// 파일이 없거나 VideoPlayer가 없을 경우 즉시 콜백을 호출한다.
/// Inspector: rawImage, overlayCanvasGroup, videoPlayer, speedButton, skipButton 연결.
/// GameManager 등에서 Play(clipName, onComplete) 호출.
/// </summary>
public class CutsceneManager : MonoBehaviour
{
    public static CutsceneManager Instance { get; private set; }

    [Header("UI 연결")]
    [SerializeField] private CanvasGroup overlayCanvasGroup; // 전체 암전용 CanvasGroup
    [SerializeField] private RawImage videoRawImage;       // VideoPlayer 출력 RenderTexture 연결

    [Header("VideoPlayer")]
    [SerializeField] private VideoPlayer videoPlayer;

    [Header("배속/스킵 버튼")]
    [SerializeField] private Button speedButton;
    [SerializeField] private TextMeshProUGUI speedButtonText;
    [SerializeField] private Button skipButton;

    [Header("페이드 설정")]
    [SerializeField] private float fadeInDuration = 0.3f;
    [SerializeField] private float fadeOutDuration = 0.5f;

    [Header("Sort Order 임시 조정 (스킵 확인 팝업이 위에 보이도록)")]
    [SerializeField] private Canvas overlayCanvas;       // CutsceneOverlay의 Canvas 컴포넌트
    [SerializeField] private int normalSortOrder = 150;   // 평소 컷씬 Sort Order
    [SerializeField] private int loweredSortOrder = 97;    // ConfirmPopup(100)보다 낮은 값

    /// <summary>StreamingAssets 내 컷신 폴더 이름</summary>
    private const string CutsceneFolder = "Cutscenes";

    private Action pendingCallback;
    private bool isFastSpeed = false;

    // ─────────────────────────────────────────────────────────────────────
    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        HideImmediate();

        if (videoPlayer != null)
            videoPlayer.loopPointReached += OnVideoFinished;

        speedButton?.onClick.AddListener(OnClickSpeedToggle);
        skipButton?.onClick.AddListener(OnClickSkip);
    }

    private void OnDestroy()
    {
        if (videoPlayer != null)
            videoPlayer.loopPointReached -= OnVideoFinished;
    }

    // ─────────────────────────────────────────────────────────────────────
    /// <summary>
    /// 컷신 재생 진입점.
    /// StreamingAssets/Cutscenes/{clipName}.mp4 경로를 시도하며,
    /// 파일이 없으면 즉시 onComplete를 호출한다.
    /// </summary>
    public void Play(string clipName, Action onComplete)
    {
        pendingCallback = onComplete;

        isFastSpeed = false;
        if (speedButtonText != null) speedButtonText.text = "1x";
        if (videoPlayer != null)
        {
            videoPlayer.playbackSpeed = 1f;
            videoPlayer.SetDirectAudioMute(0, false);
        }

        string path = Path.Combine(Application.streamingAssetsPath, CutsceneFolder, clipName + ".mp4");

        if (videoPlayer == null || !File.Exists(path))
        {
            Debug.Log($"[CutsceneManager] 파일 없음 또는 VideoPlayer 미연결 — 즉시 콜백: {clipName}");
            InvokeCallback();
            return;
        }

        videoPlayer.url = "file://" + path;
        videoPlayer.source = VideoSource.Url;
        videoPlayer.Prepare();
        videoPlayer.prepareCompleted += OnPrepared;
    }

    // ─────────────────────────────────────────────────────────────────────
    private void OnPrepared(VideoPlayer vp)
    {
        vp.prepareCompleted -= OnPrepared;

        ShowOverlay(() =>
        {
            AudioManager.Instance?.MuteBGM();
            vp.Play();
        });
    }

    private void OnVideoFinished(VideoPlayer vp)
    {
        HideOverlay(() => InvokeCallback());
    }

    private void OnClickSpeedToggle()
    {
        isFastSpeed = !isFastSpeed;
        if (videoPlayer != null)
        {
            videoPlayer.playbackSpeed = isFastSpeed ? 2f : 1f;
            videoPlayer.SetDirectAudioMute(0, isFastSpeed);
        }
        if (speedButtonText != null) speedButtonText.text = isFastSpeed ? "2x" : "1x";
    }

    private void OnClickSkip()
    {
        // ★ 추가 — ConfirmPopup이 보이도록 잠깐 Sort Order를 낮춤
        if (overlayCanvas != null) overlayCanvas.sortingOrder = loweredSortOrder;

        ConfirmPopupUI.Instance?.Open(
            "컷씬을 건너뛰시겠습니까?",
            "", "",
            onConfirm: () =>
            {
                if (overlayCanvas != null) overlayCanvas.sortingOrder = normalSortOrder; // ★ 추가 — 원복
                if (videoPlayer != null && videoPlayer.isPlaying)
                    videoPlayer.Stop();
                HideOverlay(() => InvokeCallback());
            },
            onCancel: () =>
            {
                if (overlayCanvas != null) overlayCanvas.sortingOrder = normalSortOrder; // ★ 추가 — 취소해도 원복
            },
            confirmText: "예", cancelText: "아니요"
        );
    }

    // ─────────────────────────────────────────────────────────────────────
    private void ShowOverlay(Action onReady)
    {
        if (overlayCanvasGroup == null) { onReady?.Invoke(); return; }

        overlayCanvasGroup.gameObject.SetActive(true);
        overlayCanvasGroup.alpha = 0f;
        overlayCanvasGroup.interactable = false;   // ★ 추가 — 페이드 끝나기 전엔 막아둠
        overlayCanvasGroup.blocksRaycasts = true;
        overlayCanvasGroup.DOFade(1f, fadeInDuration).OnComplete(() =>
        {
            overlayCanvasGroup.interactable = true; // ★ 추가 — 핵심 수정
            if (videoRawImage != null) videoRawImage.gameObject.SetActive(true);
            onReady?.Invoke();
        });
    }

    private void HideOverlay(Action onDone)
    {
        if (videoRawImage != null) videoRawImage.gameObject.SetActive(false);

        if (overlayCanvasGroup == null) { onDone?.Invoke(); return; }

        overlayCanvasGroup.interactable = false;   // ★ 추가
        overlayCanvasGroup.DOFade(0f, fadeOutDuration).OnComplete(() =>
        {
            overlayCanvasGroup.blocksRaycasts = false;
            overlayCanvasGroup.gameObject.SetActive(false);
            onDone?.Invoke();
        });
    }

    private void HideImmediate()
    {
        if (overlayCanvasGroup != null)
        {
            overlayCanvasGroup.alpha = 0f;
            overlayCanvasGroup.interactable = false; // ★ 추가
            overlayCanvasGroup.blocksRaycasts = false;
            overlayCanvasGroup.gameObject.SetActive(false);
        }
        if (videoRawImage != null)
            videoRawImage.gameObject.SetActive(false);
    }

    private void InvokeCallback()
    {
        AudioManager.Instance?.UnmuteBGM();
        var cb = pendingCallback;
        pendingCallback = null;
        cb?.Invoke();
    }
}