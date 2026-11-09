using System.Collections;
using UnityEngine;

public class Finish : MonoBehaviour
{
    [Header("Components")]
    [SerializeField] private Animator animator;
    
    [Header("Manager Refs")]
    [SerializeField] private CheckpointManager checkpointManager;
    [SerializeField] private LevelTimer levelTimer;

    [Header("Level Completion")]
    [SerializeField] private HUDMenu hudMenu;
    [SerializeField] private LevelCompleteMenu levelCompleteMenu;
    [SerializeField] private LevelCompleteMenu levelCompleteMenuPrefab;

    private bool levelCompleted = false;
    private ItemCollector playerItemCollector;
    private static readonly int onCompleteAnim = Animator.StringToHash("OnComplete");
    private static readonly int finishPortalIdleHash = Animator.StringToHash("FinishPortal_Idle");
    private static readonly int finishIdleHash = Animator.StringToHash("Finish_Idle");

    void Awake()
    {
        if (animator == null)
            animator = GetComponent<Animator>();

        if (checkpointManager == null)
            checkpointManager = FindFirstObjectByType<CheckpointManager>();

        if (levelTimer == null)
            levelTimer = FindFirstObjectByType<LevelTimer>();

        if (levelCompleteMenu == null)
            levelCompleteMenu = FindFirstObjectByType<LevelCompleteMenu>();

        if (levelCompleteMenu == null && levelCompleteMenuPrefab != null)
        {
            GameObject canvasRoot = GameObject.Find("Canvas");
            if (canvasRoot != null)
                levelCompleteMenu = Instantiate(levelCompleteMenuPrefab, canvasRoot.transform);
            else
                levelCompleteMenu = Instantiate(levelCompleteMenuPrefab);

            levelCompleteMenu.name = "LevelCompleteCanvas";
        }
    }

    void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.gameObject.name == "Player" && !levelCompleted)
        {
            playerItemCollector = collision.gameObject.GetComponent<ItemCollector>();
            if (playerItemCollector != null && playerItemCollector.GetKeys() < 3)
            {
                if (hudMenu != null)
                    hudMenu.ShowMessage("Collect all keys to activate this portal!");
                return;
            }

            if (animator != null)
            {
                animator.SetTrigger(onCompleteAnim);
            }

            AudioManager.Instance.PlaySound(AudioType.levelFinish);

            if (levelTimer != null)
            {
                levelTimer.StopTimer(saveBestTime: true);
            }

            if (checkpointManager != null)
            {
                checkpointManager.ClearCheckpoints();
            }

            LevelManager.Instance.MarkLevelComplete();

            levelCompleted = true;
            StartCoroutine(WaitForPortalAnimationAndComplete());
        }
    }

    private IEnumerator WaitForPortalAnimationAndComplete()
    {
        if (animator != null)
        {
            // Chờ 1 frame để Animator nhận trigger OnComplete
            yield return null;

            float safetyTimeout = 3f;
            float elapsed = 0f;

            // Chờ thoát khỏi trạng thái Idle và kết thúc quá trình chuyển tiếp (transition) sang FinishPortal_Close
            while (elapsed < safetyTimeout)
            {
                AnimatorStateInfo stateInfo = animator.GetCurrentAnimatorStateInfo(0);
                bool isStillIdle = stateInfo.shortNameHash == finishPortalIdleHash || stateInfo.shortNameHash == finishIdleHash;

                if (!isStillIdle && !animator.IsInTransition(0))
                    break;

                elapsed += Time.deltaTime;
                yield return null;
            }

            // Chờ cho animation đóng cổng (FinishPortal_Close / Finish_OnComplete) chạy hết 100% (normalizedTime >= 1f)
            while (elapsed < safetyTimeout)
            {
                AnimatorStateInfo stateInfo = animator.GetCurrentAnimatorStateInfo(0);
                if (!animator.IsInTransition(0) && stateInfo.normalizedTime >= 1f)
                    break;

                elapsed += Time.deltaTime;
                yield return null;
            }
        }
        else
        {
            yield return new WaitForSeconds(1.05f);
        }

        CompleteLevel();
    }

    private void CompleteLevel()
    {
        if (levelCompleteMenu == null)
            levelCompleteMenu = FindFirstObjectByType<LevelCompleteMenu>();

        if (levelCompleteMenu == null && levelCompleteMenuPrefab != null)
        {
            GameObject canvasRoot = GameObject.Find("Canvas");
            if (canvasRoot != null)
                levelCompleteMenu = Instantiate(levelCompleteMenuPrefab, canvasRoot.transform);
            else
                levelCompleteMenu = Instantiate(levelCompleteMenuPrefab);

            levelCompleteMenu.name = "LevelCompleteCanvas";
        }

        if (levelCompleteMenu != null)
        {
            float elapsedTime = levelTimer != null ? levelTimer.ElapsedTime : 0f;
            string formattedTime = levelTimer != null ? levelTimer.GetFormattedTime() : "00:00";
            int cherriesCollected = playerItemCollector != null ? playerItemCollector.GetLevelCherries() : 0;

            levelCompleteMenu.OpenLevelCompleteMenu(elapsedTime, formattedTime, cherriesCollected, playerItemCollector);
        }
        else
        {
            LevelManager.Instance.GoToNextLevel();
        }
    }
}
