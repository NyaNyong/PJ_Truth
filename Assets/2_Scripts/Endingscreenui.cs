using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;
using System.Collections.Generic;
using System.IO;

public class EndingScreenUI : MonoBehaviour
{
    [Header("컷씬 패널 (Dialogue Panel 복제본)")]
    [SerializeField] private CanvasGroup cutscenePanelCG;
    [SerializeField] private TextMeshProUGUI speakerNameText;
    [SerializeField] private TextMeshProUGUI cutsceneText;
    [SerializeField] private Image cutsceneBackgroundImage;

    [Header("공식뉴스 패널")]
    [SerializeField] private CanvasGroup panelCG;
    [SerializeField] private TextMeshProUGUI newsTitleText;
    [SerializeField] private TextMeshProUGUI newsContentText;

    [Header("확인 버튼")]
    [SerializeField] private Button confirmButton;

    [Header("DOTween")]
    [SerializeField] private float fadeDuration = 0.4f;
    [SerializeField] private float typeSpeed = 0.03f;

    private System.Action onConfirmCallback;
    private List<string> currentCutsceneLines = new List<string>();
    private int cutsceneIndex = 0;
    private bool isTyping = false;
    private string currentNewsTitle;
    private string currentNewsContent;
    private string currentEndingKey;

    private Dictionary<string, Dictionary<int, CutsceneBackgroundEntry>> cutsceneBackgroundMap
        = new Dictionary<string, Dictionary<int, CutsceneBackgroundEntry>>();

