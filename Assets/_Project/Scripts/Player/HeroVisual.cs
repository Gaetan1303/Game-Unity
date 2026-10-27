using UnityEngine;

/// <summary>Anime le sprite du démon : idle / marche / saut / garde / pose d'attaque / sonné + effets de coups.</summary>
public class HeroVisual : SpriteActor
{
    Sprite[] idle, walk, jump;
    Sprite guard, cast;
    PlayerController pc;
    ArteCaster caster;
    PlayerStats stats;
    float attackT, lunge, nextAura;
    static readonly int[] IdlePattern = { 0, 1, 2, 1 };

    protected override void Awake()
    {
        base.Awake();
        baseFacesRight = false; // les frames de marche regardent vers la gauche
        idle = LoadSet("idle_", 3); walk = LoadSet("walk_", 4); jump = LoadSet("jump_", 3);
        guard = Load("Sprites/Hero/guard"); cast = Load("Sprites/Hero/cast");
        pc = GetComponent<PlayerController>();
        caster = GetComponent<ArteCaster>();
        stats = GetComponent<PlayerStats>();
        pc.Attacked += OnAttack;
        caster.Casted += a => { attackT = ArteExecutor.Duration(a) * 0.8f; lunge = 0.15f; };
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
        lunge = combo == 3 ? 0.8f : 0.5f;
        string[] names = { "slash_03", "slash_04", "slash_02" };
        float size = combo == 3 ? 3.6f : 2.4f;
        Vector3 p = transform.position + transform.forward * 1.1f + Vector3.up * 0.2f;
        Fx.Spawn(names[combo - 1], p, size * 0.8f, size, combo == 3 ? new Color(1f, 0.5f, 0.5f) : Color.white,
                 0.16f, flipX: !FacingRight, order: 60);
    }

    protected override void Animate()
    {
        float dt = Time.deltaTime;
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
        else if (attackT > 0f)
        {
            attackT -= dt;
            sr.sprite = cast;
            lunge = Mathf.MoveTowards(lunge, 0f, dt * 2.5f);
            extraOffset = Vector3.right * (FacingRight ? 1f : -1f) * lunge;
        }
        else if (pc.Guarding) sr.sprite = guard;
        else if (!cc.isGrounded && Mathf.Abs(v.y) > 1f) sr.sprite = v.y > 3f ? jump[0] : (v.y < -3f ? jump[2] : jump[1]);
        else if (hs > 0.4f) sr.sprite = walk[(int)(Time.time * 10f) % walk.Length];
        else sr.sprite = idle[IdlePattern[(int)(Time.time * 4f) % IdlePattern.Length]];
    }
}
