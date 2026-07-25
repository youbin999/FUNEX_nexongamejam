# 청동 제련 미니게임 (Bronze)

고대(청동기) 시대의 타이밍 미니게임. 제한시간 3초 안에 세로 게이지의 눈금이 상단의 좁은
은백색(주석) 구간에 들어왔을 때 스페이스바를 눌러 구리와 주석을 정확한 비율로 섞는다.

프리팹 단위로 만들어져 있고(`Assets/03_Prefabs/BronzeGame.prefab`), `01001_Bronze` 씬은
재생 테스트용 개발 씬이다.

## 1. 게임 규칙

- 중앙에 식은 용광로. 주변으로 도구(집게·숟가락·못·망치)들이 서로 부딪히며 날아다닌다.
- 오른쪽 세로 게이지: 대부분이 구리색이고 **상단의 좁은 영역만 은백색(주석)**.
- 눈금이 아래에서 위로 빠르게 차오른다. **은백색 구간에 들어왔을 때 스페이스바**를 누르면 성공.
- 구간에 **못 미친 채 누르거나**, 누르지 못하고 구간을 **지나쳐 버리면** 실패.

| 상황 | 처리 |
| :--- | :--- |
| 은백색 구간 안에서 스페이스 | 성공 확정. 입력만 잠그고 제한시간이 다 찰 때 통지 |
| 구간 아래에서 스페이스 | 실패 |
| 스페이스 없이 눈금이 구간을 통과 | 실패 (한 번만 상승하는 기본 모드) |
| 아무것도 안 하고 3초 경과 | 실패 |

| 값 | 기본값 | 의미 |
| :--- | :--- | :--- |
| `timeLimit` | 3 | 제한시간(초). `TimedMiniGame`이 소유 |
| `riseDuration` | 1.2 | 눈금이 바닥에서 꼭대기까지 오르는 시간(초) |
| `bandMin` / `bandMax` | 0.88 / 0.96 | 은백색 구간. 게이지 바닥이 0, 꼭대기가 1 |
| `pingPong` | false | 켜면 눈금이 왕복해 기회가 여러 번 생긴다 |
| `failPresentDuration` | 0.35 | 실패 확정 후 연기를 보여주고 통지하기까지의 시간(초) |
| `swordRiseHeight` / `swordRiseDuration` | 3 / 0.45 | 청동검이 튀어오르는 높이(유닛)와 시간 |
| `swordSpinDegrees` | 360 | 튀어오르는 동안 도는 각도. 360이면 처음 자세로 돌아온다 |

### 입력 판정 창(window)이 얼마나 되는가 — 난이도 조절의 핵심

성공할 수 있는 시간은 딱 이 값이다.

```
입력 판정 창(초) = (bandMax - bandMin) × riseDuration
```

기본값은 `0.08 × 1.2 = 0.096초` — **약 96ms, 60fps 기준 6프레임**이다. 리듬게임의 "PERFECT"
판정과 비슷한 수준으로, 기회가 한 번뿐인 것을 감안하면 상당히 빡빡하다. 실제로 플레이해 보고
너무 어려우면 아래 중 하나로 완화한다. **띠 폭을 바꾸면 화면의 은백색 영역도 같이 넓어지므로**
"보이는 만큼 정확히 판정된다"는 규칙은 어느 쪽으로 조절해도 유지된다.

| 조합 | 판정 창 | 체감 |
| :--- | :--- | :--- |
| `0.88~0.96`, 1.2초 (기본) | 96ms | 매우 어려움 |
| `0.84~0.96`, 1.2초 | 144ms | 어려움 |
| `0.86~0.96`, 1.5초 | 150ms | 적당 |
| `0.82~0.96`, 1.5초 | 210ms | 쉬움 |

## 2. 판정과 표시를 분리한 이유

정답 구간의 소유자는 `BronzeSmeltMiniGame`이고, `BronzeGauge`는 **그 값을 받아 그리기만 한다.**

```
BronzeSmeltMiniGame          BronzeGauge
  bandMin / bandMax   ──→    SetBand(min, max)   → 은백색 띠 RectTransform 앵커
  marker (0~1)        ──→    SetMarker(value)    → 눈금 앵커 + 구리색 Image.fillAmount
```

띠 위치를 인스펙터에서 손으로 맞추면 반드시 판정 구간과 어긋난다(그리고 그 어긋남은 플레이
중에만 드러난다). 코드가 띠를 배치하므로 **보이는 은백색 영역 = 실제 판정 구간**이 구조적으로
보장된다. `OnValidate`에서도 같은 함수를 불러 에디터에서 값을 바꾸는 즉시 띠가 따라 움직인다.

> `OnValidate` 안에서 RectTransform을 바로 건드리면 `SendMessage cannot be called during
> OnValidate` 경고가 뜬다. `EditorApplication.delayCall`로 한 프레임 미뤄 처리한다.

