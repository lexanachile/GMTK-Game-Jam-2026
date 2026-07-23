#if UNITY_EDITOR

using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(MapBounds))]
public class MapBoundsEditor : Editor
{
    private void OnSceneGUI()
    {
        MapBounds bounds = (MapBounds)target;

        Vector3 center = bounds.transform.position;
        Vector3 size = new Vector3(bounds.size.x, bounds.size.y, 0);

        EditorGUI.BeginChangeCheck();

        size = Handles.ScaleHandle(
            size,
            center,
            Quaternion.identity,
            HandleUtility.GetHandleSize(center));

        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(bounds, "Resize Map Bounds");

            bounds.size = new Vector2(
                Mathf.Max(1, size.x),
                Mathf.Max(1, size.y));

            EditorUtility.SetDirty(bounds);
        }
    }
}

#endif