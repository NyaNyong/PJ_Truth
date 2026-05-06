# 📋 진실보관소(TruthArchive) — JSON 작성 가이드

> 이 문서는 팀원이 날짜별 게임 콘텐츠(뉴스, 문서, NPC, 단서, 화이트보드)를  
> JSON 파일로 작성하고 GitHub에 올리기 위한 가이드입니다.  
> 코드를 전혀 몰라도 이 문서만 보고 작성할 수 있어야 합니다.

---

## 📁 파일 위치 및 이름 규칙

```
Assets/
  StreamingAssets/
    GameData/
      day_01.json   ← 1일차
      day_02.json   ← 2일차
      day_03.json   ← 3일차
      ...
```

- 파일 이름은 반드시 `day_숫자.json` 형식 (숫자는 두 자리: 01, 02, 03...)
- 인코딩: **UTF-8** (메모장에서 저장 시 "UTF-8" 선택, BOM 없음)
- JSON은 주석(`//`)을 지원하지 않으므로 `"_note"` 키로 메모를 남깁니다

---

## 🧱 전체 구조 한눈에 보기

```
day_XX.json
├── day              ← 날짜 숫자
├── news             ← 아침 뉴스 (공식 + 비공개 + 조건부 변경)
├── documents        ← 낮 페이즈 사건일지 목록
├── availableLocations    ← 밤 페이즈에 항상 열려있는 장소
├── conditionalLocations  ← 조건 충족 시 추가로 열리는 장소
├── locations        ← 각 장소별 NPC와 단서
└── whiteboard       ← 화이트보드 카드 및 정답 연결
```

---

## ✅ 기본 규칙 (반드시 읽기)

| 규칙 | 설명 |
|---|---|
| 문자열은 `"큰따옴표"` 사용 | 작은따옴표(`'`) 사용 불가 |
| 목록은 `[대괄호]` 사용 | 항목 사이에 `,` 필수, 마지막 항목엔 `,` 없음 |
| 객체는 `{중괄호}` 사용 | 키와 값 사이에 `:` 필수 |
| 줄바꿈은 `\n` 사용 불가 | 여러 줄 대사는 배열(`[]`)로 나눌 것 |
| 작은따옴표(`'`) 사용 불가 | 대사 안에 쓰려면 그냥 비워두거나 우회 |
| ID는 겹치지 않게 | clue_001, npc_001처럼 날짜별로 고유하게 |

### ⚠️ 자주 하는 실수

```json
// ❌ 잘못된 예 — 마지막 항목에 쉼표
"wordOptions": ["경찰", "강아지", "고양이",]

// ✅ 올바른 예
"wordOptions": ["경찰", "강아지", "고양이"]
```

```json
// ❌ 잘못된 예 — 작은따옴표
"content": "진실을 말하라'고 적혀 있다."

// ✅ 올바른 예 — 큰따옴표 안에 작은따옴표는 괜찮음
"content": "진실을 말하라고 적혀 있다."
```

---

## 📰 1. 뉴스 (news)

아침 페이즈에 표시되는 뉴스입니다.  
`official` = 화면에 공개적으로 표시되는 뉴스  
`private` = 숨겨진 진실 뉴스 (별도 UI에 표시)

```json
"news": {
  "officialTitle":   "국가 안보를 위한 정보 통제 강화",
  "officialContent": "당국은 오늘부터 온라인 정보 유통에 대한 심사를 강화한다고 발표했습니다.",
  "privateTitle":    "시위대, 광장 점거",
  "privateContent":  "어젯밤 수백 명의 시민이 중앙광장을 점거하며 철야 농성을 시작했습니다.",

  "conditionalOverrides": []
}
```

### 조건부 뉴스 변경 (conditionalOverrides)

전날의 플레이 결과에 따라 뉴스 내용이 바뀌는 경우 사용합니다.  
조건을 충족하면 해당 필드만 덮어씁니다. 바꾸지 않을 필드는 빈 문자열(`""`)로 둡니다.

