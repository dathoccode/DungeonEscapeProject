using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

public class GameOverMenu : MonoBehaviour
{
    [SerializeField] private Canvas gameOverCanvas;
    [SerializeField] private GameObject mainPanel;
    [SerializeField] private GameObject startLevelAgainBtn;
    [SerializeField] private LevelTimer levelTimer;
    [SerializeField] private PlayerLife playerLife;
    [SerializeField] private ItemCollector itemCollector;

    private void Awake()
    {
        if (gameOverCanvas == null)
            gameOverCanvas = GetComponent<Canvas>();

        if (levelTimer == null)
            levelTimer = FindFirstObjectByType<LevelTimer>();

        if (playerLife == null)
            playerLife = FindFirstObjectByType<PlayerLife>();

        if (itemCollector == null)
            itemCollector = FindFirstObjectByType<ItemCollector>();
    }

    public void OpenGameOverMenu()
    {
        if (levelTimer != null)
            levelTimer.StopTimer();

        if (EventSystem.current != null)
            EventSystem.current.sendNavigationEvents = true;

        gameOverCanvas.enabled = true;
        SetTimeScale(false);

        SetStartAgainBtn();
    }
    
    public void CloseGameOverMenu()
    {
        gameOverCanvas.enabled = false;
        SetTimeScale(true);

        if (EventSystem.current != null)
        {
            EventSystem.current.sendNavigationEvents = false;
            EventSystem.current.SetSelectedGameObject(null);
        }
    }

    private void SetStartAgainBtn()
    {
        startLevelAgainBtn.SetActive(PlayerPrefs.GetInt("LifeCount", 0) > 0);
    }
    
    private void SetTimeScale(bool reset)
    {
        Time.timeScale = reset ? 1 : 0;
    }
    
    public void Home()
    {
        CloseGameOverMenu();
        if (EventSystem.current != null)
            EventSystem.current.sendNavigationEvents = true;
        SceneManager.LoadScene("Scenes/Start Screen");
    }
    
    public void StartAgain()
    {
        CloseGameOverMenu();

        if (levelTimer != null)
            levelTimer.ResumeTimer();

        if (itemCollector == null)
            itemCollector = FindFirstObjectByType<ItemCollector>();

        if (itemCollector != null)
            itemCollector.RefreshCherries();

        if (playerLife == null)
            playerLife = FindFirstObjectByType<PlayerLife>();

        if (playerLife != null)
            playerLife.RespawnFromGameOver();
    }

    public void OnExtraLivesMenuOpen()
    {
        mainPanel.SetActive(false);
    }
    
    public void OnExtraLivesMenuClose()
    {
        mainPanel.SetActive(true);
        
        SetStartAgainBtn();
    }
}
