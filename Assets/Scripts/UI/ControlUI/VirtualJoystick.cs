using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.Layouts;
using UnityEngine.InputSystem.OnScreen;

public class VirtualJoystick : OnScreenControl, IPointerDownHandler, IDragHandler, IPointerUpHandler
{
    private const string FloatingJoystickPrefKey = "FloatingJoystick";

    public enum JoystickType
    {
        Fixed,
        Floating
    }

    [Header("Settings")]
    [SerializeField] private JoystickType joystickType = JoystickType.Fixed;
    [SerializeField] private JoystickType debugJoystickType = JoystickType.Fixed;

    [SerializeField, Range(0f, 1f), Tooltip("Deadzone threshold before joystick input registers")]
    private float deadzone = 0.25f;

    [SerializeField, Min(1f)]
    private float maxDistance = 60f;

    [InputControl(layout = "Vector2")]
    [SerializeField]
    private string m_ControlPath = "<Gamepad>/leftStick";

    [Header("References")]
    [SerializeField] private RectTransform joystickBackground;
    [SerializeField] private RectTransform joystickHandle;
    [SerializeField] private RectTransform touchArea;

    protected override string controlPathInternal
    {
        get => m_ControlPath;
        set => m_ControlPath = value;
    }

    public Vector2 Direction { get; private set; }

    public float Deadzone
    {
        get => deadzone;
        set => deadzone = Mathf.Clamp01(value);
    }

    public JoystickType CurrentJoystickType => joystickType;

    private int activePointerId = -1;
    private bool isDragging;
    private Vector2 fixedBasePosition;
    private bool isInitialized;

    private void Awake()
    {
        if (joystickBackground != null)
        {
            fixedBasePosition = joystickBackground.anchoredPosition;
        }

        LoadSettings();
        isInitialized = true;
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        if (isInitialized)
        {
            LoadSettings();
        }
    }

    protected override void OnDisable()
    {
        ResetJoystick();
        base.OnDisable();
    }

    private void Update()
    {
        if (debugJoystickType != joystickType)
        {
            SetJoystickType(debugJoystickType);
        }
    }

    public void LoadSettings()
    {
        bool isFloating = PlayerPrefs.GetInt(FloatingJoystickPrefKey, 0) == 1;
        SetJoystickType(isFloating ? JoystickType.Floating : JoystickType.Fixed);
    }

    public void SetJoystickType(JoystickType newJoystickType)
    {
        joystickType = newJoystickType;
        debugJoystickType = newJoystickType;

        ResetJoystick();

        if (touchArea == null || joystickBackground == null) return;

        if (joystickType == JoystickType.Floating)
        {
            joystickBackground.gameObject.SetActive(false);

            // In Floating mode, touch area covers the left half of the screen
            touchArea.anchorMin = new Vector2(0f, 0f);
            touchArea.anchorMax = new Vector2(0.5f, 1f);
            touchArea.offsetMin = Vector2.zero;
            touchArea.offsetMax = Vector2.zero;
            touchArea.pivot = new Vector2(0.5f, 0.5f);
        }
        else
        {
            joystickBackground.anchoredPosition = fixedBasePosition;
            joystickBackground.gameObject.SetActive(true);

            // In Fixed mode, touch area covers the bottom-left area around the fixed joystick
            touchArea.anchorMin = joystickBackground.anchorMin;
            touchArea.anchorMax = joystickBackground.anchorMax;
            touchArea.pivot = joystickBackground.pivot;
            touchArea.anchoredPosition = fixedBasePosition;
            touchArea.sizeDelta = new Vector2(400f, 400f);
        }
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (EventSystem.current != null && EventSystem.current.currentSelectedGameObject != null)
        {
            EventSystem.current.SetSelectedGameObject(null);
        }

        if (isDragging) return;

        activePointerId = eventData.pointerId;
        isDragging = true;

        if (joystickType == JoystickType.Floating)
        {
            RectTransform parentRect = joystickBackground.parent as RectTransform;
            if (parentRect != null && RectTransformUtility.ScreenPointToLocalPointInRectangle(
                parentRect,
                eventData.position,
                eventData.pressEventCamera,
                out Vector2 localPoint))
            {
                joystickBackground.anchoredPosition = localPoint;
                joystickBackground.gameObject.SetActive(true);
            }
        }

        OnDrag(eventData);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!isDragging || eventData.pointerId != activePointerId)
            return;

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
            joystickBackground,
            eventData.position,
            eventData.pressEventCamera,
            out Vector2 localPoint))
        {
            return;
        }

        Vector2 offset = Vector2.ClampMagnitude(localPoint, maxDistance);
        if (joystickHandle != null)
            joystickHandle.anchoredPosition = offset;

        Vector2 rawDirection = offset / maxDistance;
        Direction = rawDirection.magnitude < deadzone ? Vector2.zero : rawDirection;
        SendValueToControl(Direction);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (!isDragging || eventData.pointerId != activePointerId)
            return;

        ResetJoystick();
    }

    private void ResetJoystick()
    {
        Direction = Vector2.zero;
        SendValueToControl(Vector2.zero);

        if (joystickHandle != null)
            joystickHandle.anchoredPosition = Vector2.zero;

        isDragging = false;
        activePointerId = -1;

        if (joystickBackground != null)
        {
            if (joystickType == JoystickType.Floating)
            {
                joystickBackground.gameObject.SetActive(false);
            }
            else
            {
                joystickBackground.anchoredPosition = fixedBasePosition;
                joystickBackground.gameObject.SetActive(true);
            }
        }
    }
}
