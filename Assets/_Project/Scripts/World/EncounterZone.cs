using UnityEngine;

/// <summary>Une rencontre : les monstres dorment jusqu'à ce que le héros s'approche, puis le combat démarre jusqu'à leur défaite.</summary>
public class EncounterZone : MonoBehaviour
{
    public EnemyAI[] enemies;
    public float triggerRadius = 9f;
    public int expReward = 30;
    public int galdReward = 50;
    public string banner = "ENCOUNTER !";

    bool started, cleared;
    Transform hero;

    void Awake() { foreach (var e in enemies) if (e != null) e.Active = false; }

    void Start() { var p = FindFirstObjectByType<PlayerController>(); if (p != null) hero = p.transform; }

    bool AllDead()
    {
        foreach (var e in enemies)
        {
            if (e == null) continue;
            var h = e.GetComponent<Health>();
            if (h != null && !h.IsDead) return false;
        }
        return true;
    }

    void Update()
    {
        if (hero == null || cleared) return;

        if (!started)
        {
            if (BattleManager.Instance == null || BattleManager.Instance.State != BattleState.Field) return;
            Vector3 d = hero.position - transform.position; d.y = 0;
            if (d.magnitude <= triggerRadius)
            {
                started = true;
                foreach (var e in enemies) if (e != null) e.Active = true;
                BattleManager.Instance.Begin(banner);
            }
        }
        else if (AllDead())
        {
            cleared = true;
            BattleManager.Instance.Win(expReward, galdReward);
        }
    }
}
