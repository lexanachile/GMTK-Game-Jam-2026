using UnityEngine;
using System.Collections;

public class CargoExplosion : MonoBehaviour
{
    [Header("Список префабов взрывов")]
    public GameObject[] explosionPrefabs;

    [Header("Количество взрывов")]
    public int minExplosions = 3;
    public int maxExplosions = 5;

    [Header("Размер")]
    public float minScale = 1f;
    public float maxScale = 2f;

    [Header("Отклонение по позиции")]
    public float maxOffsetX = 0.5f;
    public float maxOffsetY = 0.5f;

    [Header("Задержка от начала (сек)")]
    public float minDelay = 0f;
    public float maxDelay = 1f;

    private bool exploded = false;

    // Если нужен и триггер – добавьте:
    // private void OnTriggerEnter2D(Collider2D other) => TriggerExplosions();

    private void OnCollisionEnter2D(Collision2D collision) => TriggerExplosions();

    void TriggerExplosions()
    {
        if (exploded) return;
        exploded = true;

        // Прячем бочку (отключаем коллайдер и спрайт)
        Collider2D col = GetComponent<Collider2D>();
        if (col != null) col.enabled = false;
        SpriteRenderer sr = GetComponent<SpriteRenderer>();
        if (sr != null) sr.enabled = false;

        // Запускаем параллельные взрывы
        StartCoroutine(SpawnExplosionsParallel());
    }

    IEnumerator SpawnExplosionsParallel()
    {
        int count = Random.Range(minExplosions, maxExplosions + 1);
        if (explosionPrefabs == null || explosionPrefabs.Length == 0)
        {
            Debug.LogWarning("Нет префабов взрывов в списке!");
            Destroy(gameObject);
            yield break;
        }

        float maxDelay = 0f;

        for (int i = 0; i < count; i++)
        {
            float delay = Random.Range(minDelay, maxDelay);
            if (delay > maxDelay) maxDelay = delay;

            // Выбираем случайный префаб
            GameObject prefab = explosionPrefabs[Random.Range(0, explosionPrefabs.Length)];

            // Случайное смещение
            Vector3 offset = new Vector3(Random.Range(-maxOffsetX, maxOffsetX),
                                         Random.Range(-maxOffsetY, maxOffsetY), 0f);
            Vector3 spawnPos = transform.position + offset;

            // Случайный размер
            float scale = Random.Range(minScale, maxScale);

            // Запускаем корутину, которая создаст взрыв через delay
            StartCoroutine(SpawnSingleExplosion(prefab, spawnPos, scale, delay));
        }

        // Ждём самую долгую задержку и удаляем бочку
        yield return new WaitForSeconds(maxDelay + 0.1f);
        Destroy(gameObject);
    }

    IEnumerator SpawnSingleExplosion(GameObject prefab, Vector3 position, float scale, float delay)
    {
        Debug.Log($"Создаю взрыв {prefab.name} в {position} через {delay} сек.");
        yield return new WaitForSeconds(delay);
        GameObject explosion = Instantiate(prefab, position, Quaternion.identity);
        explosion.transform.localScale = new Vector3(scale, scale, 1f);
         Debug.Log($"Взрыв создан: {explosion.name}, активен: {explosion.activeSelf}, scale: {explosion.transform.localScale}");
    }
}