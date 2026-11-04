using UnityEngine;

public class Finish : MonoBehaviour
{
    [Header("Components")]
    [SerializeField] private Animator animator;
    
    [Header("Manager Refs")]
    [SerializeField] private CheckpointManager checkpointManager;

    [Header("Level Completion")]
    [SerializeField] private HUDMenu hudMenu;

    private bool levelCompleted = false;
    private static readonly int onCompleteAnim = Animator.StringToHash("OnComplete");

    void Awake()
    {
        if (animator == null)
            animator = GetComponent<Animator>();

        if (checkpointManager == null)
            checkpointManager = FindFirstObjectByType<CheckpointManager>();
    }

    void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.gameObject.name == "Player" && !levelCompleted)
        {
            if (collision.gameObject.GetComponent<ItemCollector>().GetKeys() < 3)
            {
                hudMenu.ShowMessage("Collect all keys to activate this portal!");
                return;
            }
            animator.SetTrigger(onCompleteAnim);
            AudioManager.Instance.PlaySound(AudioType.levelFinish);
            hudMenu.ShowMessage("Congratulation for completing the level!");

            checkpointManager.ClearCheckpoints();
            
            levelCompleted = true;
            Invoke(nameof(CompleteLevel), 1f);
        }
    }

    private void CompleteLevel()
    {
        LevelManager.Instance.MarkLevelComplete();
        LevelManager.Instance.GoToNextLevel();
    }
}
