using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine.EventSystems;
using UnityEngine;


namespace EditorPlus
{
    public sealed partial class GraphView 
    {
        private NodeView _hoverNode;
        private NodeView _hoverPortNode;
        private PortKind _hoverPortKind;

        private NodeView _dragFromNode;
        private PortKind _dragFromKind;

        [Header("Drag Ghost Edge")]
        [SerializeField] public Color ghostEdgeTint = new(1f, 1f, 1f, 0.40f);
        [SerializeField] public float ghostEdgeThickness = 2f;
        private RectTransform _ghostEnd;    
        private EdgeView _ghostEdge;        

        void Update()
        {
            if (!Input.GetKeyDown(KeyCode.Delete))
                return;

            if (!viewport) return;
            if (!RectTransformUtility.RectangleContainsScreenPoint(viewport, Input.mousePosition, UiCam))
                return;

            if (_hoverEdge)
            {
                OnEdgeRequestDelete(_hoverEdge);
                return;
            }

            if (_hoverPortNode)
            {
                if (_hoverPortKind == PortKind.Input) UnlinkIncoming(_hoverPortNode);
                else UnlinkOutgoing(_hoverPortNode);
                ClearHighlights();
                return;
            }

            if (_hoverNode)
            {
                UnlinkIncoming(_hoverNode);
                UnlinkOutgoing(_hoverNode);
                ClearHighlights();
            }
        }
        public void BeginLinkDrag(NodeView fromNode, PortKind fromKind, PointerEventData e)
        {
            if (fromNode == null) return;
            _dragFromNode = fromNode;
            _dragFromKind = fromKind;

            _ghostEnd = new GameObject("GhostEnd", typeof(RectTransform)).GetComponent<RectTransform>();
            _ghostEnd.SetParent(content, false);
            _ghostEnd.sizeDelta = Vector2.one;

            var begin = (fromKind == PortKind.Output) ? fromNode.RightPort : fromNode.LeftPort;
            _ghostEdge = CreateEdge("Edge_Ghost", begin, _ghostEnd, AppForGhost());

            HighlightNeighborhood(fromNode, true);
            DragLinkTo(e.position);
        }
        public void DragLinkTo(Vector2 screenPos)
        {
            if (_ghostEnd == null) return;

            RectTransformUtility.ScreenPointToLocalPointInRectangle(content, screenPos, UiCam, out var local);
            _ghostEnd.anchoredPosition = local;

            if (_dragFromNode != null)
            {
                HighlightNeighborhood(_dragFromNode, true);

                var targetPort = RaycastPortAt(screenPos);
                var to = targetPort ? targetPort.GetComponentInParent<NodeView>() : null;
                if (to != null && to != _dragFromNode && to.Kind != _dragFromNode.Kind
                    && targetPort.Kind != _dragFromKind)
                {
                    to.SetHighlighted(true);
                    _hlNodes.Add(to);
                }
            }
        }
        public void EndLinkDrag(Vector2 screenPos)
        {
            void CleanupGhost()
            {
                if (_ghostEdge) Destroy(_ghostEdge.gameObject);
                if (_ghostEnd) Destroy(_ghostEnd.gameObject);
                _ghostEdge = null; _ghostEnd = null;
                ClearHighlights();
            }

            if (_dragFromNode == null) { CleanupGhost(); return; }

            var targetPort = RaycastPortAt(screenPos);
            var fromNode = _dragFromNode;
            var fromKind = _dragFromKind;
            CleanupGhost();
            _dragFromNode = null;
            if (fromNode == null || targetPort == null) return;
            var toNode = targetPort.GetComponentInParent<NodeView>();
            if (toNode == null || toNode == fromNode) return;

            var toKind = targetPort.Kind;
            if (fromKind == PortKind.Output && toKind == PortKind.Input)
            {
                if (fromNode.Kind == toNode.Kind) return;
                ClearHighlights();
                TryCreateLink(fromNode, toNode);
            }
            else if (fromKind == PortKind.Input && toKind == PortKind.Output)
            {
                if (fromNode.Kind == toNode.Kind) return;
                ClearHighlights();
                TryCreateLink(toNode, fromNode);
            }
        }
        private void TryCreateLink(NodeView from, NodeView to)
        {
            if (from == null || to == null || ReferenceEquals(from, to)) return;
            if (from.Kind == to.Kind) return;

            foreach (var c in _connections)
                if (c.From == from && c.To == to)
                    return;

            MakeConnection(from, to);

            OnLink?.Invoke(from.Id, from.Kind == NodeKind.Objective, to.Id, to.Kind == NodeKind.Objective);

        }
        private PortView RaycastPortAt(Vector2 screenPos)
        {
            if (EventSystem.current == null) return null;
            var results = new List<RaycastResult>();
            var ped = new PointerEventData(EventSystem.current) { position = screenPos };
            EventSystem.current.RaycastAll(ped, results);

            for (int i = 0; i < results.Count; i++)
            {
                var pv = results[i].gameObject.GetComponentInParent<PortView>();
                if (pv != null) return pv;
            }
            return null;
        }

    }
}
