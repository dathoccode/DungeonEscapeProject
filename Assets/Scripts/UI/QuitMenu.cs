#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;
using UnityEngine.UI;

public class QuitMenu : MonoBehaviour
{
    private void Awake()
    {
        Button button = GetComponent<Button>();
        if (button != null && button.onClick.GetPersistentEventCount() == 0)
        {
            button.onClick.AddListener(Quit);
        }
    }

    public void Quit()
    {
#if UNITY_EDITOR
        EditorApplication.isPlaying = false;
#elif UNITY_WEBGL
        Application.OpenURL("about:blank");
#else
        Application.Quit();
#endif
    }
}
