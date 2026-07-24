using UnityEngine;
using System.Collections;

public class StandartExplosion : MonoBehaviour
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

    [Header("Объект, который будет скрыт (спрайт и коллайдер)")]
    public GameObject targetObject;   // если не указан, используется gameObject

    [Header("UI")]
    public GameObject restartPanel;

    private bool exploded = false;

    private void OnTriggerEnter2D(Collider2D other) => TriggerExplosions();
    private void OnCollisionEnter2D(Collision2D collision) => TriggerExplosions();

    void TriggerExplosions()
    {
        if (exploded) return;
        exploded = true;

        // Определяем, что скрывать
        GameObject objToHide = targetObject != null ? targetObject : gameObject;

        // Скрываем спрайт и коллайдер сразу, чтобы объект "исчез"
        ChangeStateSpriteAndCollider(objToHide, false);

        StartCoroutine(SpawnExplosionsParallel(objToHide));
    }

    IEnumerator SpawnExplosionsParallel(GameObject objToHide)
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

    void ChangeStateSpriteAndCollider(GameObject obj, bool state)
    {
        if (obj == null) return;
        SpriteRenderer sr = obj.GetComponent<SpriteRenderer>();
        if (sr != null) sr.enabled = state;

        Collider2D col = obj.GetComponent<Collider2D>();
        if (col != null) col.enabled = state;
    }

    void ShowRestartPanel()
    {
        if (restartPanel != null)
            restartPanel.SetActive(true);
    }

    public void ResetExploded()
    {
        exploded = false;
    }
}