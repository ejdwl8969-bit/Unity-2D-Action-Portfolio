using System;
using UnityEngine;

/// <summary>Visual-only observer. Never writes the player's physics, combat or time scale.</summary>
[DisallowMultipleComponent]
public sealed class PlayerAnimationController : MonoBehaviour
{
    public enum Pose
    {
        Idle, Walk, JumpStart, JumpRise, JumpApex, JumpFall,
        DoubleJump, DoubleJumpRise, DoubleJumpFall, Land,
        SwordAttack1, SwordAttack2, SwordAttack3, Hit, Attack4
    }

    [Serializable]
    public struct FrameRegistration
    {
        public Sprite sprite;
        public float feetAbovePivot;
    }

    public const string ResourcePath = "PlayerAnimation/PlayerVisual";
    [SerializeField] private Animator animator;
    [SerializeField] private SpriteRenderer visualRenderer;
    [SerializeField] private RuntimeAnimatorController legacyRootController;
    [SerializeField] private RuntimeAnimatorController swordController;
    [SerializeField] private RuntimeAnimatorController bowController;
    [SerializeField] private RuntimeAnimatorController daggerController;
    [SerializeField] private AnimationClip[] clips;
    [SerializeField] private AnimationClip[] bowClips;
    [SerializeField] private AnimationClip[] daggerClips;
    [SerializeField] private FrameRegistration[] frameRegistration;
    [SerializeField, Min(0.01f)] private float referenceBodyHeight = 2f;

    private static readonly int SpeedId = Animator.StringToHash("Speed");
    private static readonly int VelocityId = Animator.StringToHash("YVelocity");
    private static readonly int GroundedId = Animator.StringToHash("IsGrounded");
    private static readonly int PoseId = Animator.StringToHash("VisualState");
    private static readonly int AttackSpeedId = Animator.StringToHash("AttackAnimSpeed");
    private readonly ContactPoint2D[] contacts = new ContactPoint2D[16];
    private PlayerController player;
    private PlayerAttack attack;
    private PlayerHealth health;
    private Rigidbody2D body;
    private BoxCollider2D bodyCollider;
    private SpriteRenderer facingAndFlashSource;
    private bool previousForceRenderingOff;
    private Animator previousRootAnimator;
    private bool previousAnimatorEnabled;
    private bool initialized;
    private bool wasGrounded;
    private bool doubleJump;
    private bool awaitingTakeoff;
    private Pose pose;
    private float oneShotRemaining;
    private int oneShotPriority;
    private Vector3 feetLocal;
    private Sprite registeredSprite;
    private float currentFeetOffset;
    private AnimationClip[] activeClips;
    private Weapon attackVisualSource;

    // The saved scenes contain independent Player objects, not instances of a shared Player prefab.
    // A shared visual prefab is attached in Awake, also covering players spawned at runtime.
    public static void AttachTo(PlayerController owner)
    {
        if (owner == null || !Application.isPlaying) return;

        GameObject prefab = Resources.Load<GameObject>(ResourcePath);
        PlayerAnimationController template = prefab != null ? prefab.GetComponent<PlayerAnimationController>() : null;
        if (template == null || template.animator == null || template.animator.runtimeAnimatorController == null)
        {
            Debug.LogWarning("PlayerVisual is missing. Run Tools > Player Animation > Setup and Validate.", owner);
            return;
        }
        if (!template.CanTakeOver(owner.GetComponent<Animator>()))
        {
            Debug.LogWarning("PlayerVisual cannot replace an unverified Animator or a controller with non-visual animation. The original Player is preserved.", owner);
            return;
        }

        // Include inactive children: retrying attachment must not create another visual.
        foreach (PlayerAnimationController child in owner.GetComponentsInChildren<PlayerAnimationController>(true))
        {
            if (child.GetComponentInParent<PlayerController>() != owner) continue;
            child.gameObject.SetActive(true);
            child.enabled = true;
            if (child.Initialize())
            {
                child.TakeVisualOwnership();
            }
            return;
        }
        GameObject visual = Instantiate(prefab, owner.transform, false);
        visual.name = "PlayerVisual";
    }

    private bool CanTakeOver(Animator existing)
    {
        if (existing == null || existing.runtimeAnimatorController == null) return true;
        RuntimeAnimatorController controller = existing.runtimeAnimatorController;
        if (controller == animator.runtimeAnimatorController) return true;
        // Asset identity, not a controller/clip name heuristic. Other authored systems stay protected.
        if (legacyRootController == null || controller != legacyRootController) return false;
        foreach (AnimationClip clip in controller.animationClips)
            if (clip == null || clip.events.Length != 0) return false;
#if UNITY_EDITOR
        // Includes unsaved Animator edits in the running Editor, not just the file on disk.
        return IsSpriteOnlyLegacyController(controller);
#else
        // The same asset is audited again by the Editor pre-build validation.
        return true;
#endif
    }

