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
        FightingAllstar.Presentation.ScreenMotion.LoadScene(scene.ToString());
    }

    public void GachaScene()
    {
        FightingAllstar.Presentation.ScreenMotion.LoadScene("Scene-Gacha");
    }
    
    public void CharacterLoadOutScene()
    {
        FightingAllstar.Presentation.ScreenMotion.LoadScene("Scene-CharacterLoadOut");
    }

    public void GameScene()
    {
        FightingAllstar.Presentation.ScreenMotion.LoadScene("Scene-InGame");
    }
}
