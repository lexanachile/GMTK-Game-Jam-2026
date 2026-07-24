using UnityEngine;
using System.Collections;

public class CargoExplosion : MonoBehaviour
{
    [Header("Взрывы")]
    public GameObject[] explosionPrefabs;
    public int minExplosions = 3;
    public int maxExplosions = 5;
    public float minScale = 1f;
    public float maxScale = 2f;
    public float maxOffsetX = 0.5f;
    public float maxOffsetY = 0.5f;
    public float minDelay = 0f;
    public float maxDelay = 1f;

    private bool exploded = false;

    private void OnTriggerEnter2D(Collider2D other) => TriggerExplosions();
    private void OnCollisionEnter2D(Collision2D collision) => TriggerExplosions();

    void TriggerExplosions()
    {
        if (exploded) return;
        exploded = true;

        // Отключаем визуал, коллизии и управление (мотоцикл + коробка)
        if (GameManager.Instance != null)
        {
            GameManager.Instance.DisableCargo();  
            GameManager.Instance.StopBike();
        }


        StartCoroutine(SpawnExplosionsSequence());
    }

    IEnumerator SpawnExplosionsSequence()
    {
        int count = Random.Range(minExplosions, maxExplosions + 1);
        if (explosionPrefabs == null || explosionPrefabs.Length == 0)
        {
            ShowRestartPanel();
            yield break;
        }

        float maxDelay = 0f;
        for (int i = 0; i < count; i++)
        {
            float delay = Random.Range(minDelay, maxDelay);
            if (delay > maxDelay) maxDelay = delay;

            GameObject prefab = explosionPrefabs[Random.Range(0, explosionPrefabs.Length)];
            Vector3 offset = new Vector3(Random.Range(-maxOffsetX, maxOffsetX),
                                         Random.Range(-maxOffsetY, maxOffsetY), 0f);
            Vector3 spawnPos = transform.position + offset;
            float scale = Random.Range(minScale, maxScale);

            StartCoroutine(SpawnSingleExplosion(prefab, spawnPos, scale, delay));
        }

        yield return new WaitForSeconds(maxDelay + 0.1f);
        ShowRestartPanel();
    }

    IEnumerator SpawnSingleExplosion(GameObject prefab, Vector3 position, float scale, float delay)
    {
        yield return new WaitForSeconds(delay);
        GameObject explosion = Instantiate(prefab, position, Quaternion.identity);
        explosion.transform.localScale = new Vector3(scale, scale, 1f);
    }

    void ShowRestartPanel()
    {
        if (GameManager.Instance != null && GameManager.Instance.restartPanel != null)
            GameManager.Instance.restartPanel.SetActive(true);
    }

    public void ResetExploded()
    {
        exploded = false;
    }
}