using UnityEngine;
using System.Collections;
using System.Collections.Generic;

[RequireComponent(typeof(LineRenderer))]
public class LassoRope : MonoBehaviour
{
    [Header("Connection Points")]
    [SerializeField] private Transform startPoint;
    [SerializeField] private Transform endPoint;

    [Header("Physics Chain")]
    [SerializeField] private int segmentCount = 12;
    [Tooltip(
        "Масса каждого сегмента верёвки.\n\n" +
        "Влияет на то, насколько сильно верёвка тянет мотоцикл назад.\n" +
        "  0.01–0.03 = лёгкая верёвка (почти не замедляет мотоцикл)\n" +
        "  0.05–0.1  = средняя верёвка (ощутимый вес, но не критичный)\n" +
        "  0.15+     = тяжёлая верёвка (сильно тянет назад)\n\n" +
        "Рекомендуемый диапазон: 0.02–0.05 для комфортной игры.")]
    [SerializeField] private float segmentMass = 0.02f;
    [Tooltip("Gravity scale for rope segments")]
    [SerializeField] private float segmentGravityScale = 1f;
    [SerializeField] private float segmentLinearDrag = 4f;
    [SerializeField] private float segmentAngularDrag = 2f;
    [Tooltip("Max distance between adjacent links (0 = auto from spacing)")]
    [SerializeField] private float linkMaxDistance = 0f;

    [Header("Rope Length")]
    [Tooltip("Multiplier for rope length (>1 = longer rope that sags on ground)")]
    [SerializeField] private float ropeLengthMultiplier = 1.5f;
    [Tooltip("Radius of rope segment colliders")]
    [SerializeField] private float ropeColliderRadius = 0.15f;

    [Header("Collision Layers")]
    [Tooltip("Layers that rope should collide with (buildings, obstacles)")]
    [SerializeField] private LayerMask ropeCollisionMask = ~0;
    [Tooltip("Layers that rope should IGNORE (bike, cargo)")]
    [SerializeField] private LayerMask ropeIgnoreMask = 0;

    [Header("Heaviness Feel")]
    [Tooltip("Extra downward force multiplier on middle segments for sag")]
    [SerializeField] private float sagForce = 2f;
    [Tooltip("Velocity damping applied each FixedUpdate to slow rope swing")]
    [SerializeField] private float swingDamping = 0.95f;

    [Header("Cargo Pull")]
    [Tooltip("Maximum distance between bike and cargo before cargoJoint activates (0 = auto from rope length)")]
    [SerializeField] private float cargoMaxDistance = 0f;
    [Tooltip("Delay in seconds before cargoJoint activates (lets physics settle)")]
    [SerializeField] private float cargoJointDelay = 0.5f;

    [Header("Stretch Limit")]
    [Tooltip(
        "Максимальное растяжение лассо (множитель от базовой длины).\n\n" +
        "Когда расстояние между мотоциклом и грузом превышает это значение,\n" +
        "лассо начинает жёстко подтягивать груз к мотоциклу.\n\n" +
        "  1.0 = лассо не растягивается (жёсткая связь)\n" +
        "  1.2 = лассо растягивается на 20% перед подтягиванием\n" +
        "  1.5 = лассо растягивается на 50% (умеренная эластичность)\n" +
        "  2.0 = лассо растягивается на 100% (очень эластичное)\n\n" +
        "Рекомендуемый диапазон: 1.3–1.8.\n" +
        "Если груз улетает далеко — уменьшите это значение.\n" +
        "Если подтягивание слишком резкое — увеличьте.")]
    [SerializeField, Range(1f, 3f)] private float maxStretchMultiplier = 1.5f;
    [Tooltip(
        "Базовая сила подтягивания груза при максимальном растяжении.\n\n" +
        "Определяет насколько сильно лассо тянет груз к мотоциклу.\n" +
        "Сила растёт экспоненциально с увеличением растяжения.\n\n" +
        "  50–100  = слабое подтягивание (груз медленно возвращается)\n" +
        "  100–200 = среднее подтягивание (комфортное)\n" +
        "  200–400 = сильное подтягивание (груз быстро возвращается)\n\n" +
        "Рекомендуемый диапазон: 100–200.\n" +
        "Если груз всё ещё улетает — увеличьте это значение.")]
    [SerializeField] private float stretchForceBase = 150f;
    [Tooltip(
        "Экспонента роста силы подтягивания.\n\n" +
        "Определяет как быстро сила растёт с увеличением растяжения.\n" +
        "  1.0 = линейный рост (сила пропорциональна растяжению)\n" +
        "  2.0 = квадратичный рост (сила растёт быстрее)\n" +
        "  3.0 = кубический рост (очень резкий рост силы)\n\n" +
        "Рекомендуемый диапазон: 2.0–3.5.\n" +
        "Больше = более резкое подтягивание при сильном растяжении.\n" +
        "Меньше = более плавное подтягивание.")]
    [SerializeField, Range(1f, 4f)] private float stretchExponent = 2.5f;

