using UnityEngine;

/// <summary>
/// 게임이 시작될 때 저장된 설정을 읽어 화면에 적용하고, 설정 창을 한 번 띄워둔다.
/// 어느 씬에서 플레이를 눌러도 ESC 로 설정 창을 열 수 있게 하려고 쓴다.
///
/// <see cref="menuResourcePath"/> 경로에 SettingsMenu 프리팹이 있으면 그걸 쓰고,
/// 없으면 설정값만 적용하고 조용히 넘어간다(씬에 직접 놓아둔 경우).
/// </summary>
public static class SettingsBootstrap
{
    // Resources 폴더 기준 경로. Assets/Resources/UI/SettingsMenu.prefab
    private const string menuResourcePath = "UI/SettingsMenu";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Initialize()
    {
        GameSettings.Load();
        GameSettings.ApplyDisplay();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void SpawnMenu()
    {
        // 씬에 이미 놓여 있으면 그걸 쓴다.
        if (SettingsMenu.Instance != null)
            return;

        var prefab = Resources.Load<SettingsMenu>(menuResourcePath);
        if (prefab == null)
            return;

        SettingsMenu menu = Object.Instantiate(prefab);
        menu.name = prefab.name;
    }
}
