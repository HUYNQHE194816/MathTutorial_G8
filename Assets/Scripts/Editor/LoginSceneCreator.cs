#if UNITY_EDITOR
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Tự tạo Assets/Scenes/LoginScene.unity (nếu chưa có) và đặt làm scene đầu trong Build Settings.</summary>
[InitializeOnLoad]
public static class LoginSceneCreator
{
    const string ScenePath = "Assets/Scenes/LoginScene.unity";
    const string SpriteDir = "Assets/Sprites/SubjectSelect/";

    static LoginSceneCreator()
    {
        EditorApplication.delayCall += () =>
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (!File.Exists(ScenePath)) Create(); else Register();
        };
    }

    [MenuItem("Tools/Math Tutorial/Tạo lại Login Scene")]
    static void Create()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);

        var cam = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener)) { tag = "MainCamera" };
        var c = cam.GetComponent<Camera>();
        c.clearFlags = CameraClearFlags.SolidColor;
        c.backgroundColor = new Color(.45f, .75f, 1f);

        var go = new GameObject("LoginManager");
        var so = new SerializedObject(go.AddComponent<LoginManager>());
        so.FindProperty("background").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>(SpriteDir + "bg_subject.png");
        so.FindProperty("sparkle").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>(SpriteDir + "sparkle.png");
        so.ApplyModifiedPropertiesWithoutUndo();

        SceneManager.MoveGameObjectToScene(cam, scene);
        SceneManager.MoveGameObjectToScene(go, scene);
        EditorSceneManager.SaveScene(scene, ScenePath);
        EditorSceneManager.CloseScene(scene, true);
        Register();
        Debug.Log("[Login] Đã tạo " + ScenePath);
    }

    static void Register()
    {
        var scenes = EditorBuildSettings.scenes.ToList();
        if (scenes.Any(s => s.path == ScenePath)) return;
        scenes.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
        EditorBuildSettings.scenes = scenes.ToArray();
    }
}
#endif
