using System;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public class LevelCompleteMenu : MonoBehaviour
{
    [Serializable]
    public struct TimeBonusTier
    {
        [Tooltip("Maximum completion time (in seconds) to receive this bonus")]
        public float maxTimeSeconds;
        [Tooltip("Bonus coins/cherries awarded")]
        public int bonusCoins;
    }

    [Header("UI References")]
    [SerializeField] private Canvas levelCompleteCanvas;
    [SerializeField] private TextMeshProUGUI completionTimeText;
    [SerializeField] private TextMeshProUGUI cherriesCollectedText;
    [SerializeField] private TextMeshProUGUI timeBonusText;
    [SerializeField] private TextMeshProUGUI totalCoinsText;
    [SerializeField] private PlayerInput playerInput;

    [Header("Time Bonus Settings")]
    [SerializeField] private TimeBonusTier[] timeBonusTiers = new TimeBonusTier[]
    {
        new TimeBonusTier { maxTimeSeconds = 30f, bonusCoins = 15 },
        new TimeBonusTier { maxTimeSeconds = 60f, bonusCoins = 10 },
        new TimeBonusTier { maxTimeSeconds = 90f, bonusCoins = 5 }
    };
    [SerializeField] private int defaultBonusCoins = 2;

    void Awake()
    {
        if (levelCompleteCanvas == null)
            levelCompleteCanvas = GetComponent<Canvas>();

        if (playerInput == null)
            playerInput = FindFirstObjectByType<PlayerInput>();
    }

    public int CalculateTimeBonus(float elapsedSeconds)
    {
        if (timeBonusTiers != null && timeBonusTiers.Length > 0)
        {
            for (int i = 0; i < timeBonusTiers.Length; i++)
            {
                if (elapsedSeconds <= timeBonusTiers[i].maxTimeSeconds)
                {
                    return Mathf.Max(0, timeBonusTiers[i].bonusCoins);
                }
            }
        }

        return Mathf.Max(0, defaultBonusCoins);
    }

    public void OpenLevelCompleteMenu(float elapsedSeconds, string formattedTime, int cherriesCollected, ItemCollector itemCollector = null)
    {
        int bonusCoins = CalculateTimeBonus(elapsedSeconds);

        if (itemCollector != null)
        {
            itemCollector.AddBonusCherries(bonusCoins);
        }
        else if (bonusCoins > 0)
        {
            int updatedFoods = PlayerPrefs.GetInt("Foods", 0) + bonusCoins;
            PlayerPrefs.SetInt("Foods", updatedFoods);
            PlayerPrefs.Save();
        }

        int totalCoins = PlayerPrefs.GetInt("Foods", 0);

        if (completionTimeText != null)
            completionTimeText.text = $"TIME: {formattedTime}";

        if (cherriesCollectedText != null)
            cherriesCollectedText.text = $"CHERRIES: {cherriesCollected}";

        if (timeBonusText != null)
            timeBonusText.text = $"TIME BONUS: +{bonusCoins} COINS";

        if (totalCoinsText != null)
            totalCoinsText.text = $"TOTAL COINS: {totalCoins}";

        if (levelCompleteCanvas != null)
            levelCompleteCanvas.enabled = true;

        Time.timeScale = 0f;

        if (playerInput == null)
            playerInput = FindFirstObjectByType<PlayerInput>();

        if (playerInput != null)
            playerInput.SwitchCurrentActionMap("UI");
    }

    public void NextLevel()
    {
        if (levelCompleteCanvas != null)
            levelCompleteCanvas.enabled = false;

        Time.timeScale = 1f;
        LevelManager.Instance.GoToNextLevel();
    }

    public void HomeMenu()
    {
        if (levelCompleteCanvas != null)
            levelCompleteCanvas.enabled = false;

        Time.timeScale = 1f;
        SceneManager.LoadScene("Scenes/Start Screen");
    }
}
