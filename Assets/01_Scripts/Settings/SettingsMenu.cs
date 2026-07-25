using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// ESC 로 여닫는 설정 창. BGM/효과음 게이지, 창모드 토글, 해상도 변경을 다룬다.
/// 값은 <see cref="GameSettings"/> 에 저장되므로 씬을 옮겨도, 게임을 껐다 켜도 유지된다.
///
/// 프리팹으로 만들어 Resources 에 넣어두면 <see cref="SettingsBootstrap"/> 이
/// 모든 씬에서 자동으로 하나 띄워준다. 씬에 직접 놓아도 동작한다.
/// 참조는 비어 있어도 되므로 필요한 것만 연결하면 된다.
///
/// 타이틀 씬에 놓아둔 창이 플레이 씬까지 그대로 따라오게 하려면
/// <see cref="dontDestroyOnLoad"/> 를 켜둔다. 다른 오브젝트의 자식으로 놓여 있어도
/// <see cref="MakePersistent"/> 가 루트로 떼어내므로 그대로 살아남는다.
/// </summary>
/// <remarks>
/// ESC 를 쓰는 다른 UI(<c>GalleryScreen</c> 등)보다 먼저 입력을 처리해야 하므로
/// 실행 순서를 앞으로 당겨 둔다. 다른 스크립트는 <see cref="EscapeConsumedThisFrame"/> 로
/// 이 프레임의 ESC 가 설정 창에 먹혔는지 확인하면 된다.
/// </remarks>
[DisallowMultipleComponent]
[DefaultExecutionOrder(-100)]
public class SettingsMenu : MonoBehaviour
{
    private static SettingsMenu instance;

    [Header("창")]
    [Tooltip("켜고 끌 창 루트. 비워두면 이 오브젝트를 직접 켜고 끈다")]
    [SerializeField] private GameObject panel;

    [Tooltip("ESC 로 열고 닫는다")]
    [SerializeField] private bool toggleWithEscape = true;

    [Tooltip("글자 입력 칸에 포커스가 있는 동안에는 ESC 를 무시한다 (엔딩의 월드 이름 입력 등)")]
    [SerializeField] private bool ignoreEscapeWhileTyping = true;

    [Tooltip("창이 열려 있는 동안 게임을 멈춘다 (Time.timeScale = 0)")]
    [SerializeField] private bool pauseWhileOpen = true;

    [Tooltip("씬이 바뀌어도 유지한다. 프리팹 하나로 모든 씬에서 쓸 때 켠다")]
    [SerializeField] private bool dontDestroyOnLoad = true;

    [Tooltip("설정 창이 다른 UI 위에 그려지도록 Canvas 의 Sorting Order 를 이 값으로 올린다. 0 이하면 건드리지 않는다")]
    [SerializeField] private int canvasSortingOrder = 1000;

    [Header("소리")]
    [SerializeField] private Slider bgmSlider;
    [SerializeField] private Slider sfxSlider;

    [Tooltip("게이지 옆에 퍼센트를 보여줄 텍스트 (없어도 된다)")]
    [SerializeField] private TMP_Text bgmValueLabel;
    [SerializeField] private TMP_Text sfxValueLabel;

    [Header("화면")]
    [Tooltip("켜면 창모드, 끄면 전체화면")]
    [SerializeField] private Toggle windowedToggle;

    [SerializeField] private TMP_Dropdown resolutionDropdown;

    [Header("버튼")]
    [SerializeField] private Button closeButton;

    [Tooltip("기본값으로 되돌리는 버튼 (없어도 된다)")]
    [SerializeField] private Button resetButton;

    [Header("이벤트")]
    public UnityEvent onOpened;
    public UnityEvent onClosed;

    private readonly List<Vector2Int> resolutions = new List<Vector2Int>();
    private float savedTimeScale = 1f;
    private bool isOpen;
    private int escapeFrame = -1;

    // 씬에 놓인 창이 이미 따라온 창과 겹칠 때, 이쪽은 중계기가 되어 저쪽에 일을 시킨다.
    private SettingsMenu proxyTarget;

    /// <summary>지금 살아 있는 설정 창. 없으면 null.</summary>
    public static SettingsMenu Instance => instance;

