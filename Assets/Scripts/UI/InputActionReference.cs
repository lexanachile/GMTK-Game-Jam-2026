using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public class RestartByEnter : MonoBehaviour
{
    [SerializeField] private InputActionReference enterAction;

    private void OnEnable()
    {
        if (enterAction != null)
            enterAction.action.performed += OnEnterPressed;
    }

    private void OnDisable()
    {
        if (enterAction != null)
            enterAction.action.performed -= OnEnterPressed;
    }

    private void OnEnterPressed(InputAction.CallbackContext context)
    {
        int currentScene = SceneManager.GetActiveScene().buildIndex;
        SceneManager.LoadScene(currentScene);
    }
}