    private void Awake() { Initialize(); }

    private bool Initialize()
    {
        if (initialized) return true;
        player = GetComponentInParent<PlayerController>();
        if (player == null || transform == player.transform) return false;
        GameObject prefab = Resources.Load<GameObject>(ResourcePath);
        PlayerAnimationController template = prefab != null ? prefab.GetComponent<PlayerAnimationController>() : null;
        if (template == null || template.animator == null || !template.CanTakeOver(player.GetComponent<Animator>()))
            return false;
        body = player.GetComponent<Rigidbody2D>();
        bodyCollider = player.GetComponent<BoxCollider2D>();
        facingAndFlashSource = player.GetComponent<SpriteRenderer>();
        attack = player.GetComponent<PlayerAttack>();
        health = player.GetComponent<PlayerHealth>();
        if (animator == null || visualRenderer == null || body == null ||
            bodyCollider == null || facingAndFlashSource == null ||
            swordController == null || bowController == null || daggerController == null ||
            clips == null || clips.Length != 15 || bowClips == null || bowClips.Length != 15 ||
            daggerClips == null || daggerClips.Length != 15)
        {
            Debug.LogError("PlayerVisual references are incomplete; the original Player renderer is preserved.", this);
            enabled = false;
            return false;
        }

        // Reused children also use the shared prefab's controller and visual data.
        if (animator.gameObject != gameObject || visualRenderer.gameObject != gameObject) return false;
        swordController = template.swordController;
        bowController = template.bowController;
        daggerController = template.daggerController;
        animator.runtimeAnimatorController = swordController;
        clips = template.clips;
        bowClips = template.bowClips;
        daggerClips = template.daggerClips;
        frameRegistration = template.frameRegistration;
        referenceBodyHeight = template.referenceBodyHeight;
        legacyRootController = template.legacyRootController;
        // Scale/registration apply only to this new visual child, never the Player root.
        float scale = bodyCollider.size.y / Mathf.Max(0.01f, referenceBodyHeight);
        transform.localScale = Vector3.one * scale;
        feetLocal = bodyCollider.offset + Vector2.down * (bodyCollider.size.y * 0.5f);
        previousForceRenderingOff = facingAndFlashSource.forceRenderingOff;
        facingAndFlashSource.forceRenderingOff = true;
        previousRootAnimator = player.GetComponent<Animator>();
        if (previousRootAnimator != null)
        {
            previousAnimatorEnabled = previousRootAnimator.enabled;
            previousRootAnimator.enabled = false;
        }
        animator.applyRootMotion = false;
        animator.updateMode = AnimatorUpdateMode.Normal;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        initialized = true;
        activeClips = clips;
        TakeVisualOwnership();
        wasGrounded = ReadGrounded();
        animator.SetFloat(AttackSpeedId, 1f);
        SetWeaponVisual(attack != null ? attack.CurrentWeapon : null);
        SetPose(wasGrounded ? Pose.Idle : Pose.JumpFall, true);
        MirrorRenderer();
        return true;
    }

    private void OnEnable()
    {
        if (initialized) TakeVisualOwnership();
    }

    private void TakeVisualOwnership()
    {
        if (!initialized) return;
        facingAndFlashSource.forceRenderingOff = true;
        if (previousRootAnimator != null) previousRootAnimator.enabled = false;
        animator.enabled = true;
        MirrorRenderer();
    }

    private void OnDisable()
    {
        if (!initialized) return;
        if (facingAndFlashSource != null)
            facingAndFlashSource.forceRenderingOff = previousForceRenderingOff;
        if (visualRenderer != null) visualRenderer.enabled = false;
        if (animator != null) animator.enabled = false;
        if (previousRootAnimator != null) previousRootAnimator.enabled = previousAnimatorEnabled;
    }

    private bool IsFinished => (health != null && health.IsDead) ||
        (RunManager.Instance != null && RunManager.Instance.IsRunCleared);

