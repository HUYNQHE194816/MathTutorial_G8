using System;
using UnityEngine;

public static class CloudConfig
{
    [Serializable] class Data { public string url; public string anonKey; }

    static Data d;
    static bool loaded;

    static void Load()
    {
        if (loaded) return;
        loaded = true;
        d = new Data();
        var ta = Resources.Load<TextAsset>("cloud_config");
        if (ta != null) { try { d = JsonUtility.FromJson<Data>(ta.text) ?? d; } catch { } }
        if (string.IsNullOrEmpty(d.url)) d.url = Environment.GetEnvironmentVariable("SUPABASE_URL");
        if (string.IsNullOrEmpty(d.anonKey)) d.anonKey = Environment.GetEnvironmentVariable("SUPABASE_ANON_KEY");
    }

    public static string Url { get { Load(); return d.url ?? ""; } }
    public static string AnonKey { get { Load(); return d.anonKey ?? ""; } }

    public static bool Configured =>
        !string.IsNullOrEmpty(Url) && !string.IsNullOrEmpty(AnonKey) && Url.StartsWith("https://");
}