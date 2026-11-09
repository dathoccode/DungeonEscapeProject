using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

public class LevelTimer : MonoBehaviourSingleton<LevelTimer>
{
    [Header("UI Reference")]
    [SerializeField] private TextMeshProUGUI timerText;

    [Header("Settings")]
    [SerializeField] private bool startOnAwake = true;
    [SerializeField] private bool showMilliseconds = false;

    private float elapsedTime = 0f;
    private bool isRunning = false;
    private int lastDisplayedSecond = -1;

    public float ElapsedTime => elapsedTime;
    public bool IsRunning => isRunning;

    void Start()
    {
        if (startOnAwake)
        {
            StartTimer();
        }
        else
        {
            UpdateTimerUI(forceUpdate: true);
        }
    }

    void Update()
    {
        if (!isRunning)
            return;

        elapsedTime += Time.deltaTime;
        UpdateTimerUI();
    }

    public void StartTimer()
    {
        elapsedTime = 0f;
        lastDisplayedSecond = -1;
        isRunning = true;
        UpdateTimerUI(forceUpdate: true);
    }

    public void StopTimer(bool saveBestTime = false)
    {
        if (!isRunning)
            return;

        isRunning = false;
        UpdateTimerUI(forceUpdate: true);

        if (saveBestTime)
        {
            SaveCompletionTime();
        }
    }

    public void ResumeTimer()
    {
        isRunning = true;
    }

    public string GetFormattedTime()
    {
        int minutes = Mathf.FloorToInt(elapsedTime / 60f);
        int seconds = Mathf.FloorToInt(elapsedTime % 60f);

        if (showMilliseconds)
        {
            int centiseconds = Mathf.FloorToInt((elapsedTime * 100f) % 100f);
            return $"{minutes:00}:{seconds:00}.{centiseconds:00}";
        }

        return $"{minutes:00}:{seconds:00}";
    }

    private void UpdateTimerUI(bool forceUpdate = false)
    {
        if (timerText == null)
            return;

        if (!showMilliseconds && !forceUpdate)
        {
            int currentSecond = Mathf.FloorToInt(elapsedTime);
            if (currentSecond == lastDisplayedSecond)
                return;

            lastDisplayedSecond = currentSecond;
        }

        timerText.text = GetFormattedTime();
    }

    private void SaveCompletionTime()
    {
        string levelName = SceneManager.GetActiveScene().name;
        string bestTimeKey = $"BestTime_{levelName}";

        PlayerPrefs.SetFloat($"LastTime_{levelName}", elapsedTime);

        float currentBest = PlayerPrefs.GetFloat(bestTimeKey, float.MaxValue);
        if (elapsedTime < currentBest)
        {
            PlayerPrefs.SetFloat(bestTimeKey, elapsedTime);
        }

        PlayerPrefs.Save();
    }
}
