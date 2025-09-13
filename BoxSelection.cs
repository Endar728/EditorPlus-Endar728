using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace EditorPlus
{
    public sealed class BoxSelection : MonoBehaviour
    {
        public Camera selectionCamera;
        public KeyCode selectionModifierKey = KeyCode.LeftShift;
        public float minimumDragSizePixels = 6f;
        public float maximumSelectionDistanceWorld = 0f;

        private GroupFollowers _groupFollowers;
        private bool _isDragging;
        private Vector2 _dragStartScreenPosition;
        private Rect _dragRectangleScreen;

        private void Awake()
        {
            if (!selectionCamera) selectionCamera = Camera.main;

            _groupFollowers = GetComponent<GroupFollowers>();
            if (!_groupFollowers)
            {
                Debug.LogError("BoxSelection requires GroupFollowers on the same GameObject.");
                enabled = false;
            }
        }

        private void Update()
        {
            if (!enabled) return;
            if (EventSystem.current && EventSystem.current.IsPointerOverGameObject()) return;

            if (Input.GetMouseButtonDown(0) && Input.GetKey(selectionModifierKey))
                StartDragging((Vector2)Input.mousePosition);

            if (_isDragging && Input.GetMouseButton(0))
                UpdateDragRectangle((Vector2)Input.mousePosition);

            if (_isDragging && Input.GetMouseButtonUp(0))
                FinishDragging();
        }

        private void StartDragging(Vector2 screenPosition)
        {
            _isDragging = true;
            _dragStartScreenPosition = screenPosition;
            _dragRectangleScreen = new Rect(screenPosition, Vector2.zero);
        }

        private void UpdateDragRectangle(Vector2 currentScreenPosition)
        {
            Vector2 min = Vector2.Min(_dragStartScreenPosition, currentScreenPosition);
            Vector2 max = Vector2.Max(_dragStartScreenPosition, currentScreenPosition);
            _dragRectangleScreen = Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }

        private void FinishDragging()
        {
            _isDragging = false;

            bool isLargeEnough =
                _dragRectangleScreen.width > minimumDragSizePixels &&
                _dragRectangleScreen.height > minimumDragSizePixels;

            if (!isLargeEnough) return;

            List<Unit> selectedUnits = CollectUnitsInsideScreenRectangle(_dragRectangleScreen);

            if (selectedUnits.Count == 0)
            {
                _groupFollowers.ClearGroupAndTryVanillaDeselect();
                return;
            }

            Vector2 rectangleCenter = _dragRectangleScreen.center;
            Unit primary = NearestUnitToScreenPoint(selectedUnits, rectangleCenter);

            _groupFollowers.SetGroup(selectedUnits, primary);
            _groupFollowers.TryVanillaSelectPrimary(primary);
        }

        private List<Unit> CollectUnitsInsideScreenRectangle(Rect screenRectangle)
        {
            Unit[] allUnits = FindObjectsOfType<Unit>(includeInactive: false);
            var result = new List<Unit>(allUnits.Length);

            float maxDistance =
                (maximumSelectionDistanceWorld > 0f && selectionCamera)
                    ? Mathf.Min(maximumSelectionDistanceWorld, selectionCamera.farClipPlane)
                    : float.PositiveInfinity;

            Vector3 cameraPosition = selectionCamera ? selectionCamera.transform.position : Vector3.zero;

            for (int i = 0; i < allUnits.Length; i++)
            {
                Unit unit = allUnits[i];
                if (!unit) continue;

                Vector3 screenPoint3D = selectionCamera.WorldToScreenPoint(unit.transform.position);
                if (screenPoint3D.z <= 0f) continue;

                Vector2 screenPoint = new Vector2(screenPoint3D.x, screenPoint3D.y);
                if (!screenRectangle.Contains(screenPoint)) continue;

                if (maxDistance != float.PositiveInfinity)
                {
                    float worldDistance = Vector3.Distance(cameraPosition, unit.transform.position);
                    if (worldDistance > maxDistance) continue;
                }

                result.Add(unit);
            }

            return result;
        }

        private Unit NearestUnitToScreenPoint(List<Unit> units, Vector2 targetScreenPoint)
        {
            Unit best = null;
            float bestSqrDistance = float.PositiveInfinity;

            for (int i = 0; i < units.Count; i++)
            {
                Unit unit = units[i];
                if (!unit) continue;

                Vector2 unitScreenPoint = (Vector2)selectionCamera.WorldToScreenPoint(unit.transform.position);
                float sqrDistance = (unitScreenPoint - targetScreenPoint).sqrMagnitude;

                if (sqrDistance < bestSqrDistance)
                {
                    bestSqrDistance = sqrDistance;
                    best = unit;
                }
            }

            return best;
        }

        private void OnGUI()
        {
            if (!_isDragging) return;

            Rect guiRectangle = ScreenRectToGuiRect(_dragRectangleScreen);
            DrawFilledRectWithBorder(guiRectangle, new Color(1f, 1f, 1f, 0.2f), new Color(1f, 1f, 1f, 0.9f), 1f);
        }

        private static Rect ScreenRectToGuiRect(Rect screenRect)
        {
            float topY = Screen.height - screenRect.yMax;
            return new Rect(screenRect.xMin, topY, screenRect.width, screenRect.height);
        }

        private static void DrawFilledRectWithBorder(Rect rect, Color fill, Color border, float borderThickness)
        {
            Color previousColor = GUI.color;

            GUI.color = fill;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);

            GUI.color = border;
            GUI.DrawTexture(new Rect(rect.xMin, rect.yMin, rect.width, borderThickness), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(rect.xMin, rect.yMax - borderThickness, rect.width, borderThickness), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(rect.xMin, rect.yMin, borderThickness, rect.height), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(rect.xMax - borderThickness, rect.yMin, borderThickness, rect.height), Texture2D.whiteTexture);

            GUI.color = previousColor;
        }
    }
}