## 3. 성공 / 실패 통지 타이밍

`TimedMiniGame`을 상속하므로 와리오웨어 규칙을 그대로 따른다.

- **성공**: `SucceedWhenTimeUp()` — 즉시 끝내지 않는다. 입력만 잠근 채 3초가 다 찰 때까지 기다리며
  그 사이에 용광로 점화 → 청동검 팝업 → 문구 팝인 연출을 재생한다.
- **실패**: 검은 연기 연출을 `failPresentDuration`(0.35초)만큼 보여준 뒤 `FailImmediately()`.
  기다리는 동안 입력은 내부 `State`로 잠긴다. 대기 중에 제한시간이 먼저 끝나면 `OnTimeUp()` 쪽에서
  통지되고 코루틴의 `FailImmediately()`는 무시된다(양쪽 다 멱등).

계약상 잘못된 조작은 즉시 통지가 원칙이지만, 그러면 검은 연기가 한 프레임도 보이지 않는다.
"입력은 즉시 잠그되 통지만 0.35초 미룬다"로 절충했다.

## 4. 구성 요소

### 스크립트 (`Assets/01_Scripts/Mini_Bronze/`)

| 파일 | 역할 |
| :--- | :--- |
| `BronzeSmeltMiniGame.cs` | `TimedMiniGame` 상속. 눈금 진행 + 스페이스 판정 + 성공/실패 연출 총괄 |
| `BronzeGauge.cs` | 게이지 표시 전담. 정규화 값(0~1)을 RectTransform 앵커로 옮긴다 |
| `BronzeToolSwarm.cs` | 날아다니는 도구 관리. `Begin()` / `Freeze()` / `ResetAll()` |

검은 연기는 스패너 미니게임의 `SteamBurst` / `SteamPuff`를 색만 검게 해서 그대로 재사용한다
(신규 코드 없음).

### 에셋

| 경로 | 내용 |
| :--- | :--- |
| `Assets/02_Sprite/07_Bronze/` | `blast furnace_off/on`(1920x1080 배경), `Bronze Sword`, `Tool1~4`, `iron_Good/Fail.mp3` |
| `Assets/03_Prefabs/BronzeGame.prefab` | 미니게임 프리팹 |
| `Assets/03_Prefabs/BronzeToolBounce.physicsMaterial2D` | 마찰 0 / 반발 1 — 도구가 감속 없이 계속 튕긴다 |
| `Assets/03_Prefabs/BlackSmokePuff.prefab` | `Cloud.png`를 검게 물들인 `SteamPuff` |
| `Assets/00_Scenes/01001_Bronze.unity` | 개발 / 재생 테스트용 씬 |

### 프리팹 구조

```
BronzeGame                      BronzeSmeltMiniGame
├─ Furnace                      SpriteRenderer, order -10. 화면을 채우는 배경(scale 0.651)
├─ BronzeSword                  order 0, 시작 시 비활성. (0, -0.8), Z 90도, scale 0.163
├─ SmokeBurst                   SteamBurst
│  └─ SmokePoint                (0, 0.3) — 용광로 입구
├─ Tools                        BronzeToolSwarm
│  ├─ ToolBounds                BoxCollider2D 벽 4개 (x ±6.4, y ±4.4)
│  └─ Tool1~4                   SpriteRenderer + CapsuleCollider2D + Rigidbody2D
└─ Canvas                       Screen Space - Overlay, 1920x1080 기준 스케일
   ├─ Gauge                     BronzeGauge. 오른쪽 중앙 앵커, 120x760
   │  └─ Track                  어두운 구리색 배경
   │     ├─ CopperFill          Image(Filled / Vertical / Bottom) ← 눈금 아래를 채운다
   │     ├─ TinBand             은백색 띠 ← SetBand가 배치
   │     └─ Marker              눈금 ← SetMarker가 배치
   └─ ResultText                TMP + CanvasGroup. 아래쪽(0, -350), 한글 폰트 SDF
```

도구는 `Rigidbody2D`(Gravity Scale 0 / Damping 0 / Never Sleep) + 반발 1 머티리얼로,
**도구끼리 부딪히는 처리는 물리 엔진에 맡긴다.** 클리어되면 `Freeze()`가 속도를 0으로 만들고
`simulated = false`로 꺼서 그 자리에 멈춰 세운다.

### 씬 구성 (`01001_Bronze`)

```
Main Camera                     Orthographic size 5
EventSystem                     InputSystemUIInputModule
MiniGamePlayer                  gamePrefabs[0] = BronzeGame, playFirstOnStart = true
```

