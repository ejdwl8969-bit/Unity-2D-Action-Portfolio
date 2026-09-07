using System;

// Immutable values captured at Boss2 death; no Unity object references or persistence.
public sealed class RunResultData
{
    public double ClearTime { get; }
    public int FinalLevel { get; }
    public string Weapon { get; }
    public string Element { get; }
    public int EnemiesDefeated { get; }
    public int DamageTaken { get; }

    public string FormattedClearTime
    {
        get
        {
            long seconds = (long)Math.Floor(ClearTime);
            return $"{seconds / 60:00}:{seconds % 60:00}";
        }
    }

    public RunResultData(double clearTime, int finalLevel, string weapon, string element,
        int enemiesDefeated, int damageTaken)
    {
        ClearTime = double.IsNaN(clearTime) || double.IsInfinity(clearTime) ? 0 : Math.Max(0, clearTime);
        FinalLevel = Math.Max(1, finalLevel);
        Weapon = string.IsNullOrWhiteSpace(weapon) ? "None" : weapon;
        Element = string.IsNullOrWhiteSpace(element) ? "None" : element;
        EnemiesDefeated = Math.Max(0, enemiesDefeated);
        DamageTaken = Math.Max(0, damageTaken);
    }
}