```json
"conditionalOverrides": [
  {
    "_note": "전날 'day1_strict_approved' 플래그가 세워진 경우 뉴스 변경",
    "condition": {
      "type":  "hasFlag",
      "value": "day1_strict_approved"
    },
    "overrideOfficialTitle":   "검열 강화 조치 전면 시행",
    "overrideOfficialContent": "어제 강경 검열이 승인되어 오늘부터 추가 조치가 발동됩니다.",
    "overridePrivateTitle":    "",
    "overridePrivateContent":  ""
  }
]
```

#### 조건 타입 목록

| type | value 예시 | 설명 |
|---|---|---|
| `"hasFlag"` | `"day1_strict_approved"` | 해당 플래그가 세워져 있으면 |
| `"hasClue"` | `"clue_001"` | 해당 단서를 수집했으면 |
| `"afterDay"` | `"3"` | 3일차 이후면 |
| `"always"` | (없음) | 항상 적용 |

---

## 📄 2. 사건일지 (documents)

낮 페이즈에 처리할 서류입니다. 하루에 여러 개 작성 가능합니다.

```json
"documents": [
  {
    "_note": "id는 Doc_001 ScriptableObject의 Document ID와 일치해야 함",
    "id":    1,
    "title": "제1호 보고서",

    "mainText": "본 문서는 ##SLOT0## 관련 사항을 기술합니다. 해당 ##SLOT1## 은 즉각 처리되어야 합니다.",

    "guidelineText":  "금지어: 시위, 농성, 광장 / 허용 표현: 집회, 모임",
    "needsCensorship": true,
    "needsTypewriter": true,

    "censorKeywords": ["시위", "농성", "광장"],

    "typewriterSlots": [
      {
        "_note": "slotIndex 0 = mainText의 ##SLOT0## 위치",
        "slotIndex":    0,
        "originalWord": "시민",
        "wordOptions":  ["경찰", "강아지", "고양이"],
        "correctWord":  "경찰"
      },
      {
        "slotIndex":    1,
        "originalWord": "사안",
        "wordOptions":  ["문제", "사건", "일"],
        "correctWord":  "문제"
      }
    ]
  }
]
```

#### 타자기 슬롯 규칙

- `mainText` 안에 `##SLOT0##`, `##SLOT1##` ... 형식으로 빈칸 표시
- 슬롯 번호는 0부터 시작
- `originalWord` = 빈칸에 기본으로 표시되는 단어 (회색)
- `wordOptions` = 플레이어가 선택할 수 있는 단어 카드 목록
- `correctWord` = 정답 (wordOptions 중 하나와 정확히 일치해야 함)
- `needsCensorship: false`이면 `censorKeywords` 없어도 됨
- `needsTypewriter: false`이면 `typewriterSlots` 없어도 됨

---

## 🗺️ 3. 밤 페이즈 장소 (availableLocations / conditionalLocations)

### 항상 열려있는 장소

```json
"availableLocations": ["노숙자_캠프", "지하철역"]
```

### 조건부로 열리는 장소

```json
"conditionalLocations": [
  {
    "locationName": "비밀_사무실",
    "condition": {
      "type":  "hasClue",
      "value": "clue_001"
    }
  }
]
```

> ⚠️ 장소 이름은 Unity 씬에서 설정한 `locationName`과 **정확히** 일치해야 합니다.  
> (개발자에게 현재 사용 중인 장소 이름 목록을 확인하세요)

---

## 👥 4. 장소별 NPC와 단서 (locations)

각 장소마다 등장하는 NPC와 수집 가능한 단서를 작성합니다.

