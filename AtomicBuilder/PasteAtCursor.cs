using NuclearOption;
using UnityEngine;

namespace EditorPlus.AtomicBuilder
{
    /// <summary>Mouse raycast for paste anchor (terrain / water), shared by blueprint and group paste.</summary>
    internal static class PasteAtCursor
    {
        const int TerrainOnlyLayerMask = 64;

        internal static bool TryGetLocalPastePoint(out Vector3 localPoint)
        {
            localPoint = default;
            Camera cam = Camera.main;
            if (cam == null) return false;

            Ray ray = cam.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out RaycastHit terrainHit, 100000f, TerrainOnlyLayerMask))
            {
                localPoint = terrainHit.point;
                return true;
            }

            if (Physics.Raycast(ray, out RaycastHit hit, 100000f))
            {
                localPoint = hit.point;
                if (TrySnapLocalYToTerrain(localPoint, out Vector3 snapped))
                    localPoint = snapped;
                return true;
            }

            Plane waterPlane = new Plane(Vector3.up, new Vector3(0, Datum.LocalSeaY, 0));
            if (!waterPlane.Raycast(ray, out float enter)) return false;
            localPoint = ray.GetPoint(enter);
            return true;
        }

        internal static Vector3 GetLocalPastePointOrFallback()
        {
            if (TryGetLocalPastePoint(out Vector3 p))
                return p;

            Camera cam = Camera.main;
            if (cam != null)
                return cam.transform.position + cam.transform.forward * 500f;
            return Vector3.zero;
        }

        static bool TrySnapLocalYToTerrain(Vector3 worldPoint, out Vector3 snapped)
        {
            snapped = worldPoint;
            float startY = Mathf.Max(worldPoint.y, Datum.LocalSeaY) + 10000f;
            var origin = new Vector3(worldPoint.x, startY, worldPoint.z);
            if (!Physics.Raycast(origin, Vector3.down, out RaycastHit tHit, 25000f, TerrainOnlyLayerMask))
                return false;
            if (tHit.collider == null || tHit.collider.GetComponentInParent<Unit>() != null)
                return false;
            snapped = new Vector3(worldPoint.x, tHit.point.y, worldPoint.z);
            return true;
        }
    }
}
