using UnityEngine;

/// <summary>
/// Buff "Đệ Hiệp Sĩ": một đệ tử nhỏ bay lơ lửng theo sau hiệp sĩ,
/// tự động bắn phép vào đệ tử của rồng (ưu tiên) hoặc vào rồng.
/// </summary>
public class Companion : MonoBehaviour
{
    PlayerKnight owner; BossFightManager mgr; SpriteRenderer sr; float t, cd = .8f;
    static readonly Color Bolt = new Color(.5f, 1f, .72f);

    public static Companion Create(BossFightManager m, PlayerKnight p)
    {
        Vector2 pos = p.Position + new Vector2(-1.8f, 1.4f);
        var go = new GameObject("Companion"); go.transform.position = pos;
        var c = go.AddComponent<Companion>(); c.owner = p; c.mgr = m;
        c.sr = go.AddComponent<SpriteRenderer>(); c.sr.sprite = ProcSprites.Ally[0];
        Gfx.Glow(go.transform, Vector2.zero, 2.8f, new Color(.5f, 1f, .72f, .22f), -895, true);
        var sh = new GameObject("Shadow"); sh.transform.SetParent(go.transform, false); sh.transform.localPosition = new Vector3(0, -1.2f, 0);
        sh.transform.localScale = new Vector3(.2f, .07f, 1f);
        var ss = sh.AddComponent<SpriteRenderer>(); ss.sprite = ProcSprites.Circle; ss.color = new Color(0, 0, 0, .3f); ss.sortingOrder = -900;
        Fx.Ring(pos, 2.4f, Bolt, .45f); Fx.Burst(pos, 14, Fx.HealCols); Sfx.Tone(500, .25f, 2, .2f, 300, snd: Snd.AllySpawn);
        return c;
    }

    void Update()
    {
        if (owner == null || !owner.Alive) { Destroy(gameObject); return; }
        if (mgr == null || !mgr.Playing) return;
        float dt = Time.deltaTime; t += dt; cd -= dt;

        // bay theo hiệp sĩ, nằm phía sau vai
        Vector2 target = owner.Position + new Vector2(-owner.Face * 1.7f, 1.4f + Mathf.Sin(t * 3f) * .25f);
        Vector2 pos = Vector2.Lerp(transform.position, target, 1f - Mathf.Exp(-5f * dt));
        transform.position = pos;
        sr.sprite = ProcSprites.Ally[(int)(t * 6f) % 2];
        sr.sortingOrder = -Mathf.RoundToInt((pos.y - 1.3f) * 20f);

        // chọn mục tiêu: đệ tử rồng gần nhất, không có thì đánh rồng (bỏ qua khi rồng đang miễn nhiễm)
        Vector2 tp = Vector2.zero; bool has = false; float best = 16f;
        for (int i = 0; i < Minion.All.Count; i++)
        {
            var m = Minion.All[i]; if (m == null || m.Spawning) continue;
            float d = Vector2.Distance(pos, m.transform.position); if (d < best) { best = d; tp = m.transform.position; has = true; }
        }
        var boss = mgr.Boss;
        if (!has && boss != null && !boss.Dead && !boss.Invulnerable) { tp = boss.Position; has = true; }
        if (!has) return;

        Vector2 dir = (tp - pos).normalized; sr.flipX = dir.x < 0f;
        if (cd <= 0f)
        {
            cd = 1.1f;
            Projectile.SpawnFriendly(pos, dir * 11f, Mathf.Max(4f, Mathf.Round(owner.Damage * .45f)), Bolt);
            Sfx.Tone(640, .08f, 2, .1f, 200, snd: Snd.AllyShoot);
        }
    }
}
