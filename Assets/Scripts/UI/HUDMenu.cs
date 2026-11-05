using TMPro;
using UnityEngine;

public class HUDMenu : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI messageText;
    private float messageDisplayTime = 3f;
    private float messageTimer = 0f;

    private void Start()
    {
        messageText.text = ""; 
    }

    void Update()
    {
        if (messageText.text != "")
        {
            messageTimer += Time.deltaTime;
        }

        if (messageTimer >= messageDisplayTime)
        {
            HideMessage();
            messageTimer = 0f;
        }
    }

    public void ShowMessage(string message)
    {
        messageText.text = message;
    }

    private void HideMessage()
    {
        messageText.text = "";
    }
}
