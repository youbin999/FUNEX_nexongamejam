# 잡아라! 쥐 미니게임

곡식 자루로 내려오는 쥐를 손으로 잡는 게임. 놓치면 그 판 내내 쥐가 화면을 가로지른다(흑사병 맥락).

| 항목 | 내용 |
| :---- | :---- |
| 규칙 | WASD(방향키)로 손을 상하좌우로 움직여 위에서 내려오는 쥐 **3마리**를 잡는다 |
| 잡기 | 손 판정 범위(`catchRadius`)에 쥐가 닿으면 **자동으로** 잡힌다. 누를 키는 없다 |
| 성공 | 세 마리를 다 잡으면 클리어 (제한시간이 다 찰 때 통지 — 와리오웨어 규칙) |
| 실패 | 쥐가 한 마리라도 곡식 자루 선(`failLineY`)까지 내려오면 즉시 실패. 시간 초과도 실패 |
| 스크립트 | [RatCatchMiniGame.cs](../Assets/01_Scripts/Mini_Rat/RatCatchMiniGame.cs) (`TimedMiniGame` 상속) |
| 패널티 | [RatRunFailurePenalty.cs](../Assets/01_Scripts/Mini_Rat/RatRunFailurePenalty.cs) |

좌표 값(`spawnY`, `failLineY`, `spawnXRange`, `handAreaHalfSize`)은 전부 **프리팹 루트 기준 로컬 좌표**다.
손과 쥐가 계층 어디에 매달려 있어도 값의 의미가 같다. 오브젝트를 선택하면 기즈모로
이동 범위·잡기 범위·등장선·실패선이 보인다.

## 게임 프리팹 — `Assets/03_Prefabs/RatCatchGame.prefab`

```
RatCatchGame                (RatCatchMiniGame)
├─ Background               (SpriteRenderer)
├─ Hand                     (SpriteRenderer)
├─ Rats
│  ├─ Rat_0                 (SpriteRenderer)
│  ├─ Rat_1
│  └─ Rat_2
├─ GrainSack                (SpriteRenderer — 시각 전용. failLineY 높이에 둔다)
├─ Sfx_Catch                (SfxPlayer)
├─ Sfx_Clear                (SfxPlayer)
└─ Sfx_Fail                 (SfxPlayer)
```

- `Hand` / `Rats` 는 이름만 맞춰두면 인스펙터 칸을 비워도 자동으로 잡힌다.
- 쥐는 스크립트가 껐다 켜므로 저장 시 활성 상태는 상관없다.
- **Main Camera / EventSystem 은 넣지 않는다** (호스트 씬 소유 — 계약 1.6).

### 사운드 배선

소리는 [SfxPlayer](../Assets/01_Scripts/Settings/SfxPlayer.cs) 를 거친다 → `AudioManager` → 설정 창(ESC)의
효과음 게이지에 그대로 연동된다.

| 이벤트 | 연결할 곳 | 언제 |
| :---- | :---- | :---- |
| `onRatCaught` | `Sfx_Catch.Play()` | 쥐를 한 마리 잡을 때마다 |
| `onClear` | `Sfx_Clear.Play()` | 세 마리를 다 잡았을 때 |
| `onFail` | `Sfx_Fail.Play()` | 놓쳤을 때 / 시간 초과 |

### MiniGame 공통 칸

- `Game Description` — 예: "쥐를 잡아라!"
- `Keyboard Guides` — W / A / S / D 등록 (행동 문구는 필요할 때만)
- `Failure Penalty Prefab` — 아래 `RatRunPenalty` 연결

## 패널티 프리팹 — `Assets/03_Prefabs/FailurePenalties/RatRunPenalty.prefab`

```
RatRunPenalty              (Canvas: Screen Space Overlay / CanvasScaler / RatRunFailurePenalty)
├─ Rat                     (Image — 앵커 중앙(0.5, 0.5), Raycast Target 끔)
└─ Sfx_RatRun              (SfxPlayer)
```

- `onDashStarted` → `Sfx_RatRun.Play()` 로 연결하면 지나갈 때마다 소리가 난다.
- Canvas `Sorting Order` 는 **30~90** 정도. 설정 창(1000)보다는 낮고 게임 UI(100) 근처면 된다.
- 첫 한 번은 `Apply()` 가 직접 보여주고(그동안 다음 게임은 대기), 그 뒤로는
  `intervalRange` 간격으로 스스로 반복한다. 새 판을 시작하면 코어가 통째로 지운다.

## 흐름에 등록

`00000_Player` 씬의 `GameFlowController > Games` 에 항목을 추가한다.
`MiniGamePlayer.gamePrefabs` 는 자동 주입되므로 따로 등록하지 않는다.

- `era` — **Medieval(중세)** 권장
- `kind` — **Change** 권장 (실패해도 흐름은 이어지고 엔딩 서사·이미지에 반영된다)
- `eventLabel` / `successMeaning` / `failureMeaning` / `successVisual` / `failureVisual` 작성

## 테스트

- **단독**: 빈 씬에 프리팹을 놓고 [MiniGameSoloTester](../Assets/01_Scripts/Core/MiniGameSoloTester.cs) 를 붙여
  R 키로 반복 재생한다. 성공 / 놓쳐서 실패 / 시간 초과 세 경로를 각각 본다.
- **흐름**: 실패시킨 뒤 ① 첫 달리기가 끝나야 다음 게임이 시작되는지 ② 이후 게임 내내 쥐가 지나가는지
  ③ 새 판에서 사라지는지 확인한다.
