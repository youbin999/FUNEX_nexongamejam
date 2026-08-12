# 빅뱅 미니게임 (BigBang)

게임의 프롤로그. `00_BigBang` 씬에서 단독으로 돌아가며, 클리어하면 메인 씬(`00000_Player`)으로
넘어가고 실패하면 애플리케이션이 종료된다.

## 1. 게임 규칙

- 검은 화면에서 시작한다. 화면 오른쪽에 세로 온도계가 있다.
- **W(또는 위 방향키)를 꾹 누르고 있으면** 온도가 오른다. 떼면 천천히 내려간다.
- 온도가 **끝까지 차오르면 클리어** → 메인 씬으로 이동.
- 온도가 **0까지 식으면 실패** → 실패 UI를 띄운 뒤 정리. 데스크톱은 애플리케이션 종료,
  **Web 은 씬 재로드(다시 빅뱅부터)**, 에디터는 플레이 모드 종료.

| 값 | 기본값 | 의미 |
| :--- | :--- | :--- |
| `startHeat` | 0.3 | 시작 온도. 여기서부터 오르내린다 |
| `heatRisePerSecond` | 0.5 | 누르는 동안 초당 상승량 → 계속 누르면 **1.4초**에 클리어 |
| `heatFallPerSecond` | 0.15 | 떼는 동안 초당 하강량 → 방치하면 **2초**에 실패 |
| `quitDelay` | 1.5 | 실패 UI를 보여준 뒤 종료하기까지의 시간(초) |

> 실패 조건이 "온도 0"이므로 별도의 제한시간은 없다. 손을 놓는 순간부터 2초가 사실상의 제한시간이다.

## 2. 우주 팽창 연출 — 영상 스크럽

핵심은 **온도(heat)가 마스터, 영상은 종속**이라는 것이다. 빅뱅 영상은 스스로 재생되지 않는다.
`VideoPlayer`를 `Pause()` 상태로 두고, 온도 값(0~1)을 재생 시각(초)으로 환산해 대입한다.

```
보정 진행도 = progressCurve.Evaluate(heat)
영상 위치   = lerp(startRatio, endRatio, 보정 진행도)
목표 시각   = 영상 위치 * videoPlayer.length
```

- W를 안 누르면 온도가 내려가면서 영상도 **되감겨** 어둠으로 돌아간다.
- W를 누르면 온도가 오르면서 빛이 커진다. 온도계와 영상이 항상 같은 값을 본다.
- 목표 시각이 **한 프레임(1/30초) 이상 움직였을 때만** `videoPlayer.time`에 대입한다. 매 프레임
  무조건 대입하면 탐색 요청이 쌓여 버벅인다.
- 끝에 정확히 닿으면 재생 종료로 처리돼 그림이 날아갈 수 있어, 상한을 `length - 1/30`로 물려 둔다.

### 왜 프레임(`frame`)이 아니라 시각(`time`)인가 — Web 제약

원래는 `videoPlayer.frame`에 프레임 번호를 대입했다. 데스크톱에서는 잘 돌았지만 **Web(WebGL)
빌드에서 영상이 아예 뜨지 않는다.** Unity 매뉴얼이 명시하는 두 가지 제약 때문이다.

- **"VideoClips aren't supported on Web."** Web 의 `VideoPlayer`는 브라우저 `<video>` 엘리먼트로
  구현돼 있어 VideoClip 에셋을 받지 못한다. 반드시 URL 소스를 써야 한다.
- **"Web doesn't support frame accuracy."** `frameCount`가 0을 돌려주고 `frame` 대입은 무시된다.
  `frameCount <= 0` 가드에 걸려 탐색 요청이 **한 번도 나가지 않으므로**, 온도계는 정상인데
  화면만 검은 채로 멈춘다.

그래서 두 가지를 바꿨다.

