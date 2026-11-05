using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class CharacterSelectionMenu : MonoBehaviour
{
    [SerializeField] private Canvas selectionCanvas;
    [SerializeField] private Image charImg;
    [SerializeField] private TextMeshProUGUI charNameText;
    [SerializeField] private TextMeshProUGUI moveSpeedText;
    [SerializeField] private TextMeshProUGUI jumpHeightText;
    [SerializeField] private TextMeshProUGUI jumpCountText;
    [SerializeField] private TextMeshProUGUI dashText;
    [SerializeField] private TextMeshProUGUI attackText;
    [SerializeField] private TextMeshProUGUI wallGrabText;
    [SerializeField] private Button selectBtn;
    [SerializeField] private TextMeshProUGUI selectBtnText;
    [SerializeField] private CharacterDataSO charData;
    [SerializeField] private TextMeshProUGUI coinText;

    [Space]
    [SerializeField] private UnityEvent onClose;
    
    private int currChar = 0;
    
    void Awake()
    {
        if (selectionCanvas == null)
            selectionCanvas = GetComponent<Canvas>();
    }

    void Start()
    {
        InitializeCharacterUnlocks();
        ShowCharacter(GetCurrCharIndex());
        UpdateCountAmount();
    }

    public void OpenMenu()
    {
        selectionCanvas.enabled = true;
    }

    public void CloseMenu()
    {
        selectionCanvas.enabled = false;
        onClose?.Invoke();
    }

    #region Character Navigation UI
    private void ShowCharacter(int charNo)
    {
        if(charNo > charData.characterData.Count - 1)
        {
            charNo = 0;
        }
        else if(charNo < 0)
        {
            charNo = charData.characterData.Count - 1;
        }
        
        currChar = charNo;

        charImg.sprite = charData.characterData[currChar].charSprite;
        
        SetSelectBtnInteractivity();
        
        SetCharData();
    }

    public void ShowNext()
    {
        ShowCharacter(currChar + 1);
    }

    public void ShowPrevious()
    {
        ShowCharacter(currChar - 1);
    }

    private void SetSelectBtnInteractivity()
    {
        if (currChar == GetCurrCharIndex())
        {
            selectBtn.interactable = false;
            selectBtnText.text = "SELECTED";
        }
        else if (isCharAvailable(currChar))
        {
            selectBtn.interactable = true;
            selectBtnText.text = "SELECT";
        }
        else
        {
            if (PlayerPrefs.GetInt("Foods", 0) < GetCharUnlockPrice(currChar))
            {
                selectBtn.interactable = false;
            }
            else
            {
                selectBtn.interactable = true;
            }
            selectBtnText.text = $"Buy this character with {GetCharUnlockPrice(currChar)}";
        }
    }

    private void SetCharData()
    {
        charNameText.text = charData.characterData[currChar].charName.ToString();

        moveSpeedText.text = charData.characterData[currChar].moveSpeed switch
        {
            <= 6 => "Slow",
            > 6 and < 9 => "Medium",
            _ => "Fast"
        };
        
        jumpHeightText.text = charData.characterData[currChar].jumpForce switch
        {
            <= 10 => "Low",
            > 10 and <= 14 => "Medium",
            _ => "High"
        };

        jumpCountText.text = $"{charData.characterData[currChar].maxAirJumpCnt}";

        dashText.text = charData.characterData[currChar].dashSpeed > 0 ? "Yes" : "No";
        
        attackText.text = charData.characterData[currChar].canAttack ? "Yes" : "No";

        wallGrabText.text = charData.characterData[currChar].canWallGrab ? "Yes" : "No";
    }
    #endregion

    public int GetCurrCharIndex()
    {
        return PlayerPrefs.GetInt("Character", 0);
    }
    
    private void UpdateCountAmount()
    {
        int coins = PlayerPrefs.GetInt("Foods", 0);
        coinText.text = $"x {coins}";
    }

    public void SelectChar()
    {
        if (!isCharAvailable(currChar))
        {
            int price = GetCharUnlockPrice(currChar);
            int coins = PlayerPrefs.GetInt("Foods", 0);
            if (coins >= price)
            {
                coins -= price;
                PlayerPrefs.SetInt("Foods", coins);
                PlayerPrefs.SetInt(GetCharacterUnlockKey(currChar), 1);
                PlayerPrefs.Save();
            }
            else
            {
                Debug.Log("Not enough coins to unlock this character.");
                return;
            }
        }
        PlayerPrefs.SetInt("Character", currChar);
        
        SetSelectBtnInteractivity();
        SetCharData();
        UpdateCountAmount();
    }
    
    private bool isCharAvailable(int charIndex)
    {
        return PlayerPrefs.GetInt(GetCharacterUnlockKey(charIndex), 0) == 1;
    }

    private void InitializeCharacterUnlocks()
    {
        int selectedCharacter = GetCurrCharIndex();
        for (int i = 0; i < charData.characterData.Count; i++)
        {
            string unlockKey = GetCharacterUnlockKey(i);
            if (!PlayerPrefs.HasKey(unlockKey) && (i == 0 || i == selectedCharacter))
            {
                PlayerPrefs.SetInt(unlockKey, 1);
            }
        }

        PlayerPrefs.Save();
    }

    private string GetCharacterUnlockKey(int charIndex)
    {
        return $"CharacterUnlocked_{charData.characterData[charIndex].charName}";
    }

    private int GetCharUnlockPrice(int charIndex)
    {
        return (int)charData.characterData[charIndex].unlockPrice;
    }
}
