using UnityEngine;

public class BikeShadow : MonoBehaviour
{
    [Header("Shadow Settings")]
    public Vector2 shadowDirection = new Vector2(-1f, -0.5f);
    public float shadowDistance = 0.5f;
    public float minHeightScale = 0.3f;
    public float maxHeightScale = 1.2f;
    public float minAlpha = 0.3f;
    public float maxAlpha = 0.7f;

    private SpriteRenderer spriteRenderer;
    private Transform bikeTransform;
    private Rigidbody2D bikeRigidbody;
    
    // Для плавности
    private float targetAngle;
    private float currentAngle;

    void Start()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        bikeTransform = transform.parent;
        bikeRigidbody = bikeTransform.GetComponent<Rigidbody2D>();
        
        if (bikeTransform == null)
        {
            Debug.LogError("Shadow must be a child of the bike!");
        }
        
        currentAngle = bikeTransform.eulerAngles.z;
        targetAngle = currentAngle;
    }

    void FixedUpdate()
    {
        if (bikeTransform == null) return;

        // 1. Получаем угол ПРЯМО из физики (самый точный)
        float physicsAngle = bikeRigidbody.rotation;
        
        // 2. Поворачиваем тень СИНХРОННО с физикой
        transform.rotation = Quaternion.Euler(0, 0, physicsAngle);

        // 3. Смещение в локальных координатах
        Vector2 normalizedDir = shadowDirection.normalized;
        transform.localPosition = (Vector3)normalizedDir * shadowDistance;

        // 4. Масштаб по Y (используем актуальный угол из физики)
        float angle = Vector2.Angle(bikeTransform.right, normalizedDir);
        float t = Mathf.Clamp01(angle / 90f);
        float scaleY = Mathf.Lerp(maxHeightScale, minHeightScale, t);
        
        Vector3 scale = transform.localScale;
        scale.y = scaleY;
        transform.localScale = scale;

        // 5. Прозрачность
        float alpha = Mathf.Lerp(maxAlpha, minAlpha, t);
        Color color = spriteRenderer.color;
        color.a = alpha;
        spriteRenderer.color = color;
    }

    void OnDrawGizmosSelected()
    {
        if (transform.parent != null)
        {
            Gizmos.color = Color.yellow;
            Vector3 dir = shadowDirection.normalized * 2f;
            Gizmos.DrawRay(transform.parent.position, dir);
        }
    }
}