using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class RemoveCombatHudPrototype
{
    public static void RemoveFromRouteScene()
    {
        var scene = EditorSceneManager.OpenScene("Assets/Project/Scenes/Combat.unity", OpenSceneMode.Single);
        foreach (var root in scene.GetRootGameObjects())
        {
            foreach (var transform in root.GetComponentsInChildren<Transform>(true))
            {
                if (transform.name == "Local Combat")
                {
                    Object.DestroyImmediate(transform.gameObject);
                    EditorSceneManager.MarkSceneDirty(scene);
                    EditorSceneManager.SaveScene(scene);
                    Debug.Log("Removed the inactive UI Toolkit combat prototype from the route scene.");
                    return;
                }
            }
        }
        Debug.Log("No Local Combat prototype object was present in the route scene.");
    }
}
