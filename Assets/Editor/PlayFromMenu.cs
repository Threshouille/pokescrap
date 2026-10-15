using UnityEditor;
using UnityEditor.SceneManagement;

/// Pressing Play always starts from the team menu, whatever scene is open in the editor.
[InitializeOnLoad]
static class PlayFromMenu
{
    const string MenuScene = "Assets/Scenes/Menu.unity";

    static PlayFromMenu() => EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(MenuScene);
}
