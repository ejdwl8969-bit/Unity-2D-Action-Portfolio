using System;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerAttack : MonoBehaviour
{
    [Header("Weapon")]
    [SerializeField] private Weapon currentWeapon;

    public Weapon CurrentWeapon =>
        currentWeapon;

    public WeaponType CurrentWeaponType =>
        GetWeaponType(currentWeapon);

    public event Action<Weapon> WeaponChanged;

    private void Update()
    {
        if (SceneTransition.IsTransitioning || PauseManager.IsGamePaused || (RunManager.Instance != null && RunManager.Instance.IsRunCleared))
            return;

        Keyboard keyboard =
            Keyboard.current;

        if (keyboard == null)
            return;

        if (keyboard.zKey.wasPressedThisFrame)
        {
            TryAttack();
        }
    }

    private void TryAttack()
    {
        if (currentWeapon == null)
            return;

        currentWeapon.TryAttack();
    }

    public void SetWeapon(
        Weapon weapon)
    {
        if (currentWeapon == weapon)
            return;

        CopyElementLevels(currentWeapon, weapon);

        currentWeapon = weapon;
        WeaponChanged?.Invoke(currentWeapon);
        GetComponentInChildren<PlayerAnimationController>(true)?.SetWeaponVisual(currentWeapon);
    }

    public bool SetWeapon(WeaponType weaponType)
    {
        Weapon weapon = GetWeapon(weaponType);
        if (weapon == null)
        {
            Debug.LogWarning($"{name}: {weaponType} weapon component was not found on this Player.", this);
            return false;
        }

        SetWeapon(weapon);
        return true;
    }

    public void SelectSword() => SetWeapon(WeaponType.Sword);
    public void SelectBow() => SetWeapon(WeaponType.Bow);
    public void SelectDagger() => SetWeapon(WeaponType.Dagger);

    public Weapon GetWeapon(WeaponType weaponType)
    {
        foreach (Weapon weapon in GetComponentsInChildren<Weapon>(true))
        {
            if (GetWeaponType(weapon) == weaponType)
                return weapon;
        }

        return null;
    }

    public int GetWeaponLevel(WeaponType weaponType)
    {
        Weapon weapon = GetWeapon(weaponType);
        return weapon != null ? weapon.Level : Weapon.MinLevel;
    }

    public void SetWeaponLevels(
        int swordLevel,
        int bowLevel,
        int daggerLevel)
    {
        GetWeapon(WeaponType.Sword)?.SetLevel(swordLevel);
        GetWeapon(WeaponType.Bow)?.SetLevel(bowLevel);
        GetWeapon(WeaponType.Dagger)?.SetLevel(daggerLevel);
    }

    public void SetElementLevelsForAllWeapons(
        int fire,
        int lightning,
        int ice,
        int wind)
    {
        foreach (Weapon weapon in GetComponentsInChildren<Weapon>(true))
            weapon.Element?.SetLevels(fire, lightning, ice, wind);
    }

    public bool AddElementLevelForAllWeapons(
        ElementType elementType,
        int amount)
    {
        if (amount <= 0)
            return false;

        bool applied = false;
        foreach (Weapon weapon in GetComponentsInChildren<Weapon>(true))
        {
            WeaponElement weaponElement = weapon.Element;
            if (weaponElement == null)
                continue;

            switch (elementType)
            {
                case ElementType.Fire:
                    weaponElement.AddFireLevel(amount);
                    break;
                case ElementType.Lightning:
                    weaponElement.AddLightningLevel(amount);
                    break;
                case ElementType.Ice:
                    weaponElement.AddIceLevel(amount);
                    break;
                case ElementType.Wind:
                    weaponElement.AddWindLevel(amount);
                    break;
                default:
                    continue;
            }

            applied = true;
        }

        return applied;
    }

    private static void CopyElementLevels(
        Weapon source,
        Weapon destination)
    {
        if (source == null || destination == null ||
            source.Element == null || destination.Element == null)
        {
            return;
        }

        destination.Element.SetLevels(
            source.Element.FireLevel,
            source.Element.LightningLevel,
            source.Element.IceLevel,
            source.Element.WindLevel
        );
    }
    private static WeaponType GetWeaponType(Weapon weapon)
    {
        return weapon switch
        {
            Bow => WeaponType.Bow,
            Dagger => WeaponType.Dagger,
            _ => WeaponType.Sword
        };
    }
}
