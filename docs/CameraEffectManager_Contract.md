# 카메라 이펙트 매니저 개발 계약 (CameraEffectManager Contract)

미니게임이나 패널티에서 카메라 셰이크/일렁임 연출이 필요할 때 지켜야 하는 규약이다.
구현체는 [`Assets/01_Scripts/Core/CameraEffectManager.cs`](../Assets/01_Scripts/Core/CameraEffectManager.cs).

## 1. 핵심 개념

- `CameraEffectManager`는 카메라에 셰이크/일렁임 오프셋을 얹어주는 **싱글턴 프레임워크**다.
- 여러 시스템(미니게임, 패널티)이 동시에 효과를 걸어도 오프셋을 전부 합산해 한 번에 적용한다.
- 활성 효과가 하나도 없으면 기준 포즈로 되돌린 뒤 카메라를 더 이상 건드리지 않는다.
  다른 시스템이 카메라를 자유롭게 옮길 수 있도록 하기 위함이다.
- 기준 포즈(`basePosition` / `baseRotation` / `baseOrthographicSize`)는 효과가 0개 → 1개로
  전환되는 순간의 카메라 상태를 다시 캡처한다. 즉 "카메라가 마지막으로 가만히 있던 자리"가 기준이다.
- 시간은 전부 `Time.unscaledTime` 기준이라 `Time.timeScale`이 0이어도 동작한다.

## 2. 접근 방법

```csharp
// 없으면 자동으로 GameObject를 만들어 준다.
CameraEffectManager.Instance.Shake(0.4f, 0.35f);

// 이미 있을 때만 준다. 정리 코드(OnDestroy 등)에서 새로 만들지 않으려고 쓴다.
CameraEffectManager.TryGetInstance?.StopWobble(handle);
```

- `Instance`는 씬에 없으면 `new GameObject("CameraEffectManager")`로 자동 생성한다.
  씬에 미리 배치해둘 필요는 없지만, 대상 카메라를 직접 지정하고 싶다면 배치 후
  `targetCamera` 필드를 채운다. 비워두면 `Camera.main`을 찾아 쓴다.
- `OnDestroy` 등 정리 코드에서는 **`Instance`가 아니라 `TryGetInstance`를 쓴다.**
  씬 전환/종료 중 매니저가 이미 파괴됐을 때 새로 만들어버리는 것을 막기 위함이다.

## 3. 반드시 지켜야 할 규칙

### 3.1 일회성 임팩트는 `Shake`, 지속 연출은 `StartWobble`/`StopWobble`을 쓴다

```csharp
// 일회성 감쇠 셰이크. 시간이 지날수록 진폭이 선형으로 줄어들고 자동으로 사라진다.
CameraEffectManager.Instance.Shake(duration: 0.4f, amplitude: 0.35f);

// 지속 일렁임. 핸들 id를 반드시 보관해뒀다가 필요할 때 꺼야 한다.
int handle = CameraEffectManager.Instance.StartWobble(
    positionAmplitude: 0.15f, rotationAmplitude: 2f, sizeAmplitude: 0.2f, frequency: 1.2f);

// 세기만 바꾸고 싶을 때 (강한 연출 → 약한 잔상 전환 등)
CameraEffectManager.Instance.SetWobbleStrength(handle, 0.3f);

// 더 이상 필요 없을 때 반드시 정지한다. 없는 id를 넘겨도 안전하게 무시된다.
CameraEffectManager.Instance.StopWobble(handle);
```

- `StartWobble`이 돌려주는 `id`를 잃어버리면 그 일렁임은 `StopAll()`이 호출되기 전까지 영원히 남는다.
- `Shake`는 핸들이 없다. 지속시간이 지나면 매니저가 알아서 목록에서 제거한다.

### 3.2 지속 효과의 수명은 호출자가 책임진다

- `FailurePenalty`처럼 인스턴스가 판 내내 유지되는 오브젝트에서 `StartWobble`을 썼다면,
  그 오브젝트의 `OnDestroy()`에서 반드시 `StopWobble(handle)`을 호출한다.
  ([`FailurePenalty_Contract.md`](FailurePenalty_Contract.md) 2.5 항목과 동일한 책임 소재)
- `OnDestroy`에서는 `TryGetInstance`를 써서, 매니저가 이미 사라진 뒤에도 새로 만들지 않는다.