    [Header("Visual")]
    [SerializeField] private float ropeWidth = 0.3f;
    [SerializeField] private float textureTiling = 4f;
    [SerializeField] private float wobbleAmount = 0.005f;
    [SerializeField] private float wobbleSpeed = 2f;
    [SerializeField] private int smoothSubdivisions = 3;
    [SerializeField] private Color ropeColor = new Color(0.45f, 0.25f, 0.08f, 1f);

    [Header("Cargo Constraint (optional)")]
    [Tooltip("DistanceJoint2D on the Cargo GameObject (connected to Bike Rigidbody2D). Leave null to disable.")]
    [SerializeField] private DistanceJoint2D cargoJoint;

    private LineRenderer lineRenderer;
    private Material ropeMaterial;
    private GameObject chainContainer;
    private readonly List<Link> links = new List<Link>();
    private float time;

    private Rigidbody2D bikeRb;
    private Rigidbody2D cargoRb;
    private float baseRopeLength;

    private struct Link
    {
        public GameObject go;
        public Transform tr;
        public Rigidbody2D rb;
        public CircleCollider2D col;
    }

    private void Awake()
    {
        lineRenderer = GetComponent<LineRenderer>();
        lineRenderer.textureMode = LineTextureMode.Tile;
        lineRenderer.alignment = LineAlignment.TransformZ;
        lineRenderer.useWorldSpace = true;
        lineRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lineRenderer.sortingOrder = 10;
        lineRenderer.startColor = ropeColor;
        lineRenderer.endColor = ropeColor;
        lineRenderer.startWidth = ropeWidth;
        lineRenderer.endWidth = ropeWidth;

        ropeMaterial = lineRenderer.material;
        if (ropeMaterial != null)
        {
            ropeMaterial.color = ropeColor;
            ropeMaterial.SetColor("_Color", ropeColor);
        }
    }

    private void Start()
    {
        BuildChain();
        SetupCollisionLayers();
        InitializeStretchLimit();
        StartCoroutine(ActivateCargoJointDelayed());
    }

    private void InitializeStretchLimit()
    {
        if (startPoint != null && endPoint != null)
        {
            bikeRb = startPoint.GetComponentInParent<Rigidbody2D>();
            cargoRb = endPoint.GetComponentInParent<Rigidbody2D>();
            baseRopeLength = Vector2.Distance(bikeRb.position, cargoRb.position);
        }
    }

    private IEnumerator ActivateCargoJointDelayed()
    {
        if (cargoJoint != null)
        {
            cargoJoint.enabled = false;
            yield return new WaitForSeconds(cargoJointDelay);

            if (cargoJoint != null && startPoint != null && endPoint != null)
            {
                Rigidbody2D bikeRb = startPoint.GetComponentInParent<Rigidbody2D>();
                Rigidbody2D cargoRb = endPoint.GetComponentInParent<Rigidbody2D>();
                if (bikeRb != null && cargoRb != null)
                {
                    float dist = cargoMaxDistance > 0f
                        ? cargoMaxDistance
                        : Vector2.Distance(bikeRb.position, cargoRb.position) * ropeLengthMultiplier;

                    cargoJoint.autoConfigureDistance = false;
                    cargoJoint.distance = dist;
                    cargoJoint.maxDistanceOnly = true;
                    cargoJoint.enabled = true;
                }
            }
        }
    }

