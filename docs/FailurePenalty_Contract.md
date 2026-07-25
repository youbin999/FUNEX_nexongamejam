# 실패 패널티 개발 계약 (Failure Penalty Contract)

미니게임 실패 후 다음 미니게임들에도 영향을 남기는 패널티를 만들 때 지켜야 하는 규약이다.
미니게임 자체의 생명주기는 [`MiniGame_Contract.md`](MiniGame_Contract.md)를 함께 따른다.

## 1. 핵심 개념

- 패널티는 `FailurePenalty`를 상속한 **별도 프리팹**으로 만든다.
- 미니게임 프리팹의 `MiniGame > Failure Penalty Prefab` 필드에 패널티 프리팹을 지정한다.
- 미니게임이 실패하면 `MiniGamePlayer`가 패널티를 생성하고 `Apply()`를 실행한다.
- `Apply()`가 끝날 때까지 실패 결과 통지와 다음 미니게임 진행이 보류된다.
- `Apply()`가 끝나도 패널티 인스턴스는 제거되지 않고 현재 한 판 동안 유지된다.
- 한 판에 발생한 여러 패널티는 누적된다.
- `GameFlowController.StartFlow()`로 새 판을 시작할 때 모든 패널티가 제거된다.
- 성공하거나 패널티 프리팹이 비어 있는 미니게임은 기존 종료 흐름을 그대로 사용한다.

```
미니게임 실패
  → FailurePenalty 프리팹 생성
  → Apply() 최초 실패 연출
  → Apply() 종료
  → onGameFinished(false)
  → 다음 미니게임

패널티 인스턴스는 새 판 시작 전까지 유지
```

## 2. 반드시 지켜야 할 규칙

### 2.1 `FailurePenalty`를 상속한다

```csharp
using System.Collections;
using UnityEngine;

public sealed class MyFailurePenalty : FailurePenalty
{
    [SerializeField] private float introDuration = 0.5f;

    public override IEnumerator Apply()
    {
        // 일회성 실패 연출을 시작한다.

        if (introDuration > 0f)
            yield return new WaitForSeconds(introDuration);

        // 이후 미니게임에도 남길 오브젝트를 활성화한다.
        // 이 코루틴이 끝나면 다음 미니게임 진행이 허용된다.
    }
}
```

### 2.2 `Apply()`는 반드시 종료되어야 한다

- `Apply()`는 다음 미니게임 진행을 막는 대기 구간이다.
- 무한 루프나 종료되지 않는 애니메이션 대기를 넣지 않는다.
- 애니메이션 이벤트를 기다린다면 누락에 대비한 최대 대기시간을 둔다.
- `Time.timeScale`의 영향을 받으면 안 되는 연출은 `WaitForSecondsRealtime`과
  `Time.unscaledDeltaTime`을 사용한다.

### 2.3 결과 통지는 패널티가 담당하지 않는다

- 패널티에서 `ReportFinished`, `PlayGame`, `onGameFinished`를 직접 호출하지 않는다.
- 성공/실패 판정은 미니게임이, 패널티 적용과 완료 후 진행은 코어가 담당한다.
- 패널티가 미니게임 프리팹이나 현재 게임 인스턴스를 직접 비활성화하지 않는다.

### 2.4 지속 효과를 `Apply()` 종료 시 제거하지 않는다

- `Apply()` 종료는 “최초 연출이 끝났다”는 뜻이지 패널티 수명이 끝났다는 뜻이 아니다.
- 영구 오버레이, 색 보정, 조작 방해 등의 지속 상태는 패널티 인스턴스에 남긴다.
- 패널티 인스턴스를 스스로 `Destroy`하지 않는다.
- 새 판 시작 시 `FailurePenaltyController.ClearAll()`이 일괄 제거한다.

### 2.5 정리해야 하는 외부 오브젝트는 패널티가 책임진다

패널티 루트에서 분리한 파티클처럼 `FailurePenaltyController`가 직접 찾을 수 없는 오브젝트는
패널티의 `OnDestroy()`에서 함께 제거한다.

