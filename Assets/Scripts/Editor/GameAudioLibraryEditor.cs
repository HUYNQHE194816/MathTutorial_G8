using UnityEditor;
using UnityEngine;

/// <summary>Tự tạo Assets/Resources/GameAudioLibrary.asset, menu mở nhanh, và hiển thị mỗi ô âm thanh gọn trên một dòng.</summary>
public static class GameAudioLibraryCreator
{
    const string Path = "Assets/Resources/GameAudioLibrary.asset";

    [InitializeOnLoadMethod]
    static void Auto()
    {
        EditorApplication.delayCall += () => { if (AssetDatabase.LoadAssetAtPath<GameAudioLibrary>(Path) == null) Create(); };
    }

    [MenuItem("Tools/KHTN 8/Âm thanh/Mở bảng âm thanh")]
    public static void Open()
    {
        var a = AssetDatabase.LoadAssetAtPath<GameAudioLibrary>(Path);
        if (a == null) a = Create();
        Selection.activeObject = a; EditorGUIUtility.PingObject(a);
    }

    static GameAudioLibrary Create()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Resources")) AssetDatabase.CreateFolder("Assets", "Resources");
        var lib = ScriptableObject.CreateInstance<GameAudioLibrary>();
        lib.Sync();
        // Dùng luôn 2 file đúng/sai có sẵn trong project
        Preset(lib, Snd.QuizCorrect, "Assets/AudioBank/Correct.mp3");
        Preset(lib, Snd.QuizWrong, "Assets/AudioBank/Fail.mp3");
        AssetDatabase.CreateAsset(lib, Path); AssetDatabase.SaveAssets();
        Debug.Log("[GameAudio] Đã tạo " + Path + " - chọn file này rồi kéo âm thanh vào từng ô.");
        return lib;
    }

    static void Preset(GameAudioLibrary lib, Snd id, string path)
    {
        var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
        var slot = lib.GetSfx(id); if (slot != null && clip != null) slot.clip = clip;
    }
}

[CustomPropertyDrawer(typeof(SoundSlot))]
public class SoundSlotDrawer : PropertyDrawer
{
    public override void OnGUI(Rect r, SerializedProperty p, GUIContent l)
    {
        EditorGUI.BeginProperty(r, l, p);
        int indent = EditorGUI.indentLevel; EditorGUI.indentLevel = 0;
        float w = r.width, gap = 6f;
        var labelR = new Rect(r.x, r.y, w * .46f - gap, r.height);
        var clipR = new Rect(labelR.xMax + gap, r.y, w * .38f - gap, r.height);
        var volR = new Rect(clipR.xMax + gap, r.y, w * .16f, r.height);
        EditorGUI.LabelField(labelR, p.FindPropertyRelative("label").stringValue);
        EditorGUI.PropertyField(clipR, p.FindPropertyRelative("clip"), GUIContent.none);
        EditorGUI.PropertyField(volR, p.FindPropertyRelative("volume"), GUIContent.none);
        EditorGUI.indentLevel = indent;
        EditorGUI.EndProperty();
    }
}
