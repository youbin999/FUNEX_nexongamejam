using System;
using UnityEngine;

/// <summary>
/// 게임 설정값 저장소. PlayerPrefs 에 저장하고, 값이 바뀌면 이벤트로 알린다.
/// 어느 씬에서나 <c>GameSettings.BgmVolume</c> 처럼 바로 읽고 쓸 수 있다.
///
/// 볼륨은 실제 소리에 직접 관여하지 않고 값만 들고 있다. 소리에 반영하는 건
/// <see cref="AudioManager"/> 와 <see cref="VolumeTrackedSource"/> 가 이벤트를 받아서 한다.
/// </summary>
public static class GameSettings
{
    private const string BgmKey = "settings.bgmVolume";
    private const string SfxKey = "settings.sfxVolume";
    private const string FullscreenKey = "settings.fullscreen";
    private const string WidthKey = "settings.width";
    private const string HeightKey = "settings.height";

    /// <summary>BGM 볼륨(0~1)이 바뀌면 발화.</summary>
    public static event Action<float> BgmVolumeChanged;

    /// <summary>효과음 볼륨(0~1)이 바뀌면 발화.</summary>
    public static event Action<float> SfxVolumeChanged;

    /// <summary>전체화면 여부나 해상도가 바뀌면 발화.</summary>
    public static event Action DisplayChanged;

    private static float bgmVolume = 0.8f;
    private static float sfxVolume = 0.8f;
    private static bool fullscreen = true;
    private static Vector2Int resolution;
    private static bool loaded;


    // ── 설정 값 ──

    /// <summary>BGM 볼륨. 0이 무음, 1이 최대.</summary>
    public static float BgmVolume
    {
        get
        {
            EnsureLoaded();
            return bgmVolume;
        }
        set
        {
            EnsureLoaded();

            float clamped = Mathf.Clamp01(value);
            if (Mathf.Approximately(bgmVolume, clamped))
                return;

            bgmVolume = clamped;
            PlayerPrefs.SetFloat(BgmKey, clamped);
            BgmVolumeChanged?.Invoke(clamped);
        }
    }

    /// <summary>효과음 볼륨. 0이 무음, 1이 최대.</summary>
    public static float SfxVolume
    {
        get
        {
            EnsureLoaded();
            return sfxVolume;
        }
        set
        {
            EnsureLoaded();

            float clamped = Mathf.Clamp01(value);
            if (Mathf.Approximately(sfxVolume, clamped))
                return;

            sfxVolume = clamped;
            PlayerPrefs.SetFloat(SfxKey, clamped);
            SfxVolumeChanged?.Invoke(clamped);
        }
    }

    /// <summary>true 면 전체화면, false 면 창모드.</summary>
    public static bool Fullscreen
    {
        get
        {
            EnsureLoaded();
            return fullscreen;
        }
        set
        {
            EnsureLoaded();

            if (fullscreen == value)
                return;

            fullscreen = value;
            PlayerPrefs.SetInt(FullscreenKey, value ? 1 : 0);
            ApplyDisplay();
            DisplayChanged?.Invoke();
        }
    }

    /// <summary>가로 x 세로 해상도.</summary>
    public static Vector2Int Resolution
    {
        get
        {
            EnsureLoaded();
            return resolution;
        }
        set
        {
            EnsureLoaded();

            if (value.x <= 0 || value.y <= 0 || resolution == value)
                return;

            resolution = value;
            PlayerPrefs.SetInt(WidthKey, value.x);
            PlayerPrefs.SetInt(HeightKey, value.y);
            ApplyDisplay();
            DisplayChanged?.Invoke();
        }
    }


    // ── 저장과 적용 ──

    /// <summary>저장된 값을 아직 안 읽었으면 읽는다.</summary>
    public static void EnsureLoaded()
    {
        if (!loaded)
            Load();
    }

    /// <summary>PlayerPrefs 에서 값을 다시 읽는다. 저장된 게 없으면 지금 화면 설정을 기본값으로 쓴다.</summary>
    public static void Load()
    {
        loaded = true;

        bgmVolume = PlayerPrefs.GetFloat(BgmKey, 0.8f);
        sfxVolume = PlayerPrefs.GetFloat(SfxKey, 0.8f);
        fullscreen = PlayerPrefs.GetInt(FullscreenKey, Screen.fullScreen ? 1 : 0) == 1;

        int width = PlayerPrefs.GetInt(WidthKey, 0);
        int height = PlayerPrefs.GetInt(HeightKey, 0);
        resolution = width > 0 && height > 0
            ? new Vector2Int(width, height)
            : new Vector2Int(Screen.width, Screen.height);
    }

    /// <summary>디스크에 실제로 기록한다. 설정 창을 닫을 때 한 번 불러주면 된다.</summary>
    public static void Save()
    {
        PlayerPrefs.Save();
    }

    /// <summary>전체화면/해상도를 지금 화면에 적용한다. (에디터에서는 무시된다)</summary>
    public static void ApplyDisplay()
    {
        EnsureLoaded();

        FullScreenMode mode = fullscreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed;
        if (Screen.width == resolution.x && Screen.height == resolution.y && Screen.fullScreenMode == mode)
            return;

        Screen.SetResolution(resolution.x, resolution.y, mode);
    }

    /// <summary>전부 기본값으로 되돌린다.</summary>
    public static void ResetToDefaults()
    {
        BgmVolume = 0.8f;
        SfxVolume = 0.8f;
        Fullscreen = true;
        Resolution = new Vector2Int(1920, 1080);
        Save();
    }
}