```csharp
private GameObject detachedEffect;

private void OnDestroy()
{
    if (detachedEffect != null)
        Destroy(detachedEffect);
}
```

### 2.6 프리팹에는 호스트 시스템을 넣지 않는다

- 패널티 프리팹에 `Main Camera`, `EventSystem`, `MiniGamePlayer`,
  `FailurePenaltyController`를 포함하지 않는다.
- 패널티 프리팹은 연출과 지속 효과만 소유한다.
- 씬의 `MiniGamePlayer`에는 `FailurePenaltyController`가 하나만 존재해야 한다.

## 3. 새 패널티 추가 절차

1. `Assets/01_Scripts/` 아래 적절한 폴더에 `FailurePenalty` 파생 클래스를 만든다.
2. `Assets/03_Prefabs/FailurePenalties/`에 패널티 프리팹을 만든다.
3. 일회성 연출을 시작하고 기다리는 로직을 `Apply()`에 구현한다.
4. 다음 게임에도 유지할 오브젝트나 상태는 프리팹 인스턴스에 남긴다.
5. 대상 미니게임 프리팹의 `Failure Penalty Prefab` 필드에 연결한다.
6. 반드시 `MiniGamePlayer`를 통한 실제 게임 흐름에서 실패시켜 테스트한다.

## 4. 화면 연출 주의사항

### 4.1 UI 오버레이

- 영구적인 화면 텍스처는 `Screen Space Overlay` Canvas 아래의 `Image`로 구현할 수 있다.
- 입력을 막을 목적이 아니라면 `raycastTarget`, `interactable`, `blocksRaycasts`를 끈다.
- Sprite가 비어 있을 때 기본 흰 이미지가 화면을 덮지 않도록 방어한다.

### 4.2 일반 ParticleSystem

- 일반 `ParticleSystemRenderer`는 `Screen Space Overlay` Canvas 아래에서 보이지 않는다.
- 월드 카메라로 렌더링할 파티클은 Canvas 밖에 두거나 재생 시 월드 루트로 분리한다.
- 분리한 파티클은 패널티 제거 시 함께 정리해야 한다.
- 카메라 범위, Sorting Layer/Order, URP 호환 재질을 확인한다.

## 5. 현재 구현 예시

돌멘 실패 패널티는 다음 구조를 사용한다.

- `ScreenDirtFailurePenalty`
  - 먼지 바람 `ParticleSystem`을 먼저 재생한다.
  - 설정된 시간 후 렌즈 먼지 `CanvasGroup`을 페이드인한다.
  - 렌즈 먼지는 새 판을 시작할 때까지 화면에 남는다.
- `Assets/03_Prefabs/FailurePenalties/DolmenDustLensPenalty.prefab`
- `Assets/03_Prefabs/FailurePenalties/DustWindParticle.prefab`

## 6. 체크리스트

- [ ] 패널티 루트에 `FailurePenalty` 파생 컴포넌트가 있다.
- [ ] 대상 미니게임의 `Failure Penalty Prefab`에 연결했다.
- [ ] `Apply()`가 유한 시간 안에 종료된다.
- [ ] 최초 연출이 끝나기 전에 다음 미니게임이 시작되지 않는다.
- [ ] `Apply()` 종료 후 지속 효과가 다음 미니게임에서도 남는다.
- [ ] 여러 실패 패널티가 서로 제거되지 않고 누적된다.
- [ ] 성공 시 패널티가 생성되지 않는다.
- [ ] 패널티 미지정 또는 시각 참조 누락 시 예외가 발생하지 않는다.
- [ ] 새 판 시작 시 모든 패널티와 분리된 보조 오브젝트가 제거된다.
- [ ] `StopCurrent()`가 패널티 대기 중 호출되어도 이전 게임의 종료 콜백이 재진입하지 않는다.
- [ ] 프리팹에 `Main Camera`나 `EventSystem`이 없다.
- [ ] Unity 콘솔에 컴파일 오류와 런타임 예외가 없다.
