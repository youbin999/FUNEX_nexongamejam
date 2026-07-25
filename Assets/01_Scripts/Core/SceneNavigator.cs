using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 인스펙터에서 씬 이동을 걸기 위한 최소 헬퍼.
/// <see cref="SceneManager.LoadScene(string)"/> 는 정적 메서드라 UnityEvent 에 직접 연결할 수 없어서 둔다.
/// 버튼의 UnityEvent 에 <see cref="Load"/> 를 연결하고 씬 이름은 인스펙터에서 지정한다.
/// </summary>
public sealed class SceneNavigator : MonoBehaviour
{
    [Tooltip("이동할 씬 이름. Build Profiles 에 등록돼 있어야 한다")]
    [SerializeField] private string sceneName;

    /// <summary>인스펙터에 적어둔 씬으로 이동한다.</summary>
    public void Load()
    {
        Load(sceneName);
    }

    /// <summary>씬 이름을 직접 넘겨 이동한다.</summary>
    public void Load(string targetScene)
    {
        if (string.IsNullOrWhiteSpace(targetScene))
        {
            Debug.LogWarning("SceneNavigator: 씬 이름이 비어 있습니다.", this);
            return;
        }

        SceneManager.LoadScene(targetScene);
    }
}
