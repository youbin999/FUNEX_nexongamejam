# 미니게임 개발 계약 (MiniGame Contract)

와리오웨어류 미니게임을 새로 만들 때 반드시 지켜야 하는 규약이다.
공통 베이스는 `Assets/01_Scripts/Core/MiniGame.cs`, 재생기는
`Assets/01_Scripts/Core/MiniGamePlayer.cs`. 실제 구현 예시는
`Assets/01_Scripts/Mini_sha/RubMiniGame.cs` 참고.

## 1. 반드시 지켜야 할 것

### 1.1 `MiniGame`을 상속한다

```csharp
public class MyMiniGame : MiniGame
{
    protected override void OnPlay() { /* 재생 시작 처리 */ }
    protected override void OnStopAndReset() { /* 초기 상태로 복원 */ }
}
```

- `Play()` / `StopAndReset()` / `Finished` 이벤트는 베이스 클래스가 제공한다. **재정의하지 말 것.**
- 게임 로직은 `OnPlay()` / `OnStopAndReset()` 두 훅에만 구현한다.

### 1.2 게임 종료는 `ReportFinished(bool success)`로만 통지한다

- 클리어/실패가 결정되는 순간 `ReportFinished(true)` 또는 `ReportFinished(false)`를 정확히 **한 번** 호출한다.
- `ReportFinished`는 `IsPlaying == false`면 아무 일도 하지 않으므로, 클리어와 실패 조건이 같은 프레임에 겹쳐도 두 번째 호출은 안전하게 무시된다. 하지만 로직상 애초에 한 번만 호출되도록 짜는 것이 원칙이다.
- `Finished` 이벤트를 직접 구독/발화하지 않는다. 인스펙터 연출용 이벤트(`UnityEvent`, 예: `onClear`, `onFail`)는 `ReportFinished` 호출과 별도로 자유롭게 추가해도 된다. (`RubMiniGame`의 `onClear`/`onFail` 참고)

### 1.3 `Awake`/`Start`에서 게임을 자동 시작하지 않는다 (Idle 보장)

- 프리팹은 `MiniGamePlayer`가 프리로드 시점에 `Instantiate`만 해두고 활성화하지 않는다. 실제 시작은 `Play()` 호출(→ `OnPlay()`) 시점이다.
- `Awake`/`Start`에서는 캐시(카메라 참조 등)나 초기 UI 표시(`onTimerChanged.Invoke(0f)` 등)만 해도 되지만, 입력을 받거나 타이머를 흘리는 등 "게임 진행"에 해당하는 동작을 시작해서는 안 된다.
- `Update()`는 항상 자체 상태(`state == Playing`)를 확인해 가드한 뒤 로직을 실행한다. `IsPlaying`이 false인 동안 `Update`가 아무것도 하지 않아야 한다.

### 1.4 `OnStopAndReset()`은 언제 호출돼도 안전해야 한다 (멱등)

- 재생 중이 아닐 때 호출돼도 예외 없이 초기 상태로 수렴해야 한다.
- 타이머, 카운트, 오브젝트 위치/자세 등 게임이 변경한 모든 상태를 여기서 초기값으로 되돌린다. 다음 재생을 위해 인스턴스가 재사용되기 때문이다.

### 1.5 프리팹 루트의 저장된 활성 상태는 신경 쓰지 않아도 된다

- `MiniGamePlayer.Preload()`는 내부적으로 비활성 상태의 홀더 오브젝트(`[Preload Pool]`) 아래에 모든 프리팹을 `Instantiate`한다. 부모가 비활성이면 자식의 `activeInHierarchy`도 false가 되므로, 프리팹 루트가 활성으로 저장돼 있어도 프리로드 시점에 `Awake`/`OnEnable`이 돌지 않는다.
- 따라서 프리팹 저장 시 루트의 활성 체크박스를 굳이 꺼서 저장할 필요가 없다. `MiniGamePlayer`를 통해 재생되는 이상 개발 편의대로 활성 상태로 두고 작업해도 안전하다.
- 단, `MiniGamePlayer`를 거치지 않고 씬에 직접 배치해 테스트하는 경우에는 이 보장이 없다. 그 경우엔 활성 상태에서 게임이 자동으로 시작되지 않는지 반드시 확인한다 (1.3 참고).

### 1.6 프리팹에는 게임 콘텐츠만 넣는다

- 포함: 게임 로직, 배경, 이 게임 전용 UI `Canvas`.
- 포함하지 않음: `Main Camera`, `EventSystem`. 이들은 호스트 씬(`Assets/00_Scenes/00000_Player.unity`) 소유다.
- 카메라가 필요하면 직렬화 필드로 받되 비어 있으면 `Camera.main`으로 폴백한다 (`RubMiniGame.targetCamera` 참고).

## 2. 참고 사항 (알아두면 좋은 것)

- `MiniGamePlayer`는 게임 종료 통지(`onGameFinished`)를 먼저 발화한 뒤 자동으로 `StopAndReset()` + 비활성화를 호출한다. 미니게임 쪽에서 스스로를 비활성화하거나 다시 초기화할 필요는 없다.
- `IsPlaying`(베이스 클래스 프로퍼티)은 읽기 전용 상태 조회용으로만 쓴다. 게임 내부 상태 머신(예: `RubMiniGame`의 `State` enum)은 별도로 두고, `Finished` 통지 타이밍만 `IsPlaying`에 맞추면 된다.
- 인스펙터에 노출할 제네릭 이벤트(`UnityEvent<T>`)는 Unity가 제네릭을 직렬화하지 못하므로 구체 타입 서브클래스로 선언한다:
  ```csharp
  [Serializable] public class MyEvent : UnityEvent<int> { }
  public MyEvent onSomething;
  ```
- 어떤 게임을 언제 재생할지 고르는 상위 컨트롤러는 아직 없다 (향후 과제). 지금은 `MiniGamePlayer.PlayGame(index)`를 직접 호출하는 수준.

## 3. 새 미니게임 체크리스트

- [ ] `MiniGame` 상속, `OnPlay`/`OnStopAndReset` 구현
- [ ] 클리어/실패 각각 `ReportFinished(true/false)` 정확히 1회 호출
- [ ] `Awake`/`Start`에서 게임 진행 로직이 돌지 않음 확인
- [ ] `OnStopAndReset()`을 멱등하게 구현 (미재생 상태에서 호출해도 안전)
- [ ] Main Camera/EventSystem을 프리팹에 포함하지 않음, 카메라는 `Camera.main` 폴백
- [ ] `MiniGamePlayer`의 `gamePrefabs` 리스트에 등록해 재생 테스트 (프리팹 루트의 활성 상태는 상관없음)
