using System.Collections.Generic;
using UnityEngine;

/// <summary>Đạn trong trận (của người chơi hoặc của quái). Va chạm tính bằng khoảng cách, không dùng Physics. Dừng khi Combat.Dt = 0.</summary>
public class CombatBolt : MonoBehaviour
{
    Vector2 vel; float dmg, life, radius; Element el; bool friendly; int pierce, bounce;
    readonly HashSet<Monster> hit = new HashSet<Monster>();

    public static CombatBolt Fire(Vector2 pos, Vector2 dir, float speed, float dmg, float radius, Color col, bool friendly, float life,
                                  Element el = Element.None, int pierce = 0, int bounce = 0)
    {
        var go = new GameObject(friendly ? "Bolt" : "EnemyBolt"); go.transform.position = pos;
        Combat.MakeSr("Glow", go.transform, ProcSprites.Glow, new Color(col.r, col.g, col.b, .5f), new Vector2(radius * 4f, radius * 4f), 29);
        Combat.MakeSr("Core", go.transform, ProcSprites.Circle, col, new Vector2(radius * 2f, radius * 2f), 30);
        var b = go.AddComponent<CombatBolt>();
        b.vel = dir.normalized * speed; b.dmg = dmg; b.radius = radius; b.life = life; b.el = el; b.friendly = friendly; b.pierce = pierce; b.bounce = bounce;
        return b;
    }

    void Update()
    {
        float dt = Combat.Dt; if (dt <= 0f) return;
        Vector2 p = (Vector2)transform.position + vel * dt;

        if (p.x < RoomManager.MinX || p.x > RoomManager.MaxX)
        {
            if (bounce > 0) { bounce--; vel.x = -vel.x; p.x = Mathf.Clamp(p.x, RoomManager.MinX, RoomManager.MaxX); }
            else { Destroy(gameObject); return; }
        }
        if (p.y < RoomManager.MinY || p.y > RoomManager.WallY)
        {
            if (bounce > 0) { bounce--; vel.y = -vel.y; p.y = Mathf.Clamp(p.y, RoomManager.MinY, RoomManager.WallY); }
            else { Destroy(gameObject); return; }
        }
        transform.position = p;
        life -= dt; if (life <= 0f) { Destroy(gameObject); return; }

        if (friendly)
        {
            var list = Combat.Monsters;
            for (int i = list.Count - 1; i >= 0; i--)
            {
                if (i >= list.Count) continue;                       // Damage có thể làm quái chết và rời danh sách
                var m = list[i];
                if (m == null || m.Dead || hit.Contains(m)) continue;
                if (Vector2.Distance(p, m.transform.position) > radius + m.radius) continue;
                hit.Add(m);
                m.Damage(dmg, el);
                if (el != Element.None) Elements.Apply(m, el);
                if (pierce <= 0) { Destroy(gameObject); return; }
                pierce--;
            }
        }
        else
        {
            var pl = Combat.Player;
            if (pl != null && !pl.Dead && Vector2.Distance(p, pl.transform.position) < radius + pl.radius)
            { pl.Hurt(dmg, el); Destroy(gameObject); }
        }
    }
}

/// <summary>Vòng báo hiệu rồi nổ (chiêu của Phù thuật sư): đứng ngoài vòng khi hết giờ thì không bị thương.</summary>
public class CombatTelegraph : MonoBehaviour
{
    float t, delay, radius, dmg; Element el; SpriteRenderer fill;

    public static void Spawn(Vector2 pos, float radius, float delay, float dmg, Element el)
    {
        var go = new GameObject("Telegraph"); go.transform.position = pos;
        var c = el == Element.None ? new Color(1f, .3f, .3f) : Combat.ElementColor(el);
        Combat.MakeSr("Ring", go.transform, ProcSprites.Ring, c, new Vector2(radius * 2f, radius * 2f), 2);
        var t = go.AddComponent<CombatTelegraph>();
        t.fill = Combat.MakeSr("Fill", go.transform, ProcSprites.Circle, new Color(c.r, c.g, c.b, .12f), new Vector2(radius * 2f, radius * 2f), 1);
        t.delay = delay; t.radius = radius; t.dmg = dmg; t.el = el;
    }

