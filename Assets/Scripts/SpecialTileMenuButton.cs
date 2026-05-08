using UnityEngine;
using UnityEngine.SceneManagement;

public class SpecialTileMenuButton : MonoBehaviour
{
    [SerializeField] private string specialTileSceneName = "SpecialTileScene";

    public void Go()
    {
        if (!string.IsNullOrEmpty(specialTileSceneName))
            SceneManager.LoadScene(specialTileSceneName);
    }
}