    private void SetupCollisionLayers()
    {
        int ropeLayer = gameObject.layer;

        for (int i = 0; i < 32; i++)
        {
            bool shouldCollide = (ropeCollisionMask.value & (1 << i)) != 0;
            bool shouldIgnore = (ropeIgnoreMask.value & (1 << i)) != 0;

            if (shouldIgnore || !shouldCollide)
            {
                Physics2D.IgnoreLayerCollision(ropeLayer, i, true);
            }
        }
    }

    private void BuildChain()
    {
        if (startPoint == null || endPoint == null)
        {
            Debug.LogError("LassoRope: startPoint or endPoint not assigned!", this);
            return;
        }

        Rigidbody2D bikeRb = startPoint.GetComponentInParent<Rigidbody2D>();
        Rigidbody2D cargoRb = endPoint.GetComponentInParent<Rigidbody2D>();
        if (bikeRb == null || cargoRb == null)
        {
            Debug.LogError("LassoRope: Rigidbody2D missing on bike or cargo parent!", this);
            return;
        }

        chainContainer = new GameObject("LassoChain");
        chainContainer.transform.SetParent(transform, false);

        Vector2 start = startPoint.position;
        Vector2 end = endPoint.position;
        float directDist = Vector2.Distance(start, end);
        float totalDist = directDist * ropeLengthMultiplier;

        float segSpacing = totalDist / (segmentCount + 1);
        float maxDist = linkMaxDistance > 0f ? linkMaxDistance : segSpacing * 1.1f;

        Rigidbody2D prevRb = bikeRb;
        Vector2 prevAnchorWorld = start;

        for (int i = 0; i < segmentCount; i++)
        {
            float t = (float)(i + 1) / (segmentCount + 1);
            Vector2 basePos = Vector2.Lerp(start, end, t);

            float extraLength = totalDist - directDist;
            float sagAmount = Mathf.Sin(t * Mathf.PI) * extraLength * 0.5f;
            Vector2 pos = basePos - Vector2.up * sagAmount;

            GameObject go = new GameObject($"Link_{i:D2}");
            go.transform.SetParent(chainContainer.transform, false);
            go.transform.position = pos;
            go.layer = gameObject.layer;

            Rigidbody2D rb = go.AddComponent<Rigidbody2D>();
            rb.mass = segmentMass;
            rb.linearDamping = segmentLinearDrag;
            rb.angularDamping = segmentAngularDrag;
            rb.gravityScale = segmentGravityScale;
            rb.interpolation = RigidbodyInterpolation2D.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

            CircleCollider2D col = go.AddComponent<CircleCollider2D>();
            col.radius = ropeColliderRadius;

            Vector2 connectedAnchorLocal;
            if (i == 0)
            {
                connectedAnchorLocal = bikeRb.transform.InverseTransformPoint(startPoint.position);
            }
            else
            {
                connectedAnchorLocal = prevRb.transform.InverseTransformPoint(prevAnchorWorld);
            }

            DistanceJoint2D dj = go.AddComponent<DistanceJoint2D>();
            dj.connectedBody = prevRb;
            dj.autoConfigureDistance = false;
            dj.distance = maxDist;
            dj.maxDistanceOnly = true;
            dj.anchor = Vector2.zero;
            dj.connectedAnchor = connectedAnchorLocal;

            links.Add(new Link { go = go, tr = go.transform, rb = rb, col = col });
            prevRb = rb;
            prevAnchorWorld = pos;
        }

        if (links.Count > 0)
        {
            Link last = links[links.Count - 1];

            DistanceJoint2D endDj = last.go.AddComponent<DistanceJoint2D>();
            endDj.connectedBody = cargoRb;
            endDj.autoConfigureDistance = false;
            endDj.distance = maxDist;
            endDj.maxDistanceOnly = true;
            endDj.anchor = Vector2.zero;
            endDj.connectedAnchor = cargoRb.transform.InverseTransformPoint(endPoint.position);
        }
    }

    private void FixedUpdate()
    {
        if (links.Count == 0) return;

        for (int i = 0; i < links.Count; i++)
        {
            if (links[i].rb == null) continue;

            links[i].rb.linearVelocity *= swingDamping;

            if (sagForce > 0f)
            {
                float t = (float)(i + 1) / (links.Count + 1);
                float weight = Mathf.Sin(t * Mathf.PI);
                links[i].rb.AddForce(Vector2.down * sagForce * weight * links[i].rb.mass);
            }
        }

        ApplyStretchForce();
    }

