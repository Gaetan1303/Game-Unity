using UnityEngine;

public enum ArteKind { GroundWave, AreaAtTarget, Melee, Launcher, Projectile, Strike, Heal }

[CreateAssetMenu(menuName = "Jeu/Arte", fileName = "NouvelArte")]
public class ArteData : ScriptableObject
{
    public string displayName = "Arte";
    [TextArea] public string description;
    public ArteKind kind = ArteKind.GroundWave;

    [Header("Coût & rythme")]
    public float tpCost = 8f;
    public float cooldown = 1f;
    public float windup = 0.2f;
    public float recovery = 0.3f;

    [Header("Dégâts")]
    public float damage = 20f;
    public float knockback = 10f;
    public float poise = 25f;       // dégâts d'équilibre (fait vaciller)
    public float launch = 0f;       // > 0 : projette en l'air
    public bool knockdown;
    public int hits = 1;
    public float hitInterval = 0.12f;

    [Header("Portée / zone")]
    public float range = 7f;
    public float radius = 1f;
    public float projectileSpeed = 14f;
    public float blastDelay = 0.5f;
    [Range(0f, 1f)] public float healPercent = 0.35f;

    [Header("Visuel")]
    [Tooltip("Nom d'un sprite de Resources/Sprites/Fx (vide = valeur par défaut selon le type)")]
    public string fxSprite;
    public Color tint = Color.white;
}
