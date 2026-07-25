using UnityEngine;
using UnityEngine.Rendering;

namespace PS1Lofi
{
    /// <summary>
    /// 테스트 씬 전용 프리뷰 부트스트랩.
    /// 플레이 중에만 지정한 URP 파이프라인 에셋으로 교체했다가 종료 시 원복한다.
    /// QualitySettings.renderPipeline 런타임 오버라이드만 사용하므로
    /// 디스크의 전역 Graphics/Quality 설정 파일은 전혀 건드리지 않는다.
    /// (ExecuteAlways 를 쓰지 않으므로 에디트 모드에서는 아무 것도 바꾸지 않는다.)
    /// </summary>
    [AddComponentMenu("PS1 Lofi/PS1 Lofi Preview Bootstrap")]
    [DisallowMultipleComponent]
    public sealed class PS1LofiPreviewBootstrap : MonoBehaviour
    {
        [Tooltip("플레이 중 적용할 테스트 전용 URP 에셋")]
        public RenderPipelineAsset overrideAsset;

        RenderPipelineAsset _previous;
        bool _applied;

        void OnEnable()  => Apply();
        void OnDisable() => Restore();

        void Apply()
        {
            if (_applied || overrideAsset == null) return;
            _previous = QualitySettings.renderPipeline;
            QualitySettings.renderPipeline = overrideAsset;
            _applied = true;
        }

        void Restore()
        {
            if (!_applied) return;
            QualitySettings.renderPipeline = _previous;
            _applied = false;
        }
    }
}
