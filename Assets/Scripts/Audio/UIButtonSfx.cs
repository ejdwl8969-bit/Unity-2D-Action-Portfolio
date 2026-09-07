using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(Button))]
public sealed class UIButtonSfx : MonoBehaviour
{
    private Button button;
    private bool listenerRegistered;

    private void Awake()
    {
        button = GetComponent<Button>();
    }

    private void OnEnable()
    {
        if (button == null)
            button = GetComponent<Button>();

        if (button == null || listenerRegistered)
            return;

        button.onClick.AddListener(PlayClick);
        listenerRegistered = true;
    }

    private void OnDisable()
    {
        if (button != null && listenerRegistered)
            button.onClick.RemoveListener(PlayClick);

        listenerRegistered = false;
    }

    private static void PlayClick()
    {
        SfxPlayer.Play(SfxId.UiClick);
    }
}