    private void Update()
    {
        if (!initialized) return;
        animator.enabled = !IsFinished;
        if (IsFinished || Time.deltaTime <= 0f) return;

        bool grounded = ReadGrounded();
        Vector2 velocity = body.linearVelocity;
        animator.SetFloat(SpeedId, Mathf.Abs(velocity.x));
        animator.SetFloat(VelocityId, velocity.y);
        animator.SetBool(GroundedId, grounded);

        if (awaitingTakeoff && !grounded) awaitingTakeoff = false;
        if (grounded) doubleJump = false;

        // A weapon swap cancels only the old attack visual, not gameplay or cooldown.
        if (oneShotPriority == 2 && attackVisualSource != null &&
            !(attack != null && attack.CurrentWeapon == attackVisualSource))
        {
            oneShotRemaining = 0f;
            attackVisualSource = null;
        }
        oneShotRemaining = Mathf.Max(0f, oneShotRemaining - Time.deltaTime);
        if (oneShotRemaining <= 0f)
        {
            oneShotPriority = 0;
            attackVisualSource = null;
        }

        if (grounded && !wasGrounded && oneShotPriority == 0)
            PlayOneShot(Pose.Land, 1);
        wasGrounded = grounded;
        if (oneShotPriority > 0) return;

        if (grounded)
            SetPose(Mathf.Abs(velocity.x) > 0.05f ? Pose.Walk : Pose.Idle);
        else if (doubleJump)
            SetPose(velocity.y > 0.05f ? Pose.DoubleJumpRise : Pose.DoubleJumpFall);
        else if (velocity.y > 0.15f)
            SetPose(Pose.JumpRise);
        else if (velocity.y < -0.15f)
            SetPose(Pose.JumpFall);
        else
            SetPose(Pose.JumpApex);
    }

    private void LateUpdate()
    {
        if (initialized) MirrorRenderer();
    }

    private bool ReadGrounded()
    {
        if (awaitingTakeoff || body.linearVelocity.y > 0.05f) return false;
        int count = body.GetContacts(contacts);
        for (int i = 0; i < count; i++)
        {
            ContactPoint2D contact = contacts[i];
            Collider2D other = contact.collider.attachedRigidbody == body ? contact.otherCollider : contact.collider;
            if (other != null && !other.isTrigger && other.CompareTag("Ground") &&
                contact.normal.y > 0.5f)
                return true;
        }
        return false;
    }

    private void MirrorRenderer()
    {
        if (facingAndFlashSource == null) return;
        visualRenderer.enabled = facingAndFlashSource.enabled && !previousForceRenderingOff;
        visualRenderer.flipX = facingAndFlashSource.flipX;
        visualRenderer.flipY = facingAndFlashSource.flipY;
        visualRenderer.color = facingAndFlashSource.color;
        visualRenderer.sortingLayerID = facingAndFlashSource.sortingLayerID;
        visualRenderer.sortingOrder = facingAndFlashSource.sortingOrder;
        if (visualRenderer.sharedMaterial != facingAndFlashSource.sharedMaterial)
            visualRenderer.sharedMaterial = facingAndFlashSource.sharedMaterial;
        if (registeredSprite != visualRenderer.sprite)
        {
            registeredSprite = visualRenderer.sprite;
            currentFeetOffset = 0f;
            if (frameRegistration != null)
                foreach (FrameRegistration frame in frameRegistration)
                    if (frame.sprite == registeredSprite)
                    {
                        currentFeetOffset = frame.feetAbovePivot;
                        break;
                    }
        }
        // Remove the source sheet's transparent padding; physics supplies the jump height.
        transform.localPosition = feetLocal - Vector3.up * (currentFeetOffset * transform.localScale.y);
    }

    public void SetWeaponVisual(Weapon weapon)
    {
        if (!initialized || animator == null)
            return;

        RuntimeAnimatorController nextController;
        AnimationClip[] nextClips;
        switch (weapon)
        {
            case Bow:
                nextController = bowController;
                nextClips = bowClips;
                break;
            case Dagger:
                nextController = daggerController;
                nextClips = daggerClips;
                break;
            case Sword:
            default:
                nextController = swordController;
                nextClips = clips;
                break;
        }

        if (nextController == null || nextClips == null || nextClips.Length != 15)
            return;

        if (oneShotPriority == 2 && attackVisualSource != null && attackVisualSource != weapon)
        {
            oneShotRemaining = 0f;
            oneShotPriority = 0;
            attackVisualSource = null;
        }

        activeClips = nextClips;
        if (animator.runtimeAnimatorController == nextController)
            return;

        animator.runtimeAnimatorController = nextController;
        animator.SetFloat(AttackSpeedId, 1f);
        animator.SetInteger(PoseId, (int)pose);
        animator.Play("Base Layer." + pose, 0, 0f);
        MirrorRenderer();
    }

    public void PlayJump(int executedJumpNumber)
    {
        if (!initialized || IsFinished || executedJumpNumber < 1) return;
        awaitingTakeoff = true;
        wasGrounded = false;
        doubleJump = executedJumpNumber >= 2;
        PlayOneShot(doubleJump ? Pose.DoubleJump : Pose.JumpStart, 1);
    }

