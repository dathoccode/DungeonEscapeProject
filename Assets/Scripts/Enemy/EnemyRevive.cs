using UnityEngine;

public class EnemyRevive : MonoBehaviour
{
    [SerializeField] private GameObject replacementPrefab;
    private bool hasReplaced;

    public void ReplaceWithPrefab()
    {
        if (hasReplaced)
            return;

        if (replacementPrefab == null)
        {
            Debug.LogError($"{nameof(EnemyRevive)} on {name} has no replacement prefab assigned.", this);
            return;
        }

        hasReplaced = true;
        Transform current = transform;

        GameObject replacement = Instantiate(
            replacementPrefab,
            current.position,
            current.rotation,
            current.parent
        );
        replacement.transform.localScale = current.localScale;

        Destroy(gameObject);
    }
}
