using UnityEngine;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    public enum TurnState
    {
        PlayerTurn,
        AITurn,
        Win,
        Lose
    }

    public TurnState CurrentTurn { get; set; }

    [Header("Panels")]
    [SerializeField] private GameObject choosePanel;
    [SerializeField] private GameObject gameViewPanel;
    [SerializeField] private GameObject gameScreenPanel;

    public bool isGameStarted { get; private set; }

    [Header("AI")]
    public GameObject easyPrefab;
    public GameObject mediumPrefab;
    public GameObject hardPrefab;

    [SerializeField] private GameObject playerObject;



    [Header("Game Ending")]
    [SerializeField] private GameObject winEffect;
    [SerializeField] private GameObject loseEffect;
    private float delay = 2f;

    public AudioClip winSound;
    public AudioClip loseSound;

    void Awake()
    {
        Application.targetFrameRate = 60;
        QualitySettings.vSyncCount = 0;
    }

    void Start()
    {
        SetGameState(false);

        easyPrefab.SetActive(false);
        mediumPrefab.SetActive(false);
        hardPrefab.SetActive(false);

        if (playerObject != null)
        {
            playerObject.SetActive(false);
        }
    }

    private void SetGameState(bool state)
    {
        isGameStarted = state;

        choosePanel.SetActive(!state);
        gameViewPanel.SetActive(state);
        gameScreenPanel.SetActive(state);
    }






    #region Game Final Status
    public bool IsGameOver =>
        CurrentTurn == TurnState.Win ||
        CurrentTurn == TurnState.Lose;

    public void EndGame(TurnState state)
    {
        if (IsGameOver) return;

        CurrentTurn = state;
        isGameStarted = false;

        GameStatus(state);
    }
    public void GameStatus(TurnState turnState)
    {
        CurrentTurn = turnState;
        isGameStarted = false;

        if (turnState == TurnState.Win)
        {
            AudioSource.PlayClipAtPoint(winSound, Camera.main.transform.position);

            GameObject winTriggerEffect = Instantiate(
                winEffect,
                Camera.main.transform.position,
                Quaternion.identity);

            Destroy(winTriggerEffect, 2f);

            Invoke(nameof(StartSceneAgain), delay);
        }
        else if (turnState == TurnState.Lose)
        {
            AudioSource.PlayClipAtPoint(loseSound, Camera.main.transform.position);

            GameObject loseTriggerEffect = Instantiate(
                loseEffect,
                Camera.main.transform.position,
                Quaternion.identity);

            Destroy(loseTriggerEffect, 2f);

            Invoke(nameof(StartSceneAgain), delay);
        }
    }

    private void StartSceneAgain()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }
    #endregion






    #region AI State Choose
    public void EasyModeON()
    {
        ActivateMode(easyPrefab);
    }

    public void MediumModeON()
    {
        ActivateMode(mediumPrefab);
    }

    public void HardModeON()
    {
        ActivateMode(hardPrefab);
    }

    private void ActivateMode(GameObject prefab)
    {
        SetGameState(true);
        prefab.SetActive(true);

        if (playerObject != null)
            playerObject.SetActive(true);

        AbilityManager abilityManager = FindAnyObjectByType<AbilityManager>();
        if (abilityManager != null)
            abilityManager.SetAITransform(prefab.transform);

        CurrentTurn = TurnState.PlayerTurn;
    }
    #endregion
}