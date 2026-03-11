using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace EditorPlus
{
    public sealed class BoxSelection : MonoBehaviour
    {
        public KeyCode selectionModifierKey = KeyCode.LeftShift;
        public float minimumDragSizePixels = 4f;
        public float maximumSelectionDistanceWorld = 0;

        private GroupFollowers _groupFollowers;
        private bool _isDragging;
        private Vector2 _dragStartScreenPosition;
        private Rect _dragRectangleScreen;

        private void Awake()
        {
            _groupFollowers = GetComponent<GroupFollowers>();
            if (!_groupFollowers)
            {
                Plugin.Logger.LogError("BoxSelection requires GroupFollowers on the same GameObject.");
                enabled = false;
            }
        }

        private void Update()
        {
            if (!enabled) return;

            bool pointerOverUi = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();

            if (!_isDragging)
            {
                if (pointerOverUi) return;
                if (Input.GetMouseButtonDown(0) && Input.GetKey(selectionModifierKey))
                    StartDragging((Vector2)Input.mousePosition);
            }
            else
            {
                if (Input.GetMouseButton(0))
                    UpdateDragRectangle((Vector2)Input.mousePosition);
                if (Input.GetMouseButtonUp(0))
                    FinishDragging();
            }
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
            List<Unit> result = new(allUnits.Length);

            float maxDistance =
                (maximumSelectionDistanceWorld > 0f && Camera.main)
                    ? Mathf.Min(maximumSelectionDistanceWorld, Camera.main.farClipPlane)
                    : float.PositiveInfinity;

            for (int i = 0; i < allUnits.Length; i++)
            {
                Unit unit = allUnits[i];
                if (!unit) continue;

                Vector3 screenPoint3D = Camera.main.WorldToScreenPoint(unit.transform.position);
                if (screenPoint3D.z <= 0f) continue;

                Vector2 screenPoint = new(screenPoint3D.x, screenPoint3D.y);
                if (!screenRectangle.Contains(screenPoint)) continue;

                if (maxDistance != float.PositiveInfinity)
                {
                    float worldDistance = Vector3.Distance(Camera.main.transform.position, unit.transform.position);
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

                Vector2 unitScreenPoint = (Vector2)Camera.main.WorldToScreenPoint(unit.transform.position);
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
            Color fillColor = new(0f, 0f, 0f, 0.2f);
            Color edgeColor = new(1f, 1f, 1f, 0.9f);
            DrawFilledRectWithBorder(guiRectangle, fillColor, edgeColor, 1f);
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
