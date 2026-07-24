using UnityEngine;
using UnityEngine.InputSystem;

public class RestartUI : MonoBehaviour
{
    void Update()
    {
        if (Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame)
        {
            if (GameManager.Instance != null)
                GameManager.Instance.RestartLevel();
        }
    }
}