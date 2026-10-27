using UnityEngine;

[RequireComponent(typeof(AllyAI))]
public class AllySprite : SpriteActor
{
    [Tooltip("Fichier de Resources/Sprites/Allies : knight, mage, healer")]
    public string spriteName = "knight";
    public float scale = 1f;
    AllyAI ai;
    float phase;

    protected override void Awake()
    {
        base.Awake();
        sr.sprite = Load("Sprites/Allies/" + spriteName);
        ai = GetComponent<AllyAI>();
        phase = Random.value * 6f;
    }

    protected override void Animate()
    {
        float bob = 1f + 0.04f * Mathf.Sin(Time.time * 5f + phase);
        if (ai.Attacking) visual.localScale = new Vector3(scale * 1.15f, scale * 0.9f, 1f);
        else visual.localScale = new Vector3(scale, scale * bob, 1f);
    }
}
