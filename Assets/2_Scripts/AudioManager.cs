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

    // ─────────────────────────────────────────
    [Header("SFX AudioSource")]
    [SerializeField] private AudioSource sfxSource;
    [SerializeField][Range(0f, 1f)] private float sfxVolume = 1f;

    [Header("SFX 클립 - 낮 페이즈")]
    [Tooltip("1. 낮 페이즈 다음버튼 등 기본 상호작용")]
    [SerializeField] private AudioClip sfxDayInteraction;
    [Tooltip("2. 승인 도장")]
    [SerializeField] private AudioClip sfxApproveStamp;
    [Tooltip("3. 블랙마커 활성/비활성")]
    [SerializeField] private AudioClip sfxMarkerToggle;
    [Tooltip("4. 블랙마커 긋기")]
    [SerializeField] private AudioClip sfxMarkerDraw;
    [Tooltip("5. 사건일지(가이드라인) 팝업/닫기")]
    [SerializeField] private AudioClip sfxCasebookToggle;
    [Tooltip("6. 타자기 활성화")]
    [SerializeField] private AudioClip sfxTypewriterOpen;
    [Tooltip("7. 타자기 비활성화")]
    [SerializeField] private AudioClip sfxTypewriterClose;

    [Header("SFX 클립 - 밤 페이즈")]
    [Tooltip("8. 걷기 발소리")]
    [SerializeField] private AudioClip sfxFootstep;

    [Header("SFX 클립 - 대화창")]
    [Tooltip("9. 대화창 다음줄 넘기기")]
    [SerializeField] private AudioClip sfxDialogueNext;
    [Tooltip("10. 대화창 종료")]
    [SerializeField] private AudioClip sfxDialogueClose;

    // ─────────────────────────────────────────
    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        transform.SetParent(null);        // ★ 추가
        DontDestroyOnLoad(gameObject);

        if (bgmSource == null) bgmSource = gameObject.AddComponent<AudioSource>();
        bgmSource.loop   = true;
        bgmSource.volume = 0f;

        if (sfxSource == null)
        {
            sfxSource        = gameObject.AddComponent<AudioSource>();
            sfxSource.loop   = false;
            sfxSource.volume = sfxVolume;
        }

        bgmVolume = PlayerPrefs.GetFloat("BGMVolume", 0.7f); // ★ 추가
        sfxVolume = PlayerPrefs.GetFloat("SFXVolume", 1.0f); // ★ 추가
        bgmSource.volume = 0f;
        sfxSource.volume = sfxVolume;
    }

    private void Start()
    {
        PlayMainMenu();
    }

    // ── BGM 외부 호출 ─────────────────────────
    public void PlayMainMenu()  => PlayBGM(bgmMainMenu);
    public void PlayDayPhase()  => PlayBGM(bgmDayPhase);
    public void PlayNightPhase()=> PlayBGM(bgmNightPhase);
    public void PlayWhiteboard()=> PlayBGM(bgmWhiteboard);

    // ── SFX 외부 호출 ─────────────────────────
    public void PlaySfxDayInteraction() => PlaySFX(sfxDayInteraction);
    public void PlaySfxApproveStamp()   => PlaySFX(sfxApproveStamp);
    public void PlaySfxMarkerToggle()   => PlaySFX(sfxMarkerToggle);
    public void PlaySfxMarkerDraw()     => PlaySFX(sfxMarkerDraw);
    public void PlaySfxCasebookToggle() => PlaySFX(sfxCasebookToggle);
    public void PlaySfxTypewriterOpen() => PlaySFX(sfxTypewriterOpen);
    public void PlaySfxTypewriterClose()=> PlaySFX(sfxTypewriterClose);
    public void PlaySfxFootstep()       => PlaySFX(sfxFootstep);
    public void PlaySfxDialogueNext()   => PlaySFX(sfxDialogueNext);
    public void PlaySfxDialogueClose()  => PlaySFX(sfxDialogueClose);

    // ── BGM 크로스페이드 ──────────────────────
    private void PlayBGM(AudioClip clip)
    {
        if (clip == null) return;
        if (bgmSource.clip == clip && bgmSource.isPlaying) return;

        bgmSource.DOKill();

        if (bgmSource.isPlaying)
        {
            bgmSource.DOFade(0f, fadeDuration * 0.5f).OnComplete(() =>
            {
                SwapClip(clip);
                bgmSource.DOFade(bgmVolume, fadeDuration * 0.5f);
            });
        }
        else
        {
            SwapClip(clip);
            bgmSource.DOFade(bgmVolume, fadeDuration);
        }
    }

    private void SwapClip(AudioClip clip)
    {
        bgmSource.clip = clip;
        bgmSource.Play();
    }

    // ── SFX 단발 재생 ─────────────────────────
    private void PlaySFX(AudioClip clip)
    {
        if (clip == null) return;
        sfxSource.PlayOneShot(clip, sfxVolume);
    }

    // ── 볼륨 제어 (설정 UI에서 호출) ──────────────────
    public void SetBGMVolume(float value)
    {
        bgmVolume = Mathf.Clamp01(value);
        bgmSource.DOKill();
        bgmSource.volume = bgmSource.isPlaying ? bgmVolume : 0f;
        PlayerPrefs.SetFloat("BGMVolume", bgmVolume);
    }

    public void SetSFXVolume(float value)
    {
        sfxVolume = Mathf.Clamp01(value);
        sfxSource.volume = sfxVolume;
        PlayerPrefs.SetFloat("SFXVolume", sfxVolume);
    }

    public float BGMVolume => bgmVolume;
    public float SFXVolume => sfxVolume;

    // ★ 추가 — 컷씬 재생 중 BGM 일시 묵음
    public void MuteBGM()
    {
        bgmSource.DOKill();
        bgmSource.DOFade(0f, 0.2f);
    }

    public void UnmuteBGM()
    {
        bgmSource.DOKill();
        bgmSource.DOFade(bgmVolume, 0.3f);
    }
}
