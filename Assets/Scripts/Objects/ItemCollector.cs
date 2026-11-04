using TMPro;
using UnityEngine;

public class ItemCollector : MonoBehaviour
{
    private int cherries = 0;
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
        cherriesText.text = $"{cherries}";
    }

    private void UpdateKeys()
    {
        keysText.text = $"{keys}";
    }

    public int GetKeys()
    {
        return keys;
    }
}
