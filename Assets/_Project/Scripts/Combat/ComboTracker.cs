using UnityEngine;

/// <summary>Compteur de combos du groupe, réduction de dégâts sur les très longs combos, stats de combat.</summary>
public class ComboTracker : MonoBehaviour
{
    public static ComboTracker Instance { get; private set; }
    const float Window = 2.2f;

    public int Hits { get; private set; }
    public float Damage { get; private set; }
    public int LastHits { get; private set; }
    public float LastDamage { get; private set; }
    public float LastEnd { get; private set; }
    public int BattleMaxCombo { get; private set; }
    public float BattleDamage { get; private set; }
    float lastHit;

    public float DamageScale => Hits <= 5 ? 1f : Mathf.Max(0.55f, 1f - 0.03f * (Hits - 5));

    void Awake() { Instance = this; }
    void OnEnable() { CombatUtil.HitLanded += OnHit; }
    void OnDisable() { CombatUtil.HitLanded -= OnHit; }

    public void ResetBattle() { BattleMaxCombo = 0; BattleDamage = 0f; }

    void OnHit(Health target, DamageInfo d, float dealt)
    {
        if (d.source != Team.Player) return;
        if (Time.time - lastHit > Window) { Hits = 0; Damage = 0f; }
        Hits++; Damage += dealt; lastHit = Time.time;
        BattleMaxCombo = Mathf.Max(BattleMaxCombo, Hits);
        BattleDamage += dealt;
    }

    void Update()
    {
        if (Hits > 0 && Time.time - lastHit > Window)
        {
            LastHits = Hits; LastDamage = Damage; LastEnd = Time.time;
            Hits = 0; Damage = 0f;
        }
    }
}
