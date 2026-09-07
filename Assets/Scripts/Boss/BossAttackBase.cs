using System.Collections;
using UnityEngine;

public abstract class BossAttackBase : MonoBehaviour
{
    public abstract IEnumerator Execute(int phase);
}