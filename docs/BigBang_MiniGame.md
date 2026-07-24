# 빅뱅 미니게임 (BigBang)

게임의 프롤로그. `00_BigBang` 씬에서 단독으로 돌아가며, 클리어하면 메인 씬(`00000_Player`)으로
넘어가고 실패하면 애플리케이션이 종료된다.

## 1. 게임 규칙

- 검은 화면에서 시작한다. 화면 오른쪽에 세로 온도계가 있다.
- **W(또는 위 방향키)를 꾹 누르고 있으면** 온도가 오른다. 떼면 천천히 내려간다.
- 온도가 **끝까지 차오르면 클리어** → 메인 씬으로 이동.
- 온도가 **0까지 식으면 실패** → 실패 UI를 띄우고 애플리케이션 종료.

| 값 | 기본값 | 의미 |
| :--- | :--- | :--- |
| `startHeat` | 0.3 | 시작 온도. 여기서부터 오르내린다 |
| `heatRisePerSecond` | 0.5 | 누르는 동안 초당 상승량 → 계속 누르면 **1.4초**에 클리어 |
| `heatFallPerSecond` | 0.15 | 떼는 동안 초당 하강량 → 방치하면 **2초**에 실패 |
| `quitDelay` | 1.5 | 실패 UI를 보여준 뒤 종료하기까지의 시간(초) |

> 실패 조건이 "온도 0"이므로 별도의 제한시간은 없다. 손을 놓는 순간부터 2초가 사실상의 제한시간이다.

## 2. 우주 팽창 연출 — 영상 스크럽

핵심은 **온도(heat)가 마스터, 영상은 종속**이라는 것이다. 빅뱅 영상은 스스로 재생되지 않는다.
`VideoPlayer`를 `Pause()` 상태로 두고, 온도 값(0~1)을 프레임 인덱스로 환산해 대입한다.

```
보정 진행도 = progressCurve.Evaluate(heat)
영상 위치   = lerp(startRatio, endRatio, 보정 진행도)
목표 프레임 = round(영상 위치 * (frameCount - 1))
```

- W를 안 누르면 온도가 내려가면서 영상도 **되감겨** 어둠으로 돌아간다.
- W를 누르면 온도가 오르면서 빛이 커진다. 온도계와 영상이 항상 같은 값을 본다.
- 목표 프레임이 **바뀔 때만** `videoPlayer.frame`에 대입한다. 매 프레임 무조건 대입하면 탐색
  요청이 쌓여 버벅인다.

### 왜 보정 곡선(`progressCurve`)이 필요한가

영상의 시각적 변화는 균등하게 분포하지 않는다. 현재 `BigBang.mp4`(113프레임)의 실측 평균
밝기는 다음과 같다.

| 영상 위치 | 0.0 | 0.1 | 0.2 | 0.3 | 0.5 | 0.7 | 0.9 | 1.0 |
| :--- | :--- | :--- | :--- | :--- | :--- | :--- | :--- | :--- |
| 평균 밝기 | 0.001 | 0.000 | 0.000 | 0.001 | 0.018 | 0.092 | 0.748 | 0.994 |

**앞 25%는 완전한 암전이고, 눈에 보이는 변화는 뒤쪽 30%에 몰려 있다.** 온도를 영상 위치에
그대로(1:1) 매핑하면 온도 0.3 부근이 "점 하나" 구간에 놓여서, 온도계가 내려가도 화면은
사실상 정지한 것처럼 보인다.

그래서 두 단계로 보정한다.

- `startRatio = 0.25` — 앞쪽 암전 구간을 잘라낸다.
- `progressCurve` — `h^0.6` 형태의 위로 볼록한 곡선. 낮은 온도 구간에서 영상이 빠르게
  움직이도록 당겨 준다.

결과 매핑:

| heat | 0.0 | 0.1 | 0.2 | 0.3 | 0.5 | 1.0 |
| :--- | :--- | :--- | :--- | :--- | :--- | :--- |
| frame | 28 | 43 | 58 | 70 | 83 | 112 |
| 평균 밝기 | 0.001 | 0.004 | 0.020 | 0.047 | 0.144 | 0.994 |

영상을 교체하면 이 곡선도 다시 잡아야 한다. `progressCurve`를 직선으로 되돌린 뒤 화면을 보며
`startRatio`와 곡선을 조정하면 된다.

### 영상은 반드시 전 프레임 키프레임(all-intra)으로 인코딩할 것

**이건 선택이 아니라 필수다.** 일반적인 H.264는 대부분의 프레임을 "직전 프레임과의 차이"로
저장하므로, 앞으로 가는 탐색은 몇 프레임만 더 디코딩하면 되지만 **뒤로 가는 탐색은 직전
키프레임까지 되돌아가 거기서부터 다시 디코딩**해야 한다. 온도가 내려갈 때 초당 10~20회씩
역방향 탐색을 요청하므로, 디코더가 못 따라가고 화면이 뚝뚝 끊긴다.

