# 엔딩 — LLM 기반 크레딧 & 이미지 생성

변화 미니게임의 성공/실패 조합에 따라 엔딩 크레딧 텍스트와 엔딩 이미지를 **런타임에 생성**한다.
조합이 2^N 가지라 사전 제작이 불가능하므로, 매 판 생성하고 조합 키로 캐시한다.

프로바이더는 둘로 나뉜다. 둘 다 무료다.

| 용도 | 프로바이더 | 키 | 비고 |
|---|---|---|---|
| 크레딧 텍스트 | **Gemini** `gemini-3.6-flash` | 필요 | 무료 티어로 동작 확인 완료 |
| 엔딩 이미지 | **Pollinations** (flux) | 불필요 | 무료, 동작 확인 완료 |

> **Gemini 이미지 모델은 무료 티어에서 쓸 수 없다.** 2026-07-25 실측 결과, `gemini-2.5-flash-image` /
> `gemini-3.1-flash-image` / `gemini-3-pro-image` / `nano-banana-pro-preview` 전부 **첫 요청부터**
> `GenerateRequestsPerDayPerProjectPerModel-FreeTier` 위반으로 429 를 반환한다(일일 한도 0).
> 결제를 활성화하면 `imageProvider = Gemini` 로 전환할 수 있게 코드에 경로를 남겨뒀다.

## 전체 흐름

```
GameFlowController
   │  변화 미니게임 종료마다 결과 기록
   ▼
RunResult (DontDestroyOnLoad 싱글턴)
   │  씬 전환을 넘어 생존
   ▼
EndingCreditController (엔딩 씬)
   ├─ 암전 여운 (leadInDuration) ─┬─ 병렬: GeminiEndingNarrator
   │                              │        실패 → FallbackEndingNarrator
   │                              ▼
   ├─ 크레딧 롤 시작 ─────────────┬─ 병렬: EndingImageGenerator
   │                              │        캐시 히트 → 즉시 / 미스 → 생성
   │                              ▼  도착 시 배경 페이드인
   └─ 롤 종료 → 에필로그 → (이미지 합류) → GalleryStore.Save → onCreditsFinished
```

크레딧과 이미지가 모두 화면에 나온 뒤 갤러리에 한 쌍으로 저장된다 → [Gallery.md](Gallery.md)

이미지가 0초에 있을 필요가 없다는 점을 이용해 생성 지연(10~30초)을 롤 시간에 흡수시킨다.

## 엔딩 진입 경로는 두 개다

| 경로 | 발화 | RunResult 상태 |
|---|---|---|
| 미래까지 완주 | `onAllGamesCleared` | `EndedEarly == false` |
| 핵심 미니게임 실패 | `onGameEnding` | `EndedEarly == true`, `EndedAtEra`/`EndedByEvent` 채워짐 |

둘 다 같은 엔딩 씬으로 보내면 된다. 대본 생성기가 `EndedEarly` 를 보고 알아서 톤을 바꾼다.

## 파일 구성

| 파일 | 역할 |
|---|---|
| [EndingTypes.cs](../Assets/01_Scripts/Ending/EndingTypes.cs) | `MiniGameKind` / `Era` / `MiniGameOutcome` / `EndingScript` |
| [RunResult.cs](../Assets/01_Scripts/Ending/RunResult.cs) | 판 결과 누적, 조합 키 계산, 프롬프트 페이로드 생성 |
| [IEndingNarrator.cs](../Assets/01_Scripts/Ending/IEndingNarrator.cs) | 대본 생성기 인터페이스 (프로바이더 교체 지점) |
| [FallbackEndingNarrator.cs](../Assets/01_Scripts/Ending/FallbackEndingNarrator.cs) | 오프라인 폴백 + 공통 아트 디렉션 상수 |
| [GeminiApiConfig.cs](../Assets/01_Scripts/Ending/GeminiApiConfig.cs) | API 키 로딩, JSON 이스케이프 유틸 |
| [GeminiEndingNarrator.cs](../Assets/01_Scripts/Ending/GeminiEndingNarrator.cs) | Gemini 텍스트 생성 (structured output) |
| [GeminiResponseDto.cs](../Assets/01_Scripts/Ending/GeminiResponseDto.cs) | 응답 파싱용 DTO |
| [EndingImageGenerator.cs](../Assets/01_Scripts/Ending/EndingImageGenerator.cs) | 이미지 생성 + 디스크 캐시 |
| [StableHash.cs](../Assets/01_Scripts/Ending/StableHash.cs) | 실행을 넘어 유지되는 문자열 해시(조합 키·이미지 시드용) |
| [EndingCreditController.cs](../Assets/01_Scripts/Ending/EndingCreditController.cs) | 엔딩 씬 총괄 |

