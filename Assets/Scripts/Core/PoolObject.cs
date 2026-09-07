using UnityEngine;

public class PoolObject : MonoBehaviour
{
    private ObjectPool ownerPool;

    public ObjectPool OwnerPool =>
        ownerPool;

    public void SetOwnerPool(
        ObjectPool pool)
    {
        ownerPool = pool;
    }

    public void ReturnToPool()
    {
        if (ownerPool != null)
        {
            ownerPool.ReturnObject(
                gameObject
            );
        }
        else
        {
            Destroy(gameObject);
        }
    }
}