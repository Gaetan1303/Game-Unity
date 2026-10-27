using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public enum ArteDir { Neutral, Forward, Up, Down }

/// <summary>
/// Déplacement façon LMBS : en combat on est verrouillé sur la cible et on avance/recule le long de la LIGNE vers elle.
/// Maj = course libre (3D). Attaque + direction = artes standard, Technique + direction = artes techniques.
/// </summary>
[RequireComponent(typeof(CharacterController), typeof(Health), typeof(Knockable))]
public class PlayerController : MonoBehaviour
{
    [Header("Mouvement")]
    public float moveSpeed = 6f;
    public float freeRunSpeed = 8f;
    public float jumpHeight = 1.5f;
    public float gravity = -25f;
    public float turnSpeed = 1080f;

    [Header("Attaque normale (combo x3)")]
    public float attackDamage = 12f;
    public float attackReach = 1.6f;
    public float attackRadius = 1.4f;
    public float attackDelay = 0.32f;
    public float comboWindow = 0.8f;

    CharacterController cc;
    Health hp;
    Knockable kn;
    TargetSelector sel;
    PlayerStats stats;
    ArteCaster caster;
    float vy, lockUntil, nextAttack, lastAttack, bufferedAttack = -1f, guardStart;
    int combo;

    public float cancelUntil { get; private set; }
    public bool FreeRun { get; private set; }
    public bool Guarding { get; private set; }
    public bool CanAct => !kn.Stunned && Time.time >= lockUntil;
    public bool Locked => Time.time < lockUntil || kn.Stunned;
    public event System.Action<int> Attacked;

    void Awake()
    {
        cc = GetComponent<CharacterController>();
        hp = GetComponent<Health>();
        kn = GetComponent<Knockable>();
        sel = GetComponent<TargetSelector>();
        stats = GetComponent<PlayerStats>();
        caster = GetComponent<ArteCaster>();
    }

    void OnEnable() { CombatUtil.HitLanded += OnHitLanded; }
    void OnDisable() { CombatUtil.HitLanded -= OnHitLanded; }

    // un coup qui touche ouvre une fenêtre d'annulation : on peut enchaîner un arte
    void OnHitLanded(Health t, DamageInfo d, float dealt) { if (d.source == Team.Player) cancelUntil = Time.time + 0.5f; }

    void Start()
    {
        hp.OnDied += () => StartCoroutine(Reload());
        hp.OnPerfectGuard += () =>
        {
            Fx.Spawn("star_08", transform.position + Vector3.up * 1f, 1f, 3.2f, new Color(0.5f, 0.8f, 1f), 0.3f, order: 80);
            if (GameManager.Instance != null) GameManager.Instance.HitStop(0.08f);
            stats.AddTp(10f);
        };
    }

    IEnumerator Reload()
    {
        yield return new WaitForSecondsRealtime(2f);
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    public void SetLock(float seconds) { lockUntil = Time.time + seconds; }

    public void FaceTarget()
    {
        if (sel == null || sel.Current == null) return;
        Vector3 d = sel.Current.position - transform.position; d.y = 0;
        if (d.sqrMagnitude > 0.01f) transform.rotation = Quaternion.LookRotation(d);
    }

    ArteDir ReadDir(Vector2 inp, Transform tgt)
    {
        if (inp.y > 0.5f) return ArteDir.Up;
        if (inp.y < -0.5f) return ArteDir.Down;
        float s;
        if (tgt != null) { float dx = tgt.position.x - transform.position.x; s = Mathf.Abs(dx) > 0.05f ? Mathf.Sign(dx) : 1f; }
        else s = transform.forward.x >= 0f ? 1f : -1f;
        return inp.x * s > 0.5f ? ArteDir.Forward : ArteDir.Neutral;
    }

    void Rotate(Vector3 dir)
    {
        if (dir.sqrMagnitude < 0.0001f) return;
        transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(dir), turnSpeed * Time.deltaTime);
    }

