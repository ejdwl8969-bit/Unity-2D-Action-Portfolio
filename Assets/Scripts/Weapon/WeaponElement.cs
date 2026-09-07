using UnityEngine;

[System.Serializable]
public class WeaponElement
{
    [Header("Element Level")]
    [SerializeField, Min(0)]
    private int fireLevel;

    [SerializeField, Min(0)]
    private int lightningLevel;

    [SerializeField, Min(0)]
    private int iceLevel;

    [SerializeField, Min(0)]
    private int windLevel;

    public int FireLevel => fireLevel;
    public int LightningLevel => lightningLevel;
    public int IceLevel => iceLevel;
    public int WindLevel => windLevel;

    public void AddFireLevel(int value)
    {
        fireLevel =
            Mathf.Max(
                0,
                fireLevel + value
            );
    }

    public void AddLightningLevel(int value)
    {
        lightningLevel =
            Mathf.Max(
                0,
                lightningLevel + value
            );
    }

    public void AddIceLevel(int value)
    {
        iceLevel =
            Mathf.Max(
                0,
                iceLevel + value
            );
    }

    public void AddWindLevel(int value)
    {
        windLevel =
            Mathf.Max(
                0,
                windLevel + value
            );
    }

    public int GetLevel(ElementType type)
    {
        switch (type)
        {
            case ElementType.Fire:
                return fireLevel;

            case ElementType.Lightning:
                return lightningLevel;

            case ElementType.Ice:
                return iceLevel;

            case ElementType.Wind:
                return windLevel;

            default:
                return 0;
        }
    }

    public bool IsActive(ElementType type)
    {
        return GetLevel(type) > 0;
    }

    public ElementType GetMainElement()
    {
        if (fireLevel > 0)
            return ElementType.Fire;

        if (lightningLevel > 0)
            return ElementType.Lightning;

        if (iceLevel > 0)
            return ElementType.Ice;

        if (windLevel > 0)
            return ElementType.Wind;

        return ElementType.None;
    }
    public int GetMainElementLevel()
    {
        ElementType mainElement =
            GetMainElement();

        return GetLevel(
            mainElement
        );
    }

    public void SetLevels(
    int fire,
    int lightning,
    int ice,
    int wind)
    {
        fireLevel =
            Mathf.Max(
                0,
                fire
            );

        lightningLevel =
            Mathf.Max(
                0,
                lightning
            );

        iceLevel =
            Mathf.Max(
                0,
                ice
            );

        windLevel =
            Mathf.Max(
                0,
                wind
            );
    }
}