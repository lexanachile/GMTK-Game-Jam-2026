using UnityEngine;
using System.Collections;

public class BikeExplosion : MonoBehaviour
{
    [Header("Префабы взрывов")]
    public GameObject[] explosionPrefabs;

    [Header("Количество взрывов")]
    public int minExplosions = 3;
    public int maxExplosions = 5;

    [Header("Размер взрывов")]
    public float minScale = 1f;
    public float maxScale = 2f;

    [Header("Разброс позиции")]
    public float maxOffsetX = 0.5f;
    public float maxOffsetY = 0.5f;

    [Header("Задержки между взрывами")]
    public float minDelay = 0f;
    public float maxDelay = 1f;

    private bool exploded = false;

    private void OnTriggerEnter2D(Collider2D other) => TriggerExplosions();
    private void OnCollisionEnter2D(Collision2D collision) => TriggerExplosions();

    private void TriggerExplosions()
    {
        if (exploded) return;
        exploded = true;

        // Отключаем визуал, коллизию и управление мотоцикла (и коробки заодно)
        if (GameManager.Instance != null)
            GameManager.Instance.DisableBike();

        // Панель рестарта показываем сразу при контакте
        ShowRestartPanel();

        // Запускаем взрывы
        StartCoroutine(SpawnExplosionsSequence());
    }

    private IEnumerator SpawnExplosionsSequence()
    {
        if (explosionPrefabs == null || explosionPrefabs.Length == 0)
            yield break;

        int count = Random.Range(minExplosions, maxExplosions + 1);

        for (int i = 0; i < count; i++)
        {
            float delay = Random.Range(minDelay, maxDelay);

            GameObject prefab = explosionPrefabs[Random.Range(0, explosionPrefabs.Length)];
            Vector3 offset = new Vector3(Random.Range(-maxOffsetX, maxOffsetX),
                                         Random.Range(-maxOffsetY, maxOffsetY), 0f);
            Vector3 spawnPos = transform.position + offset;
            float scale = Random.Range(minScale, maxScale);

            StartCoroutine(SpawnSingleExplosion(prefab, spawnPos, scale, delay));
        }
    }

    private IEnumerator SpawnSingleExplosion(GameObject prefab, Vector3 position, float scale, float delay)
    {
        yield return new WaitForSeconds(delay);
        GameObject explosion = Instantiate(prefab, position, Quaternion.identity);
        explosion.transform.localScale = new Vector3(scale, scale, 1f);
    }

    private void ShowRestartPanel()
    {
        // Используем панель из GameManager, если она есть
        if (GameManager.Instance != null && GameManager.Instance.restartPanel != null)
            GameManager.Instance.restartPanel.SetActive(true);
    }

    public void ResetExploded()
    {
        exploded = false;
    }
}