실제로 처음 넣었던 `BigBang.mp4`는 113프레임 전체에 **키프레임이 frame 0 하나뿐**이었고,
그 탓에 되감기가 최대 9프레임까지 밀렸다 한 번에 따라잡는 현상이 있었다. 아래 명령으로
모든 프레임을 키프레임으로 재인코딩해 해결했다.

```bash
ffmpeg -y -i 원본.mp4 -c:v libx264 -preset slow -crf 18 \
  -g 1 -x264-params keyint=1:min-keyint=1:scenecut=0 \
  -pix_fmt yuv420p -an Assets/04_Video/BigBang.mp4
```

용량은 1.9MB → 2.7MB 로 거의 늘지 않았다(내용이 대부분 검정이라 그렇다). 영상을 교체할 때는
반드시 이 과정을 거치고, 아래 명령으로 키프레임 수를 확인한다. 전부 `1` 이어야 한다.

```bash
ffprobe -v error -select_streams v:0 -show_entries frame=key_frame -of csv=p=0 파일.mp4
```

> 파일을 교체할 때는 `Assets/04_Video/BigBang.mp4`를 **덮어쓰기**한다. `.meta`가 유지되어
> GUID가 그대로이므로 프리팹의 참조가 깨지지 않는다.

## 3. 구성 요소

### 스크립트 (`Assets/01_Scripts/Mini_BigBang/`)

| 파일 | 역할 |
| :--- | :--- |
| `BigBangMiniGame.cs` | `MiniGame` 상속. 온도 상태머신 + W 입력 + 클리어/실패 판정 |
| `BigBangVisual.cs` | 진행도(0~1)를 받아 영상을 스크럽하는 연출 전담 컴포넌트 |
| `BigBangSceneFlow.cs` | 씬 레벨 처리. 성공 시 씬 전환, 실패 시 실패 UI + 종료 |

`BigBangMiniGame`은 씬 전환이나 `Application.Quit()`을 직접 호출하지 않는다. `ReportFinished`만
통지하고, 바깥일은 `BigBangSceneFlow`가 처리한다. 그래야 이 미니게임을 나중에
`GameFlowController` 흐름에 그대로 꽂을 수 있다.

### 에셋

| 경로 | 내용 |
| :--- | :--- |
| `Assets/04_Video/BigBang.mp4` | 빅뱅 영상 (1920x1080, 30fps, 113프레임, all-intra) |
| `Assets/04_Video/BigBangRT.renderTexture` | 영상 출력용 RenderTexture (1280x720) |
| `Assets/03_Prefabs/BigBangGame.prefab` | 미니게임 프리팹 |
| `Assets/00_Scenes/00_BigBang.unity` | 호스트 씬 (빌드 0번) |

### 프리팹 구조

```
BigBangGame                     BigBangMiniGame / BigBangVisual / VideoPlayer
└─ Canvas                       Screen Space - Overlay, 1920x1080 기준 스케일
   ├─ Background                검정 Image (영상 준비 전 화면 보호)
   ├─ Screen                    RawImage ← BigBangRT (영상 출력)
   └─ Thermometer               오른쪽 중앙 앵커, 90x560
      ├─ Back                   온도계 배경
      └─ Fill                   Image(Filled / Vertical / Bottom) ← 온도 게이지
```

`BigBangMiniGame.onHeatChanged` → `Fill`의 `Image.fillAmount`가 인스펙터에 연결돼 있다.
온도계 외에 다른 연출(사운드, 파티클 등)을 붙이고 싶으면 `onHeatChanged` / `onClear` /
`onFail`에 추가로 등록하면 된다.

### 씬 구성 (`00_BigBang`)

```
Main Camera                     배경 검정
EventSystem                     InputSystemUIInputModule
MiniGamePlayer                  gamePrefabs[0] = BigBangGame, playFirstOnStart = true
BigBangSceneFlow                player / failUI / mainSceneName / quitDelay
FailCanvas (비활성)             sortingOrder 100
├─ Dim                          검정 85% 오버레이
└─ Label                        TextMeshProUGUI "GAME OVER"
```

**실패 UI는 반드시 씬에 둔다.** `MiniGamePlayer`는 종료 통지 직후 미니게임 인스턴스를
`StopAndReset()` + 비활성화하므로, 실패 UI를 프리팹 안에 넣으면 켜지자마자 같이 꺼진다.

빌드 세팅에는 `00_BigBang`(0번)과 `00000_Player`(1번)가 등록돼 있어야 씬 전환이 동작한다.

## 4. 미니게임 계약 준수

[MiniGame_Contract.md](MiniGame_Contract.md) 기준으로:

