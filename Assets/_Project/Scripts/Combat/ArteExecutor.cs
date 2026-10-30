using System.Collections;
using UnityEngine;

/// <summary>Exécute les effets d'un arte. Partagé entre le héros et les alliés IA.</summary>
public static class ArteExecutor
{
    public static string FxName(ArteData a)
    {
        if (!string.IsNullOrEmpty(a.fxSprite)) return a.fxSprite;
        switch (a.kind)
        {
            case ArteKind.GroundWave: return "slash_03";
            case ArteKind.AreaAtTarget: return "magic_01";
            case ArteKind.Launcher: return "slash_02";
            case ArteKind.Projectile: return "star_09";
            case ArteKind.Strike: return "spark_05";
            case ArteKind.Heal: return "circle_02";
            default: return "twirl_01";
        }
    }

    public static float Duration(ArteData a)
    {
        float extra = a.kind == ArteKind.Melee ? a.hits * a.hitInterval : (a.kind == ArteKind.Strike ? a.blastDelay : 0f);
        return a.windup + extra + a.recovery;
    }

    public static IEnumerator Run(ArteData a, Team team, Transform self, Transform target, float dmgMul,
                                  System.Action<int> onHits, System.Action onRecovery)
    {
        Vector3 feet = new Vector3(self.position.x, 0.05f, self.position.z);
        Fx.Spawn("magic_01", feet, 1.2f, 2.8f, a.tint, a.windup + 0.25f, spin: 200f, flat: true, order: -80);
        yield return new WaitForSeconds(a.windup);

        Vector3 dir = self.forward;
        float dmg = a.damage * dmgMul;

        switch (a.kind)
        {
            case ArteKind.GroundWave:
            {
                Vector3 pos = self.position + dir * 1f + Vector3.down * 0.1f;
                float size = a.radius * 3f;
                float side = CombatUtil.ScreenFacingDot(self) < -0.01f ? -1f : 1f;
                var fx = Fx.SpawnDirected(FxName(a), pos, new Vector2(side, 0f), size, size, a.tint, float.PositiveInfinity, 60);
                var go = fx != null ? fx.gameObject : new GameObject("Wave");
                go.transform.position = pos;
                go.AddComponent<ArteProjectile>().Init(team, dir, a, dmgMul, true, null);
                Fx.Spawn("scorch_03", pos, 1f, 2.2f, a.tint, 0.25f, order: 58);
                break;
            }
            case ArteKind.Projectile:
            {
                Vector3 pos = self.position + dir * 1f + Vector3.up * 0.2f;
                var fx = Fx.Spawn(FxName(a), pos, 1.4f, 1.4f, a.tint, float.PositiveInfinity, spin: 360f, order: 60);
                var go = fx != null ? fx.gameObject : new GameObject("Orb");
                go.transform.position = pos;
                go.AddComponent<ArteProjectile>().Init(team, dir, a, dmgMul, false, target);
                break;
            }
            case ArteKind.Launcher:
            {
                float uside = CombatUtil.ScreenFacingDot(self) < -0.01f ? -1f : 1f;
                Fx.SpawnDirected(FxName(a), self.position + dir * 1.3f + Vector3.up * 0.6f,
                                 new Vector2(uside * 0.5f, 1f), 2.0f, 3.8f, a.tint, 0.22f, 60);
                int n = CombatUtil.Hit(self.position + dir * 1.2f, a.radius, team, dmg, dir, a.knockback, a.poise, a.launch, a.knockdown);
                onHits?.Invoke(n);
                break;
            }
            case ArteKind.AreaAtTarget:
            {
                Vector3 pos = target != null ? target.position : self.position + dir * a.range * 0.5f;
                pos.y = 0.05f;
                var go = new GameObject("Photon");
                go.transform.position = pos;
                go.AddComponent<AreaBlast>().Run(team, a, FxName(a), dmgMul);
                break;
            }
            case ArteKind.Strike:
            {
                Vector3 pos = target != null ? target.position : self.position + dir * 4f;
                pos.y = 0.05f;
                Fx.Spawn("circle_02", pos, a.radius * 2f, a.radius * 0.6f, a.tint, a.blastDelay, flat: true, order: -79);
                yield return new WaitForSeconds(a.blastDelay);
                Fx.Spawn(FxName(a), pos + Vector3.up * 3f, 2.4f, 2.4f, a.tint, 0.28f, stretchY: 3.2f, order: 70);
                Fx.Spawn("star_09", pos + Vector3.up * 0.8f, a.radius, a.radius * 2.5f, Color.white, 0.3f, order: 71);
                int n = CombatUtil.Hit(pos, a.radius, team, dmg, Vector3.zero, a.knockback, a.poise, a.launch, a.knockdown, true);
                onHits?.Invoke(n);
                break;
            }
            case ArteKind.Melee:
            {
                for (int i = 0; i < a.hits; i++)
                {
                    Vector3 c = self.position;
                    Fx.Spawn(FxName(a), c, a.radius * 1.6f, a.radius * 2.4f, a.tint, a.hitInterval * 2f,
                             spin: (i % 2 == 0 ? 900f : -900f), angle: i * 72f, order: 60);
                    Fx.Spawn("slash_01", c + new Vector3(UnityEngine.Random.Range(-1f, 1f), UnityEngine.Random.Range(-0.3f, 0.6f), 0f),
                             1.4f, 2.2f, Color.white, 0.18f, angle: UnityEngine.Random.Range(0f, 360f), order: 61);
                    bool last = i == a.hits - 1;
                    int n = CombatUtil.Hit(c, a.radius, team, dmg, last ? dir : Vector3.zero,
                                           last ? a.knockback * 2f : a.knockback * 0.2f,
                                           last ? a.poise : 5f, 0f, last && a.knockdown);
                    onHits?.Invoke(n);
                    yield return new WaitForSeconds(a.hitInterval);
                }
                break;
            }
            case ArteKind.Heal:
            {
                foreach (var h in Object.FindObjectsByType<Health>(FindObjectsSortMode.None))
                {
                    if (h.team != team || h.IsDead) continue;
                    if (Vector3.Distance(h.transform.position, self.position) > 14f) continue;
                    h.Heal(h.maxHp * a.healPercent);
                    Vector3 p = new Vector3(h.transform.position.x, 0.05f, h.transform.position.z);
                    Fx.Spawn("circle_02", p, 0.8f, 3f, a.tint, 0.7f, flat: true, order: -78);
                    Fx.Spawn("star_09", p + Vector3.up * 1.2f, 1.0f, 2.2f, a.tint, 0.6f, vel: Vector3.up * 1.2f, order: 70);
                }
                break;
            }
        }

        onRecovery?.Invoke();
        yield return new WaitForSeconds(a.recovery);
    }
}