    public void PlaySwordAttack(Sword source, int comboIndex, float attackCooldown)
    {
        if (!initialized || IsFinished || attack == null || attack.CurrentWeapon != source ||
            comboIndex < 1 || comboIndex > 3 || oneShotPriority > 2)
            return;
        Pose next = (Pose)((int)Pose.SwordAttack1 + comboIndex - 1);
        float length = activeClips[(int)next].length;
        float duration = Mathf.Min(length, Mathf.Max(0.01f, attackCooldown));
        animator.SetFloat(AttackSpeedId, length / duration);
        attackVisualSource = source;
        PlayOneShot(next, 2, duration);
    }

    public void PlayBowAttack(Bow source, float attackCooldown)
    {
        if (!initialized || IsFinished || attack == null || attack.CurrentWeapon != source ||
            oneShotPriority > 2)
            return;
        Pose next = Pose.SwordAttack1;
        float length = activeClips[(int)next].length;
        float duration = Mathf.Min(length, Mathf.Max(0.01f, attackCooldown));
        animator.SetFloat(AttackSpeedId, length / duration);
        attackVisualSource = source;
        PlayOneShot(next, 2, duration);
    }

    public void PlayDaggerAttack(Dagger source, int comboIndex, float attackCooldown)
    {
        if (!initialized || IsFinished || attack == null || attack.CurrentWeapon != source ||
            comboIndex < 1 || comboIndex > 4 || oneShotPriority > 2)
            return;
        Pose next = comboIndex == 4
            ? Pose.Attack4
            : (Pose)((int)Pose.SwordAttack1 + comboIndex - 1);
        float length = activeClips[(int)next].length;
        float duration = Mathf.Min(length, Mathf.Max(0.01f, attackCooldown));
        animator.SetFloat(AttackSpeedId, length / duration);
        attackVisualSource = source;
        PlayOneShot(next, 2, duration);
    }

    public void PlayHit()
    {
        if (!initialized || IsFinished) return;
        attackVisualSource = null;
        PlayOneShot(Pose.Hit, 3);
    }

    private void PlayOneShot(Pose next, int priority, float duration = -1f)
    {
        if (oneShotPriority > priority) return;
        oneShotPriority = priority;
        AnimationClip clip = activeClips != null && (int)next < activeClips.Length
            ? activeClips[(int)next] : null;
        if (clip == null) return;
        oneShotRemaining = duration > 0f ? duration : clip.length;
        SetPose(next, true);
    }

    private void SetPose(Pose next, bool restart = false)
    {
        if (pose == next && !restart) return;
        pose = next;
        animator.SetInteger(PoseId, (int)next);
        if (restart) animator.Play("Base Layer." + next, 0, 0f);
    }

#if UNITY_EDITOR
    // Shared by the runtime handoff (Editor only), setup and the build gate.
    public static bool IsSpriteOnlyLegacyController(RuntimeAnimatorController controller)
    {
        if (!(controller is UnityEditor.Animations.AnimatorController authored)) return false;
        foreach (AnimationClip clip in controller.animationClips)
        {
            if (clip == null || clip.events.Length != 0 || UnityEditor.AnimationUtility.GetCurveBindings(clip).Length != 0)
                return false;
            foreach (var binding in UnityEditor.AnimationUtility.GetObjectReferenceCurveBindings(clip))
                if (binding.path != "" || binding.type != typeof(SpriteRenderer) || binding.propertyName != "m_Sprite")
                    return false;
        }
        foreach (var layer in authored.layers)
            if (!HasNoBehaviours(layer.stateMachine)) return false;
        return true;
    }

    private static bool HasNoBehaviours(UnityEditor.Animations.AnimatorStateMachine machine)
    {
        if (machine.behaviours.Length != 0) return false;
        foreach (var state in machine.states)
            if (state.state.behaviours.Length != 0) return false;
        foreach (var child in machine.stateMachines)
            if (!HasNoBehaviours(child.stateMachine)) return false;
        return true;
    }

    [ContextMenu("Debug/Play Hit (visual only)")]
    private void DebugHit() { if (Application.isPlaying) PlayHit(); }
    [ContextMenu("Debug/Play Double Jump (visual only)")]
    private void DebugJump() { if (Application.isPlaying) PlayJump(2); }
    [ContextMenu("Debug/Play Sword Attack 1 (visual only)")]
    private void DebugAttack1() { DebugAttack(1); }
    [ContextMenu("Debug/Play Sword Attack 2 (visual only)")]
    private void DebugAttack2() { DebugAttack(2); }
    [ContextMenu("Debug/Play Sword Attack 3 (visual only)")]
    private void DebugAttack3() { DebugAttack(3); }
    private void DebugAttack(int index)
    {
        if (Application.isPlaying && attack != null && attack.CurrentWeapon is Sword sword)
            PlaySwordAttack(sword, index, sword.Stat.attackCooldown);
    }
#endif
}