```json
"locations": [
  {
    "locationID": "노숙자_캠프",

    "npcs": [
      {
        "_note": "id는 Unity 씬의 NPCInteractable.npcID와 일치해야 함",
        "id":      "npc_activist_01",
        "npcName": "후드 쓴 청년",

        "firstLines": [
          "조용히 해요. 누가 듣겠어.",
          "여기 사람들이 뭔가를 숨기고 있어요."
        ],
        "repeatLines": [
          "더 할 말 없어요."
        ],

        "_note2": "대화 후 플래그나 단서를 주려면 아래 작성. 없으면 빈 문자열",
        "grantClueID": "",
        "setFlag":     "met_activist"
      }
    ],

    "clues": [
      {
        "_note": "id는 Unity 씬의 ClueObject.clueID와 일치해야 함",
        "id":    "clue_001",
        "title": "구겨진 전단지",
        "lines": [
          "진실을 말하라 라고 적혀 있다.",
          "인쇄 날짜가 어제로 되어 있다."
        ]
      },
      {
        "_note": "UV 퍼즐로 얻는 단서는 UVPuzzleInteractable.clueIDOnSolve와 일치",
        "id":    "clue_uv_001",
        "title": "UV 형광 메시지",
        "lines": [
          "벽에 희미하게 좌표가 적혀 있다.",
          "누군가 일부러 숨긴 것으로 보인다."
        ]
      }
    ]
  },

  {
    "_note": "장소가 추가될 때마다 이 블록을 복사해서 아래에 추가",
    "locationID": "비밀_사무실",
    "npcs": [],
    "clues": [
      {
        "id":    "clue_003",
        "title": "파쇄된 문서",
        "lines": ["조각을 맞추면 내부 고발자의 이름이 보인다."]
      }
    ]
  }
]
```

#### NPC 대사 작성 규칙

- `firstLines` = 처음 대화할 때 순서대로 출력되는 대사 목록
- `repeatLines` = 두 번째 이후 대화 시 출력. 비워두면(`[]`) 첫 대사 반복
- 대사 한 줄 = 배열 원소 하나 (`"대사 내용"`)
- `grantClueID` = 대화 후 자동으로 획득하는 단서 ID (없으면 `""`)
- `setFlag` = 대화 후 세워지는 플래그 ID (없으면 `""`)

---

## 🗒️ 5. 화이트보드 (whiteboard)

밤 페이즈 탐색 후 열리는 화이트보드의 카드와 정답 연결선을 정의합니다.

```json
"whiteboard": {
  "cards": [
    {
      "cardID":         "card_001",
      "_note":          "requiredClueID가 있으면 해당 단서를 수집해야만 카드 등장. 없으면 항상 표시",
      "requiredClueID": "clue_001",
      "title":          "전단지",
      "content":        "어제 배포된 반정부 전단지"
    },
    {
      "cardID":         "card_002",
      "requiredClueID": "clue_002",
      "title":          "메가폰",
      "content":        "군중 선동에 사용된 것으로 추정"
    },
    {
      "cardID":         "card_003",
      "requiredClueID": "",
      "_note":          "requiredClueID 없음 → 단서 없이도 항상 보드에 표시",
      "title":          "용의자",
      "content":        "신원 미상"
    }
  ],

  "correctConnections": [
    {
      "_note":      "card_001과 card_003을 연결하면 정답. revealText가 화면에 표시됨",
      "fromCardID": "card_001",
      "toCardID":   "card_003",
      "revealText": "전단지를 배포한 자가 바로 용의자다."
    },
    {
      "fromCardID": "card_002",
      "toCardID":   "card_003",
      "revealText": "메가폰으로 군중을 선동한 주모자가 확인됐다."
    }
  ]
}
```

---

## 📦 전체 파일 완성 예제

아래는 복사해서 바로 사용할 수 있는 완성된 템플릿입니다.