## 1. API 키 설정

키는 저장소에 커밋하지 않는다. `GeminiApiConfig.Load()` 가 아래 세 경로를 **위에서부터** 시도하고, 처음 성공한 값을 쓴다.

### 1) 환경변수 `GEMINI_API_KEY` — 개발 중 권장

설정 후 Unity Hub/에디터 재시작. 에디터에서만 편하고 빌드에는 따라가지 않는다.

### 2) Unity Remote Config — 빌드의 기본 경로

빌드에 키 파일을 동봉하지 않아도 되고, 대시보드에서 값만 바꾸면 **재빌드 없이 키를 교체·폐기**할 수 있다.

> ⚠️ **"비밀(Secrets)" 탭에 넣으면 안 된다.** 프로젝트 설정의 *비밀* 탭은 Secret Manager 이고,
> 여기 넣은 값은 Cloud Code·Build Automation 같은 **서버 쪽 서비스만** 읽을 수 있다.
> 게임 클라이언트의 `RemoteConfigService` 는 이 값을 절대 못 본다 —
> 넣어도 `'Gemini_Youbean999' 키가 없습니다` 경고가 그대로 뜬다. 아래 *Remote Config* 에 넣어야 한다.

- 대시보드: [Unity Cloud](https://cloud.unity.com) → **Development > Products** → **Remote Config** → **Config** → 환경 선택
- 키 이름: **`Gemini_Youbean999`** (타입 `string`) — 코드의 `GeminiApiConfig.RemoteKeyName` 과 반드시 일치해야 한다
- 값을 넣고 저장(Finish)해야 클라이언트에 내려간다
- Environment 주의: 클라이언트는 기본적으로 **Production** 환경을 읽는다. 다른 환경에 넣었다면 그 환경에도 넣어야 한다

동작 조건 — Project Settings → Services 에서 프로젝트가 클라우드에 연결돼 있어야 한다
(`ProjectSettings.asset` 의 `cloudProjectId`). 런타임에 `UnityServices.InitializeAsync()` →
익명 로그인(`SignInAnonymouslyAsync`) → `FetchConfigs` 순으로 진행하며, **6초 안에 응답이 없으면** 다음 경로로 넘어간다.

필요 패키지는 이미 설치돼 있다: `com.unity.remote-config` (+ 의존성 `services.core`, `services.authentication`).

### 3) `Assets/StreamingAssets/gemini_api_key.txt` — 오프라인 폴백

키 한 줄. `.gitignore` 에 이미 등록돼 있다.

```
# 이 줄처럼 # 로 시작하는 주석과 빈 줄은 무시된다
AIza...
```

Remote Config 를 쓰면 이 파일은 없어도 된다 — 삭제해도 빌드는 정상 동작하며, 오프라인 대비로 남겨둬도 된다.

> **키 노출에 대해:** Remote Config 값도 결국 클라이언트로 내려오므로 빌드 동봉과 마찬가지로 추출될 수 있다.
> Remote Config 의 이점은 "추출 불가"가 아니라 **"빌드에서 분리 + 사후 교체 가능"** 이다.
> 어느 경로를 쓰든 **사용량 한도를 낮게 건 잼 전용 키**를 쓰고 제출 후 폐기할 것.

키가 없어도 게임은 정상 동작한다 — 폴백 텍스트로 엔딩이 완주되고, 이미지만 생략된다.
어느 경로에서 키를 읽었는지는 콘솔 로그(`GeminiApiConfig: … 에서 키를 읽었습니다`)로 확인할 수 있다.

## 2. 미니게임에 엔딩 맥락 입력

`GameFlowController` 인스펙터의 각 게임 항목에서 설정한다.

| 필드 | 설명 |
|---|---|
| `kind` | `Normal` / `Change` / `Critical` |
| `era` | 시대 |
| `eventLabel` | 사건 이름. 예: `마녀사냥에서 마녀를 색출` |
| `successMeaning` / `failureMeaning` | 역사적 의미(한국어). 크레딧 문장 재료 |
| `successVisual` / `failureVisual` | 시각 요소(**영문 구절**). 이미지 프롬프트 재료 |
| `visualWeight` | 0~10. 높을수록 이미지 전경에 배치 |

`eventLabel` 아래 5개 필드는 **`kind == Change` 일 때만** 쓰인다.

### 시각 요소 작성 요령

이미지 모델은 서로 무관한 개념이 4~5개를 넘으면 요소를 누락한다. **변화 미니게임은 시대당 1개, 총 5개 이하**로 유지할 것.

- 명사구로 짧고 구체적으로. 문장으로 쓰지 않는다.
- 폭력·유혈 묘사는 안전 필터에 걸려 이미지 생성이 통째로 실패한다. 간접 표현으로 우회한다.

```
✅ a young witch in a modern school uniform sitting among ordinary students
✅ a witch burned at the stake, shown only as a faded illustration in a children's storybook
❌ a witch being burned alive, screaming        (필터 차단)
❌ 마녀가 학교에 다닌다                          (한국어 — 이미지 모델은 영어가 안정적)
```

### ⚠️ 기존 씬 재설정 필요

`isCritical` (bool) 이 `kind` (enum) 으로 교체됐다. **기존에 체크해둔 핵심 미니게임은 인스펙터에서 `kind = Critical` 로 다시 지정해야 한다.** 직렬화 타입이 달라 자동 마이그레이션이 되지 않는다.

## 3. 엔딩 씬 구성

`Assets/00_Scenes/99_Ending.unity` 는 **이미 구성돼 있고 빌드 설정에도 등록돼 있다**(index 3).
아래는 실제 배치된 구조 — 다시 만들 필요는 없고, 손볼 때 참고용이다.

```
Canvas (Screen Space - Overlay)
├── BackgroundGroup      [CanvasGroup]      ← backgroundGroup
│   └── BackgroundImage  [RawImage]         ← backgroundImage   (화면 전체, 앵커 stretch)
├── CreditViewport       [RectTransform]     (화면 전체. Mask 를 걸면 깔끔)
│   └── CreditText       [TMP_Text]         ← creditText / creditRect
│                                             (Alignment: Center, 자동 줄바꿈 ON)
└── EpilogueText         [TMP_Text]         ← epilogueText  (화면 중앙, 비활성 상태로 시작)

EndingController [EndingCreditController]
```

`EndingCreditController` 인스펙터에서 위 4개 참조를 연결하고, `onCreditsFinished` 에 갤러리 표시나 타이틀 복귀를 연결한다.

`CreditText` 의 `RectTransform` 은 **높이를 내용에 맞게 늘어나게** 두고(Content Size Fitter: Vertical = Preferred), 앵커는 상하 stretch 가 아닌 중앙 고정으로 둔다. 컨트롤러가 `anchoredPosition.y` 를 직접 움직인다.

폰트는 프로젝트의 한글 폰트 `KMU80TTFSungkokSerif SDF` 를 쓴다.
**기본 LiberationSans SDF 로 바꾸면 한글이 전부 네모로 깨진다.**

실측 레이아웃(폴백 대본 12줄 기준): 뷰포트 1080px, 콘텐츠 620px,
스크롤 -850 → 850, `scrollSpeed = 60` 에서 롤 시간 약 28초.
이미지 생성이 10~30초 걸리므로, 이미지를 더 오래 보여주고 싶으면 `scrollSpeed` 를 45 정도로 낮춘다(약 38초).

> ⚠️ **인스펙터 값은 코드 기본값과 별개로 직렬화된다.** `narratorModel` / `imageModel` 의
> 코드 기본값을 바꿔도 이미 씬에 저장된 컴포넌트는 옛 값을 유지한다. 모델명을 바꿀 때는
> 씬의 `EndingController` 인스펙터도 같이 확인할 것.

### 건너뛰기

롤이 도는 동안 화면 **오른쪽 상단**에 건너뛰기 버튼이 떠 있고, 에필로그로 넘어가면 사라진다.
누르면 롤과 에필로그 대기만 즉시 끝나고 **에필로그 → 이름 입력 → 갤러리 저장은 그대로 진행된다** —
건너뛴 판도 세계로 남는다. 누르고 있는 동안만 빨라지는 `fastForwardMultiplier` 와는 별개 기능이다.

### 연출 파라미터

| 필드 | 기본값 | 설명 |
|---|---|---|
| `leadInDuration` | 3초 | 크레딧 시작 전 암전. 대본이 더 빨리 와도 이만큼 기다린다 |
| `generatingLabel` | `세계 생성중` | 암전 동안 화면 가운데에 뜨는 문구. 대본이 준비되면 사라진다 |
| `generatingDotInterval` | 0.5초 | 문구 뒤 점이 하나씩 늘어나는 간격. 0 이하면 점 애니메이션을 끈다 |
| `generatingText` | 비어 있음 | 직접 만든 문구를 연결하면 그쪽을 쓴다. 비우면 코드로 만들어 붙인다 |
| `scrollSpeed` | 60 px/s | 롤 속도 |
| `fastForwardMultiplier` | 5배 | 스페이스바 / 마우스 좌클릭을 **누르고 있는 동안** 롤과 에필로그 대기가 이 배율로 빨라진다. 1 이하로 두면 빨리감기가 꺼진다 |
| `skipButton` | 비어 있음 | 롤을 건너뛰는 버튼. **비워두면 화면 오른쪽 상단에 코드로 만들어 붙인다**(`SimpleUiBuilder`). 직접 만든 버튼을 연결하면 그쪽이 쓰이며, OnClick 은 자동으로 `SkipCredits()` 에 묶인다 |
| `skipButtonLabel` | `건너뛰기 ▶▶` | 자동 생성 버튼의 문구 |
| `skipButtonMargin` | (48, 48) | 자동 생성 버튼과 오른쪽 상단 모서리 사이 여백(1920x1080 기준) |
| `epilogueDelay` | 1초 | 롤 종료 후 에필로그까지 |
| `imageFadeDuration` | 2초 | 배경 페이드인 |
| `narratorTimeout` | 40초 | 초과 시 폴백 텍스트. **20초는 부족했다** — 생각하는 모델이라 응답이 느려서, 서버는 성공하는데 클라이언트만 끊겼다(대시보드에는 성공률 100%로 보임) |
| `narratorModel` | `gemini-3.6-flash` | **`gemini-2.5-flash` 는 쓰지 말 것** — 신규 사용자에게 404 |
| `imageProvider` | `Pollinations` | 키 불필요·무료. `Gemini` 는 결제 활성화 시에만 |
| `imageTimeout` | 60초 | 초과 시 이미지 생략 |
| `forceFallback` | false | **켜면 API 호출 없이 폴백만 사용.** 연출만 손볼 때 유용. 이미지도 건너뛰므로 갤러리에 저장되지 않는다 |
| `saveToGallery` | true | 끄면 이번 엔딩을 갤러리에 남기지 않는다 |

## 4. 조합 키와 캐시

생성된 이미지는 조합 키로 캐시된다.

```
{Application.persistentDataPath}/endings/ending_{결과개수}_{비트마스크}_{사건해시}[_early].png
```

조합 키(`RunResult.CombinationKey`)에는 **어떤 사건을 어떤 순서로 겪었는지**까지 들어간다.
미니게임 순서는 고정이므로 완주한 판끼리는 사건 목록이 같고 성패 패턴만 다르다 — 그 경우 실질적인 구분자는 비트마스크다.
사건 해시를 함께 넣는 이유는 **핵심 미니게임 실패로 중단된 판** 때문이다. 서로 다른 시대에서 끊겼는데도
결과 개수와 패턴이 같아질 수 있고, 그러면 이전 판의 그림이 그대로 재사용된다.
흐름 구성(등록 순서·사건 이름)을 바꿨을 때 옛 캐시가 딸려 나오지 않는 효과도 있다.

- 완전히 같은 판을 다시 겪으면 API 호출 없이 즉시 로드된다.
- 이 폴더는 **재생성을 아끼기 위한 임시 캐시**다. 지워져도 무방하다.
- 영구 보관은 갤러리가 따로 한다 → [Gallery.md](Gallery.md)

> 캐시가 없는 상태에서 같은 판을 다시 겪어도 **같은 그림이 나오지는 않는다.** 시드는 조합 키로 고정되지만
> 대본을 매번 새로 쓰기 때문에 이미지 프롬프트가 달라진다.

## 5. 시대별 크레딧 문체

크레딧은 **"살아남은 세계가 자기 역사를 직접 기록한 문서"** 라는 설정이다.
그래서 인류가 어디까지 도달했는지에 따라 기록자가 바뀌고, 문체가 곧 그 판의 도달점을 말해준다.

**화자를 정하는 규칙** — 완주하면 미래, 핵심 미니게임 실패로 끊기면 **끊긴 그 시대**.
(`GeminiEndingNarrator.ResolveNarratorEra`, `RunResult.EndedAtEra` 기준)

| 시대 | 기록자 | 문체 | 예시 |
|---|---|---|---|
| 석기시대 | 살아남은 무리 | 단어 + 마침표. 조사·추상어 없음 | `우리. 세계. 끝.` |
| 청동기 시대 | 왕의 서기 | 사실과 수량의 나열. 감정 없음 | `창 삼백. 소 열두 마리.` |
| 중세 시대 | 수도원 필경사 | `~하니라` 고문투. 신의 뜻으로 해석 | `이는 죄의 값이니라.` |
| 근대 시대 | 계몽기 지식인 | 연도 + 인과 접속. 진보에 대한 확신 | `이로써 생산은 배가되었다.` |
| 현대 시대 | 인터넷에 글 쓰는 사람 | 반말 구어체 + 유행어. 자조적 | `그냥 그렇게 됐음.` |
| 미래 시대 | 기계 | `라벨: 값` 로그. 마지막은 한 단어 | `개체수: 0.` `끝.` |

**석기와 미래가 수미상관이다.** 둘 다 말이 짧게 부서지지만 이유가 정반대다 —
석기는 언어가 *아직* 없어서, 미래는 언어가 *더 이상 필요 없어서*(기록자가 인간이 아니라 기계).

화자를 정하는 규칙은 [NarratorEra.cs](../Assets/01_Scripts/Ending/NarratorEra.cs) 한 곳에 있고,
**LLM 과 폴백이 이걸 같이 쓴다** — API 가 죽어도 화자는 안 바뀐다.

### 고치는 곳

| 경로 | 파일 | 표 |
|---|---|---|
| LLM | [GeminiEndingNarrator.cs](../Assets/01_Scripts/Ending/GeminiEndingNarrator.cs) | `EraVoices` — 시대별 규칙 + **예문** |
| 폴백 | [FallbackEndingNarrator.cs](../Assets/01_Scripts/Ending/FallbackEndingNarrator.cs) | `Voices` — 도입부·머리글·줄 변환·마무리·에필로그 |

**중세·근대·현대·미래는 화자가 60% 확률로만 등장한다.** 나머지 40% 는 화자 없이
담담한 서사체(`NeutralVoice`)로 간다 — 강한 화자가 매번 나오면 특별함이 닳기 때문이다.
**석기·청동기는 항상 등장한다**: 그 둘은 문체(단어+마침표 / 장부체) 자체가 시대의 정체성이라,
담담한 서술로 바뀌면 무엇으로 끝난 판인지 알 수 없어진다.
확률은 `GeminiEndingNarrator.EraVoiceChance`, 대상 시대는 `ChanceBasedEras` 에서 바꾼다.

**시대마다 화자가 여러 명이고, 등장할 때 하나가 무작위로 걸린다.** 현재 시대당 2명씩:

| 시대 | 화자 |
|---|---|
| 석기 | 살아남은 무리 중 하나 / 동굴 벽에 그림을 그린 사람 |
| 청동기 | 왕의 서기 / 신전의 사제 |
| 중세 | 수도원 필경사 / 심문 기록 서기 |
| 근대 | 계몽기 지식인 / 만국박람회 도록 필자 |
| 현대 | 인터넷 게시글 / 밤에 올라온 짧은 스레드 |
| 미래 | 기계의 자동 기록 / 이 행성을 나중에 조사한 외부 관측자 |

**화자를 더 넣으려면** `EraVoices` 의 해당 시대 배열에 문자열을 하나 더 추가하면 된다.
`"누가, 무엇에, 어떻게 적는가" + 예문 4줄` 구조를 지킬 것.

> ⚠️ **같은 시대 키를 두 번 적지 말 것.** `[Era.Prehistoric] = ...` 은 인덱서 대입이라
> 키가 겹치면 **컴파일 에러 없이 뒤엣것이 앞엣것을 조용히 덮어쓴다.** 반드시 배열 안에 넣어야 한다.

콘솔에 `화자 변형 2/2` 또는 `화자 없음 — 담담한 서술로 진행` 이 찍히므로 무엇이 걸렸는지 확인할 수 있다.

LLM 쪽은 **예문이 핵심이다.** 규칙만 주면 모델이 두세 줄 만에 평범한 문어체로 돌아간다.
프롬프트 조립 순서는 `SystemInstructionIntro` + `EraVoices[해당 시대]` + `SystemInstructionRules` 이고,
문체는 `credit_lines` 와 `epilogue` 에만 적용된다 — **`image_prompt` 는 영향받지 않는다**(영어 유지).

### 폴백의 한계

폴백도 같은 말투를 따르지만 **완전하지는 않다.** 사건 본문(`meaning`)은 인스펙터에 적힌 평범한
서술체 그대로라 런타임에 말투를 입힐 수 없기 때문이다. 그래서 **도입부·시대 머리글·마무리·에필로그**가
문체를 짊어지고, 말이 가장 많이 부서지는 **석기·미래**에서는 본문 대신 `eventLabel` 을 짧게 끊어 쓴다.

```
석기   고인돌 만들기.  →  "고인돌 만들기." / "안 됐다."
미래   고인돌 만들기.  →  "기록: 고인돌 만들기 — 미완."
청동기~현대            →  meaning 문장 그대로 (프레임이 톤을 잡는다)
```

문장이 통째로 그 시대 말이 되지는 않지만 톤은 확실히 갈린다.
완전히 맞추려면 `GameEntry` 에 시대별 문체 변형을 따로 적어야 하는데, 19개 항목 × 성패 2가지라
잼 일정에는 맞지 않아 하지 않았다.

## 6. 세계의 정조 (성패 비율)

문체가 **누가 썼는지**를 정한다면, 정조는 **그 세계가 어떤 상태인지**를 정한다.
두 축은 **직교한다** — 같은 석기시대 화자라도 세계 상태에 따라 다른 것을 보여 준다.

변화 미니게임의 성공 비율을 5단계로 자른다 ([EndingTone.cs](../Assets/01_Scripts/Ending/EndingTone.cs)):

| 성공 비율 | 단계 | 세계의 상태 |
|---|---|---|
| ~20% | `Ruined` | 사람이 적다. 밤이 길고 불빛이 드물다. 잃은 것은 다시 만들어지지 않았다 |
| ~40% | `Struggling` | 새로 짓는 것보다 고쳐 쓰는 것이 많다. 어제와 오늘이 거의 같다 |
| ~60% | `Mixed` | 이룬 것과 이루지 못한 것이 한 거리에 같이 있다 |
| ~80% | `Rising` | 사람들이 멀리 간다. 다음 세대를 전제로 짓는다 |
| 그 이상 | `Flourishing` | 가장 큰 성취를 아무도 말하지 않는다. 너무 당연해져서 |

**조기 엔딩이면 한 단계 내린다.** 잘하다 끊긴 판과 다 망치고 끊긴 판이 구분돼야 한다.

5단계로 나눈 이유는 이분법의 절벽을 피하기 위해서다 —
변화 게임이 9개라 한 판 차이로 세계가 뒤집히면 인과가 아니라 동전 던지기로 읽힌다.

> **핵심 규칙: 정조를 형용사로 말하지 않는다.**
> `암울한`, `희망찬`, `성공적인`, `풍요로운` 같은 단어는 프롬프트에서 금지돼 있다.
> 세계가 어떤 상태인지 보여 주면 정조는 저절로 전달된다. 기존의
> "'성공'·'실패'라는 단어를 쓰지 않는다" 규칙과 같은 원칙이다.

**고치는 곳:** LLM 은 `GeminiEndingNarrator.ToneGuides`, 폴백은 `FallbackEndingNarrator.ToneLines`
(석기·미래처럼 말이 짧은 화자는 `TerseToneLines`).

### 크레딧이 판마다 비슷할 때

정조 외에 `narratorTemperature`(기본 1.1)를 올리면 같은 조합이라도 문장이 더 흔들린다.
이전에는 `temperature` 를 아예 안 보내서 출력이 잘 안 변했다.

## 7. 폴백 동작

| 실패 지점 | 동작 |
|---|---|
| API 키 없음 | **안내창** → 진행 시 폴백 텍스트, 이미지 생략 |
| 텍스트 생성 실패/타임아웃/한도 초과 | **안내창** → 진행 시 폴백 텍스트로 롤. **이미지 생성은 계속 시도** (폴백도 `image_prompt` 를 만든다) |
| 이미지 생성 실패/안전 필터 | 배경 없이 텍스트만. 암전 배경 유지 |
| 캐시 저장 실패 | 이번 판 이미지는 메모리에 있으므로 그대로 표시 |

**어떤 경우에도 엔딩은 완주된다.** 이게 이 설계의 최우선 요구사항이다.

### AI 크레딧 불가 안내창

대본 생성이 폴백으로 떨어지면, **롤이 시작되기 전에** 사실대로 알리고 계속할지 묻는다.

```
현재 api한도가 끝나 개발자가 그지가 될것같아
ai가 아닌 기본크래딧이 생성될겁니다

        [월드생성하기]   [그만두기]
```

- **월드생성하기** → 폴백 크레딧으로 그대로 진행. 이미지가 나오면 갤러리에도 저장된다
- **그만두기** → `quitSceneName`(기본 `00000_Title`)으로 이동. **이 판은 갤러리에 남지 않는다**

별도의 확인용 API 호출을 보내지 않는다. **대본 생성을 실제로 한 번 시도한 결과**로 판단하므로
키 누락이든 한도 초과든 CORS 든 전부 여기서 걸린다. 그만큼 안내창이 뜨기까지
최대 `narratorTimeout`(기본 20초)만큼 암전이 길어질 수 있다.

`forceFallback` 을 켠 개발용 실행에서는 폴백이 의도한 동작이므로 안내창을 띄우지 않는다.
문구·버튼 이름·이동할 씬은 모두 `EndingCreditController` 인스펙터에서 바꿀 수 있다.

## 실측 검증 결과 (2026-07-25)

실제 API 호출로 확인한 내용이다.

| 항목 | 결과 |
|---|---|
| API 키 (`AQ.A…` 형식) | ✅ 동작 |
| `gemini-2.5-flash` | ❌ **404** — "no longer available to new users" |
| `gemini-3.6-flash` / `3.5-flash` / `flash-latest` | ✅ 200 |
| `responseSchema` structured output | ✅ 스키마대로 `credit_lines` / `epilogue` / `image_prompt` 반환 |
| Gemini 이미지 모델 (4종) | ❌ **429** — 무료 티어 일일 한도 0 |
| Pollinations (flux) | ✅ 200, JPEG 1024×576 |

최신 Gemini 모델은 답변 앞에 `thoughtSignature` 를 가진 사고 파트를 끼워 넣기도 한다.
`GeminiEndingNarrator.ParseScript` 는 파트를 순회하며 JSON 으로 파싱되는 첫 파트를 채택하고,
파싱 실패는 파트 단위로 삼켜 다음 파트로 넘어간다.

## 알려진 제약

- **무료 티어 한도는 변동된다.** 텍스트 모델도 분당/일일 제한이 있어 반복 테스트 중 429 가 날 수 있다. 그때는 폴백 텍스트로 넘어가므로 게임이 멈추지는 않는다.
- **모델 가용성은 계정/프로젝트마다 다를 수 있다.** 위 결과는 이 프로젝트의 키 기준이다. 팀원이 다른 키를 쓰면 `narratorModel` 을 다시 확인해야 할 수 있다.
- **Pollinations 는 무료 공개 서비스라 가용성 보장이 없다.** 심사 당일 느리거나 죽을 수 있으므로 폴백은 그대로 필수다. 조합 캐시가 있으니 미리 주요 조합을 한 번씩 돌려 캐시를 채워두면 안전하다.
- **요소 6개 이상**이면 단일 장면 합성이 무너진다. 그때는 디에게틱 콜라주(박물관 전시 벽 / 신문 1면)로 아트 디렉션을 바꾸고 요소별로 나눠 생성해야 한다.
