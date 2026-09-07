using UnityEngine;

public class EnemyRoomMember : MonoBehaviour
{
    private RoomController roomController;
    private bool notified;

    public void Initialize(
        RoomController controller)
    {
        roomController = controller;
        notified = false;
    }

    private void OnDestroy()
    {
        NotifyDead();
    }

    private void NotifyDead()
    {
        if (notified)
            return;

        notified = true;

        if (roomController != null)
        {
            roomController.NotifyEnemyDead();
        }
    }
}