    // ─── 컷씬 라인 ──────────────────────────────────────────────────────────
    private static readonly Dictionary<string, List<string>> CutsceneLines
        = new Dictionary<string, List<string>>
        {
            ["ending_early_out"] = new List<string>
        {
            "기준 이탈 누적 감지.\n민감 표현 잔류율 초과.\n승급 심사 대상 제외.",
            "상사의 메시지가 도착한다.\n\"최근 문서 처리에서 기준 이탈이 반복되었습니다.\"\n\"더 이상의 상위 기록 접근은 허가되지 않습니다.\"",
            "화면이 어두워지고, {playerName}의 사원 정보가 열린다.\n검열관 상태: 감시 대상 / 승급도: 동결 / 접근 권한: 회수",
            "내부 공지.\n기준 이탈 검열관 1명 재배치. 관련 문서 재검토 완료. 외부 확산 위험 없음."
        },
            ["ending_expose_silenced"] = new List<string>
        {
            "송출 화면에 실종자 명단과 몇 개의 문장이 떠오르지만, 문장 사이가 비어 있고 일부 기록은 노이즈처럼 깨져 있다.",
            "경고음이 울린다.",
            "도시 화면에 잠깐 떠올랐던 자료들이",
            "검은 박스로 덮인다.",
            "경비 인력이 들이닥치고 주인공은 제압된다.",
            "시위대는 혼란 속에서 강제 해산되고,",
            "최종 흑막은 삭제 명령서에 서명한다."
        },
            ["ending_expose_partial"] = new List<string>
        {
            "{playerName}의 손에 USB 하나가 쥐어져 있다.",
            "실종 기록과 가족 기록 일부가 외부로 공개된다.\n거리의 사람들이 멈춰 서고, 유가족들은 화면 앞에 모인다.",
            "하지만 상위기록실 이관 정황과 여론 통제 구조는 흐릿하게 남는다.",
            "시위대가 다시 움직이지만,",
            "기관의 정정 방송이 같은 화면 위로 겹쳐진다.",
            "치직— 잠시 혼란을 드려 죄송합니다."
        },
            ["ending_expose_full"] = new List<string>
{
    "실종자 명단 데이터 이송중..",
    "이동 처리 기록 공개중..",
    "기록 훼손 단계 유출중..",
    "상위 기록실 이관 정황 공개중..",
    "여론 안정 보고서 보여지는 중..",
    "도시 곳곳의 전광판과 휴대폰 화면이 같은 내용을 띄운다.\n유가족들은 피켓을 들고 거리로 나온다.",
    "진실보관소 내부에는 경보음이 울린다.",
    "플레이어가 있던 복도의 문들이 하나씩 잠긴다.",
    "안내방송: {playerName} 위치 추적 시작."
},
            ["ending_expose_closed"] = new List<string>
{
    "{playerName}의 손이 송출 버튼 위에서 멈춘다.\n화면에 떠 있던 자료들이 전송되지 못한 채 하나씩 접힌다.",
    "여깄다.\n흑막은 약속처럼 가족 기록 일부를 열어주지만, 중요한 줄들은 여전히 검은 칸으로 가려져 있다.", // Show()에서 경로별로 교체됨
    "바깥의 유가족 집회 화면이 점점 작아진다.",
    "화면이 더 작아진다.",
    "이내 보이지 않게 된다.",
    "공식 뉴스 화면이 그 위를 덮는다."
},

            ["ending_expose_biggest_price"] = new List<string>
{
    "송출 자료 마지막에 가족 기록 원본이 열린다.",
    "복원된 이름과 사진 일부가 화면에 떠오르고,\n그 옆으로 관리 대상자 분류와 이송 기록이 연결된다.",
    "자료는 강하게 퍼져나간다.",
    "가족의 개인 기록까지 도시 화면에 함께 노출된다.",
    "거리의 사람들이 웅성거린다.",
    "{playerName은는} 화면을 바라보다가 손을 내리지 못한다.",
    "여전히 송출 버튼을 쥐고 있다."
},

            ["ending_late_person_interviewed"] = new List<string>
{
    "바깥 화면에 떠 있던 뉴스가 다시 로딩된다.\n뉴스 제목 위에 정정 안내 표시가 붙는다.",
    "시위대가 들고 있던 자료는 펼쳐지지 못한 채 접힌다.",
    "유가족의 피켓은 사람들 사이로 가려진다.",
    "승인 문서 재검토 처리\n민감 표현 송출 오류 분류\n담당 검열관 감시 등록\n1급 검열관 후보 자격 유지",
    "면담실 문이 천천히 닫힌다."
},

            ["ending_late_person"] = new List<string>
{
    "제한 시간이 0이 되는 순간, 송출 장치의 불이 꺼진다.",
    "문이 잠기고, 복도 끝에서 발소리가 가까워진다.",
    "상사: \"위험 대상자 처리 시작.\"",
    "유가족의 피켓 문구는 완성되지 못한 채 바닥에 떨어진다."
},

            ["ending_family_only"] = new List<string>
{
    "가족 기록 원본만 선명하게 남고, 다른 기록들은 하나씩 흐려진다.",
    "{playerName}_가족의_기록_파일 저장하겠습니까?",
    "밖으로 나가는 길은 조용하고, 시설은 아무 일도 없었다는 듯 계속 작동한다.",
    "거리 한편에는 아직도 실종자 전단이 붙어 있다."
},

            ["ending_new_manager"] = new List<string>
{
    "가족 기록 열람 권한이 부여된다.",
    "{playerName이가} 기록을 끝까지 읽는 동안, 상위 기록실의 조명이 차분하게 밝아진다.",
    "잠시 후 새 업무 화면이 열린다.\n새 관리 대상자 분류 검토 요청이 도착하고,\n블랙 마커와 타자기 도구가 다시 활성화된다.",
    "{playerName은는} 새 책상 앞에 앉고, 화면에는 첫 번째 사건일지가 펼쳐진다."
},
            ["ending_stealth_expose"] = new List<string>
{
    "상위 기록실 원본 기록의 외부 반출은 차단된다.",
    "하지만 플레이어의 승급 심사 로그와 임시 접근 권한이 화면에 떠오른다.",
    "플레이어는 기록 재분류 절차를 역이용하고,\n닫혀 있던 원본 기록 일부가 외부 공개 자료에 첨부된다.",
    "상위 기록실 문은 잠기지만, 이미 일부 기록은 밖으로 새어나간 뒤다."
},
        };
            

