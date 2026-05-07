using UnityEngine;
using DG.Tweening;

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [Header("BGM AudioSource")]
    [SerializeField] private AudioSource bgmSource;

    [Header("BGM 클립")]
    [SerializeField] private AudioClip bgmMainMenu;
    [SerializeField] private AudioClip bgmDayPhase;
    [SerializeField] private AudioClip bgmNightPhase;
    [SerializeField] private AudioClip bgmWhiteboard;

    [Header("페이드 설정")]
    [SerializeField] private float fadeDuration = 1f;
    [SerializeField][Range(0f, 1f)] private float bgmVolume = 0.7f;

    private void Start()
    {
        // ★ 게임 시작 시 기본으로 메인메뉴 BGM 재생
        PlayMainMenu();
    }
    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        if (bgmSource == null) bgmSource = gameObject.AddComponent<AudioSource>();
        bgmSource.loop = true;
        bgmSource.volume = 0f;
    }

    // ── 외부 호출 ────────────────────────────
    public void PlayMainMenu() => PlayBGM(bgmMainMenu);
    public void PlayDayPhase() => PlayBGM(bgmDayPhase);
    public void PlayNightPhase() => PlayBGM(bgmNightPhase);
    public void PlayWhiteboard() => PlayBGM(bgmWhiteboard);

    // ── 크로스페이드 ─────────────────────────
    private void PlayBGM(AudioClip clip)
    {
        Debug.Log($"[AudioManager] PlayBGM 요청: {clip?.name ?? "NULL"}");
        if (clip == null) return;
        if (bgmSource.clip == clip && bgmSource.isPlaying) return; // 이미 재생 중

        bgmSource.DOKill();

        if (bgmSource.isPlaying)
        {
            // 페이드아웃 후 교체
            bgmSource.DOFade(0f, fadeDuration * 0.5f).OnComplete(() =>
            {
                SwapClip(clip);
                bgmSource.DOFade(bgmVolume, fadeDuration * 0.5f);
            });
        }
        else
        {
            // 바로 페이드인
            SwapClip(clip);
            bgmSource.DOFade(bgmVolume, fadeDuration);
        }
    }

    private void SwapClip(AudioClip clip)
    {
        bgmSource.clip = clip;
        bgmSource.Play();
    }
}