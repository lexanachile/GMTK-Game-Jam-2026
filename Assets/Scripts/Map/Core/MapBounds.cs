using UnityEngine;

[ExecuteAlways]
public class MapBounds : MonoBehaviour
{
    public static MapBounds Instance;

    public Vector2 size = new Vector2(200, 200);

    private void Awake()
    {
        Instance = this;
    }

#if UNITY_EDITOR
    private void OnEnable()
    {
        Instance = this;
    }
#endif

    public Bounds Bounds =>
        new Bounds(transform.position, new Vector3(size.x, size.y, 0));

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawWireCube(transform.position,
            new Vector3(size.x, size.y, 0));
    }
}