using System.Collections;
using UnityEngine;

public enum AllyRole { Fighter, Mage, Healer }

/// <summary>Allié contrôlé par l'IA : combat sur la même cible que le héros, lance ses propres artes, soigne le groupe.</summary>
[RequireComponent(typeof(CharacterController), typeof(Health), typeof(Knockable))]
public class AllyAI : MonoBehaviour
{
    public AllyRole role = AllyRole.Fighter;
    public float maxHp = 120f;
    public float moveSpeed = 5.5f;
    public float meleeDamage = 9f;
    public float maxTp = 60f;
    public ArteData arte;            // Mage : arte offensif, Healer : arte de soin
    public bool Attacking { get; private set; }

    CharacterController cc;
    Health hp;
    Knockable kn;
    Transform hero, target;
    TargetSelector heroSel;
    float vy, tp, nextAct, nextRetarget;
    bool busy;

    void Awake()
    {
        cc = GetComponent<CharacterController>();
        hp = GetComponent<Health>();
        kn = GetComponent<Knockable>();
    }

    void Start()
    {
        hp.team = Team.Player; hp.maxPoise = 30f; hp.Init(maxHp);
        tp = maxTp;
        hp.OnDied += () => Destroy(gameObject, 1.2f);
        var pc = FindFirstObjectByType<PlayerController>();
        if (pc != null) { hero = pc.transform; heroSel = pc.GetComponent<TargetSelector>(); }
    }

    void Retarget()
    {
        nextRetarget = Time.time + 0.5f;
        if (heroSel != null && heroSel.Current != null) { target = heroSel.Current; return; }   // focus du héros
        float best = float.MaxValue; target = null;
        foreach (var e in FindObjectsByType<EnemyAI>(FindObjectsSortMode.None))
        {
            var h = e.GetComponent<Health>();
            if (!e.Active || h.IsDead) continue;
            float d = (e.transform.position - transform.position).sqrMagnitude;
            if (d < best) { best = d; target = e.transform; }
        }
    }

    Vector3 Flat(Vector3 v) { v.y = 0; return v; }

    Vector3 Toward(Vector3 pos, float stop)
    {
        Vector3 to = Flat(pos - transform.position);
        if (to.sqrMagnitude > 0.01f) transform.rotation = Quaternion.LookRotation(to);
        return to.magnitude > stop ? to.normalized * moveSpeed : Vector3.zero;
    }

    void Update()
    {
        if (hp.IsDead) return;
        if (kn.Stunned) { vy = -2f; return; }
        tp = Mathf.Min(maxTp, tp + 2f * Time.deltaTime);

        Vector3 move = Vector3.zero;
        if (!busy)
        {
            if (Time.time >= nextRetarget || target == null || target.GetComponent<Health>().IsDead) Retarget();

            if (!BattleManager.InBattle || target == null)
            {
                if (hero != null && Flat(hero.position - transform.position).magnitude > 3.5f) move = Toward(hero.position, 2.5f);
            }
            else
            {
                float d = Flat(target.position - transform.position).magnitude;
                switch (role)
                {
                    case AllyRole.Fighter:
                        move = Toward(target.position, 1.6f);
                        if (d < 2f && Time.time >= nextAct) StartCoroutine(Melee());
                        break;
                    case AllyRole.Mage:
                        if (d < 4f) { Vector3 away = Flat(transform.position - target.position).normalized; move = away * moveSpeed; transform.rotation = Quaternion.LookRotation(-away); }
                        else if (d > 9f) move = Toward(target.position, 8f);
                        else transform.rotation = Quaternion.LookRotation(Flat(target.position - transform.position));
                        if (arte != null && tp >= arte.tpCost && Time.time >= nextAct) StartCoroutine(CastArte(arte, target));
                        break;
                    case AllyRole.Healer:
                        if (hero != null && Flat(hero.position - transform.position).magnitude > 5f) move = Toward(hero.position, 4f);
                        if (arte != null && tp >= arte.tpCost && Time.time >= nextAct && LowestHpRatio() < 0.55f) StartCoroutine(CastArte(arte, null));
                        break;
                }
            }
        }

        if (cc.isGrounded && vy < 0) vy = -2f;
        vy += -25f * Time.deltaTime;
        move.y = vy;
        cc.Move(move * Time.deltaTime);
    }

    float LowestHpRatio()
    {
        float low = 1f;
        foreach (var h in FindObjectsByType<Health>(FindObjectsSortMode.None))
            if (h.team == Team.Player && !h.IsDead) low = Mathf.Min(low, h.Current / h.maxHp);
        return low;
    }

    IEnumerator Melee()
    {
        busy = true; Attacking = true;
        yield return new WaitForSeconds(0.2f);
        if (!hp.IsDead && !kn.Stunned)
            CombatUtil.Hit(transform.position + transform.forward * 0.9f, 1.4f, Team.Player, meleeDamage, transform.forward, 3f, 12f);
        yield return new WaitForSeconds(0.25f);
        nextAct = Time.time + 0.7f;
        Attacking = false; busy = false;
    }

    IEnumerator CastArte(ArteData a, Transform t)
    {
        busy = true; Attacking = true;
        tp -= a.tpCost;
        yield return ArteExecutor.Run(a, Team.Player, transform, t, 0.8f, null, null);
        nextAct = Time.time + a.cooldown + (role == AllyRole.Healer ? 4f : 1.2f);
        Attacking = false; busy = false;
    }
}
