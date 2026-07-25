# 설정 창 (SETTING) — 씬 간 연동

ESC 로 여닫는 설정 창을 **타이틀 씬에서 한 번 만들고, 그 뒤 모든 씬에서 그대로 쓴다.**
씬 순서는 `00_BigBang → 00000_Title → 00000_Player → 99_Ending` (갤러리는 타이틀에서 분기).

## 구성

| 오브젝트 | 위치 | 역할 |
| :---- | :---- | :---- |
| `Setting_manager` | 타이틀 씬 루트 | 빈 껍데기 + BGM 용 `AudioSource` |
| `Setting_Canvas` | `Setting_manager` 의 자식 | `Canvas` + [SettingsMenu](../Assets/01_Scripts/Settings/SettingsMenu.cs) |
| `Setting_Canvas/Panel` | 창 본체 | 슬라이더·토글·드롭다운·버튼 |
| `AudioManager ` | 타이틀 씬 루트 | [AudioManager](../Assets/01_Scripts/Settings/AudioManager.cs), `bgmSource` 가 `Setting_manager` 의 `AudioSource` 를 참조 |

## 씬을 넘어가는 방식

`SettingsMenu.dontDestroyOnLoad` 가 켜져 있으면 `Awake` 에서 스스로 살아남게 만든다.

- **`DontDestroyOnLoad` 는 루트 오브젝트에만 통한다.** `Setting_Canvas` 는 `Setting_manager` 의
  자식이라 그냥 걸면 경고만 뜨고 씬과 함께 사라진다. 그래서 `MakePersistent()` 가 먼저
  부모에서 떼어내 루트로 만든 뒤 `DontDestroyOnLoad` 를 건다.
- **정렬 순서.** 플레이 씬의 공용 UI Canvas 는 Sorting Order 가 100 이라 설정 창 기본값(1)으로는
  뒤에 깔린다. `canvasSortingOrder`(기본 1000)로 올려서 항상 위에 그린다. 실패 패널티 오버레이도
  최대 100 이므로 전부 덮는다.
- **중복.** 엔딩을 지나 타이틀로 돌아오면 씬에 놓인 `Setting_Canvas` 가 또 깨어난다.
  먼저 따라온 인스턴스가 이미 있으므로 새로 깨어난 쪽이 스스로 `Destroy` 된다.
- **BGM 소스.** `AudioManager` 는 루트라서 잘 따라오지만, `bgmSource` 가 타이틀 씬의
  `Setting_manager` 에 있어서 소스만 씬과 함께 사라졌다. 이제 `BuildSources()` 가 계층 밖의
  소스를 자기 밑으로 데려오므로 BGM 도 플레이 씬까지 이어진다.

`EventSystem` 은 네 씬 모두에 이미 있어서 설정 창이 따라와도 클릭이 먹는다.
빌드/에디터에서 플레이 씬을 **직접** 열면 타이틀을 거치지 않으므로 설정 창이 없다.
그때도 띄우고 싶으면 `Setting_manager` 를 프리팹으로 만들어 `Assets/Resources/UI/SettingsMenu.prefab`
에 두면 [SettingsBootstrap](../Assets/01_Scripts/Settings/SettingsBootstrap.cs) 이 모든 씬에서 하나 띄운다.

## ESC 를 같이 쓰는 UI

`SettingsMenu` 는 `[DefaultExecutionOrder(-100)]` 로 다른 UI 보다 먼저 ESC 를 처리한다.
ESC 를 쓰는 스크립트는 아래처럼 설정 창에 우선권을 넘겨야 한다 (`GalleryScreen` 이 예).

```csharp
// 설정 창이 ESC 를 먼저 먹는다.
if (SettingsMenu.IsAnyOpen || SettingsMenu.EscapeConsumedThisFrame)
    return;
```

`IsAnyOpen` 만 보면 안 된다 — ESC 로 창을 닫는 프레임에는 이미 `false` 라서 같은 입력이 두 번 먹힌다.
`EscapeConsumedThisFrame` 이 그 프레임을 막아준다.

## BGM 넣는 자리

BGM 은 전부 [AudioManager](../Assets/01_Scripts/Settings/AudioManager.cs) 를 거친다.
그래서 어디서 튼 곡이든 설정 창(ESC)의 **BGM 게이지가 그대로 먹는다** — 재생 중에 움직여도,
곡이 겹쳐 넘어가는 중에 움직여도 즉시 반영된다.