씬에는 작업 인스턴스를 두지 않았다. Play를 누르면 `MiniGamePlayer`가 프리팹을 재생하고,
배치를 수정할 때는 프리팹을 더블클릭해 프리팹 모드에서 고친다. 씬에 인스턴스를 함께 두면
재생 시 프리팹 인스턴스와 겹쳐 보인다.

## 5. 미니게임 계약 준수

[MiniGame_Contract.md](MiniGame_Contract.md) 기준으로:

- `TimedMiniGame` 상속, `OnTimedPlay()` / `OnTimedStopAndReset()` / `OnTimedUpdate()`만 구현
- 성공 `SucceedWhenTimeUp()`, 실패 `FailImmediately()` / `OnTimeUp()` — 통지는 각각 1회
- `Update()`는 프레임워크가 소유. 파생 로직은 내부 `State`가 `Rising`일 때만 돈다
- `OnTimedStopAndReset()`은 멱등 — 코루틴 정지, 도구 처음 자세 복원, 용광로 off, 검 숨김,
  연기 회수, 문구 숨김, 눈금 0
- 프리팹에 Main Camera / EventSystem 없음

### ⚠ `Start()`에서 상태를 초기화하면 안 된다 (실제로 밟은 함정)

`MiniGamePlayer`는 프리팹을 활성화한 **바로 그 프레임에** `Play()`를 호출한다
(`descriptionCanvasGroup`이 비어 있으면 대기 없이 곧장). `Awake`는 활성화 시점에 돌지만
**`Start`는 그다음 프레임에** 돌기 때문에, `Start()`에서 상태를 초기화하면 이미 시작된 게임을
다시 Idle로 되돌려 버린다. 증상은 "게임은 켜져 있는데 눈금이 0에서 꼼짝도 안 함"이다.

```csharp
private void Start()
{
    if (IsPlaying)   // Play() 가 먼저 불렸다면 건드리지 않는다
        return;

    ResetInternal();
}
```

이 함정은 이 미니게임에만 해당하는 것이 아니다. **`Start`에서 초기 표시를 하는 모든 미니게임이
같은 조건에 걸린다.**

## 6. 검증 결과

Unity CLI로 에디터에 붙어 플레이 모드에서 실측한 값이다.

**`MiniGamePlayer`를 통한 정상 경로** (timeScale 0.25로 감속해 측정)

| 시점 | state | marker | CopperFill.fillAmount | IsPlaying |
| :--- | :--- | :--- | :--- | :--- |
| T0 | Rising | 0.20 | 0.20 | true |
| T1 | Rising | 0.87 | 0.87 | true |

- 눈금 값과 게이지 `fillAmount`가 완전히 일치 → 표시/판정 분리가 정상 동작
- 은백색 띠 앵커는 항상 `0.88~0.96` — 인스펙터 값과 일치

**실패 경로** — 입력 없이 방치

- `marker`가 0.96을 넘는 순간 `state = Failed`, 이후 `IsPlaying = false`로 통지 완료
- 검은 연기가 용광로 입구에서 피어오르고 "푸슉... 실패!" 문구 표시, 용광로는 식은 상태 유지

**성공 경로** — 눈금을 구간 안에 두고 판정

- 용광로가 타오르는 스프라이트로 교체, 청동검이 회전하며 튀어올라 정점에 정지
- "SUCCESS! 청동기 시대 돌입!" 문구 팝인, 도구들 정지

**콘솔 로그 0건** (에러/경고 없음).

## 7. 알려진 제약 / 손볼 만한 것

- **판정 순간의 타격음은 없다.** `clearSfx` / `failSfx`는 성공/실패가 갈린 뒤에 울리는 소리라,
  스페이스를 누르는 순간 판정과 무관하게 나는 공통 타격음이 필요하면 필드를 따로 추가해야 한다.
- **기본 판정 창 96ms는 빡빡하다.** 1절의 표를 보고 조절할 것. 정 어려우면 `pingPong`을 켜서
  기회를 여러 번 주는 방법도 있다.
- **게이지는 색상 사각형으로 만들어져 있다.** 전용 스프라이트가 생기면 `Track` / `CopperFill` /
  `TinBand` / `Marker`의 `Image.sprite`만 교체하면 된다. 단 `CopperFill`은 `Image.Type.Filled`라
  **스프라이트가 반드시 있어야 한다** — 비우면 `fillAmount`가 무시돼 게이지가 항상 꽉 차 보인다.
- **용광로 이미지가 16:9 전체 배경**이라 4:3 화면에서는 좌우가 잘린다. 도구가 날아다니는 범위
  (`ToolBounds`, x ±6.4)는 4:3에서도 화면 안에 들어오도록 잡아 두었다.
- **`GameFlowController` 등록은 아직 안 했다.** 시대 순서와 `isCritical`(실패 시 즉시 엔딩) 여부가
  기획 판단이라 비워 두었다.
