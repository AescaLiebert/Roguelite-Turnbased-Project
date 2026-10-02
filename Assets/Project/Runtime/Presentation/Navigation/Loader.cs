using UnityEngine;
using UnityEngine.SceneManagement;

public class Loader : MonoBehaviour
{
    public enum Scene
    {
        GameScene, 
    }
    
    public static void Load(Scene scene)
    {
        SceneManager.LoadScene(scene.ToString());
    }

    public void GachaScene()
    {
        SceneManager.LoadScene("Scene-Gacha");
    }
    
    public void CharacterLoadOutScene()
    {
        SceneManager.LoadScene("Scene-CharacterLoadOut");
    }

    public void GameScene()
    {
        SceneManager.LoadScene("Scene-InGame");
    }
}
