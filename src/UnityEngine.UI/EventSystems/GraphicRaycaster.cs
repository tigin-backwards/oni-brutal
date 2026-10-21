using System.Collections.Generic;
using UnityEngine.EventSystems;

namespace UnityEngine.UI
{
    /// Raycasts the pointer against the graphics of its canvas.
    public partial class GraphicRaycaster
    {
        [System.NonSerialized] Canvas cachedCanvas;
        [System.NonSerialized] readonly List<Graphic> hits = new();

        protected GraphicRaycaster()
        {
            m_IgnoreReversedGraphics = true;
            m_BlockingObjects = BlockingObjects.None;
            m_BlockingMask = -1;
        }

        /// An overlay canvas ranks by its sorting order and render order among the raycasters.
        public override int sortOrderPriority => canvas.renderMode == RenderMode.ScreenSpaceOverlay ? canvas.sortingOrder : base.sortOrderPriority;

        public override int renderOrderPriority => canvas.renderMode == RenderMode.ScreenSpaceOverlay ? canvas.rootCanvas.renderOrder : base.renderOrderPriority;

        public bool ignoreReversedGraphics
        {
            get => m_IgnoreReversedGraphics;
            set => m_IgnoreReversedGraphics = value;
        }

        public BlockingObjects blockingObjects
        {
            get => m_BlockingObjects;
            set => m_BlockingObjects = value;
        }

        public LayerMask blockingMask
        {
            get => m_BlockingMask;
            set => m_BlockingMask = value;
        }

        Canvas canvas => cachedCanvas != null ? cachedCanvas : cachedCanvas = GetComponent<Canvas>();

        /// Null for an overlay canvas (and a camera canvas without its camera), else the
        /// canvas's camera, or the main camera.
        public override Camera eventCamera
        {
            get
            {
                var c = canvas;
                var mode = c.renderMode;
                if (mode == RenderMode.ScreenSpaceOverlay || (mode == RenderMode.ScreenSpaceCamera && c.worldCamera == null)) return null;
                return c.worldCamera != null ? c.worldCamera : Camera.main;
            }
        }

        /// Hits of the canvas's raycast targets under the pointer on the canvas's display, front
        /// first; graphics facing away (with ignoreReversedGraphics), behind the camera or behind
        /// a blocking physics hit are left out.
        public override void Raycast(PointerEventData eventData, List<RaycastResult> resultAppendList)
        {
            var c = canvas;
            if (c == null) return;
            var graphics = GraphicRegistry.GetRaycastableGraphicsForCanvas(c);
            if (graphics == null || graphics.Count == 0) return;

            var camera = eventCamera;
            int display = c.renderMode == RenderMode.ScreenSpaceOverlay || camera == null ? c.targetDisplay : camera.targetDisplay;
            // The display under the pointer (z); where the platform cannot tell (a zero result), the
            // pointer's own display.
            Vector3 position = Display.RelativeMouseAt(eventData.position);
            if (position == Vector3.zero) position = new Vector3(eventData.position.x, eventData.position.y, eventData.displayIndex);
            if ((int)position.z != display) return;

            // Points outside the display hit nothing.
            Vector2 viewport;
            if (camera == null)
            {
                float w = Screen.width, h = Screen.height;
                if (display > 0 && display < Display.displays.Length)
                {
                    w = Display.displays[display].systemWidth;
                    h = Display.displays[display].systemHeight;
                }
                viewport = new Vector2(position.x / w, position.y / h);
            }
            else viewport = camera.ScreenToViewportPoint(position);
            if (viewport.x < 0f || viewport.x > 1f || viewport.y < 0f || viewport.y > 1f) return;

            float blockedAt = float.MaxValue;
            var ray = new Ray();
            if (camera != null) ray = camera.ScreenPointToRay(position);
            if (c.renderMode != RenderMode.ScreenSpaceOverlay && blockingObjects != BlockingObjects.None)
            {
                float reach = 100f;
                if (camera != null)
                {
                    float dz = ray.direction.z;
                    reach = Mathf.Approximately(0f, dz) ? Mathf.Infinity : Mathf.Abs((camera.farClipPlane - camera.nearClipPlane) / dz);
                }
                if ((blockingObjects == BlockingObjects.ThreeD || blockingObjects == BlockingObjects.All)
                    && Physics.Raycast(ray, out var hit, reach, m_BlockingMask))
                    blockedAt = hit.distance;
                // 2D blocking (TwoD, All) needs a 2D physics ray query, which the engine does not
                // have: it keeps 2D collider shapes but simulates and queries no 2D physics
                // (Physics/Physics2D.cs). 2D colliders therefore block nothing, with the shipped
                // package on this engine as well.
            }

            hits.Clear();
            CollectHits(camera, position, graphics, hits);
            foreach (var g in hits)
            {
                var go = g.gameObject;
                var t = go.transform;
                if (m_IgnoreReversedGraphics && !FacesCamera(t, camera)) continue;
                float distance = 0f;
                if (camera != null && c.renderMode != RenderMode.ScreenSpaceOverlay)
                {
                    // Along the ray to the graphic's plane; behind the camera it is not hit.
                    var forward = t.forward;
                    distance = Vector3.Dot(forward, t.position - ray.origin) / Vector3.Dot(forward, ray.direction);
                    if (distance < 0f) continue;
                }
                if (distance >= blockedAt) continue;
                resultAppendList.Add(new RaycastResult
                {
                    gameObject = go,
                    module = this,
                    distance = distance,
                    screenPosition = position,
                    displayIndex = display,
                    index = resultAppendList.Count,
                    depth = g.depth,
                    sortingLayer = c.sortingLayerID,
                    sortingOrder = c.sortingOrder,
                    worldPosition = ray.origin + ray.direction * distance,
                    worldNormal = -t.forward,
                });
            }
        }

        static bool FacesCamera(Transform t, Camera camera)
        {
            if (camera == null) return Vector3.Dot(Vector3.forward, t.rotation * Vector3.forward) > 0f;
            var ct = camera.transform;
            var nearPlane = ct.rotation * Vector3.forward * camera.nearClipPlane;
            return Vector3.Dot(t.position - ct.position - nearPlane, t.forward) >= 0f;
        }

        /// The drawn, unculled raycast targets containing the point (with their raycast padding)
        /// that accept it, by depth from the front.
        static void CollectHits(Camera camera, Vector2 point, IList<Graphic> graphics, List<Graphic> results)
        {
            for (int i = 0; i < graphics.Count; i++)
            {
                var g = graphics[i];
                if (!g.raycastTarget || g.canvasRenderer.cull || g.depth == -1) continue;
                if (!RectTransformUtility.RectangleContainsScreenPoint(g.rectTransform, point, camera, g.raycastPadding)) continue;
                if (camera != null && camera.WorldToScreenPoint(g.rectTransform.position).z > camera.farClipPlane) continue;
                if (g.Raycast(point, camera)) results.Add(g);
            }
            results.Sort((a, b) => b.depth.CompareTo(a.depth));
        }
    }
}
