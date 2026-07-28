using UnityEngine.SceneManagement;

namespace Game
{
    public static class GameState
    {
        public static int CurrentLevel { get; set; } = 1;

        public static void LoadNextLevel()
        {
            CurrentLevel++;
            SceneManager.LoadScene("GameScene");
        }

        public static void Restart()
        {
            CurrentLevel = 1;
            SceneManager.LoadScene("GameScene");
        }
    }
}