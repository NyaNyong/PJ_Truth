using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;

/// <summary>
/// 화이트보드 등 게임플레이 중간에 짧게 끼워넣는 컷씬 재생기.
/// 엔딩 컷씬과 동일한 방식: 배경 이미지 + 대사 타이핑.
/// StreamingAssets/GameData/mid_cutscenes.json 에서 대사+배경 데이터 로드.
/// Resources/CutsceneBackgrounds/{key}.png 에서 배경 스프라이트 로드 (엔딩 컷씬과 같은 폴더 재사용).
/// </summary>
public class MidCutsceneUI : MonoBehaviour
{
    public static MidCutsceneUI Instance { get; private set; }

    [Header("UI 연결")]
    [SerializeField] private CanvasGroup panelCG;
    [SerializeField] private Image backgroundImage;
    [SerializeField] private TextMeshProUGUI bodyText;

    [Header("DOTween")]
    [SerializeField] private float fadeDuration = 0.4f;
    [SerializeField] private float typeSpeed = 0.03f;

    private Dictionary<string, List<MidCutsceneLine>> sceneMap
        = new Dictionary<string, List<MidCutsceneLine>>();

    private List<MidCutsceneLine> currentLines;
    private int lineIndex;
    private bool isTyping;
    private Coroutine typingCoroutine;
    private System.Action onCompleteCallback;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        HideImmediate();
        LoadSceneData();
    }

    private void Update()
    {
        if (panelCG == null || !panelCG.gameObject.activeSelf || panelCG.alpha < 0.5f) return;

        bool advance = Input.GetKeyDown(KeyCode.Space) ||
                       Input.GetKeyDown(KeyCode.Return) ||
                       Input.GetKeyDown(KeyCode.E) ||
                       Input.GetMouseButtonDown(0);
        if (!advance) return;

        if (isTyping) SkipTyping();
        else Advance();
    }

    // ─── 데이터 로드 ────────────────────────────────────────────────────────
    private void LoadSceneData()
    {
        string path = Path.Combine(Application.streamingAssetsPath, "GameData/mid_cutscenes.json");
        if (!File.Exists(path))
        {
            Debug.LogWarning("[MidCutsceneUI] mid_cutscenes.json 없음");
            return;
        }

        string json = File.ReadAllText(path, System.Text.Encoding.UTF8);
        var data = JsonUtility.FromJson<MidCutsceneFile>(json);
        if (data?.scenes == null) return;

        foreach (var scene in data.scenes)
        {
            if (string.IsNullOrEmpty(scene.key)) continue;
            sceneMap[scene.key] = scene.lines ?? new List<MidCutsceneLine>();
        }
    }

    // ─── 외부 호출 ──────────────────────────────────────────────────────────
    public void Play(string key, System.Action onComplete)
    {
        if (!sceneMap.TryGetValue(key, out var lines) || lines.Count == 0)
        {
            Debug.LogWarning($"[MidCutsceneUI] 미등록 키 또는 빈 컷씬: {key}");
            onComplete?.Invoke();
            return;
        }

        currentLines = lines;
        lineIndex = 0;
        onCompleteCallback = onComplete;

        if (backgroundImage != null) backgroundImage.sprite = null;

        panelCG.gameObject.SetActive(true);
        panelCG.alpha = 0f;
        panelCG.interactable = false;
        panelCG.blocksRaycasts = false;
        panelCG.DOFade(1f, fadeDuration).OnComplete(() =>
        {
            panelCG.interactable = true;
            panelCG.blocksRaycasts = true;
            ShowLine(lineIndex);
        });
    }

    private void ShowLine(int index)
    {
        if (bodyText == null) return;

        var line = currentLines[index];
        if (backgroundImage != null && !string.IsNullOrEmpty(line.background))
        {
            var sprite = Resources.Load<Sprite>($"CutsceneBackgrounds/{line.background}");
            if (sprite != null) backgroundImage.sprite = sprite;
            else Debug.LogWarning($"[MidCutsceneUI] 배경 스프라이트 없음: {line.background}");
        }

        if (typingCoroutine != null) StopCoroutine(typingCoroutine);
        typingCoroutine = StartCoroutine(TypeLine(line.text));
    }

    private System.Collections.IEnumerator TypeLine(string line)
    {
        isTyping = true;
        bodyText.text = "";
        foreach (char c in line)
        {
            bodyText.text += c;
            yield return new WaitForSeconds(typeSpeed);
        }
        isTyping = false;
    }

    private void SkipTyping()
    {
        if (bodyText == null) return;
        if (typingCoroutine != null) StopCoroutine(typingCoroutine);
        bodyText.text = currentLines[lineIndex].text;
        isTyping = false;
    }

    private void Advance()
    {
        lineIndex++;
        if (lineIndex < currentLines.Count)
        {
            ShowLine(lineIndex);
        }
        else
        {
            panelCG.interactable = false;
            panelCG.blocksRaycasts = false;
            panelCG.DOFade(0f, fadeDuration).OnComplete(() =>
            {
                panelCG.gameObject.SetActive(false);
                var cb = onCompleteCallback;
                onCompleteCallback = null;
                cb?.Invoke();
            });
        }
    }

    private void HideImmediate()
    {
        if (panelCG == null) return;
        panelCG.DOKill();
        panelCG.alpha = 0f;
        panelCG.interactable = false;
        panelCG.blocksRaycasts = false;
        panelCG.gameObject.SetActive(false);
    }
}

[System.Serializable]
public class MidCutsceneLine
{
    public string text;
    public string background; // 비어있으면 이전 배경 유지
}

[System.Serializable]
public class MidCutsceneScene
{
    public string key;
    public List<MidCutsceneLine> lines;
}

[System.Serializable]
public class MidCutsceneFile
{
    public List<MidCutsceneScene> scenes;
}