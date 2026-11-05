using UnityEngine;
using UnityEngine.EventSystems;

public class VirtualJoystick : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
{
    public enum JoystickType
    {
        Fixed,
        Floating
    }

    [Header("Settings")]
    [SerializeField] private JoystickType joystickType = JoystickType.Fixed;
    [SerializeField] private JoystickType debugJoystickType = JoystickType.Fixed;

    [SerializeField, Min(1f)]
    private float maxDistance = 60f;

    [Header("References")]
    [SerializeField] private RectTransform joystickBackground;
    [SerializeField] private RectTransform joystickHandle;
    [SerializeField] private RectTransform touchArea;

    public Vector2 Direction { get; private set; }

    private int activePointerId = -1;
    private bool isDragging;

    private Vector2 fixedBasePosition;

    private void Awake()
    {
        fixedBasePosition = joystickBackground.anchoredPosition;

        SetJoystickType(JoystickType.Fixed);
        ResetJoystick();
    }

    private void Update()
    {
        if (debugJoystickType != joystickType)
        {
            SetJoystickType(debugJoystickType);
        }
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (isDragging) return;

        activePointerId = eventData.pointerId;
        isDragging = true;

        if (joystickType == JoystickType.Floating)
        {
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                touchArea,
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

        Vector2 offset = Vector2.ClampMagnitude(
            localPoint, maxDistance);

        joystickHandle.anchoredPosition = offset;
        Direction = offset / maxDistance;
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (!isDragging ||
            eventData.pointerId != activePointerId)
            return;

        ResetJoystick();
    }

    private void ResetJoystick()
    {
        Direction = Vector2.zero;
        joystickHandle.anchoredPosition = Vector2.zero;

        isDragging = false;
        activePointerId = -1;

        if (joystickType == JoystickType.Floating)
        {
            joystickBackground.gameObject.SetActive(false);
        }
        else
        {
            joystickBackground.anchoredPosition = fixedBasePosition;
        }
    }

    private void OnDisable()
    {
        Direction = Vector2.zero;
        isDragging = false;
        activePointerId = -1;

        if (joystickHandle != null)
            joystickHandle.anchoredPosition = Vector2.zero;

        if (joystickBackground != null &&
            joystickType == JoystickType.Floating)
        {
            joystickBackground.gameObject.SetActive(false);
        }
    }

    public void SetJoystickType(JoystickType newJoystickType)
    {
        ResetJoystick();

        joystickType = newJoystickType;
        if (joystickType == JoystickType.Floating)
        {
            joystickBackground.gameObject.SetActive(false);
            touchArea.anchoredPosition = new Vector2(Screen.width / 2, Screen.height / 2);
            touchArea.sizeDelta = new Vector2(Screen.width, Screen.height);
        }
        else
        {
            joystickBackground.gameObject.SetActive(true);
            touchArea.anchoredPosition = new Vector2(300, 300);
            touchArea.sizeDelta = new Vector2(400, 400);
        }
    }
}