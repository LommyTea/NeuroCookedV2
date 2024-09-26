using UnityEngine;
using UnityEngine.SceneManagement;

public class NextSceneLoader : MonoBehaviour
{
    // Call this function to load the next scene in the build index
    public void LoadNextScene()
    {
        // Get the current active scene
        int currentSceneIndex = SceneManager.GetActiveScene().buildIndex;

        // Load the next scene by incrementing the build index
        SceneManager.LoadScene(currentSceneIndex + 1);
    }

    // Optional: You can also add this to bind to a UI button for VR or normal 2D UI
    public void OnButtonClick()
    {
        LoadNextScene();
    }
}
