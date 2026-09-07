using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class ExpGem : MonoBehaviour
{
    [Header("Experience")]
    [SerializeField, Min(1)]
    private int expAmount = 2;

    [Header("Magnet")]
    [SerializeField, Min(0f)]
    private float magnetRange = 3f;

    [SerializeField, Min(0f)]
    private float moveSpeed = 8f;

    [SerializeField, Min(0f)]
    private float magnetDelay = 0.6f;

    [SerializeField, Min(0.01f)]
    private float collectDistance = 0.2f;

    [Header("Pop")]
    [SerializeField]
    private Vector2 horizontalForceRange =
        new Vector2(-4f, 4f);

    [SerializeField]
    private Vector2 verticalForceRange =
        new Vector2(6f, 9f);

    private Transform player;
    private PlayerLevel playerLevel;

    private Rigidbody2D rb;
    private SpriteRenderer spriteRenderer;
    private Collider2D bodyCollider;
    private PoolObject poolObject;

    private bool isMagnet;
    private bool isCollected;
    private float spawnTime;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        bodyCollider = GetComponent<Collider2D>();
        poolObject = GetComponent<PoolObject>();
    }

    public void Initialize(int exp)
    {
        expAmount = Mathf.Max(1, exp);

        isMagnet = false;
        isCollected = false;
        spawnTime = Time.time;

        rb.gravityScale = 2f;
        rb.linearVelocity = Vector2.zero;

        if (bodyCollider != null)
        {
            bodyCollider.enabled = true;
        }

        SetAppearance();
        FindPlayer();

        StartCoroutine(
            PopRoutine()
        );
    }

    private void FindPlayer()
    {
        if (playerLevel != null)
            return;

        playerLevel =
            FindFirstObjectByType<PlayerLevel>();

        if (playerLevel != null)
        {
            player = playerLevel.transform;
        }
    }

    private void Update()
    {
        // Proximity collection runs in Update, even when physics is paused.
        if (SceneTransition.IsTransitioning || PauseManager.IsGamePaused || (RunManager.Instance != null && RunManager.Instance.IsRunCleared))
            return;

        if (isCollected)
            return;

        if (player == null ||
            playerLevel == null)
        {
            FindPlayer();

            if (player == null)
                return;
        }

        if (Time.time <
            spawnTime + magnetDelay)
        {
            return;
        }

        float distance =
            Vector2.Distance(
                transform.position,
                player.position
            );

        if (!isMagnet &&
            distance <= magnetRange)
        {
            StartMagnet();
        }

        if (isMagnet)
        {
            MoveTowardPlayer();
        }
    }

    private void StartMagnet()
    {
        isMagnet = true;

        rb.gravityScale = 0f;
        rb.linearVelocity = Vector2.zero;

        if (bodyCollider != null)
        {
            bodyCollider.enabled = false;
        }
    }

    private void MoveTowardPlayer()
    {
        transform.position =
            Vector2.MoveTowards(
                transform.position,
                player.position,
                moveSpeed * Time.deltaTime
            );

        float distance =
            Vector2.Distance(
                transform.position,
                player.position
            );

        if (distance <= collectDistance)
        {
            Collect();
        }
    }

    private void Collect()
    {
        if (isCollected)
            return;

        isCollected = true;

        if (playerLevel != null)
        {
            playerLevel.AddExp(
                expAmount
            );
            SfxPlayer.Play(SfxId.ExpCollect);
        }

        ReturnOrDestroy();
    }

    private IEnumerator PopRoutine()
    {
        yield return null;

        float forceX =
            Random.Range(
                horizontalForceRange.x,
                horizontalForceRange.y
            );

        float forceY =
            Random.Range(
                verticalForceRange.x,
                verticalForceRange.y
            );

        rb.AddForce(
            new Vector2(
                forceX,
                forceY
            ),
            ForceMode2D.Impulse
        );
    }

    private void SetAppearance()
    {
        if (spriteRenderer == null)
            return;

        switch (expAmount)
        {
            case 2:
                spriteRenderer.color =
                    Color.green;
                break;

            case 5:
                spriteRenderer.color =
                    Color.yellow;
                break;

            case 10:
                spriteRenderer.color =
                    Color.blue;
                break;

            case 50:
                spriteRenderer.color =
                    Color.red;
                break;

            default:
                spriteRenderer.color =
                    Color.white;
                break;
        }

        transform.localScale =
            Vector3.one * 0.1f;
    }

    private void ReturnOrDestroy()
    {
        if (poolObject != null)
        {
            poolObject.ReturnToPool();
        }
        else
        {
            Destroy(gameObject);
        }
    }
}
