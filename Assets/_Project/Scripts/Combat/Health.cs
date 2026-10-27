using System;
using UnityEngine;

public enum Team { Player, Enemy }

public class Health : MonoBehaviour
{
    public Team team = Team.Enemy;
    public float maxHp = 100f;
    public float maxPoise = 30f;
    public bool invulnerable;
    public float damageMultiplier = 1f;

    [NonSerialized] public bool guarding;
    [NonSerialized] public float guardStartTime;
    [NonSerialized] public float armorUntil;   // super armure : ignore vacillement/projection

    public float Current { get; private set; }
    public float Poise { get; private set; }
    public bool IsDead => Current <= 0f;

    public event Action<float, float> OnChanged;
    public event Action<DamageInfo> OnDamaged;
    public event Action<DamageInfo> OnStaggered;
    public event Action OnDied;
    public event Action OnPerfectGuard;

    float lastHit;

    void Awake() { Current = maxHp; Poise = maxPoise; }

    public void Init(float max)
    {
        maxHp = max; Current = max; Poise = maxPoise;
        OnChanged?.Invoke(Current, maxHp);
    }

    void Update() { if (Time.time - lastHit > 1.8f) Poise = maxPoise; }

    public void Heal(float amount)
    {
        if (IsDead) return;
        Current = Mathf.Min(maxHp, Current + amount);
        OnChanged?.Invoke(Current, maxHp);
    }

    /// <summary>Retourne les dégâts réellement infligés.</summary>
    public float TakeDamage(DamageInfo d)
    {
        if (IsDead || invulnerable) return 0f;

        if (guarding)
        {
            if (Time.time - guardStartTime < 0.18f) { OnPerfectGuard?.Invoke(); return 0f; } // garde parfaite
            d.amount *= 0.2f; d.poiseDamage = 0f; d.launch = 0f; d.knockdown = false;
        }
        if (Time.time < armorUntil) { d.poiseDamage = 0f; d.launch = 0f; d.knockdown = false; }

        float dmg = d.amount > 0f ? Mathf.Max(1f, Mathf.Round(d.amount * damageMultiplier)) : 0f;
        Current = Mathf.Max(0f, Current - dmg);
        lastHit = Time.time;
        Poise -= d.poiseDamage;
        OnChanged?.Invoke(Current, maxHp);
        OnDamaged?.Invoke(d);

        if (Poise <= 0f || d.launch > 0f || d.knockdown)
        {
            Poise = maxPoise;
            OnStaggered?.Invoke(d);
        }
        if (IsDead) OnDied?.Invoke();
        return dmg;
    }
}
