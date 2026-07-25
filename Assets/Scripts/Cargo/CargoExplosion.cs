using UnityEngine;
using System.Collections;

public class CargoExplosion : MonoBehaviour
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

    private void OnTriggerEnter2D(Collider2D other)
    {
        // Если это спецблок – ничего не делаем, всё обработает его скрипт
        if (other.GetComponent<GameEndBlock>() != null)
            return;

        TriggerExplosions();
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision.gameObject.GetComponent<GameEndBlock>() != null)
            return;

        TriggerExplosions();
    }

    /// <summary>
    /// Обычный взрыв с показом панели рестарта
    /// </summary>
    private void TriggerExplosions()
    {
        Explode(true);
    }

    /// <summary>
    /// Взрыв без панели рестарта (для вызова из спецблока)
    /// </summary>
    public void ExplodeWithoutRestart()
    {
        Explode(false);
    }

    /// <summary>
    /// Общая логика взрыва: останавливаем мотоцикл, прячем коробку, спавним взрывы
    /// </summary>
    private void Explode(bool showRestart)
    {
        if (exploded) return;
        exploded = true;

        if (GameManager.Instance != null)
        {
            GameManager.Instance.StopBike();
            GameManager.Instance.DisableCargo();
        }

        // Панель рестарта показываем сразу при контакте
        if (showRestart) ShowRestartPanel();

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
        if (GameManager.Instance != null && GameManager.Instance.restartPanel != null)
            GameManager.Instance.restartPanel.SetActive(true);
    }

    public void ResetExploded()
    {
        exploded = false;
    }
}
