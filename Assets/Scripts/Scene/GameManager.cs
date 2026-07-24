using UnityEngine;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;

    [Header("Объекты")]
    public GameObject bike;
    public GameObject cargo;

    [Header("Компоненты (можно с дочерних объектов)")]
    public SpriteRenderer bikeSprite;
    public Collider2D bikeCollider;
    public SpriteRenderer cargoSprite;
    public Collider2D cargoCollider;
    public CargoExplosion cargoExplosion;
    public  StandartExplosion bikeExplosion;

    [Header("Стартовые позиции")]
    public Vector3 bikeStartPos;
    public Vector3 cargoStartPos;

    [Header("Стартовые повороты")]
    public Quaternion bikeStartRot = Quaternion.identity;
    public Quaternion cargoStartRot = Quaternion.identity;

    [Header("UI")]
    public GameObject playButton;
    public GameObject restartPanel;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    /// Запуск игры (по кнопке "Играть")
    public void StartGame()
    {
        // Возвращаем на стартовые позиции
        SetTransform(bike, bikeStartPos, bikeStartRot);
        SetTransform(cargo, cargoStartPos, cargoStartRot);
        
        // Сбрасываем физику
        ResetRigidbody(bike);
        ResetRigidbody(cargo);
        
        cargoExplosion.ResetExploded();
        bikeExplosion.ResetExploded();
        // Включаем спрайты и коллайдеры
        EnableVisuals(true);

        // Включаем сами объекты (если были выключены)
        if (bike) bike.SetActive(true);
        if (cargo) cargo.SetActive(true);

        // Управление UI
        if (playButton) playButton.SetActive(false);
        if (restartPanel) restartPanel.SetActive(false);
    }

    /// Рестарт (вызывается по нажатию R из UI)
    public void RestartLevel()
    {
        StartGame();
    }

    /// Отключение визуала и коллизий при взрыве
    public void DisableVisuals()
    {
        EnableVisuals(false);
    }

    private void EnableVisuals(bool state)
    {
        if (bikeSprite) bikeSprite.enabled = state;
        if (bikeCollider) bikeCollider.enabled = state;
        if (cargoSprite) cargoSprite.enabled = state;
        if (cargoCollider) cargoCollider.enabled = state;
    }

    private void SetTransform(GameObject obj, Vector3 pos, Quaternion rot)
    {
        if (obj == null) return;
        obj.transform.position = pos;
        obj.transform.rotation = rot;
    }

    private void ResetRigidbody(GameObject obj)
    {
        if (obj == null) return;
        Rigidbody2D rb = obj.GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
        }
    }
}