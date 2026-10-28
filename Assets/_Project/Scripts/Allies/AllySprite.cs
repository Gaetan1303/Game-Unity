using UnityEngine;

[RequireComponent(typeof(AllyAI))]
public class AllySprite : SpriteActor
{
    [Tooltip("Fichier de Resources/Sprites/Allies : knight, mage, healer")]
    public string spriteName = "knight";
    public float scale = 1f;
    AllyAI ai;
    float phase, nextCastFx;

    protected override void Awake()
    {
        base.Awake();
        sr.sprite = Load("Sprites/Allies/" + spriteName);
        ai = GetComponent<AllyAI>();
        phase = Random.value * 6f;
    }

    protected override void Animate()
    {
        if (ai.role == AllyRole.Healer)
        {
            float healerBob = 1f + 0.04f * Mathf.Sin(Time.time * 5f + phase);
            if (ai.Attacking) visual.localScale = new Vector3(scale * 1.15f, scale * 0.9f, 1f);
            else visual.localScale = new Vector3(scale, scale * healerBob, 1f);
            return;
        }

        float speed = new Vector2(cc.velocity.x, cc.velocity.z).magnitude;
        if (ai.Attacking)
        {
            float pulse = Mathf.Sin(Time.time * 16f);
            visual.localScale = new Vector3(scale * (1.12f + pulse * 0.025f), scale * (0.9f - pulse * 0.02f), 1f);
            extraOffset = Vector3.up * (0.04f + Mathf.Abs(pulse) * 0.04f);
        }
        else if (speed > 0.15f)
        {
            phase += Time.deltaTime * Mathf.Lerp(8f, 13f, Mathf.Clamp01(speed / 5.5f));
            float step = Mathf.Abs(Mathf.Sin(phase));
            visual.localScale = new Vector3(scale * (1f + step * 0.025f), scale * (1f - step * 0.035f), 1f);
            extraOffset = Vector3.up * (step * 0.035f);
        }
        else
        {
            float breath = 1f + 0.025f * Mathf.Sin(Time.time * 3f + phase);
            visual.localScale = new Vector3(scale, scale * breath, 1f);
        }

        if (ai.role == AllyRole.Mage && ai.Casting && Time.time >= nextCastFx)
        {
            nextCastFx = Time.time + 0.14f;
            Color tint = ai.arte != null ? ai.arte.tint : new Color(0.4f, 0.75f, 1f);
            Vector3 hand = transform.position + transform.forward * 0.28f + Vector3.up * 0.62f;
            Fx.Spawn("star_04", hand, 0.12f * scale, 0.48f * scale, tint, 0.24f,
                     spin: 160f, order: 62);
            Fx.Spawn("magic_03", hand, 0.1f * scale, 0.34f * scale, tint, 0.3f,
                     vel: Vector3.up * 0.3f, order: 61);
        }
    }
}