    void Update()
    {
        float dt = Combat.Dt; if (dt <= 0f) return;
        t += dt;
        var c = fill.color; c.a = .12f + .25f * Mathf.Clamp01(t / delay); fill.color = c;          // đậm dần để dễ canh giờ né
        if (t < delay) return;
        var pl = Combat.Player;
        if (pl != null && !pl.Dead && Vector2.Distance(transform.position, pl.transform.position) <= radius + pl.radius * .5f) pl.Hurt(dmg, el);
        CombatFx.Pulse(transform.position, radius, new Color(c.r, c.g, c.b));
        Destroy(gameObject);
    }
}

/// <summary>Vật hồi máu rơi từ quái (mục 5.6).</summary>
public class CombatPickup : MonoBehaviour
{
    float heal, bob;

    public static void SpawnHeal(Vector2 pos, float heal)
    {
        var go = new GameObject("HealPickup"); go.transform.position = pos;
        Combat.MakeSr("Glow", go.transform, ProcSprites.Glow, new Color(.4f, 1f, .5f, .6f), new Vector2(1.2f, 1.2f), 8);
        Combat.MakeSr("Core", go.transform, ProcSprites.Circle, new Color(.45f, .95f, .55f), new Vector2(.4f, .4f), 9);
        go.AddComponent<CombatPickup>().heal = heal;
    }

    void Update()
    {
        float dt = Combat.Dt; if (dt <= 0f) return;
        bob += dt;
        transform.localScale = Vector3.one * (1f + .1f * Mathf.Sin(bob * 6f));
        var pl = Combat.Player;
        if (pl != null && !pl.Dead && Vector2.Distance(transform.position, pl.transform.position) < .9f) { pl.Heal(heal); Destroy(gameObject); }
    }
}

/// <summary>Vòng sáng nở ra rồi mờ đi (báo hiệu nổ, đòn chém, phản ứng nguyên tố).</summary>
public class CombatFx : MonoBehaviour
{
    float t; const float Dur = .35f; float target; SpriteRenderer sr; Color col;

    public static void Pulse(Vector2 pos, float radius, Color c)
    {
        var go = new GameObject("Pulse"); go.transform.position = pos;
        var fx = go.AddComponent<CombatFx>();
        fx.sr = Combat.MakeSr("Ring", go.transform, ProcSprites.Ring, c, new Vector2(.4f, .4f), 28);
        fx.target = radius * 2f; fx.col = c;
    }

    void Update()
    {
        t += Time.deltaTime;                                       // hiệu ứng thị giác chạy cả khi đóng băng cũng không sao
        float k = Mathf.Clamp01(t / Dur);
        float s = Mathf.Lerp(.4f, target, 1f - (1f - k) * (1f - k));
        var b = sr.sprite.bounds.size; sr.transform.localScale = new Vector3(s / b.x, s / b.y, 1f);
        var c = col; c.a = 1f - k; sr.color = c;
        if (t >= Dur) Destroy(gameObject);
    }
}

/// <summary>Số sát thương / chữ nổi bay lên rồi mờ dần (TextMesh, không cần Canvas).</summary>
public class FloatText : MonoBehaviour
{
    TextMesh tm; float t; Color col; const float Life = .8f;

    public static void Spawn(Vector3 pos, string text, Color col, float size = .13f)
    {
        var go = new GameObject("Float"); go.transform.position = new Vector3(pos.x, pos.y, 0f);
        var tm = go.AddComponent<TextMesh>();
        tm.font = GameFont.Body; tm.text = text; tm.fontSize = 64; tm.characterSize = size; tm.anchor = TextAnchor.MiddleCenter; tm.color = col;
        var mr = go.GetComponent<MeshRenderer>(); mr.sharedMaterial = tm.font.material; mr.sortingOrder = 100;
        var f = go.AddComponent<FloatText>(); f.tm = tm; f.col = col;
    }

    void Update()
    {
        t += Time.deltaTime;
        transform.position += Vector3.up * 1.1f * Time.deltaTime;
        var c = col; c.a = Mathf.Clamp01(1f - t / Life); tm.color = c;
        if (t >= Life) Destroy(gameObject);
    }
}
