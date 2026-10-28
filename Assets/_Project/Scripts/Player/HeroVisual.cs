using UnityEngine;

/// <summary>Anime le sprite du démon : idle / marche / saut / garde / pose d'attaque / sonné + effets de coups.</summary>
public class HeroVisual : SpriteActor
{
    Sprite[] idle, walk, jump;
    Sprite guard, cast;
    PlayerController pc;
    ArteCaster caster;
    PlayerStats stats;
    float attackT, spellT, spellDuration, spellAge, nextSpellFx, lunge, nextAura;
    float walkPhase;
    ArteData spellArte;
    static readonly int[] IdlePattern = { 0, 1, 2, 1 };

    protected override void Awake()
    {
        base.Awake();
        SetBaseFacing(false); // les frames du héros regardent vers la gauche
        idle = LoadSet("idle_", 3); walk = LoadSet("walk_", 4); jump = LoadSet("jump_", 3);
        guard = Load("Sprites/Hero/guard"); cast = Load("Sprites/Hero/cast");
        pc = GetComponent<PlayerController>();
        caster = GetComponent<ArteCaster>();
        stats = GetComponent<PlayerStats>();
        pc.Attacked += OnAttack;
        caster.Casted += OnArte;
    }

    static Sprite[] LoadSet(string prefix, int n)
    {
        var a = new Sprite[n];
        for (int i = 0; i < n; i++) a[i] = Load($"Sprites/Hero/{prefix}{i}");
        return a;
    }

    void OnAttack(int combo)
    {
        attackT = 0.28f;
        spellT = 0f;
        lunge = combo == 3 ? 0.8f : 0.5f;
        string[] names = { "slash_03", "slash_04", "slash_02" };
        float size = combo == 3 ? 3.6f : 2.4f;
        Vector3 p = transform.position + transform.forward * 1.1f + Vector3.up * 0.2f;
        Fx.Spawn(names[combo - 1], p, size * 0.8f, size, combo == 3 ? new Color(1f, 0.5f, 0.5f) : Color.white,
                 0.16f, flipX: CombatUtil.ScreenFacingDot(transform) < -0.01f, order: 60);
    }

    void OnArte(ArteData arte)
    {
        float duration = Mathf.Max(0.18f, ArteExecutor.Duration(arte) * 0.8f);
        lunge = 0.15f;

        if (arte.kind == ArteKind.Melee)
        {
            attackT = duration;
            spellT = 0f;
            spellArte = null;
            return;
        }

        attackT = 0f;
        spellT = spellDuration = duration;
        spellAge = 0f;
        nextSpellFx = 0f;
        spellArte = arte;
    }

    protected override void Animate()
    {
        float dt = Time.deltaTime;
        attackT = Mathf.Max(0f, attackT - dt);
        spellT = Mathf.Max(0f, spellT - dt);
        if (spellT > 0f) spellAge += dt;
        Vector3 v = cc.velocity;
        float hs = new Vector2(v.x, v.z).magnitude;

        // aura d'Overlimit
        if (stats != null && stats.InOverlimit && Time.time >= nextAura)
        {
            nextAura = Time.time + 0.12f;
            Fx.Spawn("circle_05", transform.position + new Vector3(Random.Range(-0.4f, 0.4f), Random.Range(-0.8f, 0.6f), 0f),
                     0.5f, 1.4f, new Color(1f, 0.25f, 0.2f, 0.7f), 0.4f, vel: Vector3.up * 1.5f, order: -1);
        }

        if (kn != null && kn.Stunned) sr.sprite = jump[2];
        else if (spellT > 0f)
        {
            float progress = 1f - Mathf.Clamp01(spellT / spellDuration);
            float pulse = Mathf.Sin(progress * Mathf.PI);
            bool charging = spellArte != null && spellAge < Mathf.Max(0.16f, spellArte.windup);
            sr.sprite = charging ? cast : idle[1];
            visual.localScale = new Vector3(1f + pulse * 0.06f, 1f + pulse * 0.1f, 1f);
            extraOffset = Vector3.up * (0.04f + pulse * 0.12f)
                        + CombatUtil.ScreenRight * (FacingRight ? 1f : -1f) * lunge;
            lunge = Mathf.MoveTowards(lunge, 0f, dt * 2.5f);
            if (spellArte != null && spellAge >= nextSpellFx)
            {
                nextSpellFx = spellAge + 0.12f;
                Vector3 hand = transform.position + transform.forward * 0.32f + Vector3.up * 0.72f;
                float strength = charging ? 0.55f : 0.9f;
                Fx.Spawn("star_04", hand, 0.18f, strength, spellArte.tint, 0.22f,
                         spin: 180f, order: 62);
                Fx.Spawn("magic_03", hand, 0.12f, strength * 0.65f, spellArte.tint, 0.28f,
                         vel: Vector3.up * 0.35f, order: 61);
            }
        }
        else if (attackT > 0f)
        {
            sr.sprite = walk[Mathf.FloorToInt(Time.time * 16f) % 2 == 0 ? 1 : 3];
            lunge = Mathf.MoveTowards(lunge, 0f, dt * 2.5f);
            extraOffset = CombatUtil.ScreenRight * (FacingRight ? 1f : -1f) * lunge;
        }
        else if (pc.Guarding) sr.sprite = guard;
        else if (!cc.isGrounded)
        {
            sr.sprite = v.y > 0.5f ? jump[0] : (v.y < -0.5f ? jump[2] : jump[1]);
            walkPhase = 0f;
        }
        else if (hs > 0.4f)
        {
            float cadence = pc.FreeRun ? 10f : 8f;
            walkPhase += dt * cadence * Mathf.Clamp(hs / Mathf.Max(pc.moveSpeed, 0.1f), 0.65f, 1.35f);
            sr.sprite = walk[Mathf.FloorToInt(walkPhase) % walk.Length];
            float step = Mathf.Abs(Mathf.Sin(walkPhase * Mathf.PI));
            visual.localScale = new Vector3(1f + step * 0.02f, 1f - step * 0.03f, 1f);
            extraOffset = Vector3.up * (step * 0.025f);
        }
        else
        {
            walkPhase = 0f;
            sr.sprite = idle[IdlePattern[(int)(Time.time * 2.6f) % IdlePattern.Length]];
            float breath = Mathf.Sin(Time.time * 2.6f);
            visual.localScale = new Vector3(1f, 1f + breath * 0.012f, 1f);
            extraOffset = Vector3.up * (0.015f + breath * 0.012f);
        }
    }
}
