using UnityEngine;

public class AngryPigDamage : EnemyDamage
{
    [SerializeField] EnemyMovement angryPigMovement;
    [SerializeField] float angrySpeed = 8;
    protected static readonly int angryAnim = Animator.StringToHash("AngryMod");
    private bool isAngry = false;

    private void Awake()
    {
        if (angryPigMovement == null)
            angryPigMovement = GetComponent<EnemyMovement>();
    }

    public override void Die()
    {
        AudioManager.Instance.PlaySound(AudioType.enemyHit);
        animator.SetTrigger(dieAnim);
        if (!isAngry)
        {
            angryPigMovement.SetIdleDuration(0);
            angryPigMovement.SetSpeed(angrySpeed);
        }
        else
        {
            enemyCollider.enabled = false;
            Destroy(transform.parent.gameObject, 1.25f);
        }
        
    }

    private void SetAngryModOn()
    {
        isAngry = true;
        animator.SetBool(angryAnim, true);
    }
}
