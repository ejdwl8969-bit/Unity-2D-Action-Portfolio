using UnityEngine;

public class WaitingRoomProgress : MonoBehaviour
{
    [Header("Exit")]
    [SerializeField]
    private ExitDoor exitDoor;

    [Header("Run Clear")]
    [SerializeField]
    private GameObject runClearPanel;

    private void Start()
    {
        if (runClearPanel != null)
        {
            runClearPanel.SetActive(false);
        }

        UpdateProgress();
    }

    private void UpdateProgress()
    {
        if (RunManager.Instance == null)
        {
            Debug.LogWarning(
                "RunManager가 없습니다."
            );

            return;
        }

        // 게임 완주
        if (RunManager.Instance.IsRunCleared)
        {
            ShowRunClear();
            return;
        }

        int chapter =
            RunManager.Instance.CurrentChapter;

        if (exitDoor != null)
        {
            exitDoor.gameObject.SetActive(true);
        }

        switch (chapter)
        {
            case 1:
                exitDoor.SetNextSceneName(
                    "Room1-1"
                );

                break;

            case 2:
                exitDoor.SetNextSceneName(
                    "Room2-1"
                );

                break;
        }
    }

    private void ShowRunClear()
    {
        if (exitDoor != null)
        {
            exitDoor.gameObject.SetActive(false);
        }

        RunResultUI resultUI = FindFirstObjectByType<RunResultUI>();
        if (resultUI != null)
            resultUI.ShowCurrentResult();
    }

    public void StartNewRun()
    {
        if (RunManager.Instance != null)
        {
            RunManager.Instance.ReturnToWaitingRoom();
            return;
        }

        SceneTransition.LoadScene(
            "WaitingRoom"
        );
    }
}
