using System;
using UnityEngine;

public class PlayerLevel : MonoBehaviour
{
    [Header("Level")]
    [SerializeField] private int level = 1;

    [Header("Experience")]
    [SerializeField] private int currentExp = 0;
    [SerializeField] private int needExp = 30;
    [SerializeField] private int expIncreasePerLevel = 20;

    public event Action OnLevelUp;

    public int Level => level;
    public int CurrentExp => currentExp;
    public int NeedExp => needExp;

    public void AddExp(int exp)
    {
        if (exp <= 0)
            return;

        currentExp += exp;       

        CheckLevelUp();
    }

    private void CheckLevelUp()
    {
        while (currentExp >= needExp)
        {
            LevelUp();
        }
    }

    private void LevelUp()
    {
        currentExp -= needExp;

        level++;

        needExp += expIncreasePerLevel;        

        OnLevelUp?.Invoke();
    }

    public void RestoreProgress(
    int savedLevel,
    int savedCurrentExp,
    int savedNeedExp)
    {
        level =
            Mathf.Max(
                1,
                savedLevel
            );

        currentExp =
            Mathf.Max(
                0,
                savedCurrentExp
            );

        needExp =
            Mathf.Max(
                1,
                savedNeedExp
            );        
    }
}