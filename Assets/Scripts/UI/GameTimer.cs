using UnityEngine;
using UnityEngine.Events;
using TMPro; // если используешь TextMeshPro, иначе замени на UnityEngine.UI

public class GameTimer : MonoBehaviour
{
    [Header("Настройки времени")]
    [SerializeField] private float startTime = 60f;   // начальное время в секундах
    [SerializeField] private bool autoStart = true;    // запускать ли таймер при старте сцены

    [Header("UI")]
    [SerializeField] private TextMeshProUGUI timerText; // или Text (из UnityEngine.UI)
    [SerializeField] private string timeFormat = "mm':'ss"; // формат отображения (можно "hh':'mm':'ss")

    [Header("События")]
    public UnityEvent OnTimerEnd;   // вызывается, когда таймер достигает 0

    // Публичные свойства для доступа из других скриптов
    public float CurrentTime { get; private set; }
    public bool IsRunning { get; private set; }
    public bool IsPaused { get; private set; }

    private void Start()
    {
        ResetTimer(); // устанавливаем CurrentTime = startTime
        if (autoStart)
            StartTimer();
    }

    private void Update()
    {
        if (!IsRunning || IsPaused) return;

        // Уменьшаем время на прошедший кадр (используем unscaledDeltaTime, если нужно игнорировать Time.timeScale)
        CurrentTime -= Time.deltaTime; // или Time.unscaledDeltaTime

        // Обновляем отображение
        UpdateUI();

        // Проверяем окончание
        if (CurrentTime <= 0f)
        {
            CurrentTime = 0f;
            IsRunning = false;
            UpdateUI(); // обновить UI, чтобы показать 0
            OnTimerEnd?.Invoke(); // вызываем все привязанные методы
        }
    }

    /// <summary>
    /// Запустить таймер (если он не идёт)
    /// </summary>
    public void StartTimer()
    {
        if (CurrentTime <= 0f)
            ResetTimer(); // если время уже на нуле, сбрасываем на стартовое

        IsRunning = true;
        IsPaused = false;
    }

    /// <summary>
    /// Остановить таймер (сбрасывает флаг, но время сохраняет)
    /// </summary>
    public void StopTimer()
    {
        IsRunning = false;
    }

    /// <summary>
    /// Пауза / возобновление
    /// </summary>
    public void TogglePause()
    {
        if (IsRunning)
            IsPaused = !IsPaused;
    }

    /// <summary>
    /// Добавить время (положительное или отрицательное)
    /// </summary>
    public void AddTime(float seconds)
    {
        CurrentTime += seconds;
        if (CurrentTime < 0f) CurrentTime = 0f;
        UpdateUI();
    }

    /// <summary>
    /// Отнять время (удобный метод, можно использовать AddTime(-value))
    /// </summary>
    public void SubtractTime(float seconds)
    {
        AddTime(-seconds);
    }

    /// <summary>
    /// Сброс на начальное значение (останавливает таймер)
    /// </summary>
    public void ResetTimer()
    {
        CurrentTime = startTime;
        IsRunning = false;
        IsPaused = false;
        UpdateUI();
    }

    /// <summary>
    /// Обновить текстовое поле
    /// </summary>
    private void UpdateUI()
    {
        if (timerText != null)
        {
            // Форматируем время в зависимости от формата
            timerText.text = FormatTime(CurrentTime);
        }
    }

    /// <summary>
    /// Преобразует секунды в строку по заданному формату
    /// </summary>
    private string FormatTime(float timeInSeconds)
    {
        // Округляем до целых, чтобы не мелькали миллисекунды
        int totalSeconds = Mathf.FloorToInt(timeInSeconds);
        int hours = totalSeconds / 3600;
        int minutes = (totalSeconds % 3600) / 60;
        int seconds = totalSeconds % 60;

        // Подставляем в пользовательский формат (по умолчанию mm:ss)
        string formatted = timeFormat;
        formatted = formatted.Replace("hh", hours.ToString("00"));
        formatted = formatted.Replace("mm", minutes.ToString("00"));
        formatted = formatted.Replace("ss", seconds.ToString("00"));
        return formatted;
    }

    // Опционально: метод для установки стартового времени из кода
    public void SetStartTime(float newStartTime)
    {
        startTime = Mathf.Max(0, newStartTime);
        if (!IsRunning) ResetTimer();
    }
}