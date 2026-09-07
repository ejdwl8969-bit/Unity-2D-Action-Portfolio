using UnityEngine;

public enum ElementType
{
    None,
    Fire,
    Lightning,
    Ice,
    Wind
}

public struct DamageData
{
    public int Damage;
    public bool IsCritical;
    public ElementType Element;
    public int ElementLevel;

    public DamageData(
        int damage,
        bool isCritical = false,
        ElementType element = ElementType.None,
        int elementLevel = 0)
    {
        Damage = Mathf.Max(0, damage);
        IsCritical = isCritical;
        Element = element;
        ElementLevel = Mathf.Max(0, elementLevel);
    }
}