    // ─── 엔딩별 공식뉴스 ────────────────────────────────────────────────────
    private static readonly Dictionary<string, (string title, string content)> EndingNews
        = new Dictionary<string, (string, string)>
        {
            ["ending_stealth_expose"] = (
            "상위기록실 원본 기록 외부 유출… 정보관리 체계 전면 중단",
            "상위기록실에 보관 중이던 최고 등급 원본 기록 일부가 외부 자료와 연결되어 공개된 사실이 확인되었다.\n" +
            "관계 기관은 기록 재분류 절차가 내부 권한을 통해 우회 사용된 정황을 조사 중이며,\n" +
            "상위기록실 접근 체계와 검증 절차를 전면 중단한다고 밝혔다."
        ),
            ["ending_family_only"] = (
            "실종 사건 추가 단서 없어… 기존 조사 유지",
            "최근 확산된 여러 제보에도 불구하고\n" +
            "실종 사건과 관련한 새로운 사실은 확인되지 않았다고 관계 당국은 밝혔다.\n" +
            "수사는 기존 절차에 따라 진행될 예정이다."
        ),
            ["ending_new_manager"] = (
            "정보 안정화 정책 확대 시행… 검증 체계 강화",
            "관계 기관은 보다 정확하고 신속한 정보 관리를 위해 기존 검증 체계를 확대 시행한다고 밝혔다.\n" +
            "신규 책임자 임명도 함께 발표되었다."
        ),
            ["ending_expose_full"] = (
            "정보관리기관 전면 감사 착수… 상위기록실 이관 기록 조사",
            "최근 공개된 내부 기록을 통해 실종자 관리 대상자 분류와 상위기록실 이관 정황이 드러나며\n" +
            "관계 기관 전반에 대한 특별 조사가 시작되었다.\n" +
            "일부 책임자는 직무 정지된 것으로 알려졌다."
        ),
            ["ending_expose_partial"] = (
            "실종 기록 관련 내부 자료 유출… 관계 기관 \"절차상 오해\" 해명",
            "일부 내부 자료가 외부에 공개되며 논란이 확산되고 있다.\n" +
            "관계 기관은 해당 기록이 절차상 관리 중인 자료였으며,\n" +
            "실종 사건과 직접 연결짓는 것은 신중해야 한다고 밝혔다."
        ),
            ["ending_expose_silenced"] = (
            "허위 정보 유포 시도 차단… 내부 직원 조사 중",
            "검증되지 않은 내부 자료가 외부에 유포되려 했으나 관계 기관의 즉각적인 조치로 차단되었다.\n" +
            "관계 당국은 해당 자료가 사실과 다른 내용을 포함하고 있어 추가 확산을 막았다고 밝혔다."
        ),
            ["ending_expose_closed"] = (
            "내부 혼선 정리… 관련 기록 검토 절차 종료",
            "관계 기관은 최근 제기된 일부 기록 관련 의혹에 대해 절차상 검토를 마쳤으며,\n" +
            "추가 공개가 필요한 사항은 확인되지 않았다고 밝혔다.\n" +
            "관련 기록은 기존 기준에 따라 관리될 예정이다."
        ),
            ["ending_expose_biggest_price"] = (
            "상위기록실 원본 자료 유출… 관리 대상자 분류 체계 논란",
            "상위기록실에 보관되어 있던 원본 기록 일부가 외부로 공개되며,\n" +
            "관리 대상자 분류와 기록 이관 절차에 대한 논란이 확산되고 있다.\n" +
            "공개 자료에는 특정 개인의 원본 기록도 포함되어 있어 추가 파장이 예상된다."
        ),
            ["ending_name_left"] = (
            "내부 기록 유출 파장… 기록 통제 의혹 확산",
            "진실보관소 내부 기록 일부가 외부로 공개되며 기록 통제 의혹이 확산되고 있다.\n" +
            "관계 기관은 자료의 출처와 유출 경위를 조사 중이라고 밝혔다."
        ),
            ["ending_late_person_interviewed"] = (
            "일부 기록 보도 정정… 미확정 정보 검토 중",
            "관계 기관은 일부 기록 관련 표현이 검토 과정에서 잘못 송출되었다고 밝혔다.\n" +
            "해당 내용은 확정된 사실이 아니며,\n" +
            "관련 기록은 내부 절차에 따라 재검토될 예정이다."
        ),
            ["ending_late_person"] = (
            "내부 보안 사고 수습 완료… 시스템 정상 운영",
            "일부 내부 혼선은 즉시 수습되었으며,\n" +
            "정보관리 시스템은 정상 운영 중이라고 발표했다.\n" +
            "추가 피해는 없는 것으로 알려졌다."
        ),
            ["ending_early_out"] = (
            "기준 이탈 검열관 재배치… 관련 문서 재검토 완료",
            "기준 이탈이 누적된 검열관 1명이 재배치되었다.\n" +
            "관련 문서는 재검토 완료되었으며 외부 확산 위험은 없는 것으로 확인되었다."
        ),
        };

