using UnityEngine;
using System.Collections;

public class GameEndBlock : MonoBehaviour
{
    [Header("Префабы взрывов")]
    public GameObject[] explosionPrefabs;

    [Header("Количество взрывов")]
    public int minExplosions = 4;
    public int maxExplosions = 6;

    [Header("Размер взрывов")]
    public float minScale = 1f;
    public float maxScale = 2f;

    [Header("Разброс позиции")]
    public float maxOffsetX = 0.7f;
    public float maxOffsetY = 0.7f;

    [Header("Задержки между взрывами")]
    public float minDelay = 0f;
    public float maxDelay = 1.5f;

    [Header("Невидимая зона обнаружения")]
    public Vector2 zoneSize = new Vector2(5f, 3f);    // ширина и высота прямоугольника
    public Vector2 zoneOffset = Vector2.zero;          // смещение относительно позиции объекта

    [Header("Объект коробки (с CargoExplosion)")]
    public Transform boxTarget;                        // перетащите сюда коробку (или оставьте пустым для авто-поиска)

    public BikeExplosion bike;

    private bool triggered = false;
    private bool wasInside = false;

    private void Start()
    {
        // Если коробка не назначена вручную, пытаемся найти её автоматически
        if (boxTarget == null)
        {
            CargoExplosion box = FindObjectOfType<CargoExplosion>();
            if (box != null)
                boxTarget = box.transform;
        }
    }

    void Update()
    {
        if (triggered || boxTarget == null) return;

        // Мировая позиция центра зоны
        Vector3 zoneCenter = transform.position + (Vector3)zoneOffset;

        // Прямоугольная область
        Bounds bounds = new Bounds(zoneCenter, zoneSize);
        bool isInside = bounds.Contains(boxTarget.position);

        if (isInside && !wasInside)
        {
            TryActivate(boxTarget.gameObject);
        }

        wasInside = isInside;
    }

    private void TryActivate(GameObject other)
    {
        if (triggered) return;

        CargoExplosion cargo = other.GetComponent<CargoExplosion>();
        if (cargo == null) return;

        triggered = true;
        bike.SetExploded();

        // Отключаем визуал самого блока (если есть SpriteRenderer)
        SpriteRenderer sr = GetComponent<SpriteRenderer>();
        if (sr != null) sr.enabled = false;

        // Взрыв коробки без показа меню
        cargo.ExplodeWithoutRestart();

        // Собственная серия взрывов
        StartCoroutine(ExplodeSequence());
    }

    private IEnumerator ExplodeSequence()
    {
        if (explosionPrefabs == null || explosionPrefabs.Length == 0)
        {
            ShowGameOverAndDestroy();
            yield break;
        }

        int count = Random.Range(minExplosions, maxExplosions + 1);
        float lastDelay = 0f;

        for (int i = 0; i < count; i++)
        {
            float delay = Random.Range(minDelay, maxDelay);
            if (i == 0)
            {
                delay = 0f;
            }
            if (delay > lastDelay) lastDelay = delay;

            GameObject prefab = explosionPrefabs[Random.Range(0, explosionPrefabs.Length)];
            Vector3 offset = new Vector3(Random.Range(-maxOffsetX, maxOffsetX),
                                         Random.Range(-maxOffsetY, maxOffsetY), 0f);
            Vector3 spawnPos = transform.position + offset;
            float scale = Random.Range(minScale, maxScale);

            StartCoroutine(SpawnSingleExplosion(prefab, spawnPos, scale, delay));
        }

        yield return new WaitForSeconds(lastDelay + 0.1f);
        ShowGameOverAndDestroy();
    }

    private IEnumerator SpawnSingleExplosion(GameObject prefab, Vector3 position, float scale, float delay)
    {
        yield return new WaitForSeconds(delay);
        GameObject explosion = Instantiate(prefab, position, Quaternion.identity);
        explosion.transform.localScale = new Vector3(scale, scale, 1f);
        if (AudioManager.Instance != null)
            AudioManager.Instance.PlayExplosion();
    }

    private void ShowGameOverAndDestroy()
    {
        if (GameManager.Instance != null)
            GameManager.Instance.ShowGameEndMenu();

        Destroy(gameObject);
    }

    // Визуализация зоны в редакторе (только для разработчика)
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1, 1, 0, 0.3f);
        Gizmos.DrawCube(transform.position + (Vector3)zoneOffset, zoneSize);
    }
}