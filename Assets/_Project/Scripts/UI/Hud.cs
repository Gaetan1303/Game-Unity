using System.Collections.Generic;
using UnityEngine;

/// <summary>HUD en OnGUI (aucun Canvas à configurer), résolution virtuelle 1080p.</summary>
public class Hud : MonoBehaviour
{
    Health hp; PlayerStats stats; ArteCaster caster; TargetSelector sel; PlayerController pc;
    GUIStyle st;
    float W; const float H = 1080f;
    EnemyAI[] enemies = new EnemyAI[0];
    AllyAI[] allies = new AllyAI[0];
    float nextScan;

    void Start()
    {
        hp = GetComponent<Health>(); stats = GetComponent<PlayerStats>();
        caster = GetComponent<ArteCaster>(); sel = GetComponent<TargetSelector>(); pc = GetComponent<PlayerController>();
    }

    // ---------- helpers ----------
    void Bar(Rect r, float k, Color c)
    {
        GUI.color = new Color(0, 0, 0, 0.7f); GUI.DrawTexture(new Rect(r.x - 2, r.y - 2, r.width + 4, r.height + 4), Texture2D.whiteTexture);
        GUI.color = c; GUI.DrawTexture(new Rect(r.x, r.y, r.width * Mathf.Clamp01(k), r.height), Texture2D.whiteTexture);
        GUI.color = Color.white;
    }

    void Txt(Rect r, string s, int size = 22, TextAnchor a = TextAnchor.UpperLeft, Color? col = null)
    {
        st.fontSize = size; st.alignment = a;
        st.normal.textColor = Color.black;
        GUI.Label(new Rect(r.x + 2, r.y + 2, r.width, r.height), s, st);
        st.normal.textColor = col ?? Color.white;
        GUI.Label(r, s, st);
    }

    bool ToGui(Vector3 world, out Vector2 p)
    {
        p = Vector2.zero;
        var cam = Camera.main; if (cam == null) return false;
        Vector3 sp = cam.WorldToScreenPoint(world);
        if (sp.z <= 0f) return false;
        float s = Screen.height / H;
        p = new Vector2(sp.x / s, (Screen.height - sp.y) / s);
        return true;
    }

    void Scan()
    {
        if (Time.time < nextScan) return;
        nextScan = Time.time + 0.4f;
        enemies = FindObjectsByType<EnemyAI>(FindObjectsSortMode.None);
        allies = FindObjectsByType<AllyAI>(FindObjectsSortMode.None);
    }

    string DirLabel(ArteDir d, string btn)
    {
        switch (d)
        {
            case ArteDir.Forward: return "→+" + btn;
            case ArteDir.Up: return "↑+" + btn;
            case ArteDir.Down: return "↓+" + btn;
            default: return btn;
        }
    }

    void ArteCell(float x, float y, ArteData a, string label, string fallback = null)
    {
        string name = a != null ? a.displayName : fallback;
        if (name == null) return;
        bool ok = a == null || (caster.Affordable(a) && caster.CooldownLeft(a) <= 0f);
        GUI.color = new Color(0, 0, 0, 0.55f); GUI.DrawTexture(new Rect(x, y, 300, 34), Texture2D.whiteTexture); GUI.color = Color.white;
        var col = ok ? Color.white : new Color(1, 1, 1, 0.4f);
        Txt(new Rect(x + 6, y + 4, 300, 30), $"[{label}] {name}" + (a != null ? $"  {a.tpCost:0}" : ""), 18, TextAnchor.UpperLeft, col);
    }

    // ---------- dessin ----------
    void OnGUI()
    {
        if (hp == null) return;
        if (st == null) { st = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, wordWrap = false }; }
        Scan();
        float s = Screen.height / H;
        W = Screen.width / s;
        GUI.matrix = Matrix4x4.Scale(new Vector3(s, s, 1f));

        var bm = BattleManager.Instance;
        var ct = ComboTracker.Instance;