```csharp
private int wobbleHandle;

private void OnDestroy()
{
    CameraEffectManager.TryGetInstance?.StopWobble(wobbleHandle);
}
```

### 3.3 새 판을 시작할 때는 `StopAll()`로 일괄 정리한다

- `StopAll()`은 모든 셰이크/일렁임을 제거하고 카메라를 기준 포즈로 즉시 되돌린다.
- `GameFlowController.StartFlow()` 등 판 전체를 리셋하는 지점에서 호출해,
  이전 판의 카메라 연출이 다음 판까지 새는 일이 없게 한다.
- 개별 패널티가 정리되며 각자 `StopWobble`을 호출하므로, 정상 흐름에서는 `StopAll()`이
  최후의 안전망 역할이다.

### 3.4 직교 카메라가 아니면 `sizeAmplitude`는 무시된다

- `orthographicSize` 맥동은 `cam.orthographic == true`일 때만 적용된다.
- 이 프로젝트의 호스트 카메라는 orthographic이므로 보통 문제없지만, 다른 카메라를
  `targetCamera`로 지정할 때는 확인한다.

### 3.5 매니저가 카메라를 옮기는 동안 다른 시스템이 같은 카메라를 직접 건드리지 않는다

- 효과가 1개 이상 걸려있는 동안, 매니저는 매 프레임 `LateUpdate`에서 카메라의
  `localPosition` / `localRotation` / `orthographicSize`를 기준값+오프셋으로 덮어쓴다.
- 이 구간에 다른 스크립트가 같은 값을 직접 대입하면 서로 밀어내며 떨림이 생길 수 있다.
  카메라를 계속 움직여야 하는 연출과 병행할 때는 순서(Script Execution Order)를 조정하거나
  둘 중 하나로 책임을 합친다.

## 4. 파라미터 감각

| 파라미터 | 단위 | 참고 |
| :---- | :---- | :---- |
| `Shake.amplitude` | 월드 단위(카메라 로컬 좌표) | orthographicSize가 작을수록 체감이 커진다 |
| `Shake.duration` | 초 | 지나면 자동 제거, 호출자가 정지시킬 필요 없음 |
| `StartWobble.positionAmplitude` | 월드 단위 | 위 amplitude와 동일 척도 |
| `StartWobble.rotationAmplitude` | 도(degree) | Z축 회전 |
| `StartWobble.sizeAmplitude` | orthographicSize 단위 | orthographic 카메라에서만 유효 |
| `StartWobble.frequency` | Hz(기준 주파수) | 내부에서 0.29~1배 사이 배수로 흩어 합성 |

시작/정지 시 `wobbleRampDuration`(기본 0.3초) 동안 강도가 부드럽게 오르내리므로 툭 끊기지 않는다.

## 5. 현재 구현 예시

- `PtsdWobbleFailurePenalty` ([Assets/01_Scripts/Mini_dig/PtsdWobbleFailurePenalty.cs](../Assets/01_Scripts/Mini_dig/PtsdWobbleFailurePenalty.cs))
  - `Apply()`에서 강한 `StartWobble` + `Shake` + 비네트 페이드인을 동시에 시작한다.
  - 일정 시간 후 `SetWobbleStrength`로 약하게 낮춰 판이 끝날 때까지 은은하게 유지한다.
  - `OnDestroy()`에서 `TryGetInstance?.StopWobble(handle)`로 정리한다.

## 6. 체크리스트

- [ ] `StartWobble`로 받은 핸들을 필드에 보관했다.
- [ ] 지속 효과를 건 오브젝트의 `OnDestroy()`에서 `TryGetInstance?.StopWobble(handle)`을 호출한다.
- [ ] 정리 코드에서 `Instance`가 아니라 `TryGetInstance`를 썼다(불필요한 재생성 방지).
- [ ] 새 판 시작 지점에서 `StopAll()`이 호출되는 흐름에 편입돼 있다(또는 개별 정리로 충분함을 확인했다).
- [ ] `targetCamera`를 비워뒀다면 `Camera.main`이 실제로 대상 카메라를 가리키는지 확인했다.
- [ ] orthographic 여부와 `sizeAmplitude` 사용 여부가 맞는지 확인했다.
