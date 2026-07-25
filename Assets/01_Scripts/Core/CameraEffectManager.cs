using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 카메라에 셰이크/일렁임 같은 임팩트 연출을 얹어주는 싱글턴 매니저.
/// 여러 연출이 동시에 걸려도 오프셋을 전부 합산해 한 번에 적용하며,
/// 활성 효과가 하나도 없으면 기준 포즈로 되돌린 뒤 카메라를 건드리지 않는다.
/// (다른 시스템이 카메라를 자유롭게 움직일 수 있게 하기 위함)
///
/// 시간은 전부 <see cref="Time.unscaledTime"/> 기준이라 <see cref="Time.timeScale"/>이 0이어도 동작한다.
/// </summary>
[DisallowMultipleComponent]
public class CameraEffectManager : MonoBehaviour
{
    private static CameraEffectManager instance;

    [Header("대상 카메라")]
    [Tooltip("비워두면 Camera.main 을 찾아 쓴다")]
    [SerializeField] private Camera targetCamera;

    [Header("일렁임 램프")]
    [Tooltip("일렁임이 시작/정지할 때 툭 튀지 않도록 강도를 올리고 내리는 시간(초)")]
    [SerializeField] private float wobbleRampDuration = 0.3f;

    private Camera cachedCamera;

    private readonly List<ShakeEffect> shakes = new List<ShakeEffect>();
    private readonly List<WobbleEffect> wobbles = new List<WobbleEffect>();

    private bool hasBasePose;
    private Vector3 basePosition;
    private Quaternion baseRotation;
    private float baseOrthographicSize;

    private int nextWobbleId = 1;
    private bool poseDirty;