    // ─────────────────────────────────────────────────────────────────────────
    private void Awake()
    {
        HideAllImmediate();
        LoadCutsceneBackgroundData();
        if (confirmButton != null)
            confirmButton.onClick.AddListener(OnClickConfirm);
    }

    private void LoadCutsceneBackgroundData()
    {
        string path = Path.Combine(Application.streamingAssetsPath, "GameData/ending_backgrounds.json");
        if (!File.Exists(path))
        {
            Debug.LogWarning("[EndingScreenUI] ending_backgrounds.json 없음 — 컷씬 배경 전환 비활성");
            return;
        }

        string json = File.ReadAllText(path, System.Text.Encoding.UTF8);
        var data = JsonUtility.FromJson<CutsceneBackgroundFile>(json);
        if (data?.entries == null) return;

        foreach (var entry in data.entries)
        {
            if (string.IsNullOrEmpty(entry.endingKey)) continue;
            if (!cutsceneBackgroundMap.TryGetValue(entry.endingKey, out var lineMap))
            {
                lineMap = new Dictionary<int, CutsceneBackgroundEntry>();
                cutsceneBackgroundMap[entry.endingKey] = lineMap;
            }
            lineMap[entry.lineIndex] = entry;
        }
    }

    private void Update()
    {
        if (cutscenePanelCG == null ||
            !cutscenePanelCG.gameObject.activeSelf ||
            cutscenePanelCG.alpha < 0.5f)
        {
            
            return;
        }
        

        bool advance = Input.GetKeyDown(KeyCode.Space) ||
                       Input.GetKeyDown(KeyCode.Return) ||
                       Input.GetKeyDown(KeyCode.E) ||
                       Input.GetMouseButtonDown(0);
        if (!advance) return;

        if (isTyping)
            SkipTyping();
        else
            AdvanceCutscene();
    }

    private Coroutine typingCoroutine;

