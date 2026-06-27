using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System.Collections;


public class MenuManagement : MonoBehaviour
{
    public TypeWriterEffect typeWriterEffect;

    public Button startButton;
    public Button aboutGameButton;
    public Button quitButton;
    public GameObject aboutGamePanel;

    private bool isAboutGamePanelActive = false;

    public AudioClip buttonClickSound;

    void Start()
    {
        startButton.onClick.AddListener(StartGame);
        aboutGameButton.onClick.AddListener(AboutGamePanel);
        quitButton.onClick.AddListener(ExitGame);
    }
    void StartGame()
    {
        AudioSource.PlayClipAtPoint(buttonClickSound, Camera.main.transform.position);
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex + 1);
    }
    void AboutGamePanel()
    {
        AudioSource.PlayClipAtPoint(buttonClickSound, Camera.main.transform.position);

        isAboutGamePanelActive = !isAboutGamePanelActive;
        aboutGamePanel.SetActive(isAboutGamePanelActive);

        if (isAboutGamePanelActive)
        {
            StartCoroutine(StartTypingNextFrame());
        }
        else
        {
            typeWriterEffect.ResetText();
        }
    }

    IEnumerator StartTypingNextFrame()
    {
        yield return null;
        typeWriterEffect.RestartTyping();
    }
    void ExitGame()
    {
        AudioSource.PlayClipAtPoint(buttonClickSound, Camera.main.transform.position);

#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
