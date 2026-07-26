using UnityEngine;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;

    [Header("Объекты (перетащите корневые)")]
    public GameObject bike;
    public GameObject cargo;

    [Header("Стартовые позиции")]
    public Vector3 bikeStartPos;
    public Vector3 cargoStartPos;
    public Quaternion bikeStartRot = Quaternion.identity;
    public Quaternion cargoStartRot = Quaternion.identity;

    [Header("UI")]
    public GameObject playButton;
    public GameObject restartPanel;
    public GameObject gameEndPanel;

    [Header("Monster spawn distances")]
    public float innerSpawnDist = 3f;   // первый прямоугольник (ближний)
    public float middleSpawnDist = 6f;  // второй прямоугольник (граница спавна)
    public float outerSleepDist = 10f;  // третий прямоугольник (граница сна)

    public GridManager gridManager;
    public MonsterManager monsterManager;
    public PreSpawner preSpawner;
    public GameTimer gameTimer;

    // Автоматически найденные компоненты
    private SpriteRenderer bikeSprite;
    private Collider2D bikeCollider;
    private Rigidbody2D bikeRb;
    private BikeExplosion bikeExplosion;
    private MotorcycleController bikeController;
    private SpriteRenderer cargoSprite;
    private Collider2D cargoCollider;
    private CargoExplosion cargoExplosion;
    private CargoController cargoController;

    private void Awake()
    {
        if (Instance == null)
            {
                Instance = this;
                CacheComponents();
                DontDestroyOnLoad(gameObject);
            }
            else
            {
                Destroy(gameObject);
            }
    }

    /// Ищет все нужные компоненты на корневых объектах и их детях
    private void CacheComponents()
    {
        if (bike != null)
        {
            bikeSprite = bike.GetComponentInChildren<SpriteRenderer>();
            bikeCollider = bike.GetComponentInChildren<Collider2D>();
            bikeRb = bike.GetComponentInChildren<Rigidbody2D>();
            bikeExplosion = bike.GetComponentInChildren<BikeExplosion>();
            bikeController = bike.GetComponentInChildren<MotorcycleController>();
        }
        if (cargo != null)
        {
            cargoSprite = cargo.GetComponentInChildren<SpriteRenderer>();
            cargoCollider = cargo.GetComponentInChildren<Collider2D>();
            cargoExplosion = cargo.GetComponentInChildren<CargoExplosion>();
            cargoController = cargo.GetComponentInChildren<CargoController>();
        }
    }

    /// <summary>
    /// Запуск / перезапуск уровня
    /// </summary>
    public void StartGame()
    {
        // Позиции и повороты
        SetTransform(bike, bikeStartPos, bikeStartRot);
        SetTransform(cargo, cargoStartPos, cargoStartRot);

        // Физика
        ResetRigidbody(bike);
        ResetRigidbody(cargo);

        // Сброс флагов взрывов
        if (bikeExplosion) bikeExplosion.SetExploded(false);
        if (cargoExplosion) cargoExplosion.SetExploded(false);

        // Монстры: уничтожаем всех и спавним заново через PreSpawner
        if (monsterManager) monsterManager.DestroyAllMonsters();
        if (preSpawner) preSpawner.Respawn();

        // Включаем визуал, коллизии и управление
        EnableBike(true);
        EnableCargo(true);

        // Объекты активны
        if (bike) bike.SetActive(true);
        if (cargo) cargo.SetActive(true);

        // UI
        if (playButton) playButton.SetActive(false);
        if (restartPanel) restartPanel.SetActive(false);
        if (gameEndPanel) gameEndPanel.SetActive(false);
    }

    /// <summary>
    /// Рестарт (вызывается из UI, например, по клавише R)
    /// </summary>
    public void RestartLevel(){
        gameTimer.ResetTimer();
        gameTimer.StartTimer();
        StartGame();
    }

    /// <summary>
    /// Отключает спрайты, коллайдеры и управление (используется при взрыве)
    /// </summary>
    public void DisableBike() => EnableBike(false);
    public void DisableCargo() => EnableCargo(false);

    /// <summary>
    /// Останавливает мотоцикл: отключает управление, гасит скорость
    /// и замораживает Rigidbody2D, чтобы его не толкали монстры/физика
    /// </summary>
    public void StopBike()
    {
        if (bikeController) bikeController.enabled = false;

        if (bikeRb)
        {
            bikeRb.linearVelocity = Vector2.zero;
            bikeRb.angularVelocity = 0f;
            bikeRb.constraints = RigidbodyConstraints2D.FreezeAll;
        }
    }

    /// <summary>
    /// Показывает панель конца игры и останавливает мотоцикл
    /// </summary>
