using System.Collections.Generic;
using UnityEngine;

public static class CombatUtil
{
    public const float VerticalTolerance = 2.6f;
    /// <summary>(cible, dégâts, dégâts réellement infligés)</summary>
    public static event System.Action<Health, DamageInfo, float> HitLanded;

    /// <summary>Zone cylindrique 2,5D : rayon horizontal + tolérance verticale (permet de toucher les ennemis projetés).</summary>
    public static int Hit(Vector3 center, float radius, Team attacker, float damage, Vector3 dir, float knockback,
                          float poise = 15f, float launch = 0f, bool knockdown = false, bool magic = false,
                          HashSet<Health> already = null)
    {
        var seen = already ?? new HashSet<Health>();
        int n = 0;
        float scale = 1f;
        if (attacker == Team.Player && ComboTracker.Instance != null) scale = ComboTracker.Instance.DamageScale;

        foreach (var c in Physics.OverlapSphere(center, radius + VerticalTolerance + 0.5f))
        {
            var h = c.GetComponentInParent<Health>();
            if (h == null || h.team == attacker || h.IsDead || seen.Contains(h)) continue;

            Vector3 p = h.transform.position;
            float dx = p.x - center.x, dz = p.z - center.z;
            float cr = h.TryGetComponent(out CharacterController hcc) ? hcc.radius : 0.4f;
            if (Mathf.Sqrt(dx * dx + dz * dz) > radius + cr) continue;
            if (Mathf.Abs(p.y - center.y) > VerticalTolerance) continue;
            seen.Add(h);

            Vector3 d = dir.sqrMagnitude < 0.001f ? (p - center) : dir;
            d.y = 0f;
            d = d.sqrMagnitude < 0.0001f ? Vector3.right : d.normalized;

            var info = new DamageInfo
            {
                amount = damage * scale, knockback = d * knockback, poiseDamage = poise,
                launch = launch, knockdown = knockdown, magic = magic, source = attacker
            };
            float dealt = h.TakeDamage(info);
            if (dealt <= 0f && info.amount > 0f) continue;   // garde parfaite

            Fx.Hit(p + Vector3.up * 1f);
            HitLanded?.Invoke(h, info, dealt);
            bool big = launch > 0f || poise >= 30f || knockdown;
            if (GameManager.Instance != null) GameManager.Instance.HitStop(big ? 0.07f : 0.035f);
            CameraFollow.Shake(big ? 0.25f : 0.08f);
            n++;
        }
        return n;
    }
}
