using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class SettingsManager : MonoBehaviour
{
    private const string FloatingJoystickPrefKey = "FloatingJoystick";

    [SerializeField] private Canvas settingsCanvas;
    [SerializeField] private Slider musicVolumeSlider;
    [SerializeField] private Slider effectsVolumeSlider;
    [SerializeField] private Toggle floatingJoystickToggle;
    [Space]
    [SerializeField] private UnityEvent onClose;

    private void Awake()
    {
        if (settingsCanvas == null)
            settingsCanvas = GetComponent<Canvas>();

        EnsureJoystickToggle();
    }

    private void Start()
    {
        if (floatingJoystickToggle != null)
        {
            floatingJoystickToggle.onValueChanged.AddListener(OnFloatingJoystickToggleChange);
        }
    }

    public void OnMusicVolumeChange()
    {
        if (AudioManager.Instance != null && musicVolumeSlider != null)
            AudioManager.Instance.musicVolumeChanged(musicVolumeSlider.value);
    }
    
    public void OnEffectsVolumeChange()
    {
        if (AudioManager.Instance != null && effectsVolumeSlider != null)
            AudioManager.Instance.effectVolumeChanged(effectsVolumeSlider.value);
    }

    public void OnFloatingJoystickToggleChange(bool isFloating)
    {
        PlayerPrefs.SetInt(FloatingJoystickPrefKey, isFloating ? 1 : 0);
        PlayerPrefs.Save();

        UpdateActiveJoystick(isFloating);
    }

    private void UpdateActiveJoystick(bool isFloating)
    {
        var joystick = FindFirstObjectByType<VirtualJoystick>(FindObjectsInactive.Include);
        if (joystick != null)
        {
            joystick.SetJoystickType(isFloating ? VirtualJoystick.JoystickType.Floating : VirtualJoystick.JoystickType.Fixed);
        }
    }

    private void SetVolumeSliderValues()
    {
        if (musicVolumeSlider != null)
            musicVolumeSlider.value = PlayerPrefs.GetFloat("MusicVolume", 1f);

        if (effectsVolumeSlider != null)
            effectsVolumeSlider.value = PlayerPrefs.GetFloat("EffectsVolume", 1f);

        if (floatingJoystickToggle != null)
        {
            floatingJoystickToggle.SetIsOnWithoutNotify(PlayerPrefs.GetInt(FloatingJoystickPrefKey, 0) == 1);
        }
    }
    
    public void OpenMenu()
    {
        SetVolumeSliderValues();
        settingsCanvas.enabled = true;
    }

    public void CloseMenu()
    {
        settingsCanvas.enabled = false;
        onClose?.Invoke();
    }

    private void EnsureJoystickToggle()
    {
        if (floatingJoystickToggle != null) return;

        floatingJoystickToggle = GetComponentInChildren<Toggle>(true);
        if (floatingJoystickToggle != null) return;

        Transform targetParent = FindTargetParentForToggle();
        if (targetParent == null) return;

        floatingJoystickToggle = CreateToggleUI(targetParent);
    }

    private Transform FindTargetParentForToggle()
    {
        foreach (var t in GetComponentsInChildren<Transform>(true))
        {
            if (t.name == "Options") return t;
        }

        foreach (var t in GetComponentsInChildren<Transform>(true))
        {
            if (t.name == "Panel") return t;
        }

        return transform;
    }

    private Toggle CreateToggleUI(Transform parent)
    {
        GameObject toggleObj = new GameObject("FloatingJoystickToggle", typeof(RectTransform), typeof(Toggle));
        toggleObj.transform.SetParent(parent, false);
        toggleObj.layer = 5;

        RectTransform toggleRect = toggleObj.GetComponent<RectTransform>();
        
        bool isOptionsContainer = parent.name == "Options";
        if (isOptionsContainer)
        {
            toggleRect.sizeDelta = new Vector2(600, 60);
        }
        else
        {
            toggleRect.anchorMin = new Vector2(0.5f, 0.5f);
            toggleRect.anchorMax = new Vector2(0.5f, 0.5f);
            toggleRect.anchoredPosition = new Vector2(0, -380);
            toggleRect.sizeDelta = new Vector2(500, 60);
        }

        HorizontalLayoutGroup hlg = toggleObj.AddComponent<HorizontalLayoutGroup>();
        hlg.childAlignment = TextAnchor.MiddleCenter;
        hlg.spacing = 20;
        hlg.childControlWidth = false;
        hlg.childControlHeight = false;
        hlg.childForceExpandWidth = false;
        hlg.childForceExpandHeight = false;

        // Background box
        GameObject bgObj = new GameObject("Background", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        bgObj.transform.SetParent(toggleObj.transform, false);
        bgObj.layer = 5;

        RectTransform bgRect = bgObj.GetComponent<RectTransform>();
        bgRect.sizeDelta = new Vector2(40, 40);

        Image bgImage = bgObj.GetComponent<Image>();
        bgImage.color = new Color(0.2f, 0.2f, 0.2f, 1f);

        // Checkmark inside Background
        GameObject checkObj = new GameObject("Checkmark", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        checkObj.transform.SetParent(bgObj.transform, false);
        checkObj.layer = 5;

        RectTransform checkRect = checkObj.GetComponent<RectTransform>();
        checkRect.anchorMin = Vector2.zero;
        checkRect.anchorMax = Vector2.one;
        checkRect.sizeDelta = new Vector2(-10, -10);
        checkRect.anchoredPosition = Vector2.zero;

        Image checkImage = checkObj.GetComponent<Image>();
        checkImage.color = new Color(0.3f, 0.85f, 0.3f, 1f);

        // Label
        GameObject labelObj = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer));
        labelObj.transform.SetParent(toggleObj.transform, false);
        labelObj.layer = 5;

        RectTransform labelRect = labelObj.GetComponent<RectTransform>();
        labelRect.sizeDelta = new Vector2(300, 45);

        TMPro.TextMeshProUGUI labelTmp = labelObj.AddComponent<TMPro.TextMeshProUGUI>();
        labelTmp.text = "Floating Joystick";
        labelTmp.fontSize = 32;
        labelTmp.alignment = TMPro.TextAlignmentOptions.MidlineLeft;
        labelTmp.color = Color.white;

        TMPro.TextMeshProUGUI existingTmp = parent.GetComponentInChildren<TMPro.TextMeshProUGUI>(true);
        if (existingTmp != null && existingTmp.font != null)
        {
            labelTmp.font = existingTmp.font;
        }

        Toggle toggle = toggleObj.GetComponent<Toggle>();
        toggle.targetGraphic = bgImage;
        toggle.graphic = checkImage;
        toggle.SetIsOnWithoutNotify(PlayerPrefs.GetInt(FloatingJoystickPrefKey, 0) == 1);

        return toggle;
    }
}
