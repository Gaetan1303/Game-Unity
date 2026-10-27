using System.Collections;
using UnityEngine;

/// <summary>Photon : un cercle runique se dessine au sol, puis une colonne de lumière explose.</summary>
public class AreaBlast : MonoBehaviour
{
    public void Run(Team t, ArteData a, string rune, float dmgMul) { StartCoroutine(Go(t, a, rune, dmgMul)); }

    IEnumerator Go(Team t, ArteData a, string rune, float dmgMul)
    {
        Vector3 c = transform.position;
        Fx.Spawn(rune, c, 0.6f, a.radius * 2f, a.tint, a.blastDelay + 0.05f, spin: 120f, flat: true, order: -80);
        Fx.Spawn("circle_02", c, a.radius * 2f, a.radius * 2f,
                 new Color(a.tint.r, a.tint.g, a.tint.b, 0.6f), a.blastDelay + 0.05f, flat: true, order: -79);
        yield return new WaitForSeconds(a.blastDelay);

        for (int i = 0; i < a.hits; i++)
        {
            Fx.Spawn("star_09", c + Vector3.up * 1.2f, a.radius * 1.0f, a.radius * 2.6f, Color.white, 0.35f, spin: 60f, order: 70);
            Fx.Spawn("star_04", c + Vector3.up * 3f, 1.4f, 0.6f, a.tint, 0.4f, stretchY: 5f, order: 69);
            Fx.Spawn("circle_02", c + Vector3.up * 0.1f, a.radius * 0.6f, a.radius * 2.6f, Color.white, 0.4f, flat: true, order: -78);
            CombatUtil.Hit(c, a.radius, t, a.damage * dmgMul, Vector3.zero, a.knockback, a.poise, a.launch, a.knockdown, true);
            if (i < a.hits - 1) yield return new WaitForSeconds(a.hitInterval);
        }
        Destroy(gameObject, 0.1f);
    }
}
