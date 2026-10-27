using System.Collections.Generic;
using UnityEngine;

/// <summary>Singleton persistant : progression, mondes conquis, arrêt sur image (hit-stop).</summary>
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    // progression conservée entre les rechargements de scène
    public int level = 1, exp = 0, gald = 0, items = 3;

    public string LastConquered { get; private set; }
    public float LastConqueredTime { get; private set; }
    readonly HashSet<string> conquered = new HashSet<string>();
    float hitStopUntil;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Boot()
    {
        if (Instance != null) return;
        var go = new GameObject("GameManager");
        go.AddComponent<GameManager>();
        go.AddComponent<ComboTracker>();
        go.AddComponent<BattleManager>();
        DontDestroyOnLoad(go);
    }

    void Awake() { Instance = this; Time.timeScale = 1f; }

    public void HitStop(float seconds)
    {
        float until = Time.unscaledTime + seconds;
        if (until <= hitStopUntil) return;
        hitStopUntil = until;
        Time.timeScale = 0.05f;
    }

    void Update()
    {
        if (hitStopUntil > 0f && Time.unscaledTime >= hitStopUntil) { hitStopUntil = 0f; Time.timeScale = 1f; }
    }

    public void Conquer(WorldData w)
    {
        if (w == null) return;
        if (conquered.Add(w.worldName)) { Debug.Log($"Monde conquis : {w.worldName}"); LastConquered = w.worldName; LastConqueredTime = Time.time; }
    }

    public bool IsConquered(WorldData w) => w != null && conquered.Contains(w.worldName);
}