        Txt(new Rect(20, 8, 1800, 24), "ZQSD: ligne/profondeur | J: attaque (+direction = artes) | K: technique (+direction) | Ctrl: garde | Maj: course libre | Espace: saut | Tab: cible | L: Overlimit | H: objet", 15);

        // --- barres des ennemis en combat ---
        foreach (var e in enemies)
        {
            if (e == null || !e.Active) continue;
            var eh = e.GetComponent<Health>(); if (eh == null || eh.IsDead) continue;
            var ecc = e.GetComponent<CharacterController>();
            if (!ToGui(e.transform.position + Vector3.up * (ecc.height * 0.5f + 1.3f), out Vector2 p)) continue;
            Bar(new Rect(p.x - 45, p.y, 90, 8), eh.Current / eh.maxHp, new Color(0.9f, 0.2f, 0.2f));
            Bar(new Rect(p.x - 45, p.y + 11, 90, 4), eh.Poise / Mathf.Max(1f, eh.maxPoise), new Color(1f, 0.8f, 0.2f));
            Txt(new Rect(p.x - 100, p.y - 24, 200, 22), e.name, 15, TextAnchor.UpperCenter);
            if (sel.Current == e.transform)
                Txt(new Rect(p.x - 20, p.y - 52, 40, 30), "▼", 28, TextAnchor.UpperCenter, new Color(1f, 0.85f, 0.2f));
        }

        // --- groupe ---
        float py = H - 150f;
        Txt(new Rect(30, py, 400, 30), $"Vilain   Nv.{stats.level}", 26);
        Bar(new Rect(30, py + 34, 380, 22), hp.Current / hp.maxHp, new Color(0.2f, 0.8f, 0.3f));
        Bar(new Rect(30, py + 62, 380, 14), stats.Tp / stats.MaxTp, new Color(0.3f, 0.5f, 1f));
        Bar(new Rect(30, py + 82, 380, 6), (float)stats.exp / stats.ExpToNext, new Color(0.8f, 0.8f, 0.3f));
        Txt(new Rect(420, py + 30, 200, 26), $"PV {hp.Current:0}/{hp.maxHp:0}", 18);
        Txt(new Rect(420, py + 58, 200, 26), $"TP {stats.Tp:0}/{stats.MaxTp:0}", 18);

        float ay = py - 40f;
        foreach (var a in allies)
        {
            if (a == null) continue;
            var ah = a.GetComponent<Health>(); if (ah == null) continue;
            Txt(new Rect(30, ay - 4, 200, 22), a.name, 16);
            Bar(new Rect(160, ay + 2, 250, 12), ah.Current / ah.maxHp, ah.IsDead ? Color.gray : new Color(0.3f, 0.9f, 0.5f));
            ay -= 28f;
        }

        // --- artes ---
        float cx = W * 0.5f - 640f;
        float ry = H - 120f;
        ArteDir[] dirs = { ArteDir.Neutral, ArteDir.Forward, ArteDir.Up, ArteDir.Down };
        for (int i = 0; i < 4; i++)
        {
            ArteCell(cx + i * 310f, ry, caster.standard[i], DirLabel(dirs[i], "J"), i == 0 ? "Attaque" : null);
            ArteCell(cx + i * 310f, ry + 40f, caster.technical[i], DirLabel(dirs[i], "K"));
        }

        // --- Overlimit + objets ---
        float ox = W - 430f;
        Bar(new Rect(ox, H - 110f, 380, 20), stats.Overlimit / 100f, stats.InOverlimit ? new Color(1f, 0.3f, 0.2f) : new Color(1f, 0.7f, 0.2f));
        string olTxt = stats.InOverlimit ? "OVERLIMIT !" : (stats.Overlimit >= 100f ? "OVERLIMIT PRÊT  [L]" : "Overlimit");
        Txt(new Rect(ox, H - 140f, 380, 28), olTxt, 22, TextAnchor.UpperLeft, stats.Overlimit >= 100f && !stats.InOverlimit && (int)(Time.time * 4) % 2 == 0 ? Color.yellow : Color.white);
        Txt(new Rect(ox, H - 78f, 380, 26), $"Pomme gel x{stats.items} [H]   Gald {stats.gald}", 18);

