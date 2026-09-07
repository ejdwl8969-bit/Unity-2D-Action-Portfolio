using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerController : MonoBehaviour
{
    [Header("Movement")]
    public float moveSpeed = 7f;
    public float jumpForce = 7f;

    [Header("Jump")]
    [SerializeField] private int maxJumpCount = 2;

    public bool IsFacingRight { get; private set; } = true;

    private Rigidbody2D rb;
    private SpriteRenderer spriteRenderer;
    private WeaponSelectionChest weaponSelectionChest;

    private int jumpCount = 0;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        PlayerAnimationController.AttachTo(this);
    }

    private void Update()
    {
        if (SceneTransition.IsTransitioning || PauseManager.IsGamePaused || (RunManager.Instance != null && RunManager.Instance.IsRunCleared))
            return;

        Keyboard keyboard = Keyboard.current;

        if (keyboard == null)
            return;

        HandleMovement(keyboard);
        HandleJump(keyboard);
    }

    private void HandleMovement(Keyboard keyboard)
    {
        float x = 0f;

        if (keyboard.aKey.isPressed ||
            keyboard.leftArrowKey.isPressed)
        {
            x = -1f;
        }
        else if (keyboard.dKey.isPressed ||
                 keyboard.rightArrowKey.isPressed)
        {
            x = 1f;
        }

        rb.linearVelocity =
            new Vector2(
                x * moveSpeed,
                rb.linearVelocity.y
            );

        if (x > 0f)
        {
            SetFacingDirection(true);
        }
        else if (x < 0f)
        {
            SetFacingDirection(false);
        }
    }

    private void HandleJump(Keyboard keyboard)
    {
        if (!keyboard.spaceKey.wasPressedThisFrame)
            return;

        if (weaponSelectionChest != null && weaponSelectionChest.TrySelectWeapon(this))
            return;

        if (jumpCount >= maxJumpCount)
            return;

        // 2단 점프 시 기존 낙하 속도를 제거하고
        // 일정한 높이로 다시 점프
        rb.linearVelocity =
            new Vector2(
                rb.linearVelocity.x,
                jumpForce
            );

        jumpCount++;
        SfxPlayer.Play(SfxId.PlayerJump);
        GetComponentInChildren<PlayerAnimationController>()?.PlayJump(jumpCount);
    }

    internal void RegisterWeaponSelectionChest(WeaponSelectionChest chest)
    {
        if (chest != null)
        {
            weaponSelectionChest = chest;
        }
    }

    internal void UnregisterWeaponSelectionChest(WeaponSelectionChest chest)
    {
        if (weaponSelectionChest == chest)
        {
            weaponSelectionChest = null;
        }
    }

    private void SetFacingDirection(bool facingRight)
    {
        IsFacingRight = facingRight;

        if (spriteRenderer != null)
        {
            spriteRenderer.flipX = !facingRight;
        }
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        TryResetJumpCount(collision);
    }

    private void OnCollisionStay2D(Collision2D collision)
    {
        TryResetJumpCount(collision);
    }

    private void TryResetJumpCount(Collision2D collision)
    {
        if (!collision.gameObject.CompareTag("Ground"))
            return;

        for (int i = 0; i < collision.contactCount; i++)
        {
            if (collision.GetContact(i).normal.y > 0.5f)
            {
                jumpCount = 0;
                return;
            }
        }
    }

    public void AddMoveSpeed(float value)
    {
        moveSpeed += value;

    }
}
