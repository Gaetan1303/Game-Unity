using UnityEngine;

/// <summary>Sprite 2D d'un monstre : respiration, écrasement avant l'attaque, flash, chute et fondu à la mort.</summary>
[RequireComponent(typeof(EnemyAI))]
public class EnemySprite : SpriteActor
{
    [Tooltip("Fichier de Resources/Sprites/Enemies (slime, brute, imp, cultist, bat, ghost, spider, scorpion, rat)")]
    public string spriteName = "slime";
    public float scale = 1f;
    public bool facesRight = true;

    EnemyAI ai;
    float phase;

    protected override void Awake()
    {
        base.Awake();
        SetBaseFacing(facesRight);
        sr.sprite = Load("Sprites/Enemies/" + spriteName);
        ai = GetComponent<EnemyAI>();
        phase = Random.value * 6f;
    }

    protected override void Animate()
    {
        float speed = new Vector2(cc.velocity.x, cc.velocity.z).magnitude;
        if (ai.Attacking)
        {
            float windup = 0.5f + 0.5f * Mathf.Sin(Time.time * 18f);
            visual.localScale = new Vector3(scale * (1.12f + windup * 0.08f), scale * (0.92f - windup * 0.07f), 1f);
            extraOffset = Vector3.up * (windup * 0.05f);
        }
        else if (speed > 0.15f)
        {
            phase += Time.deltaTime * Mathf.Lerp(8f, 14f, Mathf.Clamp01(speed / Mathf.Max(ai.moveSpeed, 0.1f)));
            float step = Mathf.Abs(Mathf.Sin(phase));
            visual.localScale = new Vector3(scale * (1f + step * 0.04f), scale * (1f - step * 0.05f), 1f);
            extraOffset = Vector3.up * (step * 0.035f);
        }
        else
        {
            float bob = 1f + 0.035f * Mathf.Sin(Time.time * 4f + phase);
            visual.localScale = new Vector3(scale, scale * bob, 1f);
        }

        switch (spriteName)
        {
            case "slime":
                float squash = 1f + Mathf.Sin(Time.time * (ai.Attacking ? 13f : 5f) + phase) * 0.07f;
                visual.localScale = new Vector3(scale * (2f - squash), scale * squash, 1f);
                break;
            case "bat":
                float flap = Mathf.Sin(Time.time * (speed > 0.15f ? 18f : 9f) + phase);
                visual.localScale = new Vector3(scale * (1f + flap * 0.16f), scale * (1f - Mathf.Abs(flap) * 0.08f), 1f);
                extraOffset += Vector3.up * (0.035f + Mathf.Sin(Time.time * 6f + phase) * 0.035f);
                break;
            case "ghost":
                extraOffset += Vector3.up * (0.07f + Mathf.Sin(Time.time * 3f + phase) * 0.08f);
                break;
            case "spider":
            case "scorpion":
                if (speed > 0.15f)
                    visual.localScale = new Vector3(scale * (1f + Mathf.Sin(phase * 2f) * 0.06f), scale, 1f);
                break;
        }
    }
}