- 원본을 `Assets/StreamingAssets/BigBang.mp4`로 옮기고, `BigBangVisual.Awake`에서
  `source = VideoSource.Url` / `url = $"{Application.streamingAssetsPath}/{videoFileName}"` 로 지정한다.
  URL 소스는 데스크톱에서도 그대로 동작하므로 플랫폼 분기를 두지 않는다.
- 탐색을 `frame`(프레임) 대신 `time`(초)으로 한다. `length`는 Web 에서도 읽을 수 있다.

`MapProgress()`가 여전히 0~1 미디어 위치를 돌려주므로 `startRatio`·`progressCurve` 튜닝값은
그대로 살아 있다. 바뀐 것은 그 결과를 프레임이 아니라 초로 환산한다는 것뿐이다.

### Web 에서 검은 화면이 나왔던 진짜 이유 — 탐색 인터록과 첫 프레임

위까지 고치고도 **Web 빌드는 여전히 검은 화면이었다.** 온도계는 정상이고, DevTools Network 에는
`/StreamingAssets/BigBang.mp4` 가 304 로 2.7MB 전부(byte-range 5조각) 도착해 있었으며 콘솔에
비디오 에러도 0건이었다. 즉 **로딩이 아니라 텍스처 업로드가 막힌 것**이다.

원인은 Unity 의 Web 비디오 구현체를 직접 읽어 확인했다
(`<Unity 설치경로>/Editor/Data/PlaybackEngines/WebGLSupport/BuildTools/lib/Video.js`).
Web 의 `VideoPlayer` 는 브라우저 `<video>` 엘리먼트이고, 매 프레임 `JS_Video_UpdateToTexture`
가 그 그림을 텍스처로 올리는데 그 함수 앞에 가드가 둘 있다.

```js
if (!v.isLoaded) return false;   // 프레임이 한 번도 "표시"된 적 없으면 거부
...
if (v.seeking) return false;     // 탐색이 끝나지 않았으면 거부
```

**두 번째 가드가 결정타였다.** 기존 코드는 목표가 한 프레임만 움직여도 매번 `videoPlayer.time`
에 대입했는데, 실제 요청 빈도를 §2-2 배속 표로 환산하면 이렇다.

| 상황 | 영상 배속 | 초당 탐색 요청 |
| :--- | :--- | :--- |
| W 누름 (heat 0.3) | 1.37x | 약 41회 (24ms 간격) |
| 뗌 (heat 0.3) | 0.41x | 약 12회 (81ms 간격) |

1080p all-intra 프레임 하나를 디코드하는 데 24ms 보다 오래 걸리므로, 이전 탐색이 끝나기 전에
다음 `currentTime` 대입이 들어가 `v.seeking` 이 **영구히 참**으로 유지된다 → 텍스처가 한 번도
갱신되지 않는다. 데스크톱 백엔드에는 이 인터록이 없어서 에디터에서만 멀쩡했던 것이다.

→ **탐색은 한 번에 하나만 띄운다.** `seekCompleted` 를 구독해, 탐색이 떠 있는 동안에는 요청을
보내지 않고 최신 진행도만 `pendingProgress` 에 남겼다가 완료 시 그 값으로 한 번에 따라잡는다
(중간 값은 버린다). 통지가 유실될 경우를 대비해 `SeekTimeout = 0.5초` 안전장치를 뒀다.

첫 번째 가드 때문에 `OnPrepared` 의 "한 번 재생했다 멈춰 첫 프레임을 올리는" 트릭도 **Web 에서
빼면 안 된다 — 오히려 Web 에서 가장 필요하다.** `isLoaded` 를 세우는 유일한 방법이 재생이기
때문이다. Video.js 주석도 못박아 두었다: *"or else the first frame never shows up after calling
Pause()"*. 예전에 이걸 `#if !UNITY_WEBGL` 로 빼 둔 근거("브라우저가 제스처 전 `play()`를 거부한다")는
**틀렸다.** Video.js 는 엘리먼트를 만들 때 무조건 `video.muted = true` 로 두고(자동재생 정책은
뮤트 미디어를 막지 않는다), 설령 거부되더라도 `jsVideoAddPendingBlockedVideo` 가 첫 클릭에
자동 재시도한다. 다만 같은 프레임에 바로 `Pause()` 하면 프레임이 표시되기 전에 멈출 수 있어,
코루틴으로 한 프레임 준 뒤 멈춘다.

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