public void ShowGameEndMenu(float finalTime)   // ← добавили параметр
{
    StopBike();

    if (restartPanel) restartPanel.SetActive(false);
    if (gameEndPanel) gameEndPanel.SetActive(true);

    // Передаём время скрипту, который висит на gameEndPanel
    GameEndPanel panel = gameEndPanel.GetComponent<GameEndPanel>();
    if (panel != null)
        panel.DisplayTime(finalTime);

    if (AudioManager.Instance != null)
        AudioManager.Instance.PlayEndPanel();
}

    /// <summary>
    /// Включает/отключает визуал, коллизии и контроллер мотоцикла
    /// </summary>
    private void EnableBike(bool enable)
    {
        // Снимаем заморозку, установленную StopBike()
        if (enable && bikeRb)
            bikeRb.constraints = RigidbodyConstraints2D.None;

        if (bikeSprite) bikeSprite.enabled = enable;
        if (bikeCollider) bikeCollider.enabled = enable;
        if (bikeController) bikeController.enabled = enable;

        // Сбрасываем внутреннее состояние контроллера (forward/lateral/lean dynamics),
        // чтобы скорость не переносилась с предыдущего заезда.
        if (enable && bikeController) bikeController.ResetState();

        ResetRigidbody(bike);
    }

    /// <summary>
    /// Включает/отключает визуал, коллизии и контроллер коробки
    /// </summary>
    private void EnableCargo(bool enable)
    {
        ResetRigidbody(cargo);

        if (cargoSprite) cargoSprite.enabled = enable;
        if (cargoCollider) cargoCollider.enabled = enable;
        if (cargoController) cargoController.enabled = enable;
    }

    // --- Вспомогательные методы ---

    private void SetTransform(GameObject obj, Vector3 pos, Quaternion rot)
    {
        if (obj == null) return;
        obj.transform.SetPositionAndRotation(pos, rot);
    }

    private void ResetRigidbody(GameObject obj)
    {
        if (obj == null) return;
        // GetComponentInChildren: Rigidbody2D может быть на дочернем объекте
        Rigidbody2D rb = obj.GetComponentInChildren<Rigidbody2D>();
        if (rb)
        {
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
        }
    }
    
    // Добавьте в конец класса GameManager (после всех методов)

    /// <summary>
    /// Включает/отключает управление мотоциклом (используется панелькой)
    /// </summary>
    public void SetBikeControlEnabled(bool enabled)
    {
        if (bikeController != null)
            bikeController.enabled = enabled;
    
        // Сбрасываем физику при отключении
        if (!enabled && bikeRb != null)
        {
            bikeRb.linearVelocity = Vector2.zero;
            bikeRb.angularVelocity = 0f;
        }
    }

    /// <summary>
    /// Включает/отключает управление коробкой (используется панелькой)
    /// </summary>
    public void SetCargoControlEnabled(bool enabled)
    {
        if (cargoController != null)
            cargoController.enabled = enabled;
    }

    /// <summary>
    /// Проверяет, включено ли управление мотоциклом
    /// </summary>
    public bool IsBikeControlEnabled()
    {
        return bikeController != null && bikeController.enabled;
    }
}
