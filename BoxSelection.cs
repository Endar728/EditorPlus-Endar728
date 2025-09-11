using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
[RequireComponent(typeof(GroupFollowers))]
public sealed class BoxSelectVanilla : MonoBehaviour
{
    [SerializeField] private Camera worldCam;
    [SerializeField] private readonly KeyCode modifyKey = KeyCode.LeftShift;
    [SerializeField] private readonly float minDragPixels = 6f;
    private GroupFollowers _followers;
    void Awake()
    {
        if (!worldCam) worldCam = Camera.main;
        _followers = GetComponent<GroupFollowers>();
    }
    Vector2 _dragStart;
    bool _dragging;
    Rect _rectBL;
    Rect _rectGUI;
    void Update()
    {
        if (EventSystem.current && EventSystem.current.IsPointerOverGameObject()) return;
        if (Input.GetMouseButtonDown(0) && Input.GetKey(modifyKey))
        {
            _dragStart = Input.mousePosition;
            _dragging = true;
            UpdateRects(_dragStart, _dragStart);
        }
        if (_dragging && Input.GetMouseButton(0))
        {
            Vector2 now = (Vector2)Input.mousePosition;
            UpdateRects(_dragStart, now);
        }
        if (_dragging && Input.GetMouseButtonUp(0))
        {
            _dragging = false;
            Vector2 size = new(_rectBL.width, _rectBL.height);
            bool wasBox = Mathf.Abs(size.x) > minDragPixels && Mathf.Abs(size.y) > minDragPixels;
            if (wasBox)
            {
                List<Unit> picked = [.. CollectUnitsInside(_rectBL)];
                if (picked.Count == 0)
                {
                    _followers.ClearGroupAndTryVanillaDeselect();
                    return;
                }
                Vector2 center = new(_rectBL.center.x, _rectBL.center.y);
                Unit primary = picked
                    .OrderBy(u => Vector2.SqrMagnitude((Vector2)worldCam.WorldToScreenPoint(u.transform.position) - center))
                    .First();
                _followers.SetGroup(picked, primary);
                _followers.TryVanillaSelectPrimary(primary);
            }
            else
            {
                if (!RaycastUnitUnderMouse(out Unit unit))
                {
                    _followers.ClearGroupAndTryVanillaDeselect();
                }
                else
                {
                    _followers.SetGroup([unit], unit);
                    _followers.TryVanillaSelectPrimary(unit);
                }
            }
        }
    }
    void UpdateRects(Vector2 a, Vector2 b)
    {
        Vector2 min = Vector2.Min(a, b);
        Vector2 max = Vector2.Max(a, b);
        _rectBL = Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        float yMinGUI = Screen.height - max.y;
        float yMaxGUI = Screen.height - min.y;
        _rectGUI = Rect.MinMaxRect(min.x, yMinGUI, max.x, yMaxGUI);
    }
    IEnumerable<Unit> CollectUnitsInside(Rect blRect)
    {
        Unit[] all = FindObjectsOfType<Unit>(includeInactive: false);
        foreach (Unit u in all)
        {
            if (!u || !u.transform) continue;
            Vector3 sp = worldCam.WorldToScreenPoint(u.transform.position);
            if (sp.z < 0) continue;
            if (blRect.Contains(new Vector2(sp.x, sp.y)))
                yield return u;
        }
    }
    bool RaycastUnitUnderMouse(out Unit unit)
    {
        unit = null;
        Ray ray = worldCam.ScreenPointToRay(Input.mousePosition);
        if (Physics.Raycast(ray, out RaycastHit hit, 100000f))
        {
            unit = hit.collider.GetComponentInParent<Unit>();
        }
        return unit;
    }
    void OnGUI()
    {
        if (!_dragging) return;
        DrawRect(_rectGUI, new Color(1, 1, 1, 0.2f), new Color(1, 1, 1, 0.9f), 1f);
    }
    static void DrawRect(Rect r, Color fill, Color border, float t)
    {
        Color old = GUI.color;
        GUI.color = fill; GUI.DrawTexture(r, Texture2D.whiteTexture);
        GUI.color = border;
        GUI.DrawTexture(new Rect(r.xMin, r.yMin, r.width, t), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(r.xMin, r.yMax - t, r.width, t), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(r.xMin, r.yMin, t, r.height), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(r.xMax - t, r.yMin, t, r.height), Texture2D.whiteTexture);
        GUI.color = old;
    }
}
