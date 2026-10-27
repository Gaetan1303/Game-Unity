using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Artes du héros. Attaque (J) + direction = artes STANDARD, Technique (K) + direction = artes TECHNIQUES.
/// Index : 0 neutre, 1 avant (vers la cible), 2 haut, 3 bas. Un arte peut annuler la récupération d'une attaque qui a touché.
/// </summary>
[RequireComponent(typeof(PlayerController), typeof(PlayerStats))]
public class ArteCaster : MonoBehaviour
{
    [Tooltip("0 = neutre (attaque normale, laisser vide), 1 = avant, 2 = haut, 3 = bas")]
    public ArteData[] standard = new ArteData[4];
    [Tooltip("0 = neutre, 1 = avant, 2 = haut, 3 = bas")]
    public ArteData[] technical = new ArteData[4];

    PlayerController pc;
    PlayerStats stats;
    TargetSelector sel;
    Knockable kn;
    readonly Dictionary<ArteData, float> cd = new Dictionary<ArteData, float>();
    Coroutine running;
    bool inRecovery;

    public bool IsCasting { get; private set; }
    public string Message { get; private set; }
    public float MessageUntil { get; private set; }
    public event System.Action<ArteData> Casted;

    void Say(string m) { Message = m; MessageUntil = Time.time + 1.2f; }

    public float CooldownLeft(ArteData a) => (a != null && cd.TryGetValue(a, out float t)) ? Mathf.Max(0f, t - Time.time) : 0f;
    public bool Affordable(ArteData a) => a != null && (stats.InOverlimit || stats.Tp >= a.tpCost);

    void Awake()
    {
        pc = GetComponent<PlayerController>();
        stats = GetComponent<PlayerStats>();
        sel = GetComponent<TargetSelector>();
        kn = GetComponent<Knockable>();
    }

    public void TryStandard(ArteDir d) { if (d != ArteDir.Neutral) Try(standard[(int)d]); }
    public void TryTechnical(ArteDir d) { Try(technical[(int)d]); }

    bool CanStart()
    {
        if (kn.Stunned) return false;
        if (IsCasting) return inRecovery && Time.time < pc.cancelUntil;   // annulation d'un arte qui a touché
        return pc.CanAct || Time.time < pc.cancelUntil;                    // annulation d'une attaque qui a touché
    }

    void Try(ArteData a)
    {
        if (a == null || !CanStart()) return;
        if (!stats.InOverlimit && Time.time < (cd.TryGetValue(a, out float t) ? t : 0f)) return;
        if (!stats.TrySpend(a.tpCost)) { Say("Pas assez de TP !"); return; }

        if (IsCasting && running != null) StopCoroutine(running);
        cd[a] = Time.time + a.cooldown;
        running = StartCoroutine(Cast(a));
    }

    IEnumerator Cast(ArteData a)
    {
        IsCasting = true; inRecovery = false;
        pc.FaceTarget();
        pc.SetLock(ArteExecutor.Duration(a));
        Say(a.displayName + " !");
        Casted?.Invoke(a);

        yield return ArteExecutor.Run(a, Team.Player, transform, sel.Current, stats.AttackMultiplier,
                                      n => { if (n > 0) stats.AddTp(1f * n); }, () => inRecovery = true);
        IsCasting = false;
    }
}