    private void ApplyStretchForce()
    {
        if (bikeRb == null || cargoRb == null) return;

        Vector2 bikePos = bikeRb.position;
        Vector2 cargoPos = cargoRb.position;
        float currentDistance = Vector2.Distance(bikePos, cargoPos);
        float maxDistance = baseRopeLength * maxStretchMultiplier;

        if (currentDistance <= maxDistance) return;

        float overshoot = currentDistance - maxDistance;
        float maxOvershoot = baseRopeLength * (maxStretchMultiplier - 1f);
        float normalizedOvershoot = Mathf.Clamp01(overshoot / maxOvershoot);

        float forceMagnitude = stretchForceBase * Mathf.Pow(normalizedOvershoot, stretchExponent);

        Vector2 pullDirection = (bikePos - cargoPos).normalized;
        cargoRb.AddForce(pullDirection * forceMagnitude, ForceMode2D.Force);
    }

    private void LateUpdate()
    {
        if (links.Count == 0 || startPoint == null || endPoint == null)
            return;

        time += Time.deltaTime * wobbleSpeed;
        DrawRope();
    }

    private void DrawRope()
    {
        List<Vector3> points = new List<Vector3>(links.Count + 2);
        points.Add(startPoint.position);
        for (int i = 0; i < links.Count; i++)
        {
            if (links[i].tr != null)
                points.Add(links[i].tr.position);
        }
        points.Add(endPoint.position);

        List<Vector3> smoothed = CatmullRomSmooth(points, smoothSubdivisions);

        if (wobbleAmount > 0f)
        {
            Vector3 mainDir = ((Vector3)endPoint.position - (Vector3)startPoint.position).normalized;
            Vector3 wobbleAxis = new Vector3(-mainDir.y, mainDir.x, 0f);
            if (wobbleAxis.sqrMagnitude < 0.0001f)
                wobbleAxis = Vector3.right;
            for (int i = 0; i < smoothed.Count; i++)
            {
                float t = (float)i / (smoothed.Count - 1);
                float sagCurve = Mathf.Sin(t * Mathf.PI);
                float w = Mathf.Sin(time + t * 6f) * sagCurve * wobbleAmount;
                smoothed[i] += wobbleAxis * w;
            }
        }

        lineRenderer.positionCount = smoothed.Count;
        lineRenderer.SetPositions(smoothed.ToArray());
        lineRenderer.startWidth = ropeWidth;
        lineRenderer.endWidth = ropeWidth;

        if (ropeMaterial != null)
        {
            float len = 0f;
            for (int i = 0; i < smoothed.Count - 1; i++)
                len += Vector3.Distance(smoothed[i], smoothed[i + 1]);
            ropeMaterial.mainTextureScale = new Vector2(len * textureTiling, 1f);
        }
    }

    private static List<Vector3> CatmullRomSmooth(List<Vector3> pts, int sub)
    {
        if (pts.Count < 3 || sub <= 0)
            return new List<Vector3>(pts);

        List<Vector3> r = new List<Vector3>();

        for (int i = 0; i < pts.Count - 1; i++)
        {
            Vector3 p0 = i > 0 ? pts[i - 1] : pts[i];
            Vector3 p1 = pts[i];
            Vector3 p2 = pts[i + 1];
            Vector3 p3 = i + 2 < pts.Count ? pts[i + 2] : pts[i + 1];

            for (int j = 0; j < sub; j++)
            {
                float t = (float)j / sub;
                float t2 = t * t;
                float t3 = t2 * t;
                Vector3 p = 0.5f * (
                    2f * p1 +
                    (-p0 + p2) * t +
                    (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 +
                    (-p0 + 3f * p1 - 3f * p2 + p3) * t3
                );
                r.Add(p);
            }
        }

        r.Add(pts[pts.Count - 1]);
        return r;
    }

    private void OnDestroy()
    {
        if (chainContainer != null)
            Destroy(chainContainer);
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (links.Count == 0) return;
        Gizmos.color = Color.yellow;
        for (int i = 0; i < links.Count; i++)
        {
            if (links[i].tr != null)
                Gizmos.DrawWireSphere(links[i].tr.position, ropeColliderRadius);
        }
    }
#endif
}