    /// <summary>설정 창이 열려 있는지 여부. 다른 스크립트에서 입력을 막을 때 쓰면 된다.</summary>
    public bool IsOpen => proxyTarget != null ? proxyTarget.IsOpen : isOpen;

    /// <summary>설정 창이 살아 있고 열려 있는지. 씬 어디서나 안전하게 물어볼 수 있다.</summary>
    public static bool IsAnyOpen => instance != null && instance.isOpen;

    /// <summary>
    /// 이 프레임의 ESC 를 설정 창이 이미 먹었는지 여부.
    /// ESC 를 쓰는 다른 UI 는 이 값이 true 면 그냥 넘겨야 한다 — 설정 창을 ESC 로 닫는 순간
    /// <see cref="IsOpen"/> 은 이미 false 이므로 그것만 보고 판단하면 입력이 두 번 먹힌다.
    /// </summary>
    public static bool EscapeConsumedThisFrame =>
        instance != null && instance.escapeFrame == Time.frameCount;

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            // 타이틀로 되돌아온 경우다. 이미 따라온 창이 있으니 이쪽은 창 노릇을 하지 않는다.
            // 다만 오브젝트를 지워 버리면 타이틀 씬의 SETTING 버튼이 죽은 컴포넌트를 가리키게 돼
            // 버튼이 먹통이 된다(OnClick 은 이 컴포넌트를 직접 참조한다).
            // 그래서 지우는 대신 중계기로 남겨 클릭을 살아 있는 창으로 넘긴다.
            proxyTarget = instance;
            SetOpen(false, notify: false);
            return;
        }

        instance = this;

        if (dontDestroyOnLoad)
            MakePersistent();

        ApplyCanvasSortingOrder();

        GameSettings.EnsureLoaded();
        BuildResolutions();
        BindControls();
        RefreshControls();

        SetOpen(false, notify: false);
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnDestroy()
    {
        if (instance != this)
            return;

        instance = null;

        // 창을 연 채로 씬이 날아가면 게임이 멈춘 상태로 남는다.
        if (isOpen && pauseWhileOpen)
            Time.timeScale = savedTimeScale;
    }

    /// <summary>
    /// 씬이 바뀌면 창을 닫는다. 창을 연 채로 화면이 넘어가면(갤러리에서 타이틀로 돌아가기 등)
    /// 새 씬이 <c>Time.timeScale = 0</c> 인 채로 시작해서 멈춘 것처럼 보인다.
    /// </summary>
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (instance != this || !isOpen)
            return;

        Close();
    }

    private void Update()
    {
        // 중계기는 입력을 보지 않는다. 두 쪽이 다 ESC 를 먹으면 열자마자 다시 닫힌다.
        if (proxyTarget != null || !toggleWithEscape)
            return;

        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
            return;

        if (!keyboard.escapeKey.wasPressedThisFrame)
            return;

        // 이름을 치고 있는 중이면 ESC 는 입력 칸 몫이다. 창까지 열리면 글자는 날아가는데
        // 설정 창만 뜨는 꼴이 된다. 칸에서 포커스가 빠진 다음부터 ESC 가 창을 연다.
        if (ignoreEscapeWhileTyping && IsTypingInInputField())
            return;

        escapeFrame = Time.frameCount;
        Toggle();
    }

    /// <summary>
    /// 지금 글자를 입력하는 칸에 포커스가 있는지.
    /// 엔딩의 월드 이름 입력처럼 ESC 를 입력 칸이 먼저 써야 하는 상황을 걸러낸다.
    /// 이 컴포넌트가 EventSystem 보다 먼저 도는 덕에(<c>DefaultExecutionOrder(-100)</c>)
    /// 입력 칸이 ESC 로 포커스를 놓기 전에 확인할 수 있다.
    /// </summary>
    private static bool IsTypingInInputField()
    {
        EventSystem events = EventSystem.current;
        GameObject selected = events != null ? events.currentSelectedGameObject : null;
        if (selected == null)
            return false;

        var tmpField = selected.GetComponent<TMP_InputField>();
        if (tmpField != null && tmpField.isFocused)
            return true;

        var uiField = selected.GetComponent<InputField>();
        return uiField != null && uiField.isFocused;
    }

    /// <summary>
    /// 씬이 바뀌어도 살아남게 만든다.
    /// <c>DontDestroyOnLoad</c> 는 루트 오브젝트에만 통하므로(자식에 걸면 경고만 뜨고
    /// 씬과 함께 사라진다) 부모가 있으면 먼저 루트로 떼어낸다.
    /// 타이틀 씬의 <c>Setting_manager</c> 밑에 놓인 <c>Setting_Canvas</c> 가 이 경우다.
    /// </summary>
    private void MakePersistent()
    {
        if (transform.parent != null)
            transform.SetParent(null, worldPositionStays: true);

        DontDestroyOnLoad(gameObject);
    }

    /// <summary>
    /// 설정 창 Canvas 를 다른 UI 위로 올린다.
    /// 플레이 씬의 공용 UI Canvas 는 Sorting Order 가 100 이라 기본값(1)으로는 가려진다.
    /// </summary>
    private void ApplyCanvasSortingOrder()
    {
        if (canvasSortingOrder <= 0)
            return;

        var canvas = GetComponentInParent<Canvas>();
        if (canvas == null)
            return;

        // 중첩 Canvas 라면 스스로 정렬을 결정하게 만들어야 값이 먹는다.
        if (!canvas.isRootCanvas)
            canvas.overrideSorting = true;

        canvas.sortingOrder = canvasSortingOrder;
    }

    /// <summary>설정 창을 연다.</summary>
    public void Open()
    {
        if (proxyTarget != null)
        {
            proxyTarget.Open();
            return;
        }

        if (isOpen)
            return;

        // 다른 데서 값을 바꿨을 수도 있으니 열 때마다 현재 값으로 맞춘다.
        RefreshControls();
        SetOpen(true, notify: true);
    }

    /// <summary>설정 창을 닫고 값을 디스크에 저장한다.</summary>
    public void Close()
    {
        if (proxyTarget != null)
        {
            proxyTarget.Close();
            return;
        }

        if (!isOpen)
            return;

        SetOpen(false, notify: true);
        GameSettings.Save();
    }

    /// <summary>열려 있으면 닫고, 닫혀 있으면 연다. ESC 와 같은 동작.</summary>
    public void Toggle()
    {
        if (proxyTarget != null)
        {
            proxyTarget.Toggle();
            return;
        }

        if (isOpen)
            Close();
        else
            Open();
    }

    /// <summary>버튼으로 창모드를 켜고 끄고 싶을 때 OnClick 에 연결한다.</summary>
    public void ToggleWindowed()
    {
        if (proxyTarget != null)
        {
            proxyTarget.ToggleWindowed();
            return;
        }

        GameSettings.Fullscreen = !GameSettings.Fullscreen;
        RefreshControls();
    }

    /// <summary>모든 설정을 기본값으로 되돌린다.</summary>
    public void ResetToDefaults()
    {
        if (proxyTarget != null)
        {
            proxyTarget.ResetToDefaults();
            return;
        }

        GameSettings.ResetToDefaults();
        BuildResolutions();
        RefreshControls();
    }

    private void SetOpen(bool value, bool notify)
    {
        // 처음 창을 숨길 때는 상태가 안 바뀐 것이므로 timeScale 을 건드리면 안 된다.
        // (다른 곳에서 이미 멈춰둔 게임을 풀어버릴 수 있다)
        bool changed = isOpen != value;
        isOpen = value;

        GameObject target = panel != null ? panel : gameObject;
        // 창 루트가 이 오브젝트면 꺼버릴 수 없다. Update 가 멈춰서 ESC 를 못 받는다.
        if (target == gameObject && panel == null)
        {
            foreach (Transform child in transform)
                child.gameObject.SetActive(value);
        }
        else
        {
            target.SetActive(value);
        }

        if (pauseWhileOpen && changed)
        {
            if (value)
            {
                savedTimeScale = Time.timeScale;
                Time.timeScale = 0f;
            }
            else
            {
                Time.timeScale = savedTimeScale;
            }
        }

        if (!notify)
            return;

        if (value)
            onOpened.Invoke();
        else
            onClosed.Invoke();
    }

    private void BindControls()
    {
        if (bgmSlider != null)
        {
            bgmSlider.minValue = 0f;
            bgmSlider.maxValue = 1f;
            bgmSlider.onValueChanged.AddListener(OnBgmSliderChanged);
        }

        if (sfxSlider != null)
        {
            sfxSlider.minValue = 0f;
            sfxSlider.maxValue = 1f;
            sfxSlider.onValueChanged.AddListener(OnSfxSliderChanged);
        }

        if (windowedToggle != null)
            windowedToggle.onValueChanged.AddListener(OnWindowedToggled);

        if (resolutionDropdown != null)
            resolutionDropdown.onValueChanged.AddListener(OnResolutionSelected);

        if (closeButton != null)
            closeButton.onClick.AddListener(Close);

        if (resetButton != null)
            resetButton.onClick.AddListener(ResetToDefaults);
    }

    /// <summary>설정값을 UI 에 그대로 옮긴다. 콜백이 다시 돌지 않도록 알림 없이 넣는다.</summary>
    private void RefreshControls()
    {
        if (bgmSlider != null)
            bgmSlider.SetValueWithoutNotify(GameSettings.BgmVolume);

        if (sfxSlider != null)
            sfxSlider.SetValueWithoutNotify(GameSettings.SfxVolume);

        UpdateLabel(bgmValueLabel, GameSettings.BgmVolume);
        UpdateLabel(sfxValueLabel, GameSettings.SfxVolume);

        if (windowedToggle != null)
            windowedToggle.SetIsOnWithoutNotify(!GameSettings.Fullscreen);

        if (resolutionDropdown != null)
        {
            int index = resolutions.IndexOf(GameSettings.Resolution);
            resolutionDropdown.SetValueWithoutNotify(Mathf.Max(index, 0));
            resolutionDropdown.RefreshShownValue();
        }
    }

    private void OnBgmSliderChanged(float value)
    {
        GameSettings.BgmVolume = value;
        UpdateLabel(bgmValueLabel, value);
    }

    private void OnSfxSliderChanged(float value)
    {
        GameSettings.SfxVolume = value;
        UpdateLabel(sfxValueLabel, value);
    }

    private void OnWindowedToggled(bool windowed)
    {
        GameSettings.Fullscreen = !windowed;
    }

    private void OnResolutionSelected(int index)
    {
        if (index < 0 || index >= resolutions.Count)
            return;

        GameSettings.Resolution = resolutions[index];
    }

    private static void UpdateLabel(TMP_Text label, float value)
    {
        if (label != null)
            label.text = $"{Mathf.RoundToInt(value * 100f)}%";
    }

    /// <summary>모니터가 지원하는 해상도를 중복 없이 모아 드롭다운을 채운다.</summary>
    private void BuildResolutions()
    {
        resolutions.Clear();

        foreach (Resolution option in Screen.resolutions)
        {
            var size = new Vector2Int(option.width, option.height);
            if (!resolutions.Contains(size))
                resolutions.Add(size);
        }

        // 에디터처럼 목록이 비는 환경을 대비한 기본값.
        if (resolutions.Count == 0)
        {
            resolutions.Add(new Vector2Int(1280, 720));
            resolutions.Add(new Vector2Int(1600, 900));
            resolutions.Add(new Vector2Int(1920, 1080));
            resolutions.Add(new Vector2Int(2560, 1440));
        }

        // 저장된 해상도가 목록에 없으면(모니터를 바꾼 경우 등) 같이 넣어준다.
        Vector2Int current = GameSettings.Resolution;
        if (!resolutions.Contains(current))
            resolutions.Add(current);

        resolutions.Sort((a, b) => a.x != b.x ? a.x.CompareTo(b.x) : a.y.CompareTo(b.y));

        if (resolutionDropdown == null)
            return;

        var labels = new List<string>(resolutions.Count);
        foreach (Vector2Int size in resolutions)
            labels.Add($"{size.x} x {size.y}");

        resolutionDropdown.ClearOptions();
        resolutionDropdown.AddOptions(labels);
    }
}
