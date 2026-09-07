using UnityEngine;

public class RoomController : MonoBehaviour
{
    [Header("Room")]
    [SerializeField]
    private Transform enemyRoot;

    [SerializeField]
    private ExitDoor exitDoor;

    private int remainingEnemies;
    private bool isCleared;

    public bool IsCleared => isCleared;
    public int RemainingEnemies => remainingEnemies;


    // ==================================================
    // Unity
    // ==================================================

    private void Start()
    {
        InitializeRoom();
    }


    // ==================================================
    // Initialize
    // ==================================================

    private void InitializeRoom()
    {
        if (exitDoor != null)
        {
            exitDoor.Lock();
        }

        CountEnemies();

        if (remainingEnemies <= 0)
        {
            ClearRoom();
        }
    }


    // ==================================================
    // Enemy
    // ==================================================

    private void CountEnemies()
    {
        if (enemyRoot == null)
        {
            Debug.LogWarning(
                $"{name}: Enemy Root가 연결되지 않았습니다."
            );

            remainingEnemies = 0;
            return;
        }

        EnemyRoomMember[] enemies =
            enemyRoot.GetComponentsInChildren<EnemyRoomMember>(
                true
            );

        remainingEnemies =
            enemies.Length;

        foreach (EnemyRoomMember enemy in enemies)
        {
            enemy.Initialize(this);
        }
    }


    public void NotifyEnemyDead()
    {
        if (isCleared)
            return;

        remainingEnemies =
            Mathf.Max(
                0,
                remainingEnemies - 1
            );

        if (remainingEnemies <= 0)
        {
            ClearRoom();
        }
    }


    // ==================================================
    // Clear
    // ==================================================

    private void ClearRoom()
    {
        if (isCleared)
            return;

        isCleared = true;

        if (exitDoor != null)
        {
            exitDoor.Unlock();
        }
    }
}