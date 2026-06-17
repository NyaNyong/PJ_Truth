using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;
using System.Collections.Generic;

public class EndingScreenUI : MonoBehaviour
{
    [Header("컷씬 패널")]
    [SerializeField] private CanvasGroup cutscenePanelCG;
    [SerializeField] private TextMeshProUGUI cutsceneText;

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

    // ─── 컷씬 라인 ──────────────────────────────────────────────────────────
    private static readonly Dictionary<string, List<string>> CutsceneLines
        = new Dictionary<string, List<string>>
        {
            ["ending_early_out"] = new List<string>
        {
            "낮 페이즈 종료 후 업무 평가 화면이 뜬다.",
            "기준 이탈 누적 감지.\n민감 표현 잔류율 초과.\n승급 심사 대상 제외.",
            "상사의 메시지가 도착한다.\n'더 이상의 상위 기록 접근은 허가되지 않습니다.'",
            "화면이 어두워지고, 플레이어의 사원 정보가 열린다.",
            "기준 이탈자."
        },
            ["ending_expose_silenced"] = new List<string>
        {
            "송출 화면에 실종자 명단과 몇 개의 문장이 떠오른다.",
            "하지만 문장 사이가 비어 있고, 일부 기록은 노이즈처럼 깨져 있다.",
            "경고음이 울리고 도시 화면에 잠깐 떠올랐던 자료들이 검은 박스로 덮인다.",
            "경비 인력이 들이닥치고 주인공은 제압된다.",
            "시위대는 혼란 속에서 강제 해산되고, 최종 흑막은 삭제 명령서에 서명한다."
        },
            ["ending_expose_partial"] = new List<string>
        {
            "실종 기록과 가족 기록 일부가 외부로 공개된다.",
            "거리의 사람들이 멈춰 서고, 유가족들은 화면 앞에 모인다.",
            "하지만 상위기록실 이관 정황과 여론 통제 구조는 흐릿하게 남는다.",
            "기관의 정정 방송이 같은 화면 위로 겹쳐진다."
        },
            ["ending_expose_full"] = new List<string>
        {
            "송출 화면에 실종자 명단, 이동 처리 기록, 기록 훼손 단계,\n상위기록실 이관 정황, 여론 안정 보고서가 순서대로 출력된다.",
            "도시 곳곳의 전광판과 휴대폰 화면이 같은 내용을 띄운다.",
            "유가족들은 피켓을 들고 거리로 나오고, 진실보관소 내부에는 경보음이 울린다.",
            "플레이어가 있던 복도의 문들이 하나씩 잠기고,\n내부 위치 추적 표시가 켜진다."
        },
            ["ending_expose_closed"] = new List<string>
        {
            "플레이어의 손이 송출 버튼 위에서 멈춘다.",
            "화면에 떠 있던 자료들이 전송되지 못한 채 하나씩 접힌다.",
            "흑막은 약속처럼 가족 기록 일부를 열어주지만,\n중요한 줄들은 여전히 검은 칸으로 가려져 있다.",
            "바깥의 유가족 집회 화면은 점점 작아지고,\n공식 뉴스 화면이 그 위를 덮는다."
        },
            ["ending_expose_biggest_price"] = new List<string>
        {
            "송출 자료 마지막에 가족 기록 원본이 열린다.",
            "복원된 이름과 사진 일부가 화면에 떠오르고,\n그 옆으로 관리 대상자 분류와 이송 기록이 연결된다.",
            "자료는 강하게 퍼져나가지만,\n가족의 개인 기록까지 도시 화면에 함께 노출된다.",
            "플레이어는 화면을 바라보다가 손을 내리지 못한다."
        },
            ["ending_name_left"] = new List<string>
        {
            "송출 직전, 가족 기록 원본 카드가 화면 중앙에 떠오른다.",
            "플레이어는 그 카드를 송출 목록에서 제외한다.",
            "실종자 이송 기록과 기록 훼손 자료는 밖으로 나가지만,\n가족 기록 원본은 조용히 닫힌다.",
            "플레이어의 화면 한쪽에는 끝내 열리지 않은 가족 기록이 남아 있다."
        },
            ["ending_late_person_interviewed"] = new List<string>
        {
            "플레이어는 상사의 호출에 응해 면담실로 향한다.",
            "복도 뒤편에서 들리던 유가족과 시위대의 소리는 점점 멀어진다.",
            "'지금이라도 정정할 수 있습니다.\n실수로 처리하면 됩니다.'",
            "뉴스 제목 위에 정정 안내 표시가 붙는다.",
            "면담실 문이 천천히 닫힌다."
        },
            ["ending_late_person"] = new List<string>
        {
            "제한 시간이 0이 되는 순간, 송출 장치의 불이 꺼진다.",
            "문이 잠기고, 복도 끝에서 발소리가 가까워진다.",
            "화면에 떠 있던 단서 카드들이 하나씩 회색으로 변한다.",
            "유가족의 피켓 문구는 완성되지 못한 채 바닥에 떨어진다."
        },
            ["ending_stealth_expose"] = new List<string>
        {
            "상위 기록실 원본 기록의 외부 반출은 차단된다.",
            "하지만 플레이어의 승급 심사 로그와 임시 접근 권한이 화면에 떠오른다.",
            "플레이어는 기록 재분류 절차를 역이용한다.",
            "닫혀 있던 원본 기록 일부가 외부 공개 자료에 첨부된다.",
            "상위 기록실 문은 잠기지만, 이미 일부 기록은 밖으로 새어나간 뒤다."
        },
            ["ending_family_only"] = new List<string>
        {
            "가족 기록 원본만 선명하게 남고,\n다른 기록들은 하나씩 흐려진다.",
            "플레이어는 가족 기록을 저장한다.",
            "밖으로 나가는 길은 조용하고,\n시설은 아무 일도 없었다는 듯 계속 작동한다.",
            "거리 한편에는 아직도 실종자 전단이 붙어 있다."
        },
            ["ending_new_manager"] = new List<string>
        {
            "가족 기록 열람 권한이 부여된다.",
            "플레이어가 기록을 끝까지 읽는 동안,\n상위 기록실의 조명이 차분하게 밝아진다.",
            "잠시 후 새 업무 화면이 열린다.\n새 관리 대상자 분류 검토 요청이 도착한다.",
            "플레이어는 새 책상 앞에 앉는다.\n화면에는 첫 번째 사건일지가 펼쳐진다."
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
        if (confirmButton != null)
            confirmButton.onClick.AddListener(OnClickConfirm);
    }

    private void Update()
    {
        if (cutscenePanelCG == null ||
            !cutscenePanelCG.gameObject.activeSelf ||
            cutscenePanelCG.alpha < 0.5f) return;

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

        if (!EndingNews.TryGetValue(endingKey, out var news))
        {
            Debug.LogWarning($"[EndingScreenUI] 미등록 키: {endingKey}");
            news = ("알 수 없는 결말", "기록이 남아 있지 않습니다.");
        }

        currentNewsTitle = news.title;
        currentNewsContent = news.content;

        CutsceneLines.TryGetValue(endingKey, out var lines);
        currentCutsceneLines = lines ?? new List<string>();
        cutsceneIndex = 0;

        if (currentCutsceneLines.Count > 0)
            ShowCutscenePanel();
        else
            ShowNewsPanel();
    }

    // ─── 컷씬 ────────────────────────────────────────────────────────────────
    private void ShowCutscenePanel()
    {
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
        if (typingCoroutine != null) StopCoroutine(typingCoroutine);
        typingCoroutine = StartCoroutine(TypeLine(currentCutsceneLines[index]));
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

    private void SkipTyping()
    {
        if (cutsceneText == null) return;
        if (typingCoroutine != null) StopCoroutine(typingCoroutine);
        cutsceneText.text = currentCutsceneLines[cutsceneIndex];
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
            // 컷씬 종료 → 뉴스 패널로 전환
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
            onConfirmCallback?.Invoke();
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