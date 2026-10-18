using System;
using System.Collections.Generic;

namespace UnityEngine.UI
{
    /// A queued layout rebuild of one layout root: sizes are computed bottom-up and applied
    /// top-down, horizontally first.
    public partial class LayoutRebuilder
    {
        RectTransform root;
        int hash;


        static LayoutRebuilder()
        {
            RectTransform.reapplyDrivenProperties += rect => MarkLayoutForRebuild(rect);
        }

        public LayoutRebuilder() { }

        LayoutRebuilder(RectTransform layoutRoot)
        {
            root = layoutRoot;
            hash = layoutRoot == null ? 0 : layoutRoot.GetHashCode();
        }

        public Transform transform => root;

        public bool IsDestroyed() => root == null;

        /// Lays out the rect's layout now.
        public static void ForceRebuildLayoutImmediate(RectTransform layoutRoot) => new LayoutRebuilder(layoutRoot).Rebuild(CanvasUpdate.Layout);

        public void Rebuild(CanvasUpdate executing)
        {
            if (executing != CanvasUpdate.Layout) return;
            Calculate(root, e => e.CalculateLayoutInputHorizontal());
            Control(root, c => c.SetLayoutHorizontal());
            Calculate(root, e => e.CalculateLayoutInputVertical());
            Control(root, c => c.SetLayoutVertical());
        }

        static bool Disabled(Component c) => c is Behaviour b && !b.isActiveAndEnabled;

        static readonly Predicate<Component> disabled = Disabled;

        /// The enabled components of the type; give the list back to Scratch.
        static List<Component> Active(RectTransform rect, Type type)
        {
            var list = Scratch<Component>.Take();
            rect.GetComponents(type, list);
            list.RemoveAll(disabled);
            return list;
        }

        /// Children before parents. A rect without layout elements or a layout group ends the walk:
        /// layouts below it are roots of their own.
        static void Calculate(RectTransform rect, Action<ILayoutElement> step)
        {
            if (rect == null) return;
            var elements = Active(rect, typeof(ILayoutElement));
            if (elements.Count > 0 || rect.GetComponent(typeof(ILayoutGroup)) != null)
            {
                for (int i = 0; i < rect.childCount; i++) Calculate(rect.GetChild(i) as RectTransform, step);
                foreach (var e in elements) step((ILayoutElement)e);
            }
            Scratch<Component>.Return(elements);
        }

        /// Parents before children; on one object the self controllers (fitters) before the
        /// others (groups), which lay out children within the size just set. A rect without
        /// controllers ends the walk.
        static void Control(RectTransform rect, Action<ILayoutController> step)
        {
            if (rect == null) return;
            var controllers = Active(rect, typeof(ILayoutController));
            if (controllers.Count > 0)
            {
                foreach (var c in controllers)
                    if (c is ILayoutSelfController) step((ILayoutController)c);
                foreach (var c in controllers)
                    if (c is not ILayoutSelfController) step((ILayoutController)c);
                for (int i = 0; i < rect.childCount; i++) Control(rect.GetChild(i) as RectTransform, step);
            }
            Scratch<Component>.Return(controllers);
        }

        /// Queues the rect's layout root for the next canvas update: the topmost of the
        /// consecutive ancestors with an active layout group, or the rect itself when it has a
        /// layout controller.
        public static void MarkLayoutForRebuild(RectTransform rect)
        {
            if (rect == null || rect.gameObject == null) return;
            var layoutRoot = rect;
            for (var parent = rect.parent as RectTransform; parent != null; parent = parent.parent as RectTransform)
            {
                if (!HasActive(parent, typeof(ILayoutGroup))) break;
                layoutRoot = parent;
            }
            if (layoutRoot == rect && !HasActive(rect, typeof(ILayoutController))) return;
            MarkLayoutRootForRebuild(layoutRoot);
        }

        static bool HasActive(RectTransform rect, Type type)
        {
            var list = Scratch<Component>.Take();
            rect.GetComponents(type, list);
            bool found = false;
            foreach (var c in list)
                if (c is Behaviour b && b.isActiveAndEnabled)
                {
                    found = true;
                    break;
                }
            Scratch<Component>.Return(list);
            return found;
        }

        static void MarkLayoutRootForRebuild(RectTransform controller)
        {
            if (controller == null) return;
            CanvasUpdateRegistry.TryRegisterCanvasElementForLayoutRebuild(new LayoutRebuilder(controller));
        }

        public void LayoutComplete() { }

        public void GraphicUpdateComplete() { }

        /// Rebuilders of the same root are one queue entry.
        public override bool Equals(object obj) => obj is LayoutRebuilder other && other.root == root;

        public override int GetHashCode() => hash;

        public override string ToString() => "(Layout Rebuilder for) " + root;
    }

    /// Reads layout sizes from the ILayoutElement components on a rect.
    public static class LayoutUtility
    {
        public static float GetMinSize(RectTransform rect, int axis) => axis == 0 ? GetMinWidth(rect) : GetMinHeight(rect);

        public static float GetPreferredSize(RectTransform rect, int axis) => axis == 0 ? GetPreferredWidth(rect) : GetPreferredHeight(rect);

        public static float GetFlexibleSize(RectTransform rect, int axis) => axis == 0 ? GetFlexibleWidth(rect) : GetFlexibleHeight(rect);

        public static float GetMinWidth(RectTransform rect) => GetLayoutProperty(rect, e => e.minWidth, 0f);

        /// Never below the minimum.
        public static float GetPreferredWidth(RectTransform rect) => Mathf.Max(GetLayoutProperty(rect, e => e.minWidth, 0f), GetLayoutProperty(rect, e => e.preferredWidth, 0f));

        public static float GetFlexibleWidth(RectTransform rect) => GetLayoutProperty(rect, e => e.flexibleWidth, 0f);

        public static float GetMinHeight(RectTransform rect) => GetLayoutProperty(rect, e => e.minHeight, 0f);

        public static float GetPreferredHeight(RectTransform rect) => Mathf.Max(GetLayoutProperty(rect, e => e.minHeight, 0f), GetLayoutProperty(rect, e => e.preferredHeight, 0f));

        public static float GetFlexibleHeight(RectTransform rect) => GetLayoutProperty(rect, e => e.flexibleHeight, 0f);

        public static float GetLayoutProperty(RectTransform rect, Func<ILayoutElement, float> property, float defaultValue)
            => GetLayoutProperty(rect, property, defaultValue, out _);

        /// The value of the enabled layout element with the highest priority that sets it (a
        /// negative value is unset); among equal priorities the largest. defaultValue when none
        /// sets it.
        public static float GetLayoutProperty(RectTransform rect, Func<ILayoutElement, float> property, float defaultValue, out ILayoutElement source)
        {
            source = null;
            if (rect == null) return 0f;
            float result = defaultValue;
            int best = int.MinValue;
            var list = Scratch<Component>.Take();
            rect.GetComponents(typeof(ILayoutElement), list);
            foreach (var c in list)
            {
                if (c is Behaviour b && !b.isActiveAndEnabled) continue;
                var e = (ILayoutElement)c;
                int priority = e.layoutPriority;
                if (priority < best) continue;
                float value = property(e);
                if (value < 0f) continue;
                if (priority > best)
                {
                    result = value;
                    best = priority;
                    source = e;
                }
                else if (value > result)
                {
                    result = value;
                    source = e;
                }
            }
            Scratch<Component>.Return(list);
            return result;
        }
    }
}