브라우저의 `currentTime` 탐색도 똑같이 키프레임에 의존하므로, 이 인코딩은 Web 빌드에서도
그대로 필요하다.

```bash
ffmpeg -y -i 원본.mp4 -c:v libx264 -preset slow -crf 18 \
  -g 1 -x264-params keyint=1:min-keyint=1:scenecut=0 \
  -pix_fmt yuv420p -an Assets/StreamingAssets/BigBang.mp4
```

용량은 1.9MB → 2.7MB 로 거의 늘지 않았다(내용이 대부분 검정이라 그렇다). 영상을 교체할 때는
반드시 이 과정을 거치고, 아래 명령으로 키프레임 수를 확인한다. 전부 `1` 이어야 한다.

```bash
ffprobe -v error -select_streams v:0 -show_entries frame=key_frame -of csv=p=0 파일.mp4
```

> 파일을 교체할 때는 `Assets/StreamingAssets/BigBang.mp4`를 **덮어쓰기**한다. 프리팹은 이제
> 에셋 GUID가 아니라 **파일 이름**(`BigBangVisual.videoFileName`)으로 참조하므로, 이름만 같으면
> 참조가 깨지지 않는다. 이름을 바꾸려면 인스펙터의 `Video File Name` 도 같이 고친다.

## 2-2. 효과음도 같이 왔다갔다 — PCM 스크럽

효과음(`(Audio) BigBang.wav`, 3.755초)은 영상에서 추출한 것이라 영상(3.767초)과 1:1로 대응한다.
그래서 **영상과 똑같은 위치 값을 받아 같이 앞뒤로 움직인다.** 테이프를 손으로 돌리는 것과 같아서,
W를 누르면 정방향으로 흐르고 떼면 역재생되며, 온도가 멈추면 소리도 멈춘다.

### `AudioSource.time` 대입은 쓸 수 없다

영상에는 `videoPlayer.frame` 이라는 임의 탐색 API가 있지만 오디오에는 그에 대응하는 게 없다.
`audioSource.time` 을 매 프레임 대입하는 방식은 세 가지 이유로 실패한다.

- seek 할 때마다 파형이 불연속이 되어 팝 노이즈가 난다. 초당 60번이면 노이즈가 곧 효과음이 된다.
- 압축 오디오의 seek 은 인코딩 프레임 경계로 스냅되므로 정밀도가 20ms 단위다.
- 역재생은 아예 표현되지 않는다.

그래서 `BigBangAudio` 는 원본 PCM 전체를 `AudioClip.GetData` 로 메모리에 올린 뒤,
`OnAudioFilterRead` 에서 재생 헤드를 직접 움직여 출력 버퍼를 채운다. 3.7초짜리라 전부 올려도
720KB 남짓이다.

```
[메인 스레드] 속도 = (이번 위치 - 지난 위치) / Time.deltaTime      (1차 필터로 평활화)
               배속 = 속도 * (frameCount - 1) / sampleRate
[오디오 스레드] 샘플당 증분 = 배속 + (목표 - 헤드) * 따라잡기계수
               출력 = lerp(samples[floor(head)], samples[floor(head)+1], frac) * gain
```

증분이 음수면 자연히 역재생이 된다. 헤드가 샘플 사이에 걸치므로 **선형 보간이 필수**다.
그냥 반올림하면 지직거린다.

### 소리가 뚝뚝 끊겼던 이유 — 두 번 밟은 지뢰

