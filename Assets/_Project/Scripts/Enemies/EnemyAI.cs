using System.Collections;
using UnityEngine;

[RequireComponent(typeof(CharacterController), typeof(Health), typeof(Knockable))]
public class EnemyAI : MonoBehaviour
{
    public float moveSpeed = 3f;
    public float aggroRange = 14f;
    public float attackRange = 1.8f;
    public float attackDamage = 8f;
    public float attackCooldown = 1.6f;
    public float windup = 0.45f;
    public float maxHp = 60f;
    public float maxPoise = 25f;
    [Tooltip("Ne vacille jamais pendant ses attaques")] public bool superArmor;
    [Tooltip("Toutes les 3 attaques : onde de choc de zone qui projette")] public bool bossSlam;
    [Tooltip("Boss de monde : le tuer conquiert ce monde")]
    public WorldData conquersWorldOnDeath;

    /// <summary>Dormant tant que la rencontre n'a pas commencé.</summary>
    public bool Active { get; set; }
    public bool Attacking => attacking;
    /// <summary>(délai avant le coup, durée totale, onde de choc ?) pour caler l'animation.</summary>
    public event System.Action<float, float, bool> AttackStarted;

    CharacterController cc;
    Health hp;
    Knockable kn;
    Transform target;
    float nextAtk, vy, nextRetarget;
    int atkCount;
    bool attacking;

    void Awake()
    {
        cc = GetComponent<CharacterController>();
        hp = GetComponent<Health>();
        kn = GetComponent<Knockable>();
    }

    void Start()
    {
        hp.team = Team.Enemy;
        hp.maxPoise = maxPoise;
        hp.Init(maxHp);
        hp.OnDied += OnDied;
    }

    void OnDied()
    {
        if (conquersWorldOnDeath != null && GameManager.Instance != null) GameManager.Instance.Conquer(conquersWorldOnDeath);
        Fx.Spawn("circle_02", new Vector3(transform.position.x, 0.05f, transform.position.z), 0.5f, 2.5f, Color.white, 0.4f, flat: true, order: -78);
        Destroy(gameObject, 0.9f);
    }

    void Retarget()
    {
        nextRetarget = Time.time + 0.4f;
        float best = float.MaxValue; target = null;
        foreach (var h in FindObjectsByType<Health>(FindObjectsSortMode.None))
        {
            if (h.team != Team.Player || h.IsDead) continue;
            float d = (h.transform.position - transform.position).sqrMagnitude;
            if (d < best) { best = d; target = h.transform; }
        }
    }

    void Update()
    {
        if (hp.IsDead) return;
        if (kn.Stunned) { vy = -2f; return; }

        Vector3 move = Vector3.zero;
        if (Active && !attacking)
        {
            if (Time.time >= nextRetarget || target == null) Retarget();
            if (target != null)
            {
                Vector3 to = target.position - transform.position; to.y = 0;
                float d = to.magnitude;
                if (d < aggroRange)
                {
                    if (to.sqrMagnitude > 0.01f) transform.rotation = Quaternion.LookRotation(to);
                    if (d > attackRange * 0.9f) move = to.normalized * moveSpeed;
                    else if (Time.time >= nextAtk) StartCoroutine(Attack());
                }
            }
        }

        if (cc.isGrounded && vy < 0) vy = -2f;
        vy += -25f * Time.deltaTime;
        move.y = vy;
        cc.Move(move * Time.deltaTime);
    }

    IEnumerator Attack()
    {
        attacking = true;
        atkCount++;
        bool slam = bossSlam && atkCount % 3 == 0;
        float hitT = slam ? 0.9f : windup;
        AttackStarted?.Invoke(hitT, hitT + 0.35f, slam);
        if (superArmor) hp.armorUntil = Time.time + windup + 0.8f;

        // télégraphe : un "!" au-dessus de la tête
        Fx.Spawn("star_01", transform.position + Vector3.up * (cc.height * 0.5f + 0.9f), 0.6f, 1.2f, new Color(1f, 0.2f, 0.2f), windup, order: 90);

        if (slam)
        {
            float r = 3.4f;
            Vector3 c = new Vector3(transform.position.x, 0.05f, transform.position.z);
            Fx.Spawn("circle_02", c, r * 2f, r * 2f, new Color(1f, 0.15f, 0.1f, 0.8f), 0.9f, flat: true, order: -79);
            Fx.Spawn("circle_05", c, 0.5f, r * 2f, new Color(1f, 0.15f, 0.1f, 0.35f), 0.9f, flat: true, order: -80);
            yield return new WaitForSeconds(0.9f);
            if (!hp.IsDead && !kn.Stunned)
            {
                Fx.Spawn("star_09", c + Vector3.up, 2f, r * 2.2f, new Color(1f, 0.6f, 0.3f), 0.35f, order: 70);
                CombatUtil.Hit(c, r, Team.Enemy, attackDamage * 1.6f, Vector3.zero, 9f, 60f, 7f);
                CameraFollow.Shake(0.4f);
            }
        }
        else
        {
            yield return new WaitForSeconds(windup);
            if (!hp.IsDead && !kn.Stunned)
                CombatUtil.Hit(transform.position + transform.forward * 1.0f, attackRange * 0.75f, Team.Enemy, attackDamage,
                               transform.forward, 5f, 15f);
        }
        yield return new WaitForSeconds(0.3f);
        nextAtk = Time.time + attackCooldown;
        attacking = false;
    }
}
