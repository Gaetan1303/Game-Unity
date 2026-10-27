using UnityEngine;

public struct DamageInfo
{
    public float amount;
    public Vector3 knockback;   // impulsion horizontale
    public float poiseDamage;   // fait vaciller quand l'équilibre tombe à 0
    public float launch;        // vitesse verticale > 0 = projection en l'air
    public bool knockdown;      // met à terre
    public bool magic;
    public Team source;
}
