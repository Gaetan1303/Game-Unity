using System.Collections.Generic;
using UnityEngine;

/// <summary>Verrouille une cible en combat (la ligne du LMBS). Tab / RB change de cible.</summary>
public class TargetSelector : MonoBehaviour
{
    public float maxRange = 20f;
    public Transform Current { get; private set; }
    float next;

    List<Transform> Enemies()
    {
        var list = new List<Transform>();
        foreach (var e in FindObjectsByType<EnemyAI>(FindObjectsSortMode.None))
        {
            var h = e.GetComponent<Health>();
            if (!e.Active || h == null || h.IsDead) continue;
            if (Vector3.Distance(transform.position, e.transform.position) > maxRange) continue;
            list.Add(e.transform);
        }
        list.Sort((a, b) => Vector3.Distance(transform.position, a.position).CompareTo(Vector3.Distance(transform.position, b.position)));
        return list;
    }

    void Update()
    {
        if (!BattleManager.InBattle) { Current = null; return; }
        if (Current != null)
        {
            var h = Current.GetComponent<Health>();
            if (h == null || h.IsDead) Current = null;
        }
        if (Current == null && Time.time >= next)
        {
            next = Time.time + 0.15f;
            var l = Enemies();
            Current = l.Count > 0 ? l[0] : null;
        }
    }

    public void Cycle()
    {
        var l = Enemies();
        if (l.Count == 0) return;
        int i = l.IndexOf(Current);
        Current = l[(i + 1) % l.Count];
    }
}
