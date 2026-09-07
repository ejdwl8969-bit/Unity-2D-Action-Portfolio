using UnityEngine;

public class ExitDoor : MonoBehaviour
{
    [Header("Scene")]
    [SerializeField] private string nextSceneName;

    [Header("Visual")]
    [SerializeField] private SpriteRenderer spriteRenderer;

    [SerializeField]
    private Sprite lockedSprite;

    [SerializeField]
    private Sprite unlockedSprite;

    private bool isUnlocked;

    public bool IsUnlocked => isUnlocked;

    private void Awake()
    {
        if (spriteRenderer == null)
        {
            spriteRenderer =
                GetComponent<SpriteRenderer>();
        }

        UpdateVisual();
    }

    public void Lock()
    {
        isUnlocked = false;

        UpdateVisual();
    }

    public void Unlock()
    {
        isUnlocked = true;

        UpdateVisual();
    }

    private void UpdateVisual()
    {
        if (spriteRenderer == null)
            return;

        spriteRenderer.color = Color.white;

        spriteRenderer.sprite =
            isUnlocked
                ? unlockedSprite
                : lockedSprite;
    }

    private void OnTriggerEnter2D(
        Collider2D other)
    {
        if (!isUnlocked)
            return;

        PlayerController player =
            other.GetComponent<PlayerController>();

        if (player == null)
            return;

        LoadNextScene();
    }

    private void LoadNextScene()
    {
        // A queued trigger must not leave BossRoom2 behind the result screen.
        if (RunManager.Instance != null &&
            RunManager.Instance.IsRunCleared)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(
                nextSceneName))
        {
            Debug.LogWarning(
                $"{name}: Next Scene Name이 비어 있습니다."
            );

            return;
        }

        if (!SceneTransition.CanLoadScene(nextSceneName))
            return;

        if (RunManager.Instance != null)
        {
            RunManager.Instance.SaveCurrentRun();
        }

        SceneTransition.LoadScene(
            nextSceneName
        );
    }

    public void SetNextSceneName(string sceneName)
    {
        nextSceneName = sceneName;
    }
}