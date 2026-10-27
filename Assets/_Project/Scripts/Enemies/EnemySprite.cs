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
        baseFacesRight = facesRight;
        sr.sprite = Load("Sprites/Enemies/" + spriteName);
        ai = GetComponent<EnemyAI>();
        phase = Random.value * 6f;
    }

    protected override void Animate()
    {
        float bob = 1f + 0.05f * Mathf.Sin(Time.time * 6f + phase);
        if (ai.Attacking) visual.localScale = new Vector3(scale * 1.2f, scale * 0.85f, 1f);
        else visual.localScale = new Vector3(scale, scale * bob, 1f);
    }
}
