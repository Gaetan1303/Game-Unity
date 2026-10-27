using System.Collections.Generic;
using UnityEngine;

public class ArteProjectile : MonoBehaviour
{
    Vector3 dir; float speed, range, damage, knockback, radius, poise, launch, travelled, nextTrail;
    bool pierce, knockdown;
    Team team; Color tint; Transform homing;
    readonly HashSet<Health> hit = new HashSet<Health>();

    public void Init(Team t, Vector3 direction, ArteData a, float dmgMul, bool piercing, Transform homingTarget)
    {
        team = t; dir = direction.normalized; speed = a.projectileSpeed; range = a.range;
        damage = a.damage * dmgMul; knockback = a.knockback; radius = a.radius; poise = a.poise;
        launch = a.launch; knockdown = a.knockdown; tint = a.tint; pierce = piercing; homing = homingTarget;
    }

    void Update()
    {
        float dt = Time.deltaTime;
        if (homing != null)
        {
            Vector3 want = (homing.position + Vector3.up * 0.2f - transform.position).normalized;
            dir = Vector3.RotateTowards(dir, want, 4f * dt, 0f).normalized;
        }
        float step = speed * dt;
        transform.position += dir * step;
        travelled += step;

        if (Time.time >= nextTrail)
        {
            nextTrail = Time.time + 0.05f;
            Fx.Spawn("circle_05", transform.position, radius * 1.4f, radius * 0.3f,
                     new Color(tint.r, tint.g, tint.b, 0.5f), 0.3f, order: 55);
        }

        int n = CombatUtil.Hit(transform.position, radius, team, damage, dir, knockback, poise, launch, knockdown, !pierce, hit);
        if (n > 0 && !pierce)
        {
            Fx.Spawn("star_09", transform.position, radius * 1.5f, radius * 3.5f, tint, 0.3f, spin: 90f, order: 70);
            Destroy(gameObject); return;
        }
        if (travelled >= range) Destroy(gameObject);
    }
}
