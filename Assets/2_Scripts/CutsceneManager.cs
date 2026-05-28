using System;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;
using DG.Tweening;

/// <summary>
/// VideoPlayer 기반 컷신 재생기.
/// 파일이 없거나 VideoPlayer가 없을 경우 즉시 콜백을 호출한다.
/// Inspector: rawImage, overlayCanvasGroup, videoPlayer 연결.
/// GameManager 등에서 Play(clipName, onComplete) 호출.
/// </summary>
public class CutsceneManager : MonoBehaviour
{
    public static CutsceneManager Instance { get; private set; }

    [Header("UI 연결")]
    [SerializeField] private CanvasGroup overlayCanvasGroup; // 전체 암전용 CanvasGroup
    [SerializeField] private RawImage    videoRawImage;       // VideoPlayer 출력 RenderTexture 연결

    [Header("VideoPlayer")]
    [SerializeField] private VideoPlayer videoPlayer;

    [Header("페이드 설정")]
    [SerializeField] private float fadeInDuration  = 0.3f;
    [SerializeField] private float fadeOutDuration = 0.5f;

    /// <summary>StreamingAssets 내 컷신 폴더 이름</summary>
    private const string CutsceneFolder = "Cutscenes";

    private Action pendingCallback;

    // ─────────────────────────────────────────────────────────────────────
    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        HideImmediate();

        if (videoPlayer != null)
            videoPlayer.loopPointReached += OnVideoFinished;
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

        string path = Path.Combine(Application.streamingAssetsPath, CutsceneFolder, clipName + ".mp4");

        if (videoPlayer == null || !File.Exists(path))
        {
            Debug.Log($"[CutsceneManager] 파일 없음 또는 VideoPlayer 미연결 — 즉시 콜백: {clipName}");
            InvokeCallback();
            return;
        }

        videoPlayer.url    = "file://" + path;
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
            vp.Play();
        });
    }

    private void OnVideoFinished(VideoPlayer vp)
    {
        HideOverlay(() => InvokeCallback());
    }

    // ─────────────────────────────────────────────────────────────────────
    private void ShowOverlay(Action onReady)
    {
        if (overlayCanvasGroup == null) { onReady?.Invoke(); return; }

        overlayCanvasGroup.gameObject.SetActive(true);
        overlayCanvasGroup.alpha = 0f;
        overlayCanvasGroup.blocksRaycasts = true;
        overlayCanvasGroup.DOFade(1f, fadeInDuration).OnComplete(() =>
        {
            if (videoRawImage != null) videoRawImage.gameObject.SetActive(true);
            onReady?.Invoke();
        });
    }

    private void HideOverlay(Action onDone)
    {
        if (videoRawImage != null) videoRawImage.gameObject.SetActive(false);

        if (overlayCanvasGroup == null) { onDone?.Invoke(); return; }

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
            overlayCanvasGroup.blocksRaycasts = false;
            overlayCanvasGroup.gameObject.SetActive(false);
        }
        if (videoRawImage != null)
            videoRawImage.gameObject.SetActive(false);
    }

    private void InvokeCallback()
    {
        var cb = pendingCallback;
        pendingCallback = null;
        cb?.Invoke();
    }
}
