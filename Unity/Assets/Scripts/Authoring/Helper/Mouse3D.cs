using System.Collections;
using UnityEngine;

#if UNITY_EDITOR
    using UnityEditor;
#endif

public static class Mouse3D 
{
    #if UNITY_EDITOR
    public static Vector3 GetMouseWorldPositionInSceneView(SceneView sv,Event e, LayerMask MouseColliderLayerMask)
    {
        Vector2 screenPosition = e.mousePosition;
        float ppp = EditorGUIUtility.pixelsPerPoint;
        screenPosition.y = sv.camera.pixelHeight - screenPosition.y * ppp;
        screenPosition.x *= ppp;

        Ray ray = sv.camera.ScreenPointToRay(screenPosition);
        if (Physics.Raycast(ray, out RaycastHit raycastHit, 999f, MouseColliderLayerMask))
        {
            return raycastHit.point;
        }
        else
        {
            return Vector3.zero;
        }
        
    }
    #endif
    public static Vector3 GetMouseWorldPosition(LayerMask MouseColliderLayerMask)
    {
        Ray ray = Camera.current.ScreenPointToRay(Input.mousePosition);
        if (Physics.Raycast(ray, out RaycastHit raycastHit, 999f, MouseColliderLayerMask))
        {
            return raycastHit.point;
        }
        else
        {
            return Vector3.zero;
        }
    }
}
