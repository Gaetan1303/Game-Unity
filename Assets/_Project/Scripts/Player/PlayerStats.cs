using UnityEngine;

[RequireComponent(typeof(Health))]
public class PlayerStats : MonoBehaviour
{
    public int level = 1, exp, gald, items = 3;
    public float baseHp = 200f, baseTp = 100f, tpRegen = 1.5f;
    public float overlimitDuration = 12f;

    public float MaxHp => baseHp + (level - 1) * 25f;
    public float MaxTp => baseTp + (level - 1) * 6f;
    public int ExpToNext => 40 + level * 30;
    public float Tp { get; private set; }
    public float Overlimit { get; private set; }       // 0..100
    public bool InOverlimit { get; private set; }
    public float LevelUpUntil { get; private set; }

    public float AttackMultiplier => (1f + 0.08f * (level - 1)) * (InOverlimit ? 1.3f : 1f);
    public float SpeedMultiplier => InOverlimit ? 1.25f : 1f;

    Health hp;
    float olTimer;

    void Awake() { hp = GetComponent<Health>(); }

    void Start()
    {
        var gm = GameManager.Instance;
        if (gm != null) { level = gm.level; exp = gm.exp; gald = gm.gald; items = gm.items; }
        hp.team = Team.Player; hp.maxPoise = 45f;
        hp.Init(MaxHp);
        Tp = MaxTp;
        hp.OnDamaged += d => AddOverlimit(d.amount * 0.5f);
    }

    void OnEnable() { CombatUtil.HitLanded += OnHit; }
    void OnDisable() { CombatUtil.HitLanded -= OnHit; }

    void OnHit(Health t, DamageInfo d, float dealt) { if (d.source == Team.Player) AddOverlimit(dealt * 0.35f); }

    void AddOverlimit(float v) { if (!InOverlimit) Overlimit = Mathf.Min(100f, Overlimit + v); }

    void Update()
    {
        if (InOverlimit)
        {
            olTimer -= Time.deltaTime;
            Overlimit = Mathf.Max(0f, olTimer / overlimitDuration * 100f);
            Tp = MaxTp;
            if (olTimer <= 0f) { InOverlimit = false; Overlimit = 0f; }
        }
        else Tp = Mathf.Min(MaxTp, Tp + tpRegen * Time.deltaTime);
    }

    public bool TryOverlimit()
    {
        if (InOverlimit || Overlimit < 100f) return false;
        InOverlimit = true; olTimer = overlimitDuration;
        CameraFollow.Shake(0.3f);
        return true;
    }

    public bool TrySpend(float cost)
    {
        if (InOverlimit) return true;
        if (Tp < cost) return false;
        Tp -= cost; return true;
    }

    public void AddTp(float v) { Tp = Mathf.Min(MaxTp, Tp + v); }

    public void UseItem()
    {
        if (items <= 0 || hp.IsDead || hp.Current >= hp.maxHp) return;
        items--;
        hp.Heal(hp.maxHp * 0.4f);
        Fx.Spawn("star_09", transform.position, 1f, 2.4f, new Color(1f, 0.4f, 0.4f), 0.6f, vel: Vector3.up, order: 70);
        Save();
    }

    public void AddExp(int amount)
    {
        exp += amount;
        while (exp >= ExpToNext)
        {
            exp -= ExpToNext; level++;
            hp.maxHp = MaxHp; hp.Heal(hp.maxHp);
            Tp = MaxTp; LevelUpUntil = Time.time + 4f;
        }
        Save();
    }

    public void AddGald(int amount) { gald += amount; Save(); }

    void Save()
    {
        var gm = GameManager.Instance;
        if (gm != null) { gm.level = level; gm.exp = exp; gm.gald = gald; gm.items = items; }
    }
}
