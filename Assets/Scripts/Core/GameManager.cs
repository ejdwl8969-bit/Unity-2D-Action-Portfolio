using UnityEngine;

public class GameManager : MonoBehaviour
{
    [SerializeField]
    private PlayerLevel playerLevel;

    [SerializeField]
    private LevelUpUI levelUpUI;

    void Start()
    {
        playerLevel.OnLevelUp += levelUpUI.Show;
    }

    void OnDestroy()
    {
        playerLevel.OnLevelUp -= levelUpUI.Show;
    }
}