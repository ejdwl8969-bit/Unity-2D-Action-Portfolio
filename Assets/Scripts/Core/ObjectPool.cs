using System.Collections.Generic;
using UnityEngine;

public class ObjectPool : MonoBehaviour
{
    [Header("Pool")]
    [SerializeField] private GameObject prefab;

    [SerializeField, Min(0)]
    private int initialSize = 10;

    private readonly Queue<GameObject> pool =
        new Queue<GameObject>();

    private readonly HashSet<GameObject> pooledObjects =
        new HashSet<GameObject>();

    private void Awake()
    {
        InitializePool();
    }

    private void InitializePool()
    {
        if (prefab == null)
        {
            Debug.LogWarning(
                $"{name}: ObjectPool Prefab이 연결되지 않았습니다."
            );

            return;
        }

        for (int i = 0; i < initialSize; i++)
        {
            CreateNewObject();
        }
    }

    private GameObject CreateNewObject()
    {
        if (prefab == null)
            return null;

        GameObject obj =
            Instantiate(
                prefab,
                transform
            );

        PoolObject poolObject =
            obj.GetComponent<PoolObject>();

        if (poolObject == null)
        {
            poolObject =
                obj.AddComponent<PoolObject>();
        }

        poolObject.SetOwnerPool(this);

        obj.SetActive(false);

        pool.Enqueue(obj);
        pooledObjects.Add(obj);

        return obj;
    }

    public GameObject GetObject()
    {
        while (pool.Count > 0)
        {
            GameObject obj =
                pool.Dequeue();

            if (obj == null)
                continue;

            pooledObjects.Remove(obj);

            obj.transform.SetParent(null);
            obj.SetActive(true);

            return obj;
        }

        GameObject newObject =
            CreateNewObject();

        if (newObject == null)
            return null;

        // CreateNewObject()는 새 객체를
        // 풀 안에 넣기 때문에 다시 꺼낸다.
        return GetObject();
    }

    public void ReturnObject(
        GameObject obj)
    {
        if (obj == null)
            return;

        // 이미 풀에 들어가 있는 오브젝트의
        // 중복 반환 방지
        if (pooledObjects.Contains(obj))
            return;

        PoolObject poolObject =
            obj.GetComponent<PoolObject>();

        if (poolObject != null &&
            poolObject.OwnerPool != this)
        {
            Debug.LogWarning(
                $"{obj.name}은(는) 다른 ObjectPool 소유입니다."
            );

            return;
        }

        obj.SetActive(false);

        obj.transform.SetParent(
            transform
        );

        obj.transform.localPosition =
            Vector3.zero;

        obj.transform.localRotation =
            Quaternion.identity;

        pool.Enqueue(obj);
        pooledObjects.Add(obj);
    }

    public int GetAvailableCount()
    {
        return pool.Count;
    }
}