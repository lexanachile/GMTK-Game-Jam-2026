using UnityEngine;
using UnityEngine.InputSystem;

public class MapInput : MonoBehaviour
{
    public GameObject mapPanel;

    void Update()
    {
        Keyboard kb = Keyboard.current;

        if (kb == null)
            return;

        if (Keyboard.current.tabKey.wasPressedThisFrame)
        {
            if (Keyboard.current.wKey.isPressed ||
                Keyboard.current.aKey.isPressed ||
                Keyboard.current.sKey.isPressed ||
                Keyboard.current.dKey.isPressed)
                return;

            mapPanel.SetActive(!mapPanel.activeSelf);
        }

        if (!mapPanel.activeSelf)
            return;

        if (kb.wKey.wasPressedThisFrame ||
            kb.aKey.wasPressedThisFrame ||
            kb.sKey.wasPressedThisFrame ||
            kb.dKey.wasPressedThisFrame)
        {
            mapPanel.SetActive(false);
        }
    }
}