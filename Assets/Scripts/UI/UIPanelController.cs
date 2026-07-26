using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;

public class UIPanelController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Animator panelAnimator;
    [SerializeField] private InputActionReference enterAction;
    
    [Header("Animation Settings")]
    [SerializeField] private string showTrigger = "Show";
    [SerializeField] private string hideTrigger = "Hide";
    
    private bool isPanelActive = false;
    private bool isAnimating = false;
    private bool isControlEnabled = false; // запоминаем состояние управления

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

    private void Start()
    {
        // Анимации работают при остановленном времени
        if (panelAnimator != null)
            panelAnimator.updateMode = AnimatorUpdateMode.UnscaledTime;
        
        // 1. Отключаем управление через GameManager
        SetPlayerControlEnabled(false);
        
        // 2. Запускаем анимацию прилета
        ShowPanel();
        
        // 3. Ждём окончания анимации
        StartCoroutine(WaitForAnimationEnd("In", OnInAnimationEnd));
    }

    private void OnEnterPressed(InputAction.CallbackContext context)
    {
        if (!isPanelActive || isAnimating) return;
        
        // Запускаем анимацию вылета
        HidePanel();
    }

    public void ShowPanel()
    {
        if (panelAnimator == null) return;
        panelAnimator.SetTrigger(showTrigger);
        isAnimating = true;
        isPanelActive = false;
    }

    public void HidePanel()
    {
        if (panelAnimator == null) return;
        panelAnimator.SetTrigger(hideTrigger);
        isAnimating = true;
        isPanelActive = false;
        
        StartCoroutine(WaitForAnimationEnd("Out", OnOutAnimationEnd));
    }

    // --- Ожидание окончания анимации (работает при Time.timeScale = 0) ---
    private IEnumerator WaitForAnimationEnd(string clipName, System.Action onComplete)
    {
        float duration = GetAnimationLength(clipName);
        float elapsed = 0f;
        
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }
        
        onComplete?.Invoke();
    }

    private float GetAnimationLength(string clipName)
    {
        if (panelAnimator == null) return 1f;
        
        AnimationClip[] clips = panelAnimator.runtimeAnimatorController.animationClips;
        foreach (var clip in clips)
        {
            if (clip.name.ToLower().Contains(clipName.ToLower()))
                return clip.length;
        }
        return 1f;
    }

    // --- Callback'и окончания анимаций ---
    private void OnInAnimationEnd()
    {
        isAnimating = false;
        isPanelActive = true;
        Debug.Log("Панель показана. Ждём Enter...");
    }

    private void OnOutAnimationEnd()
    {
        isAnimating = false;
        isPanelActive = false;
        
        // ВОЗВРАЩАЕМ УПРАВЛЕНИЕ через GameManager
        SetPlayerControlEnabled(true);
        Debug.Log("Панель скрыта. Управление возвращено.");
    }

    // --- УПРАВЛЕНИЕ ЧЕРЕЗ GAME MANAGER ---
    private void SetPlayerControlEnabled(bool enabled)
    {
        if (GameManager.Instance == null)
        {
            Debug.LogWarning("GameManager.Instance не найден!");
            return;
        }

        // Используем методы GameManager для управления мотоциклом
        if (enabled)
        {
            // Включаем управление
            EnableBikeController(true);
            EnableCargoController(true);
        }
        else
        {
            // Отключаем управление
            EnableBikeController(false);
            EnableCargoController(false);
        }

        isControlEnabled = enabled;
        Debug.Log($"Управление {(enabled ? "включено" : "отключено")}");
    }

    // Вспомогательные методы для включения/отключения контроллеров
    private void EnableBikeController(bool enable)
    {
        if (GameManager.Instance.bike == null) return;
        
        var controller = GameManager.Instance.bike.GetComponentInChildren<MotorcycleController>();
        if (controller != null)
            controller.enabled = enable;
        
        // Если отключаем - сбрасываем скорость
        if (!enable)
        {
            var rb = GameManager.Instance.bike.GetComponentInChildren<Rigidbody2D>();
            if (rb != null)
            {
                rb.linearVelocity = Vector2.zero;
                rb.angularVelocity = 0f;
            }
        }
    }

    private void EnableCargoController(bool enable)
    {
        if (GameManager.Instance.cargo == null) return;
        
        var controller = GameManager.Instance.cargo.GetComponentInChildren<CargoController>();
        if (controller != null)
            controller.enabled = enable;
    }

    // --- Дополнительно: если панельку нужно скрыть принудительно ---
    public void ForceHidePanel()
    {
        if (!isPanelActive && !isAnimating) return;
        
        // Мгновенно скрываем и включаем управление
        StopAllCoroutines();
        panelAnimator.Rebind(); // сбрасываем аниматор
        isAnimating = false;
        isPanelActive = false;
        SetPlayerControlEnabled(true);
        gameObject.SetActive(false);
    }
}