    /// <summary>없으면 자동으로 만들어 주는 인스턴스 접근자.</summary>
    public static CameraEffectManager Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindAnyObjectByType<CameraEffectManager>();
                if (instance == null)
                {
                    var go = new GameObject("CameraEffectManager");
                    instance = go.AddComponent<CameraEffectManager>();
                }
            }

            return instance;
        }
    }

    /// <summary>이미 있을 때만 준다. 정리 코드(OnDestroy 등)에서 새로 만들지 않으려고 쓴다.</summary>
    public static CameraEffectManager TryGetInstance => instance;

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(this);
            return;
        }

        instance = this;
    }

    private void OnDestroy()
    {
        if (instance == this)
            instance = null;
    }

    /// <summary>일회성 감쇠 셰이크. 시간이 지날수록 진폭이 선형으로 줄어든다.</summary>
    public void Shake(float duration, float amplitude)
    {
        if (duration <= 0f || amplitude <= 0f)
            return;

        EnsureBasePose();
        shakes.Add(new ShakeEffect
        {
            startTime = Time.unscaledTime,
            duration = duration,
            amplitude = amplitude,
            seed = Random.Range(0f, 1000f),
        });
    }

    /// <summary>
    /// 지속 일렁임을 시작하고 핸들 id를 돌려준다.
    /// 사인파를 서로 다른 위상/주파수로 합성해 취한 듯한 흔들림을 만든다.
    /// </summary>
    /// <param name="positionAmplitude">위치 흔들림 크기(월드 단위)</param>
    /// <param name="rotationAmplitude">Z축 회전 흔들림 크기(도)</param>
    /// <param name="sizeAmplitude">직교 카메라 크기 맥동 폭</param>
    /// <param name="frequency">기준 주파수(Hz)</param>
    public int StartWobble(float positionAmplitude, float rotationAmplitude, float sizeAmplitude, float frequency)
    {
        EnsureBasePose();

        int id = nextWobbleId++;
        wobbles.Add(new WobbleEffect
        {
            id = id,
            positionAmplitude = positionAmplitude,
            rotationAmplitude = rotationAmplitude,
            sizeAmplitude = sizeAmplitude,
            frequency = Mathf.Max(0.01f, frequency),
            strength = 1f,
            startTime = Time.unscaledTime,
            phase = Random.Range(0f, 100f),
        });

        return id;
    }

    /// <summary>일렁임 세기 배율을 바꾼다. 강한 연출 → 약한 잔상으로 전환할 때 쓴다.</summary>
    public void SetWobbleStrength(int id, float multiplier)
    {
        for (int i = 0; i < wobbles.Count; i++)
        {
            if (wobbles[i].id != id)
                continue;

            WobbleEffect effect = wobbles[i];
            effect.strength = Mathf.Max(0f, multiplier);
            wobbles[i] = effect;
            return;
        }
    }

    /// <summary>해당 일렁임을 제거한다. 없는 id는 무시한다.</summary>
    public void StopWobble(int id)
    {
        for (int i = 0; i < wobbles.Count; i++)
        {
            if (wobbles[i].id != id)
                continue;

            wobbles.RemoveAt(i);
            return;
        }
    }

    /// <summary>모든 효과를 제거하고 즉시 기준 포즈로 되돌린다.</summary>
    public void StopAll()
    {
        shakes.Clear();
        wobbles.Clear();
        RestoreBasePose();
        hasBasePose = false;
    }

    private void LateUpdate()
    {
        Camera cam = ResolveCamera();
        if (cam == null)
            return;

        PruneFinishedShakes();

        if (shakes.Count == 0 && wobbles.Count == 0)
        {
            if (poseDirty)
            {
                RestoreBasePose();
                // 기준 포즈를 다시 잡을 수 있게 비워둔다. 다음 효과가 시작될 때 현재 포즈를 새로 캡처한다.
                hasBasePose = false;
            }

            return;
        }

        Vector3 positionOffset = Vector3.zero;
        float rotationOffset = 0f;
        float sizeOffset = 0f;

        float now = Time.unscaledTime;

        for (int i = 0; i < shakes.Count; i++)
        {
            ShakeEffect shake = shakes[i];
            float t = Mathf.Clamp01((now - shake.startTime) / shake.duration);
            float decay = 1f - t;
            float strength = shake.amplitude * decay;

            // 노이즈 대신 시드별 사인 합성으로 빠르게 튀는 진동을 만든다.
            positionOffset.x += Mathf.Sin((now + shake.seed) * 47f) * strength;
            positionOffset.y += Mathf.Cos((now + shake.seed) * 53f) * strength;
        }

        for (int i = 0; i < wobbles.Count; i++)
        {
            WobbleEffect wobble = wobbles[i];
            float strength = wobble.strength * RampFactor(now - wobble.startTime);
            if (strength <= 0f)
                continue;

            float t = (now - wobble.startTime + wobble.phase) * wobble.frequency;
            float tau = Mathf.PI * 2f;

            positionOffset.x += Mathf.Sin(t * tau) * wobble.positionAmplitude * strength;
            positionOffset.y += Mathf.Sin(t * tau * 0.73f + 1.3f) * wobble.positionAmplitude * 0.7f * strength;
            rotationOffset += Mathf.Sin(t * tau * 0.41f + 2.1f) * wobble.rotationAmplitude * strength;
            sizeOffset += Mathf.Sin(t * tau * 0.29f + 0.7f) * wobble.sizeAmplitude * strength;
        }

        Transform camTransform = cam.transform;
        camTransform.localPosition = basePosition + positionOffset;
        camTransform.localRotation = baseRotation * Quaternion.Euler(0f, 0f, rotationOffset);

        if (cam.orthographic)
            cam.orthographicSize = Mathf.Max(0.01f, baseOrthographicSize + sizeOffset);

        poseDirty = true;
    }

    /// <summary>시작 직후 강도를 0에서 1로 부드럽게 끌어올린다.</summary>
    private float RampFactor(float elapsed)
    {
        float ramp = Mathf.Max(0f, wobbleRampDuration);
        if (ramp <= 0f)
            return 1f;

        float t = Mathf.Clamp01(elapsed / ramp);
        return t * t * (3f - 2f * t);
    }

    private void PruneFinishedShakes()
    {
        float now = Time.unscaledTime;
        for (int i = shakes.Count - 1; i >= 0; i--)
        {
            if (now - shakes[i].startTime >= shakes[i].duration)
                shakes.RemoveAt(i);
        }
    }

    /// <summary>효과가 없다가 새로 생기는 순간의 카메라 포즈를 기준으로 삼는다.</summary>
    private void EnsureBasePose()
    {
        if (hasBasePose)
            return;

        Camera cam = ResolveCamera();
        if (cam == null)
            return;

        Transform camTransform = cam.transform;
        basePosition = camTransform.localPosition;
        baseRotation = camTransform.localRotation;
        baseOrthographicSize = cam.orthographicSize;
        hasBasePose = true;
        poseDirty = false;
    }

    private void RestoreBasePose()
    {
        poseDirty = false;

        if (!hasBasePose)
            return;

        Camera cam = ResolveCamera();
        if (cam == null)
            return;

        Transform camTransform = cam.transform;
        camTransform.localPosition = basePosition;
        camTransform.localRotation = baseRotation;

        if (cam.orthographic)
            cam.orthographicSize = baseOrthographicSize;
    }

    private Camera ResolveCamera()
    {
        if (targetCamera != null)
            return targetCamera;

        if (cachedCamera == null)
            cachedCamera = Camera.main;

        return cachedCamera;
    }

    private struct ShakeEffect
    {
        public float startTime;
        public float duration;
        public float amplitude;
        public float seed;
    }

    private struct WobbleEffect
    {
        public int id;
        public float positionAmplitude;
        public float rotationAmplitude;
        public float sizeAmplitude;
        public float frequency;
        public float strength;
        public float startTime;
        public float phase;
    }
}