**1차 시도 (실패): 배속을 콜백 간 목표 변화량에서 뽑음.** 오디오 스레드에서
`(목표 - 헤드) / 버퍼길이` 로 계산했다. "이 버퍼 안에서 목표까지 정확히 도달한다"는 뜻이라
언뜻 맞아 보이지만, 콜백이 실시간과 1:1로 페이싱된다는 가정이 깔려 있다. 목표 위치는
`Update` 에서 60Hz로 갱신되는데 오디오 버퍼는 1024샘플 @48kHz = 46.9Hz라, 대략 4번에 1번꼴로
목표가 갱신되지 않은 채 콜백이 들어온다. 그때 배속이 0이 되어 21ms짜리 무음이 초당 10여 번
끼어들었다.

→ **배속은 메인 스레드에서 `Time.deltaTime` 기준으로 계산해 넘긴다.** 그러면 콜백이 언제 몇 번
불리든 샘플당 증분이 같다.

**2차 시도 (실패): 훅이 `AudioClip.Create(..., stream: true)` 의 리더 콜백이었음.** 배속을
고쳤는데도 끊겼다. 결정적 단서는 가상 클립 길이를 4096샘플(85ms)에서 1초로 늘렸더니 **더
심해졌다**는 것. 이 콜백은 호출 시점이 실제 재생 위치와 묶여 있지 않고, FMOD 가 스트림 버퍼를
채우려고 **클립 길이만큼 통째로 미리 읽어 간다.** 즉 한순간의 배속 값으로 생성된 긴 구간이
그대로 재생되고 다음 읽기에서 점프한다. 클립을 길게 잡을수록 아티팩트가 거칠어진다.

→ **`OnAudioFilterRead` 로 교체.** DSP 블록당 정확히 한 번, 출력과 실시간 락스텝으로 호출되는
것이 보장되는 유일한 훅이다.

위치 오차(헤드와 목표의 차이)는 배속에 직접 반영하지 않고 `catchUpSeconds` 에 걸쳐 천천히
흡수한다. 이 항을 세게 잡으면 다시 배속이 요동친다.

### `Pan2D` 는 "2D 모드"가 아니다

AudioSource 프리팹 YAML 의 `Pan2D` 는 `AudioSource.panStereo`(-1 왼쪽 ~ +1 오른쪽)다.
2D/3D 설정이 아니다(그건 `panLevelCustomCurve` = `spatialBlend`). 여기에 `1` 을 넣으면
**오른쪽 채널에서만 소리가 난다.** 한 번 이걸로 삽질했으므로 `BigBangAudio.Awake` 에서
`panStereo = 0` / `spatialBlend = 0` 을 코드로도 강제해 둔다.

`OnAudioFilterRead` 는 AudioSource 가 재생 중일 때만 호출되므로, 내용을 덮어쓸 무음 클립을
하나 물려 두고 루프로 돌린다(전부 0이라 루프 이음매가 없다). 이 필터는 **원본이 아니라 출력
스트림 위에서** 돌기 때문에, 배속·따라잡기 계수·게인 페이드는 모두 원본 클립이 아닌
`AudioSettings.outputSampleRate` 기준으로 계산해야 한다. 출력 채널 수도 원본과 다를 수 있어
콜백이 넘겨주는 `channels` 를 그대로 써야 한다.

### 배속(피치)은 일부러 고정하지 않았다

재생 배속이 곧 진행도 변화 속도이므로 피치가 함께 흔들린다. 이게 "우주가 팽창/수축하는" 연출과
맞아떨어져서 그대로 뒀다. `progressCurve`(h^0.6)와 `startRatio = 0.25` 를 반영한 실제 배속은:

| 상황 | heat 0.05 | 0.3 | 0.5 | 1.0 |
| :--- | :--- | :--- | :--- | :--- |
| W 누름 (+0.5/s) | 2.80x | 1.37x | 1.12x | 0.85x |
| 뗌 (-0.15/s) | -0.84x | -0.41x | -0.33x | -0.25x |