    // ─── 외부 호출 ────────────────────────────────────────────────────────────
    public void Show(string endingKey, System.Action onConfirm)
    {
        onConfirmCallback = onConfirm;
        currentEndingKey = endingKey;

        if (speakerNameText != null)
            speakerNameText.text = PlayerData.Instance?.PlayerName ?? "";

        if (!EndingNews.TryGetValue(endingKey, out var news))
        {
            Debug.LogWarning($"[EndingScreenUI] 미등록 키: {endingKey}");
            news = ("알 수 없는 결말", "기록이 남아 있지 않습니다.");
        }

        currentNewsTitle = news.title;
        currentNewsContent = news.content;

        CutsceneLines.TryGetValue(endingKey, out var lines);
        currentCutsceneLines = new List<string>(lines ?? new List<string>()); // ★ 수정 — 복사본

        // ★ 추가 — 닫힌 기록: 가족기록 요구 경로 여부에 따라 1번 줄 내용만 교체 (인덱스 유지)
        if (endingKey == "ending_expose_closed" && currentCutsceneLines.Count > 1)
        {
            bool tookFamilyPath = GameFlags.Instance?.HasFlag("choice_expose_family_request") == true;
            currentCutsceneLines[1] = tookFamilyPath
                ? "여깄다.\n흑막은 약속처럼 가족 기록 일부를 열어주지만, 중요한 줄들은 여전히 검은 칸으로 가려져 있다."
                : "송출 장치의 화면이 조용히 꺼진다.";
        }

        cutsceneIndex = 0;

        if (currentCutsceneLines.Count > 0)
            ShowCutscenePanel();
        else
            ShowNewsPanel();
    }

    // ─── 컷씬 ────────────────────────────────────────────────────────────────
    private void ShowCutscenePanel()
    {
        if (cutsceneBackgroundImage != null)
            cutsceneBackgroundImage.sprite = null;
        if (cutsceneText != null)
            cutsceneText.text = ""; // ★ 추가 — 페이드인 동안 더미 텍스트 안 보이게

        cutscenePanelCG.gameObject.SetActive(true);
        cutscenePanelCG.alpha = 0f;
        cutscenePanelCG.interactable = false;
        cutscenePanelCG.blocksRaycasts = false;
        cutscenePanelCG.DOFade(1f, fadeDuration).OnComplete(() =>
        {
            cutscenePanelCG.interactable = true;
            cutscenePanelCG.blocksRaycasts = true;
            ShowCutsceneLine(cutsceneIndex);
        });
    }

    private void ShowCutsceneLine(int index)
    {
        if (cutsceneText == null) return;

        if (cutsceneBackgroundImage != null &&
            cutsceneBackgroundMap.TryGetValue(currentEndingKey, out var lineMap) &&
            lineMap.TryGetValue(index, out var entry))
        {
            bool isMale = PlayerData.Instance?.IsMale ?? true;
            string bgKey = isMale && !string.IsNullOrEmpty(entry.backgroundMale) ? entry.backgroundMale
                         : !isMale && !string.IsNullOrEmpty(entry.backgroundFemale) ? entry.backgroundFemale
                         : entry.background;

            if (!string.IsNullOrEmpty(bgKey))
            {
                var sprite = Resources.Load<Sprite>($"CutsceneBackgrounds/{bgKey}");
                if (sprite != null) cutsceneBackgroundImage.sprite = sprite;
                else Debug.LogWarning($"[EndingScreenUI] 배경 스프라이트 없음: {bgKey}");
            }
        }

        if (typingCoroutine != null) StopCoroutine(typingCoroutine);
        typingCoroutine = StartCoroutine(TypeLine(ResolveLine(currentCutsceneLines[index])));
    }

    private System.Collections.IEnumerator TypeLine(string line)
    {
        isTyping = true;
        cutsceneText.text = "";
        foreach (char c in line)
        {
            cutsceneText.text += c;
            yield return new WaitForSeconds(typeSpeed);
        }
        isTyping = false;
    }

    // ★ 추가 — {playerName} 치환
    private string ResolveLine(string raw)
    {
        if (string.IsNullOrEmpty(raw)) return raw;
        string name = PlayerData.Instance?.PlayerName ?? "";
        raw = raw.Replace("{playerName}", name);
        raw = raw.Replace("{playerName은는}", name + AttachParticle(name, "은", "는"));
        raw = raw.Replace("{playerName이가}", name + AttachParticle(name, "이", "가"));
        return raw;
    }

