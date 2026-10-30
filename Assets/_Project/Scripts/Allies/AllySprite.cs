using UnityEngine;

/// <summary>Allié animé par pose (creature.json) : trottinement, attaque (guerrier) ou incantation (mage, soigneuse).</summary>
[RequireComponent(typeof(AllyAI))]
public class AllySprite : SpriteActor
{
    [Tooltip("Fichier de Resources/Sprites/Allies : knight, mage, healer")]
    public string spriteName = "knight";
    AllyAI ai;
    bool wasAttacking;
    float nextCastFx;

    protected override void Awake()
    {
        base.Awake();
        UseAnims("creature");
        selfSprite = Load("Sprites/Allies/" + spriteName);
        sr.sprite = selfSprite;
        ai = GetComponent<AllyAI>();
    }

    protected override void Animate()
    {
        if (ai.Attacking && !wasAttacking && !Forced)
        {
            if (ai.role == AllyRole.Fighter) PlayAligned("attack", "hit", 0.2f, 0.45f);
            else PlayAligned("cast", "release", 0.25f, 0.6f);
        }
        wasAttacking = ai.Attacking;
        if (ai.role == AllyRole.Mage && ai.Casting && Time.time >= nextCastFx)
        {
            nextCastFx = Time.time + 0.14f;
            Color tint = ai.arte != null ? ai.arte.tint : new Color(0.4f, 0.75f, 1f);
            float side = FacingRight ? 1f : -1f;
            Vector3 hand = transform.position + CombatUtil.ScreenRight * side * 0.28f + Vector3.up * 0.4f;
            Fx.Spawn("star_04", hand, 0.12f, 0.48f, tint, 0.24f, spin: 160f, order: 62);
            Fx.Spawn("magic_03", hand, 0.1f, 0.34f, tint, 0.3f, vel: Vector3.up * 0.3f, order: 61);
        }
        if (Forced || InAction) return;
        Play(cc.velocity.sqrMagnitude > 0.09f ? "walk_waddle" : "idle");
    }
}
