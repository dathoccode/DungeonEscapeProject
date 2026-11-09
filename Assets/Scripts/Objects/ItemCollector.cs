using TMPro;
using UnityEngine;

public class ItemCollector : MonoBehaviour
{
    private int cherries = 0;
    private int levelCherries = 0;
    private int keys = 0;
    // [SerializeField] private int TotalCherries = 5;
    [SerializeField] private TextMeshProUGUI cherriesText;
    [SerializeField] private TextMeshProUGUI keysText;

    void Start()
    {
        cherries = PlayerPrefs.GetInt("Foods", cherries);
        UpdateCherries();
        UpdateKeys();
    }

    void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.gameObject.CompareTag("Cherry"))
        {
            AudioManager.Instance.PlaySound(AudioType.itemCollect);
            Destroy(collision.gameObject);
            cherries++;
            levelCherries++;
            PlayerPrefs.SetInt("Foods", cherries);
            UpdateCherries();
        }
        if (collision.gameObject.CompareTag("Key"))
        {
            AudioManager.Instance.PlaySound(AudioType.itemCollect);
            Destroy(collision.gameObject);
            keys++;
            UpdateKeys();
        }
    }

    private void UpdateCherries()
    {
        if (cherriesText != null)
            cherriesText.text = $"{cherries}";
    }

    private void UpdateKeys()
    {
        if (keysText != null)
            keysText.text = $"{keys}";
    }

    public int GetKeys()
    {
        return keys;
    }

    public int GetLevelCherries()
    {
        return levelCherries;
    }

    public int GetTotalCherries()
    {
        return cherries;
    }

    public void AddBonusCherries(int bonus)
    {
        if (bonus <= 0)
            return;

        cherries = PlayerPrefs.GetInt("Foods", cherries) + bonus;
        PlayerPrefs.SetInt("Foods", cherries);
        PlayerPrefs.Save();
        UpdateCherries();
    }
}