    void Update()
    {
        if (hp.IsDead) return;

        Vector2 inp = GameInput.Move();
        FreeRun = GameInput.FreeRunHeld();
        Transform tgt = sel.Current;

        // garde (une garde déclenchée juste avant un coup = garde parfaite)
        bool wantGuard = GameInput.GuardHeld() && !kn.Stunned && !caster.IsCasting && Time.time >= lockUntil && cc.isGrounded;
        if (wantGuard && !Guarding) guardStart = Time.time;
        Guarding = wantGuard;
        hp.guarding = Guarding; hp.guardStartTime = guardStart;

        if (kn.Stunned) { vy = -2f; return; }   // Knockable gère le déplacement

        if (GameInput.SwitchTargetPressed()) sel.Cycle();
        if (GameInput.OverlimitPressed()) stats.TryOverlimit();
        if (GameInput.ItemPressed()) stats.UseItem();

        // --- actions : attaque + direction ---
        if (!Guarding)
        {
            if (GameInput.AttackPressed())
            {
                ArteDir d = ReadDir(inp, tgt);
                if (d == ArteDir.Neutral) bufferedAttack = Time.time;
                else caster.TryStandard(d);
            }
            else if (GameInput.TechPressed()) caster.TryTechnical(ReadDir(inp, tgt));
        }
        if (!Guarding && bufferedAttack > 0f && Time.time - bufferedAttack < 0.3f && CanAct && Time.time >= nextAttack && !caster.IsCasting)
        {
            bufferedAttack = -1f;
            Attack();
        }

        // --- déplacement ---
        Vector3 move = Vector3.zero;
        float mul = stats.SpeedMultiplier;
        if (CanAct && !Guarding && !caster.IsCasting)
        {
            if (tgt != null && !FreeRun)
            {
                // LMBS : la ligne est l'axe héros -> cible
                Vector3 L = tgt.position - transform.position; L.y = 0;
                float dist = L.magnitude;
                Vector3 Ld = dist > 0.01f ? L / dist : transform.forward;
                float s = Mathf.Abs(Ld.x) > 0.05f ? Mathf.Sign(Ld.x) : 1f;
                float along = inp.x * s;                  // + = vers la cible
                if (along > 0f && dist < 1.4f) along = 0f;
                move = Ld * along * moveSpeed * mul;
                transform.rotation = Quaternion.LookRotation(Ld);
            }
            else
            {
                Vector3 w = new Vector3(inp.x, 0f, inp.y);
                move = w * (FreeRun ? freeRunSpeed : moveSpeed) * mul;
                if (w.sqrMagnitude > 0.01f) Rotate(w.normalized);
            }
            if (GameInput.JumpPressed() && cc.isGrounded) vy = Mathf.Sqrt(jumpHeight * -2f * gravity);
        }
        else if (Guarding) FaceTarget();

        if (cc.isGrounded && vy < 0f) vy = -2f;
        vy += gravity * Time.deltaTime;
        move.y = vy;
        cc.Move(move * Time.deltaTime);
    }

    void Attack()
    {
        combo = (Time.time - lastAttack < comboWindow) ? (combo % 3) + 1 : 1;
        lastAttack = Time.time;
        nextAttack = Time.time + attackDelay;
        lockUntil = Time.time + attackDelay * 0.85f;
        FaceTarget();

        // petit pas en avant vers la cible (magnétisme d'attaque)
        if (sel.Current != null && Vector3.Distance(transform.position, sel.Current.position) > 1.8f)
            cc.Move(transform.forward * 0.4f);

        Attacked?.Invoke(combo);

        bool fin = combo == 3;
        float dmg = attackDamage * stats.AttackMultiplier * (fin ? 1.8f : 1f);
        Vector3 c = transform.position + transform.forward * attackReach * 0.6f;
        int n = CombatUtil.Hit(c, attackRadius, Team.Player, dmg, transform.forward, fin ? 9f : 2f, fin ? 35f : 9f);
        if (n > 0) stats.AddTp(2f * n);
    }
}