채널을 두 개 두고 곡을 겹쳐 넘긴다(크로스페이드). 곡별 크기(`trackVolume`)를 따로 받으므로
녹음 크기가 제각각인 AI 생성 BGM 을 곡마다 맞출 수 있다. 최종 볼륨은 다음과 같다.

```
설정 창 BGM 게이지 × AudioManager.bgmBaseVolume × 곡별 크기
```

### 1. 시대별 BGM — [EraBgmDirector](../Assets/01_Scripts/Settings/EraBgmDirector.cs)

플레이 씬에서 시대가 바뀔 때 곡을 갈아 끼운다. 인스펙터에 `Era` 마다 칸이 하나씩 자동으로 생긴다
(빅뱅 / 선사시대 / 고대 그리스 / 중세 / 근대 / 현대 / 미래).

배선 순서:

1. `00000_Player` 씬에 빈 오브젝트를 만들고 이름을 `BgmDirector` 로 준 뒤 `EraBgmDirector` 를 붙인다.
2. **Tracks** 목록의 시대 칸에 mp3 를 하나씩 꽂는다. 곡이 아직 없는 시대는 **비워 두면 된다** —
   그 시대에는 직전 곡이 그대로 흐른다.
3. `GameFlowController` 의 **Bgm Director** 칸에 방금 만든 오브젝트를 넣는다.
   (비워 둬도 씬에 있는 디렉터를 자동으로 찾는다)

`GameFlowController` 가 미니게임을 시작할 때마다 그 항목의 `era` 를 디렉터에 넘긴다.
같은 시대가 이어지면 곡을 끊지 않고, 시대가 바뀔 때만 겹쳐 넘긴다.

곡별로 `volume`(원본 크기)과 `fadeDuration`(겹치는 시간, 음수면 기본값)을 따로 줄 수 있다.

### 2. 씬 하나에 한 곡 — [SceneBgm](../Assets/01_Scripts/Settings/SceneBgm.cs)

타이틀·갤러리·엔딩처럼 씬 내내 한 곡이면 이걸 쓴다. 빈 오브젝트에 붙이고 클립만 꽂으면 끝이다.
앞 씬과 같은 곡이면 끊지 않고 이어서 흐른다.

타이틀은 지금 `AudioManager` 의 **Startup Bgm**(`Title.mp3`)으로 곡이 물려 있다. 다만 `AudioManager`
는 씬을 넘어 살아남으므로 **Startup Bgm 은 게임을 켜고 딱 한 번만 돈다** — 엔딩을 지나 타이틀로
돌아오면 마지막 시대 곡이 그대로 흐른다. 돌아올 때마다 타이틀 곡을 다시 틀고 싶으면
`Startup Bgm` 을 비우고 타이틀 씬에 `SceneBgm` 을 붙여 `Title.mp3` 를 꽂으면 된다.
(둘 다 쓰면 서로 곡을 밀어내니 씬당 하나만 쓴다)

### 3. 그 밖

- 엔딩으로 넘어갈 때 소리를 비우고 싶으면 `GameFlowController` 의 `onGameEnding` /
  `onAllGamesCleared` 이벤트에 `EraBgmDirector.Stop()` 을 연결한다.
- 미니게임 안에서 깔리는 앰비언스는 BGM 이 아니라 [LoopSfx](../Assets/01_Scripts/Settings/LoopSfx.cs)
  + `VolumeTrackedSource` 로 붙인다 — 효과음 게이지에 묶인다.
- 타이틀을 거치지 않고 플레이 씬을 바로 열어도 `AudioManager.EnsureInstance()` 가 매니저를
  하나 만들어 주므로 BGM 은 나온다. 단 설정 창은 없다(위 참고).

## 남은 제약

창이 열려 있으면 `Time.timeScale = 0` 이라 타이머와 이동은 멈추지만, `Update` 자체는 계속 돌기 때문에
**키를 두드려 진행하는 미니게임은 창이 열린 동안에도 입력이 먹는다.** 막으려면 각 미니게임 `Update`
맨 앞에 `if (SettingsMenu.IsAnyOpen) return;` 을 넣으면 된다.
</content>
</invoke>
