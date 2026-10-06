using System.Collections.Generic;
using UnityEngine;

/// <summary>Quỷ nhỏ (đệ của rồng): đuổi theo người chơi, cắn khi chạm, thỉnh thoảng phun cầu lửa. Bị chém trúng thì mất máu.</summary>
public class Minion : MonoBehaviour
{
    public static readonly List<Minion> All = new List<Minion>();
    public float Hp = 42f;
    public bool Spawning => spawnT > 0f;

    PlayerKnight pl; SpriteRenderer sr; float t, spawnT = .7f, flashT, shootT, hurtCd, phase; Vector2 knock;
    float burnT, burnDmg, burnTick, chillT, frozenT;   // trạng thái: đốt (Kiếm Lửa), làm chậm / đóng băng (Kiếm Băng)
    static readonly Color[] Puff = { new Color(.7f, .4f, 1f), new Color(1f, .5f, .8f), Color.white };

    public static Minion Spawn(Vector2 pos, PlayerKnight p)
    {
        var go = new GameObject("Imp"); go.transform.position = pos; go.transform.localScale = Vector3.one * 1.3f;
        var m = go.AddComponent<Minion>(); m.pl = p; m.phase = Random.value * 6.28f; m.shootT = Random.Range(2f, 3.5f);
        m.sr = go.AddComponent<SpriteRenderer>(); m.sr.sprite = ProcSprites.Imp[0]; Gfx.Glow(go.transform, Vector2.zero, 3f, new Color(.7f, .4f, 1f, .3f), -895, true);
        var sh = new GameObject("Shadow"); sh.transform.SetParent(go.transform, false); sh.transform.localPosition = new Vector3(0, -.75f, 0);
        sh.transform.localScale = new Vector3(.3f, .1f, 1f);
        var ss = sh.AddComponent<SpriteRenderer>(); ss.sprite = ProcSprites.Circle; ss.color = new Color(0, 0, 0, .35f); ss.sortingOrder = -900;
        Fx.Ring(pos, 2.4f, new Color(.7f, .4f, 1f), .5f); Fx.Burst(pos, 14, Puff); Sfx.Tone(260, .25f, 2, .2f, 200, snd: Snd.MinionSpawn);
        return m;
    }

    void OnEnable() { All.Add(this); }
    void OnDisable() { All.Remove(this); }

    void Update()
    {
        float dt = Time.deltaTime; t += dt; flashT -= dt; hurtCd -= dt;
        sr.sprite = ProcSprites.Imp[(int)(t * 9f) % 2];
        sr.sortingOrder = -Mathf.RoundToInt(transform.position.y * 20f);
        if (spawnT > 0f) { spawnT -= dt; sr.color = new Color(1f, 1f, 1f, .35f + .3f * Mathf.Sin(t * 30f)); return; }
        sr.color = flashT > 0f ? new Color(1f, .6f, .6f) : (frozenT > 0f ? new Color(.6f, .85f, 1f) : (chillT > 0f ? new Color(.78f, .92f, 1f) : (burnT > 0f ? new Color(1f, .78f, .62f) : Color.white)));
        if (burnT > 0f)
        {
            burnT -= dt; burnTick -= dt;
            if (Random.value < dt * 20f) Fx.Spawn(ProcSprites.Pixel, (Vector2)transform.position + Random.insideUnitCircle * .6f, new Color(1f, .55f, .15f), .45f, 1.4f, .3f, new Vector2(0f, Random.Range(1.5f, 3f)));
            if (burnTick <= 0f) { burnTick = .5f; TakeDamage(burnDmg, false, true); if (Hp <= 0f) return; }
        }
        if (chillT > 0f) chillT -= dt;
        if (frozenT > 0f) { frozenT -= dt; return; }   // đóng băng: đứng yên, không cắn, không bắn
        if (pl == null || !pl.Alive) return;
        float slow = chillT > 0f ? .5f : 1f;

        Vector2 pos = transform.position, to = pl.Center - pos; float d = to.magnitude;
        Vector2 dir = d > .01f ? to / d : Vector2.zero;
        Vector2 side = new Vector2(-dir.y, dir.x) * Mathf.Sin(t * 2.2f + phase) * .9f;
        Vector2 vel = (dir * (d > 2.2f ? 3.8f : 1.2f) + side) * slow + knock;
        knock = Vector2.Lerp(knock, Vector2.zero, dt * 8f);
        pos += vel * dt + Vector2.up * Mathf.Sin(t * 5f + phase) * .6f * dt;
        pos.x = Mathf.Clamp(pos.x, -14.5f, 14.5f); pos.y = Mathf.Clamp(pos.y, -8f, 6f);
        transform.position = pos; sr.flipX = dir.x < 0f;

        if (d < 1.0f && hurtCd <= 0f) { hurtCd = .8f; pl.Hurt(DragonBoss.Scale(8)); knock = -dir * 6f; }
        shootT -= dt * slow;
        if (shootT <= 0f && d > 3f)
        {
            shootT = Random.Range(2.8f, 4f);
            Projectile.Spawn(pos, dir * 5.5f, pl); Sfx.Tone(380, .1f, 0, .12f, -100, snd: Snd.MinionShoot);
        }
    }

    /// <param name="dot">true = sát thương theo thời gian (đốt): không đẩy lùi, hiệu ứng nhẹ.</param>
    public void TakeDamage(float dmg, bool crit, bool dot = false)
    {
        if (Spawning) return;
        Hp -= dmg; flashT = dot ? .04f : .12f;
        var mgr = BossFightManager.I;
        if (!dot)
        {
            Vector2 away = (Vector2)transform.position - (pl != null ? pl.Center : Vector2.zero); knock = away.normalized * 9f;
            Fx.Burst(transform.position, crit ? 10 : 6, Puff); Sfx.Tone(150, .08f, 0, .2f, -60, snd: Snd.MinionHit);
        }
        if (mgr != null) mgr.ui.Float((Vector2)transform.position + Vector2.up * 1.1f, Mathf.RoundToInt(dmg).ToString(), dot ? new Color(1f, .55f, .2f) : (crit ? new Color(1f, .82f, .4f) : Color.white), crit);
        if (Hp <= 0f) Kill();
    }

    public void Ignite(float dur, float perTick) { if (burnT <= 0f) burnTick = .5f; burnT = dur; burnDmg = perTick; }
    public void Chill(float dur) { chillT = Mathf.Max(chillT, dur); }
    public void Freeze(float dur)
    {
        if (Spawning || frozenT > 0f) return;
        frozenT = dur; Fx.Ring(transform.position, 2f, new Color(.55f, .9f, 1f), .3f); Fx.Burst(transform.position, 10, Fx.IceCols);
    }

    public void Kill()
    {
        Fx.Ring(transform.position, 2.2f, new Color(.7f, .4f, 1f), .35f); Fx.Burst(transform.position, 18, Puff, 3f, 10f);
        Sfx.Tone(110, .25f, 4, .25f, -50, snd: Snd.MinionDie); CameraRig.Shake(2f); Destroy(gameObject);
    }

    public static void ClearAll(bool withFx = false)
    {
        for (int i = All.Count - 1; i >= 0; i--) { if (All[i] == null) continue; if (withFx) All[i].Kill(); else Destroy(All[i].gameObject); }
    }
}