즉 **누르고 있으면 거의 원래 속도(0.85~1.4x)로 정방향 재생**되고, 떼면 그보다 느리게 되감긴다.
`maxRate = 3` 은 저온 구간에서 배속이 튀는 것을 막는 안전장치다.

heat 가 1에 닿을 때 위치도 클립 끝에 닿으므로, 클리어 순간에 잘려 나가는 소리는 사실상 없다
(한 버퍼 분량인 20ms 남짓이 전부다). 별도의 꼬리 재생 처리를 두지 않은 이유다.

### 위치 계산은 반드시 한 곳에서만

`BigBangVisual.MapProgress()` 가 `startRatio` / `endRatio` / `progressCurve` 를 적용한 미디어
위치를 계산하고, 영상과 효과음이 **둘 다 그 결과만** 받는다. 곡선을 다시 잡아도 소리가 저절로
따라오게 하기 위함이다. `heat` 를 오디오에 그대로 넘기면 안 된다 — 보정이 빠져 영상과 다른
지점을 가리킨다.

오디오로 넘기는 호출은 `SetProgress()` 안, **프레임 중복 체크보다 앞**에 있어야 한다.
`ApplyProgress()` 안쪽(중복 체크 뒤)에 두면 30fps 격자로 스냅된 위치가 넘어가 소리가
계단처럼 끊긴다.

### 오디오 임포트 설정 (필수)

| 항목 | 값 | 이유 |
| :--- | :--- | :--- |
| Load Type | **Decompress On Load** | 다른 값이면 `GetData` 가 무음을 돌려준다 |
| Preload Audio Data | **켬** | 꺼져 있으면 `Awake` 의 `GetData` 시점에 아직 로드 전이다 |
| Compression Format | PCM | 3.7초짜리라 720KB. 재인코딩 패딩으로 영상과 어긋나는 것을 막는다 |
| 3D | 끔 | 2D 효과음 (스크립트에서도 `spatialBlend = 0` 을 강제한다) |

앞의 두 개는 틀리면 **소리가 아예 안 난다.** `BigBangAudio` 가 이 둘을 검사해서 경고 로그를
남기므로, 무음이면 콘솔부터 볼 것.

## 3. 구성 요소

### 스크립트 (`Assets/01_Scripts/Mini_BigBang/`)

| 파일 | 역할 |
| :--- | :--- |
| `BigBangMiniGame.cs` | `MiniGame` 상속. 온도 상태머신 + W 입력 + 클리어/실패 판정 |
| `BigBangVisual.cs` | 진행도(0~1)를 미디어 위치로 변환해 영상을 시각(초) 단위로 스크럽. 같은 값을 `BigBangAudio` 에도 넘긴다 |
| `BigBangAudio.cs` | 미디어 위치를 받아 효과음을 스크럽하는 PCM 재생기 |
| `BigBangSceneFlow.cs` | 씬 레벨 처리. 성공 시 씬 전환, 실패 시 실패 UI + 종료 |

`BigBangMiniGame`은 씬 전환이나 `Application.Quit()`을 직접 호출하지 않는다. `ReportFinished`만
통지하고, 바깥일은 `BigBangSceneFlow`가 처리한다. 그래야 이 미니게임을 나중에
`GameFlowController` 흐름에 그대로 꽂을 수 있다.

### 에셋

| 경로 | 내용 |
| :--- | :--- |
| `Assets/StreamingAssets/BigBang.mp4` | 빅뱅 영상 (1920x1080, 30fps, 113프레임 = 3.767초, all-intra). **VideoClip 에셋이 아니라 URL 로 읽는다** — Web 이 VideoClip 을 지원하지 않는다 |
| `Assets/04_Video/(Audio) BigBang.wav` | 위 영상에서 추출한 효과음 (48kHz 스테레오 PCM, 3.755초) |
| `Assets/04_Video/BigBangRT.renderTexture` | 영상 출력용 RenderTexture (1280x720) |
| `Assets/03_Prefabs/BigBangGame.prefab` | 미니게임 프리팹 |
| `Assets/00_Scenes/00_BigBang.unity` | 호스트 씬 (빌드 0번) |

