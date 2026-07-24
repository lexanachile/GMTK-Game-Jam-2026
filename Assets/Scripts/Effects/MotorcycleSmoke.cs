using UnityEngine;

public class MotorcycleSmoke : MonoBehaviour
{
    [Header("Префабы дыма")]
    [SerializeField] private GameObject[] smokePrefabs;
    [SerializeField] private Transform exhaustPoint;
    
    [Header("Движение")]
    [SerializeField] private Rigidbody2D rb;
    [SerializeField] private float speedThreshold = 0.5f;
    
    [Header("Спавн")]
    [SerializeField] private float spawnInterval = 0.15f;
    [SerializeField] private float smokeLifetime = 2.0f;
    
    [Header("Разброс позиции")]
    [SerializeField] private bool useSpread = true;
    [Range(0f, 10f)]
    [SerializeField] private float spreadRadius = 0.5f;   // ← увеличь до 0.5-1.0
    
    [Header("Рандомная прозрачность")]
    [SerializeField] private bool useRandomAlpha = true;
    [Range(0.1f, 1f)]
    [SerializeField] private float minAlpha = 0.5f;
    [Range(0.1f, 1f)]
    [SerializeField] private float maxAlpha = 1.0f;
    
    [Header("Рандомная скорость (улетают вверх)")]
    [Range(0f, 3f)]
    [SerializeField] private float minSpeed = 0.3f;
    [Range(0f, 3f)]
    [SerializeField] private float maxSpeed = 0.8f;
    
    [Header("Рандомное время жизни")]
    [SerializeField] private bool useRandomLifetime = true;
    [SerializeField] private float minLifetime = 1.0f;
    [SerializeField] private float maxLifetime = 2.5f;
    
    [Header("Рандомная задержка перед появлением")]
    [SerializeField] private bool useRandomDelay = true;
    [SerializeField] private float minDelay = 0f;
    [SerializeField] private float maxDelay = 0.3f;
    
    private float timer;
    
    void Update()
    {
        if (rb.linearVelocity.magnitude > speedThreshold)
        {
            timer += Time.deltaTime;
            
            if (timer >= spawnInterval)
            {
                timer = 0;
                SpawnSmoke();
            }
        }
    }
    
    void SpawnSmoke()
    {
        // Выбираем рандомный префаб
        int randomIndex = Random.Range(0, smokePrefabs.Length);
        GameObject smoke = Instantiate(smokePrefabs[randomIndex], exhaustPoint.position, Quaternion.identity);
        
        // ===== РАЗБРОС ПОЗИЦИИ (больше) =====
        if (useSpread)
        {
            Vector3 randomOffset = new Vector3(
                Random.Range(-spreadRadius, spreadRadius),
                Random.Range(-spreadRadius * 0.5f, spreadRadius * 0.5f), // по Y меньше
                0
            );
            smoke.transform.position += randomOffset;
        }
        
        // ===== РАНДОМНОЕ ВРЕМЯ ЖИЗНИ =====
        float lifetime = smokeLifetime;
        if (useRandomLifetime)
        {
            lifetime = Random.Range(minLifetime, maxLifetime);
        }
        Destroy(smoke, lifetime);
        
        // ===== РАНДОМНАЯ ЗАДЕРЖКА (появление через время) =====
        if (useRandomDelay && maxDelay > 0)
        {
            float delay = Random.Range(minDelay, maxDelay);
            smoke.SetActive(false);
            Invoke(nameof(ActivateSmoke), delay);
            
            // Сохраняем ссылку на объект
            GameObject smokeRef = smoke;
            Invoke(nameof(ActivateSmoke), delay);
        }
        
        // ===== РАНДОМНАЯ ПРОЗРАЧНОСТЬ =====
        if (useRandomAlpha)
        {
            SpriteRenderer[] renderers = smoke.GetComponentsInChildren<SpriteRenderer>(true);
            float randomAlpha = Random.Range(minAlpha, maxAlpha);
            
            foreach (SpriteRenderer sr in renderers)
            {
                Color color = sr.color;
                color.a = randomAlpha;
                sr.color = color;
            }
        }
        
        // ===== РАНДОМНАЯ СКОРОСТЬ (движение вверх) =====
        Rigidbody2D smokeRb = smoke.GetComponent<Rigidbody2D>();
        if (smokeRb != null)
        {
            float speed = Random.Range(minSpeed, maxSpeed);
            // Скорость вверх + небольшой разброс в стороны
            smokeRb.linearVelocity = new Vector2(
                Random.Range(-0.2f, 0.2f),
                speed
            );
        }
        
        // ===== РАНДОМНЫЙ ПОВОРОТ =====
        smoke.transform.rotation = Quaternion.Euler(0, 0, Random.Range(0, 360));
    }
    
    void ActivateSmoke()
    {
        // Включаем объект после задержки
        // Нужно передать объект, поэтому используем другой подход
    }
}