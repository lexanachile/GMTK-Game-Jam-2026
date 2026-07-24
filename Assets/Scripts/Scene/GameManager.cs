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

    // Автоматически найденные компоненты
    private SpriteRenderer bikeSprite;
    private Collider2D bikeCollider;
    private BikeExplosion bikeExplosion;
    private MotorcycleController bikeController;
    private SpriteRenderer cargoSprite;
    private Collider2D cargoCollider;
    private CargoExplosion cargoExplosion;
    private CargoController cargoController;

    void Awake()
    {
        Instance = this;
        CacheComponents();
    }

    /// <summary>
    /// Ищет все нужные компоненты на корневых объектах и их детях
    /// </summary>
    void CacheComponents()
    {
        if (bike != null)
        {
            bikeSprite = bike.GetComponentInChildren<SpriteRenderer>();
            bikeCollider = bike.GetComponentInChildren<Collider2D>();
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
        //ResetRigidbody(bike);
        //ResetRigidbody(cargo);

        // Сброс флагов взрывов
        if (bikeExplosion) bikeExplosion.ResetExploded();
        if (cargoExplosion) cargoExplosion.ResetExploded();

        // Включаем визуал, коллизии и управление
        EnableBike(true);
        EnableCargo(true);

        // Объекты активны
        if (bike) bike.SetActive(true);
        if (cargo) cargo.SetActive(true);

        // UI
        if (playButton) playButton.SetActive(false);
        if (restartPanel) restartPanel.SetActive(false);
    }

    /// <summary>
    /// Рестарт (вызывается из UI, например, по клавише R)
    /// </summary>
    public void RestartLevel() => StartGame();

    /// <summary>
    /// Отключает спрайты, коллайдеры и управление (используется при взрыве)
    /// </summary>
    public void DisableBike() => EnableBike(false);
    public void DisableCargo() => EnableCargo(false);

    /// <summary>
    /// Включает/отключает визуал, коллизии и контроллер мотоцикла
    /// </summary>
    private void EnableBike(bool enable)
    {
        if (bikeSprite) bikeSprite.enabled = enable;
        if (bikeCollider) bikeCollider.enabled = enable;
        if (bikeController) bikeController.enabled = enable;

        // Физика
        ResetRigidbody(bike);
    }
    private void EnableCargo(bool enable)
    {
        ResetRigidbody(cargo);

        if (cargoSprite) cargoSprite.enabled = enable;
        if (cargoCollider) cargoCollider.enabled = enable;
        if (cargoController) cargoController.enabled = enable;
    }
    public void StopBike()
    {
        if (bikeController) bikeController.enabled = false;
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
        Rigidbody2D rb = obj.GetComponent<Rigidbody2D>();
        if (rb)
        {
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
        }
    }
}