### 프리팹 구조

```
BigBangGame                     BigBangMiniGame / BigBangVisual / VideoPlayer
                                + BigBangAudio / AudioSource
└─ Canvas                       Screen Space - Overlay, 1920x1080 기준 스케일
   ├─ Background                검정 Image (영상 준비 전 화면 보호)
   ├─ Screen                    RawImage ← BigBangRT (영상 출력)
   └─ Thermometer               오른쪽 중앙 앵커, 90x560
      ├─ Back                   온도계 배경
      └─ Fill                   Image(Filled / Vertical / Bottom) ← 온도 게이지
```

`BigBangMiniGame.onHeatChanged` → `Fill`의 `Image.fillAmount`가 인스펙터에 연결돼 있다.
온도계 외에 다른 연출(파티클 등)을 붙이고 싶으면 `onHeatChanged` / `onClear` / `onFail`에
추가로 등록하면 된다. 단 **효과음은 여기에 붙이지 않는다** — 스크럽 효과음은 보정이 적용된
미디어 위치를 받아야 하므로 `BigBangVisual.audioScrub` 슬롯으로 연결돼 있다.

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

### URL 소스 + 시각(`time`) 스크럽 전환 후 재측정

에디터 플레이 모드에 붙어 실제 인스턴스를 읽은 값이다.

| 항목 | 값 |
| :--- | :--- |
| `source` | `Url` |
| `url` | `D:/.../Assets/StreamingAssets/BigBang.mp4` |
| `isPrepared` | `True` |
| `length` | `3.7667` 초 |
| `heat` | `0.2374` |
| `time` | `2.1333` 초 |
| RT 평균 밝기 | `0.0350` (준비 전 `0.0007` → 그림이 올라옴) |
| `isPlaying` | `False` — 재생이 아니라 **스크럽** |

`heat = 0.2374` 를 매핑식에 넣으면
`0.2374^0.6 = 0.4220` → `lerp(0.25, 1, 0.4220) = 0.5665` → `× 3.7667 = 2.1335` 초로,
측정된 `time = 2.1333` 과 일치한다. 즉 `startRatio`·`progressCurve` 보정이 전환 후에도
그대로 살아 있다. 밝기 `0.0350` 도 §2 의 실측표(heat 0.2 → 0.020, 0.3 → 0.047) 사이에 들어맞는다.

**콘솔 로그 0건.** 스크립트 재컴파일도 에러 0건.

### Web 빌드 실측 (127.0.0.1:5500)

위 표는 전부 에디터(데스크톱 백엔드) 값이고, Web 은 별도로 갈렸다. 첫 Web 빌드는 **검은 화면**이
나왔으며 DevTools 로 확인한 상태는 다음과 같다.

| 항목 | 관측값 | 판정 |
| :--- | :--- | :--- |
| `BigBang.mp4` 요청 URL | `/StreamingAssets/BigBang.mp4` | 경로 정상 |
| 상태 / 크기 | 304 × 5 조각, 합 ≈ 2.67MB | 파일 전체 도착 |
| Type / Initiator | `media` / `Other` | `<video>` 엘리먼트가 직접 요청 = 소스 연결 성공 |
| 비디오 에러 로그 | 0건 | 코덱·CORS 정상 |
| 게임 로직 | `Quitting...` 까지 도달 | 온도/입력/실패 판정 정상 |

즉 **로딩 단계는 전부 통과했고 영상만 검었다.** 원인과 수정은 §2 의 "Web 에서 검은 화면이
나왔던 진짜 이유" 참고. `Application.streamingAssetsPath` 는 Web 에서
`new URL("StreamingAssets", document.URL).href` 로 풀리므로(`UnityLoader.js`), 페이지 주소가
슬래시 없이 끝나는 디렉터리 형태면 한 단계 위로 풀려 404 가 난다는 점만 주의한다.

