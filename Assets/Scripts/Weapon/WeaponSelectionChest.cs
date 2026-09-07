using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(BoxCollider2D))]
public sealed class WeaponSelectionChest : MonoBehaviour
{
    [SerializeField] private WeaponType weaponType = WeaponType.Bow;
    [SerializeField] private GameObject interactionPrompt;

    private readonly HashSet<Collider2D> overlappingPlayerColliders = new HashSet<Collider2D>();
    private PlayerController currentPlayer;

    public WeaponType WeaponType => weaponType;

    private void Awake()
    {
        BoxCollider2D trigger = GetComponent<BoxCollider2D>();
        trigger.isTrigger = true;
        SetPromptVisible(false);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        PlayerController player = other.GetComponentInParent<PlayerController>();

        if (player == null || player.GetComponent<PlayerAttack>() == null)
            return;

        if (currentPlayer != null && currentPlayer != player)
            return;

        currentPlayer = player;
        overlappingPlayerColliders.Add(other);
        currentPlayer.RegisterWeaponSelectionChest(this);
        SetPromptVisible(true);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (!overlappingPlayerColliders.Remove(other) || overlappingPlayerColliders.Count > 0)
            return;

        ClearCurrentPlayer();
    }

    private void OnDisable()
    {
        overlappingPlayerColliders.Clear();
        ClearCurrentPlayer();
    }

    internal bool TrySelectWeapon(PlayerController player)
    {
        if (player == null || player != currentPlayer || overlappingPlayerColliders.Count == 0)
            return false;

        PlayerAttack playerAttack = player.GetComponent<PlayerAttack>();

        if (playerAttack == null)
            return false;

        playerAttack.SetWeapon(weaponType);
        return true;
    }

    private void ClearCurrentPlayer()
    {
        if (currentPlayer != null)
        {
            currentPlayer.UnregisterWeaponSelectionChest(this);
            currentPlayer = null;
        }

        SetPromptVisible(false);
    }

    private void SetPromptVisible(bool visible)
    {
        if (interactionPrompt != null)
        {
            interactionPrompt.SetActive(visible);
        }
    }
}