using UnityEngine;

[RequireComponent(typeof(LineRenderer))]
public class RopeRenderer : MonoBehaviour
{
    [SerializeField] private Transform start;

    [SerializeField] private Transform end;

    [SerializeField] private int segments = 12;

    [SerializeField] private float sag = 0.4f;

    [SerializeField] private float wobble = 0.15f;

    private LineRenderer line;

    private void Awake()
    {
        line = GetComponent<LineRenderer>();

        line.positionCount =
            segments;
    }

    private void LateUpdate()
    {
        DrawRope();
    }

    private void DrawRope()
    {
        Vector3 a =
            start.position;

        Vector3 b =
            end.position;

        for (int i = 0; i < segments; i++)
        {
            float t =
                i /
                (float)(segments - 1);

            Vector3 point =
                Vector3.Lerp(
                    a,
                    b,
                    t
                );

            float curve =
                Mathf.Sin(
                    t * Mathf.PI
                );

            point -=
                Vector3.up *
                curve *
                sag;

            point +=
                end.right *
                curve *
                wobble;

            line.SetPosition(
                i,
                point
            );
        }
    }
}