using System.Collections.Generic;
using UnityEngine;

/// <summary>Vòng cảnh báo đỏ: sau thời gian đếm ngược sẽ nổ, ai đứng trong vòng sẽ mất máu.</summary>
public class Meteor : MonoBehaviour
{
    public static readonly List<Meteor> All = new List<Meteor>();
    float t, t0, r; PlayerKnight pl; SpriteRenderer outer; Transform inner; SpriteRenderer innerSr;

    public static void Spawn(Vector2 pos, float time, PlayerKnight p, float radius = 1.6f)
    {
        var go = new GameObject("MeteorWarning"); go.transform.position = pos;
        var m = go.AddComponent<Meteor>(); m.t = m.t0 = time; m.r = radius; m.pl = p;
        go.transform.localScale = Vector3.one * (m.r / 2f);                       // sprite Circle đường kính 4 đơn vị
        m.outer = go.AddComponent<SpriteRenderer>(); m.outer.sprite = ProcSprites.Circle; m.outer.sortingOrder = -840;
        var rg = new GameObject("Ring"); rg.transform.SetParent(go.transform, false);
        var rs = rg.AddComponent<SpriteRenderer>(); rs.sprite = ProcSprites.Ring; rs.color = new Color(.9f, .28f, .3f); rs.sortingOrder = -839;
        var inn = new GameObject("Inner"); inn.transform.SetParent(go.transform, false);
        m.inner = inn.transform; m.innerSr = inn.AddComponent<SpriteRenderer>(); m.innerSr.sprite = ProcSprites.Circle;
        m.innerSr.color = new Color(1f, .55f, .24f, .5f); Gfx.SetAdd(m.innerSr, true); m.innerSr.sortingOrder = -838; m.inner.localScale = Vector3.zero;
    }

    void OnEnable() { All.Add(this); }
    void OnDisable() { All.Remove(this); }

    void Update()
    {
        t -= Time.deltaTime;
        outer.color = new Color(.9f, .28f, .3f, .2f + .15f * Mathf.Sin(Time.time * 20f));
        inner.localScale = Vector3.one * Mathf.Clamp01(1f - t / t0);
        if (t > 0f) return;
        Fx.Boom(transform.position, r * .8f, Fx.FireCols); Fx.Scorch(transform.position, r);
        if (pl != null && pl.Alive && Vector2.Distance(transform.position, pl.Position) < r + .3f) pl.Hurt(DragonBoss.Scale(18));
        Destroy(gameObject);
    }

    public static void ClearAll() { for (int i = All.Count - 1; i >= 0; i--) if (All[i] != null) Destroy(All[i].gameObject); }
}
