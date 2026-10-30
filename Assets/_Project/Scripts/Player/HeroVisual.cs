using UnityEngine;

/// <summary>
/// Animation du démon (clips dans Resources/Animations/hero.json) :
/// idle / marche / marche arrière (recul le long de la ligne) / course / saut / chute / réception / garde,
/// 3 attaques distinctes, un clip par type d'arte, touché / projeté / à terre / relevé / mort / victoire.
/// Les attaques sont calées sur le moment réel des dégâts (évènement "hit" / "release").
/// </summary>
public class HeroVisual : SpriteActor
{
    PlayerController pc;
    ArteCaster caster;
    PlayerStats stats;
    int comboFx = 1;
    ArteData spellArte;
    float spellStart, spellUntil, nextSpellFx;
    float nextAura, nextGhost, airVy;
    bool wasGrounded = true;

    protected override void Awake()
    {
        base.Awake();
        UseAnims("hero");
        pc = GetComponent<PlayerController>();
        caster = GetComponent<ArteCaster>();
        stats = GetComponent<PlayerStats>();
        pc.Attacked += OnAttack;
        caster.Casted += OnCast;
    }

    static string ClipFor(ArteData a)
    {
        if (!string.IsNullOrEmpty(a.animClip)) return a.animClip;
        switch (a.kind)
        {
            case ArteKind.GroundWave: return "cast_wave";
            case ArteKind.Projectile: return "cast_throw";
            case ArteKind.Launcher: return "cast_launch";
            case ArteKind.Melee: return "cast_spin";
            case ArteKind.AreaAtTarget: return "cast_magic";
            case ArteKind.Strike: return "cast_strike";
            case ArteKind.Heal: return "cast_heal";
        }
        return "cast_wave";
    }

    void OnAttack(int combo)
    {
        comboFx = combo;
        PlayAligned("attack" + combo, "hit", pc.hitDelay, pc.attackDelay + 0.1f);
    }

    void OnCast(ArteData a)
    {
        bool melee = a.kind == ArteKind.Melee;
        spellArte = melee ? null : a;
        spellStart = Time.time; spellUntil = Time.time + ArteExecutor.Duration(a) * 0.8f; nextSpellFx = 0f;
        PlayAligned(ClipFor(a), "release", Mathf.Max(0.06f, a.windup), ArteExecutor.Duration(a));
    }

    protected override void OnAnimEvent(string ev)
    {
        switch (ev)
        {
            case "step": Dust(0.6f); break;
            case "land": Dust(1.3f); break;
            case "hit": SpawnSlash(); Ghost(new Color(1f, 0.45f, 0.45f, 0.5f), 0.22f); break;
        }
    }

    /// <summary>Arc de coup orienté VERS la direction regardée (rotation, jamais de flip).</summary>
    void SpawnSlash()
    {
        float side = FacingRight ? 1f : -1f;
        Vector3 p = transform.position + Vector3.right * side * 1.1f + Vector3.up * 0.1f;
        switch (comboFx)
        {
            case 1: Fx.SpawnDirected("slash_03", p, new Vector2(side, 0.15f), 2.0f, 2.6f, Color.white, 0.16f); break;
            case 2: Fx.SpawnDirected("slash_01", p + Vector3.up * 0.2f, new Vector2(side, -0.7f), 2.0f, 2.6f, Color.white, 0.16f); break;
            default:
                Fx.SpawnDirected("slash_03", p, new Vector2(side, -0.25f), 3.0f, 3.8f, new Color(1f, 0.55f, 0.5f), 0.2f);
                Fx.Spawn("scorch_03", p + Vector3.right * side * 0.3f, 1.0f, 2.2f, new Color(1f, 0.6f, 0.4f), 0.2f, order: 58);
                break;
        }
    }

    protected override void Animate()
    {
        Vector3 v = cc.velocity;
        Vector3 hv = new Vector3(v.x, 0f, v.z);
        float hs = hv.magnitude;
        bool grounded = cc.isGrounded;

        // ---- effets continus (Overlimit : teinte pulsante, aura, traînée) ----
        if (stats != null && stats.InOverlimit)
        {
            baseTint = Color.Lerp(Color.white, new Color(1f, 0.6f, 0.55f), 0.5f + 0.5f * Mathf.Sin(Time.time * 10f));
            if (Time.time >= nextAura)
            {
                nextAura = Time.time + 0.12f;
                Fx.Spawn("circle_05", transform.position + new Vector3(Random.Range(-0.4f, 0.4f), Random.Range(-0.8f, 0.6f), 0f),
                         0.5f, 1.4f, new Color(1f, 0.25f, 0.2f, 0.7f), 0.4f, vel: Vector3.up * 1.5f, order: -1);
            }
            if (Time.time >= nextGhost) { nextGhost = Time.time + 0.09f; Ghost(new Color(1f, 0.3f, 0.3f, 0.35f), 0.3f); }
        }

        // ---- réception après une chute ----
        if (!wasGrounded && grounded && airVy < -5f && !Forced) PlayAction("land", 0.18f);
        wasGrounded = grounded;
        if (!grounded) airVy = v.y;

        // étincelles aux mains pendant l'incantation (plus intenses une fois le sort libéré)
        if (spellArte != null && Time.time < spellUntil && !Forced && Time.time >= nextSpellFx)
        {
            nextSpellFx = Time.time + 0.12f;
            bool charging = Time.time - spellStart < Mathf.Max(0.16f, spellArte.windup);
            float side = FacingRight ? 1f : -1f;
            Vector3 hand = transform.position + CombatUtil.ScreenRight * side * 0.35f + Vector3.up * 0.5f;
            float strength = charging ? 0.55f : 0.9f;
            Fx.Spawn("star_04", hand, 0.18f, strength, spellArte.tint, 0.22f, spin: 180f, order: 62);
            Fx.Spawn("magic_03", hand, 0.12f, strength * 0.65f, spellArte.tint, 0.28f, vel: Vector3.up * 0.35f, order: 61);
        }

        if (Forced || InAction) return;

        if (BattleManager.Instance != null && BattleManager.Instance.State == BattleState.Results) { Play("victory"); return; }
        if (pc.Guarding) { Play("guard"); return; }

        if (!grounded && Mathf.Abs(v.y) > 1f)
        {
            Play(v.y > 3f ? "jump_up" : (v.y < -3f ? "fall" : "jump_peak"));
            return;
        }

        if (hs > 0.4f)
        {
            // on avance dans le sens regardé (marche/course) ou on RECULE en gardant la cible en face (marche arrière)
            float along = Vector3.Dot(hv / hs, transform.forward);
            if (along < -0.3f) Play("walk_back", Mathf.Clamp(hs / 5f, 0.7f, 1.5f));
            else if (hs > 7f)
            {
                Play("run");
                if (Time.time >= nextGhost) { nextGhost = Time.time + 0.07f; Ghost(new Color(0.9f, 0.3f, 0.3f, 0.22f), 0.2f); }
            }
            else Play("walk", Mathf.Clamp(hs / 6f, 0.8f, 1.4f));
            return;
        }
        Play("idle");
    }
}