    private string AttachParticle(string name, string withBatchim, string noBatchim)
    {
        if (string.IsNullOrEmpty(name)) return noBatchim;
        char last = name[name.Length - 1];
        if (last < 0xAC00 || last > 0xD7A3) return noBatchim;
        int finalIndex = (last - 0xAC00) % 28;
        return finalIndex != 0 ? withBatchim : noBatchim;
    }

    private void SkipTyping()
    {
        if (cutsceneText == null) return;
        if (typingCoroutine != null) StopCoroutine(typingCoroutine);
        cutsceneText.text = ResolveLine(currentCutsceneLines[cutsceneIndex]); // ★ 수정
        isTyping = false;
    }

    private void AdvanceCutscene()
    {
        cutsceneIndex++;
        if (cutsceneIndex < currentCutsceneLines.Count)
        {
            ShowCutsceneLine(cutsceneIndex);
        }
        else
        {
            cutscenePanelCG.interactable = false;
            cutscenePanelCG.blocksRaycasts = false;
            cutscenePanelCG.DOFade(0f, fadeDuration).OnComplete(() =>
            {
                cutscenePanelCG.gameObject.SetActive(false);
                ShowNewsPanel();
            });
        }
    }

    // ─── 뉴스 패널 ───────────────────────────────────────────────────────────
    private void ShowNewsPanel()
    {
        if (newsTitleText != null) newsTitleText.text = currentNewsTitle;
        if (newsContentText != null) newsContentText.text = currentNewsContent;

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

    private void OnClickConfirm()
    {
        panelCG.interactable = false;
        panelCG.blocksRaycasts = false;
        panelCG.DOFade(0f, fadeDuration).OnComplete(() =>
        {
            HideAllImmediate();

            Debug.Log($"[크레딧디버그] currentEndingKey={currentEndingKey}, " +
                      $"CutsceneManager.Instance={CutsceneManager.Instance != null}");

            if (CutsceneManager.Instance != null)
            {
                // "엔딩크레딧영상키"는 실제 사용하는 영상 키로 변경 유지
                CutsceneManager.Instance.Play("mp4_3", () =>
                {
                    // ★ 엔딩 분기 처리: 엔딩 키값에 따라 재시작 지점을 다르게 설정
                    if (currentEndingKey == "ending_early_out")
                    {
                        // 1. 조기 퇴근 엔딩: Day 5부터 재시작
                        // (실제 GameManager에 구현된 Day 5 재시작 메서드 이름으로 변경해주세요)
                        FindObjectOfType<GameManager>()?.ConfirmRestartFromDay5();
                    }
                    else if (currentEndingKey == "ending_late_person_interviewed" ||
                             currentEndingKey == "ending_late_person")
                    {
                        // 2. 야근 관련 배드 엔딩 2종: Day 7부터 재시작
                        // (실제 GameManager에 구현된 Day 7 재시작 메서드 이름으로 변경해주세요)
                        FindObjectOfType<GameManager>()?.ConfirmRestartFromDay7();
                    }
                    else
                    {
                        // 3. 그 외의 엔딩(트루/노멀 등): 완전히 처음부터 초기화
                        FindObjectOfType<GameManager>()?.ResetGame();
                    }
                });
            }
            else
            {
                // CutsceneManager가 씬에 없을 경우의 예비 동작
                onConfirmCallback?.Invoke();
            }
        });
    }

    // ─── 초기화 ──────────────────────────────────────────────────────────────
    private void HideAllImmediate()
    {
        HideImmediate(panelCG);
        HideImmediate(cutscenePanelCG);
    }

    private void HideImmediate(CanvasGroup cg)
    {
        if (cg == null) return;
        cg.DOKill();
        cg.alpha = 0f;
        cg.interactable = false;
        cg.blocksRaycasts = false;
        cg.gameObject.SetActive(false);
    }
}