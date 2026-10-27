using UnityEngine;
using UnityEngine.SceneManagement;

public enum BattleState { Field, Battle, Results }

public class BattleResult
{
    public int exp, gald, maxCombo, levelsGained;
    public float damage, time;
    public string grade;
}

/// <summary>Machine à états Field -> Battle -> Results, comme les rencontres d'un Tales of.</summary>
public class BattleManager : MonoBehaviour
{
    public static BattleManager Instance { get; private set; }
    public static bool InBattle => Instance != null && Instance.State == BattleState.Battle;

    public BattleState State { get; private set; }
    public string Banner { get; private set; }
    public float BannerUntil { get; private set; }
    public BattleResult Result { get; private set; }
    float battleStart, resultsAt;

    void Awake() { Instance = this; }
    void OnEnable() { SceneManager.sceneLoaded += OnSceneLoaded; }
    void OnDisable() { SceneManager.sceneLoaded -= OnSceneLoaded; }
    void OnSceneLoaded(Scene s, LoadSceneMode m) { State = BattleState.Field; Time.timeScale = 1f; }

    public void Begin(string banner)
    {
        State = BattleState.Battle;
        battleStart = Time.time;
        Banner = banner; BannerUntil = Time.time + 1.8f;
        if (ComboTracker.Instance != null) ComboTracker.Instance.ResetBattle();
        CameraFollow.Shake(0.35f);
        if (GameManager.Instance != null) GameManager.Instance.HitStop(0.12f);
    }

    public void Win(int exp, int gald)
    {
        var stats = FindFirstObjectByType<PlayerStats>();
        var ct = ComboTracker.Instance;
        int lvBefore = stats != null ? stats.level : 1;
        if (stats != null) { stats.AddExp(exp); stats.AddGald(gald); }

        var r = new BattleResult
        {
            exp = exp, gald = gald, time = Time.time - battleStart,
            maxCombo = ct != null ? ct.BattleMaxCombo : 0,
            damage = ct != null ? ct.BattleDamage : 0f,
            levelsGained = stats != null ? stats.level - lvBefore : 0
        };
        float score = r.maxCombo * 3f + Mathf.Max(0f, 60f - r.time);
        r.grade = score > 90f ? "S" : score > 65f ? "A" : score > 40f ? "B" : "C";
        Result = r;
        State = BattleState.Results;
        resultsAt = Time.time;
        Banner = "VICTOIRE !"; BannerUntil = Time.time + 2f;
    }

    void Update()
    {
        if (State == BattleState.Results && Time.time - resultsAt > 0.6f && GameInput.ConfirmPressed())
            State = BattleState.Field;
    }
}
