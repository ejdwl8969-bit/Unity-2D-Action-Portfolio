using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class EnemyHealthBar : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private EnemyHealth enemyHealth;
    [SerializeField] private Image fillImage;
    [SerializeField] private Image delayFillImage;
    [SerializeField] private GameObject barRoot;

    [Header("Display")]
    [SerializeField, Min(0f)]
    private float visibleTime = 2f;

    [SerializeField, Min(0.01f)]
    private float delayFillSpeed = 3f;

    private Coroutine hideCoroutine;

    private void Awake()
    {
        if (enemyHealth == null)
        {
            enemyHealth =
                GetComponentInParent<EnemyHealth>();
        }
    }

    private void Start()
    {
        if (barRoot != null)
        {
            barRoot.SetActive(false);
        }

        SetFillAmount(1f);
    }

    private void Update()
    {
        if (enemyHealth == null)
            return;

        UpdateHealthBar();
    }

    private void UpdateHealthBar()
    {
        float targetFill =
            Mathf.Clamp01(
                enemyHealth.GetHPRatio()
            );

        if (fillImage != null)
        {
            fillImage.fillAmount =
                targetFill;
        }

        if (delayFillImage == null)
            return;

        delayFillImage.fillAmount =
            Mathf.MoveTowards(
                delayFillImage.fillAmount,
                targetFill,
                delayFillSpeed * Time.deltaTime
            );
    }

    public void Show()
    {
        if (barRoot == null)
            return;

        barRoot.SetActive(true);

        if (hideCoroutine != null)
        {
            StopCoroutine(
                hideCoroutine
            );
        }

        hideCoroutine =
            StartCoroutine(
                HideRoutine()
            );
    }

    public void Hide()
    {
        if (hideCoroutine != null)
        {
            StopCoroutine(
                hideCoroutine
            );

            hideCoroutine = null;
        }

        if (barRoot != null)
        {
            barRoot.SetActive(false);
        }
    }

    private IEnumerator HideRoutine()
    {
        yield return new WaitForSeconds(
            visibleTime
        );

        if (barRoot != null)
        {
            barRoot.SetActive(false);
        }

        hideCoroutine = null;
    }

    private void SetFillAmount(float amount)
    {
        amount =
            Mathf.Clamp01(amount);

        if (fillImage != null)
        {
            fillImage.fillAmount =
                amount;
        }

        if (delayFillImage != null)
        {
            delayFillImage.fillAmount =
                amount;
        }
    }

    private void OnDisable()
    {
        if (hideCoroutine != null)
        {
            StopCoroutine(
                hideCoroutine
            );

            hideCoroutine = null;
        }
    }
}