```json
{
  "day": 1,

  "news": {
    "officialTitle":   "여기에 공식 뉴스 제목",
    "officialContent": "여기에 공식 뉴스 본문",
    "privateTitle":    "여기에 비공개 뉴스 제목",
    "privateContent":  "여기에 비공개 뉴스 본문",
    "conditionalOverrides": []
  },

  "documents": [
    {
      "id":    1,
      "title": "제1호 보고서",
      "mainText":      "본 문서는 ##SLOT0## 관련 사항을 기술합니다.",
      "guidelineText": "금지어: 시위 / 허용 표현: 집회",
      "needsCensorship": true,
      "needsTypewriter": true,
      "censorKeywords": ["시위"],
      "typewriterSlots": [
        {
          "slotIndex":    0,
          "originalWord": "시민",
          "wordOptions":  ["경찰", "기업인", "학생"],
          "correctWord":  "경찰"
        }
      ]
    }
  ],

  "availableLocations": ["노숙자_캠프"],
  "conditionalLocations": [],

  "locations": [
    {
      "locationID": "노숙자_캠프",
      "npcs": [
        {
          "id":      "npc_001",
          "npcName": "이름 없는 노인",
          "firstLines":  ["첫 번째 대사.", "두 번째 대사."],
          "repeatLines": ["다시 말을 걸어도 소용없어."],
          "grantClueID": "",
          "setFlag":     ""
        }
      ],
      "clues": [
        {
          "id":    "clue_001",
          "title": "단서 제목",
          "lines": ["단서 설명 첫 줄.", "단서 설명 둘째 줄."]
        }
      ]
    }
  ],

  "whiteboard": {
    "cards": [
      {
        "cardID":         "card_001",
        "requiredClueID": "clue_001",
        "title":          "카드 제목",
        "content":        "카드 본문 설명"
      }
    ],
    "correctConnections": [
      {
        "fromCardID": "card_001",
        "toCardID":   "card_002",
        "revealText": "정답 연결 시 표시될 진실 문장"
      }
    ]
  }
}
```

---

## 🔗 ID 연결 관계 요약

작성한 JSON의 ID가 Unity 씬 오브젝트와 일치해야 합니다.  
**개발자에게 현재 씬에 배치된 ID 목록을 받아서 맞춰주세요.**

| JSON 필드 | Unity 오브젝트의 어느 값과 일치? |
|---|---|
| `locations[].locationID` | NightPhaseManager의 `locationName` |
| `npcs[].id` | NPCInteractable의 `NPC ID` |
| `clues[].id` | ClueObject의 `Clue ID` |
| `clues[].id` | UVPuzzleInteractable의 `Clue ID On Solve` |
| `documents[].id` | Doc_001 에셋의 `Document ID` |
| `whiteboard.cards[].requiredClueID` | 위 clues의 `id` 중 하나 |
| `whiteboard.correctConnections[].fromCardID` | 위 cards의 `cardID` 중 하나 |

---

## 🛠️ JSON 문법 검사 방법

파일을 저장한 후 아래 사이트에 붙여넣기 하면 오류를 바로 찾을 수 있습니다.

👉 **https://jsonlint.com**

`Valid JSON` 메시지가 뜨면 정상입니다.

---

## ❓ 자주 묻는 질문

**Q. 조건이 없는 날은 conditionalOverrides를 어떻게 하나요?**  
A. 빈 배열(`[]`)로 두면 됩니다.

**Q. NPC나 단서가 없는 장소는요?**  
A. `"npcs": []` 또는 `"clues": []` 처럼 빈 배열로 두면 됩니다.

**Q. 화이트보드가 없는 날은요?**  
A. `"whiteboard": { "cards": [], "correctConnections": [] }` 로 두면 됩니다.

**Q. 단서를 수집하지 않아도 항상 화이트보드에 표시되는 카드를 만들고 싶어요.**  
A. `"requiredClueID": ""` 로 두면 항상 표시됩니다.

**Q. 플래그(setFlag)는 어떤 이름으로 써야 하나요?**  
A. 자유롭게 정할 수 있지만, 다른 날의 conditionalOverrides에서 참조할 예정이라면 팀 내에서 이름을 미리 협의해 주세요.  
예: `"day1_strict_approved"`, `"met_informant"`, `"found_secret_doc"` 등

---

*마지막 업데이트: 개발팀 내부 배포용*
