# 엔딩 — LLM 기반 크레딧 & 이미지 생성

변화 미니게임의 성공/실패 조합에 따라 엔딩 크레딧 텍스트와 엔딩 이미지를 **런타임에 생성**한다.
조합이 2^N 가지라 사전 제작이 불가능하므로, 매 판 생성하고 조합 키로 캐시한다.

프로바이더는 **Google Gemini** 단일. 텍스트와 이미지를 같은 키로 처리한다.

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
   └─ 롤 종료 → 에필로그 → onCreditsFinished
```

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
| [EndingCreditController.cs](../Assets/01_Scripts/Ending/EndingCreditController.cs) | 엔딩 씬 총괄 |

## 1. API 키 설정

키는 저장소에 커밋하지 않는다. 두 경로 중 하나로 넣는다(환경변수가 우선).

**개발 중 (권장)** — 환경변수 `GEMINI_API_KEY` 설정 후 Unity Hub/에디터 재시작.

**빌드 동봉** — `Assets/StreamingAssets/gemini_api_key.txt` 에 키 한 줄. `.gitignore` 에 이미 등록돼 있다.

```
# 이 줄처럼 # 로 시작하는 주석과 빈 줄은 무시된다
AIza...
```

> 클라이언트 빌드에 동봉한 키는 추출될 수 있다. **사용량 한도를 낮게 건 잼 전용 키**를 쓰고 제출 후 폐기할 것.

키가 없어도 게임은 정상 동작한다 — 폴백 텍스트로 엔딩이 완주되고, 이미지만 생략된다.

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

`Assets/00_Scenes/99_Ending.unity` 를 만들고 아래 구조를 배치한다.

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

### 연출 파라미터

| 필드 | 기본값 | 설명 |
|---|---|---|
| `leadInDuration` | 3초 | 크레딧 시작 전 암전. 대본이 더 빨리 와도 이만큼 기다린다 |
| `scrollSpeed` | 60 px/s | 롤 속도 |
| `epilogueDelay` | 1초 | 롤 종료 후 에필로그까지 |
| `imageFadeDuration` | 2초 | 배경 페이드인 |
| `narratorTimeout` | 20초 | 초과 시 폴백 텍스트 |
| `imageTimeout` | 60초 | 초과 시 이미지 생략 |
| `forceFallback` | false | **켜면 API 호출 없이 폴백만 사용.** 연출만 손볼 때 유용 |

## 4. 캐시와 갤러리

생성된 이미지는 조합 키로 저장된다.

```
{Application.persistentDataPath}/endings/ending_{결과개수}_{비트마스크}[_early].png
```

- 같은 조합을 다시 뽑으면 API 호출 없이 즉시 로드된다.
- 이 폴더가 그대로 **갤러리 저장소**다. 기획서의 "판마다 수집한 결과를 영구 보존" 요구를 만족한다.
- 갤러리 UI는 `EndingImageGenerator.CacheDirectory` 를 나열하면 된다.

## 5. 폴백 동작

| 실패 지점 | 동작 |
|---|---|
| API 키 없음 | 폴백 텍스트, 이미지 생략 |
| 텍스트 생성 실패/타임아웃 | 폴백 텍스트로 롤 진행. **이미지 생성은 계속 시도** (폴백도 `image_prompt` 를 만든다) |
| 이미지 생성 실패/안전 필터 | 배경 없이 텍스트만. 암전 배경 유지 |
| 캐시 저장 실패 | 이번 판 이미지는 메모리에 있으므로 그대로 표시 |

**어떤 경우에도 엔딩은 완주된다.** 이게 이 설계의 최우선 요구사항이다.

## 알려진 제약

- **무료 티어 한도**는 변동된다. 제출 전 실제 호출로 한 번 확인할 것. 한도 초과 시 429 가 오고 폴백으로 넘어간다.
- **엔드포인트/필드명**(`gemini-2.5-flash`, `gemini-2.5-flash-image`, `responseModalities`)은 현행 문서 기준으로 재확인하는 편이 안전하다. 실패 시 응답 본문이 그대로 로그에 남으므로 디버깅은 어렵지 않다.
- **요소 6개 이상**이면 단일 장면 합성이 무너진다. 그때는 디에게틱 콜라주(박물관 전시 벽 / 신문 1면)로 아트 디렉션을 바꾸고 요소별로 나눠 생성해야 한다.