- `MiniGame` 상속, `OnPlay()` / `OnStopAndReset()`만 구현
- 클리어 `ReportFinished(true)` / 실패 `ReportFinished(false)` 각각 1회
- `Awake`/`Start`에 게임 진행 로직 없음. `Update()`는 `state != Playing`이면 즉시 return
- `OnStopAndReset()`은 멱등 — 온도를 `startHeat`로, 영상을 0프레임으로 되돌린다
- 프리팹에 Main Camera / EventSystem 없음

`BigBangVisual`은 영상 준비(`Prepare()`)가 비동기라는 점을 흡수한다. 준비 완료 전에 들어온
진행도는 보관했다가 완료 시점에 반영하므로, 재생 시작 직후 프레임에서도 값이 어긋나지 않는다.

## 5. 검증 결과

Unity CLI로 에디터에 직접 붙어 플레이 모드에서 실측한 값이다.

**클리어 경로** — W를 누른 상태로 유지

| 시점 | Heat | fillAmount | video.frame (전체 113) |
| :--- | :--- | :--- | :--- |
| T1 | 0.346 | 0.346 | 39 |
| T2 | 0.396 | 0.396 | 44 |
| T3 | 0.443 | 0.443 | 50 |

- `fillAmount`가 온도와 완전히 일치 → 인스펙터 이벤트 연결 정상
- 프레임이 `heat * 112`에 정확히 비례 (0.346→38.8, 0.396→44.4, 0.443→49.6)
- `video.isPlaying = false` — 재생이 아니라 **스크럽**으로 동작함을 확인
- 온도가 만땅에 도달하자 `activeScene`이 `00000_Player`로 전환됨

**실패 경로** — 입력 없이 방치

- 온도가 0에 도달하자 `failUI.activeSelf = true` (라벨 "GAME OVER" 정상 렌더)
- 미니게임 인스턴스는 자동으로 비활성화됨 (`MiniGamePlayer`의 정리 경로)
- `quitDelay` 경과 후 플레이 모드 자동 종료 = `Application.Quit()` 경로 도달

**역방향 스크럽** — RenderTexture 픽셀의 평균 밝기를 직접 읽어 확인

- 진행도를 0.95 → 0.5 → 0.05 로 되돌리면 밝기가 0.9954 → 0.0177 → 0.0000 으로 따라 내려간다.

**방향별 탐색 지연** — 상승/하강 속도를 똑같이 0.15/s 로 맞추고 방향만 바꿔,
`BigBangVisual`이 요청한 프레임과 `VideoPlayer`가 실제로 표시 중인 프레임의 차이를 측정

| 방향 | 재인코딩 전 | 재인코딩 후 |
| :--- | :--- | :--- |
| 상승 | 0, 0 | 0, 0, 0 |
| 하강 | **-3, -9, -1** | 0, 0, -1, 0 |
| 하강(저온 구간, 약 15프레임/초) | — | 0, 0, 0 |

재인코딩 전에는 하강 시에만 최대 9프레임이 밀렸고, 이것이 "빛이 뚝뚝 끊기며 작아지는"
현상의 정체였다. 전 프레임 키프레임으로 바꾼 뒤에는 양방향 모두 지연이 사실상 0이다.

**콘솔 로그 0건** (에러/경고 없음).

## 6. 알려진 제약 / 손볼 만한 것

- **시작 화면이 완전한 암전은 아니다.** 시작 온도 0.3 은 frame 70(작은 빛 덩어리)에 대응한다.
  진짜 암전에서 시작하게 하려면 `startHeat`를 낮추거나 `progressCurve`를 아래로 볼록하게
  바꾼다. 다만 `startHeat`를 낮추면 실패까지의 여유 시간도 같이 줄어든다.
- **실패 UI 문구가 영어("GAME OVER")**다. TMP 기본 폰트(LiberationSans SDF)에 한글 글리프가
  없어서다. 한글로 바꾸려면 한글 폰트 애셋을 만들어 `FailCanvas/Label`에 지정한다.
- **"실패 즉시 종료"는 `quitDelay = 1.5초`로 완충**해 두었다. 0으로 두면 UI가 보이기 전에
  꺼진다. 에디터 플레이 중에는 `Application.Quit()`이 동작하지 않으므로 플레이 모드 종료로
  대체된다(`BigBangSceneFlow.Quit()`의 `#if UNITY_EDITOR` 분기).
- **스크럽 성능**은 현재 113프레임 720p에서 문제없다. 더 길거나 큰 영상으로 교체해 버벅이면
  `BigBangVisual`만 이미지 시퀀스(`Sprite[]` 인덱싱) 방식으로 갈아끼우면 된다.
  게임 로직은 `SetProgress(float)` 인터페이스만 보므로 수정할 필요가 없다.
