using UnityEngine;

/// <summary>
/// Sprite 2D d'un monstre animé par pose (Resources/Animations/creature.json) : respiration, démarche propre à chaque type
/// (saut, vol, trottinement, piétinement), anticipation puis coup, flash, chute, mise à terre, mort.
/// Échelle ENTIÈRE uniquement : un grand monstre = un sprite plus grand (ex. brute_boss 48 px), jamais un agrandissement.
/// </summary>
[RequireComponent(typeof(EnemyAI))]
public class EnemySprite : SpriteActor
{
    [Tooltip("Fichier de Resources/Sprites/Enemies (slime, bat, spider, cultist, imp, ghost, scorpion, rat, brute, brute_boss)")]
    public string spriteName = "slime";
    [Tooltip("Garder 1 (norme pixel-art : pas d'échelle non entière)")]
    public float scale = 1f;
    public string idleClip = "idle";
    [Tooltip("walk_waddle | walk_hop | walk_scuttle | walk_flap | walk_stomp")]
    public string walkClip = "walk_waddle";
    [Tooltip("Hauteur de vol stationnaire (chauve-souris...)")]
    public float hover = 0f;

    EnemyAI ai;

    protected override void Awake()
    {
        base.Awake();
        UseAnims("creature");
        selfSprite = Load("Sprites/Enemies/" + spriteName);
        sr.sprite = selfSprite;
        baseScale = Vector3.one * scale;
        ai = GetComponent<EnemyAI>();
        ai.AttackStarted += (hitTime, total, slam) => PlayAligned(slam ? "slam" : "attack", "hit", hitTime, total);
    }

    protected override void OnAnimEvent(string ev)
    {
        if (ev == "stomp") CameraFollow.Shake(0.04f);
        if (ev == "hit" && ai != null && ai.Attacking && walkClip == "walk_stomp") CameraFollow.Shake(0.06f);
    }

    protected override void Animate()
    {
        if (!Forced) extraOffset = Vector3.up * hover;
        if (Forced || InAction) return;
        bool moving = ai.Active && cc.velocity.sqrMagnitude > 0.09f;
        Play(moving ? walkClip : idleClip);
    }
}