> **수정 후 브라우저 재측정은 아직이다.** 탐색 합치기와 첫 프레임 재생은 위 Video.js 분석에
> 근거한 수정이고, 컴파일까지만 확인했다. 그래도 검으면 §6 의 이미지 시퀀스 대안으로 간다.

> **효과음 스크럽은 계측 검증 전이다.** 첫 구현은 플레이 모드에서 "소리가 뚝뚝 끊긴다"는
> 문제가 나왔고, 원인(배속을 콜백 간 목표 변화량에서 뽑은 것)은 위 2-2 절에 적어 두었다.
> 수정본은 컴파일과 프리팹 참조 해소까지만 확인했으며, 위 배속 표도 `progressCurve`(h^0.6)로
> 계산한 값이지 측정값이 아니다.

## 6. 알려진 제약 / 손볼 만한 것

- **시작 화면이 완전한 암전은 아니다.** 시작 온도 0.3 은 frame 70(작은 빛 덩어리)에 대응한다.
  진짜 암전에서 시작하게 하려면 `startHeat`를 낮추거나 `progressCurve`를 아래로 볼록하게
  바꾼다. 다만 `startHeat`를 낮추면 실패까지의 여유 시간도 같이 줄어든다.
- **실패 UI 문구가 영어("GAME OVER")**다. TMP 기본 폰트(LiberationSans SDF)에 한글 글리프가
  없어서다. 한글로 바꾸려면 한글 폰트 애셋을 만들어 `FailCanvas/Label`에 지정한다.
- **"실패 즉시 종료"는 `quitDelay = 1.5초`로 완충**해 두었다. 0으로 두면 UI가 보이기 전에
  꺼진다. 마무리 동작은 `BigBangSceneFlow.Quit()` 이 플랫폼별로 나눈다 — 에디터는 플레이 모드
  종료, **Web 은 현재 씬 재로드**, 그 외는 `Application.Quit()`. Web 에서 `Application.Quit()` 은
  캔버스를 정지시켜 새로고침 말고는 복구가 안 되고, 기획상 빅뱅 실패는 "다시 빅뱅부터"이므로
  재로드가 맞다.
- **스크럽 성능**은 현재 113프레임 720p에서 문제없다. 더 길거나 큰 영상으로 교체해 버벅이면
  `BigBangVisual`만 이미지 시퀀스(`Sprite[]` 인덱싱) 방식으로 갈아끼우면 된다.
  게임 로직은 `SetProgress(float)` 인터페이스만 보므로 수정할 필요가 없다.
- **Web 에서 효과음이 나지 않는다.** `BigBangAudio` 의 PCM 스크럽은 `OnAudioFilterRead` 기반인데,
  Unity 매뉴얼상 Web 은 "scriptable audio pipeline"을 지원하지 않아 이 콜백이 호출되지 않는다
  (경고만 뜨고 크래시는 없다). 우회로가 없으므로 Web 전용 폴백 — 역재생을 포기하고 위치가
  크게 벌어졌을 때만 `AudioSource.time` 을 보정하는 단순 재생/정지 — 이 필요하다. **미구현.**
- **Web 에서 영상이 그래도 안 뜨면 이미지 시퀀스로 간다.** 3.7초 113프레임짜리라 이쪽이 오히려
  정공법에 가깝다. 브라우저 탐색 지연·자동재생 정책·all-intra 재인코딩이 전부 무의미해지고
  역방향 스크럽이 공짜가 된다. 2프레임당 1장(57장) × 640×360 DXT1 이면 VRAM 6.4MB 로
  Web 에서도 감당할 수 있다.

  ```bash
  ffmpeg -i BigBang.mp4 -vf "select='not(mod(n,2))',scale=640:360" -vsync 0 bb_%03d.png
  ```