        // --- combo ---
        if (ct != null)
        {
            int hits = ct.Hits; float dmg = ct.Damage; float a = 1f;
            if (hits == 0 && Time.time - ct.LastEnd < 1.5f) { hits = ct.LastHits; dmg = ct.LastDamage; a = 1f - (Time.time - ct.LastEnd) / 1.5f; }
            if (hits > 1)
            {
                var c = new Color(1f, 0.85f, 0.3f, a);
                Txt(new Rect(W - 420, 240, 380, 70), $"{hits} HITS", 60, TextAnchor.UpperRight, c);
                Txt(new Rect(W - 420, 305, 380, 30), $"{dmg:0} dégâts", 24, TextAnchor.UpperRight, new Color(1, 1, 1, a));
            }
        }

        // --- messages ---
        if (Time.time < caster.MessageUntil) Txt(new Rect(0, H * 0.5f - 230f, W, 40), caster.Message, 32, TextAnchor.UpperCenter, new Color(0.9f, 0.8f, 1f));
        if (bm != null && Time.time < bm.BannerUntil) Txt(new Rect(0, 170, W, 90), bm.Banner, 72, TextAnchor.UpperCenter, new Color(1f, 0.9f, 0.4f));
        if (Time.time < stats.LevelUpUntil) Txt(new Rect(0, 270, W, 50), $"NIVEAU {stats.level} !", 40, TextAnchor.UpperCenter, new Color(0.6f, 1f, 0.6f));
        var gm = GameManager.Instance;
        if (gm != null && gm.LastConquered != null && Time.time - gm.LastConqueredTime < 6f)
            Txt(new Rect(0, 340, W, 50), "MONDE CONQUIS : " + gm.LastConquered, 40, TextAnchor.UpperCenter, new Color(1f, 0.5f, 0.5f));

        // --- résultats ---
        if (bm != null && bm.State == BattleState.Results && bm.Result != null)
        {
            var r = bm.Result;
            var box = new Rect(W * 0.5f - 330f, 380f, 660f, 360f);
            GUI.color = new Color(0, 0, 0, 0.8f); GUI.DrawTexture(box, Texture2D.whiteTexture); GUI.color = Color.white;
            Txt(new Rect(box.x, box.y + 14, box.width, 50), "VICTOIRE", 44, TextAnchor.UpperCenter, new Color(1f, 0.9f, 0.4f));
            float y = box.y + 80f;
            Txt(new Rect(box.x + 50, y, 560, 30), $"EXP  +{r.exp}", 26); y += 38;
            Txt(new Rect(box.x + 50, y, 560, 30), $"Gald  +{r.gald}", 26); y += 38;
            Txt(new Rect(box.x + 50, y, 560, 30), $"Combo max  {r.maxCombo} HITS", 26); y += 38;
            Txt(new Rect(box.x + 50, y, 560, 30), $"Dégâts totaux  {r.damage:0}", 26); y += 38;
            Txt(new Rect(box.x + 50, y, 560, 30), $"Temps  {r.time:0}s", 26); y += 38;
            Txt(new Rect(box.x + 420, box.y + 90, 200, 120), r.grade, 110, TextAnchor.UpperCenter, new Color(1f, 0.8f, 0.2f));
            if (r.levelsGained > 0) Txt(new Rect(box.x, y, box.width, 30), $"NIVEAU SUPÉRIEUR ! Nv.{stats.level}", 26, TextAnchor.UpperCenter, new Color(0.6f, 1f, 0.6f));
            Txt(new Rect(box.x, box.yMax - 40, box.width, 30), "Entrée / J pour continuer", 18, TextAnchor.UpperCenter);
        }

        if (hp.IsDead) Txt(new Rect(0, H * 0.4f, W, 100), "GAME OVER", 90, TextAnchor.UpperCenter, new Color(0.9f, 0.2f, 0.2f));
    }
}
