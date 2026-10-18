using System;
using System.Collections.Generic;
using UnityEngine.Rendering;

namespace UnityEngine.UI
{
    /// Shared stencil variants of materials, counted by use: equal requests get the same material,
    /// and it is destroyed when its last user removes it.
    public static class StencilMaterial
    {
        sealed class Variant
        {
            public Material Base;
            public Material Material;
            public int Users;
            public int Id;
            public StencilOp Operation;
            public CompareFunction Compare;
            public int ReadMask;
            public int WriteMask;
            public ColorWriteMask ColorMask;
            public bool AlphaClip;
        }

        static readonly List<Variant> variants = new();

        static readonly string[] requiredProperties = ["_Stencil", "_StencilOp", "_StencilComp", "_StencilReadMask", "_StencilWriteMask", "_ColorMask"];

        [Obsolete("Use Material.Add instead.", true)]
        public static Material Add(Material baseMat, int stencilID) => null;

        public static Material Add(Material baseMat, int stencilID, StencilOp operation, CompareFunction compareFunction, ColorWriteMask colorWriteMask)
            => Add(baseMat, stencilID, operation, compareFunction, colorWriteMask, 255, 255);

        /// The base material with the stencil state set; the base material itself when there is
        /// nothing to change (no stencil reference and every colour channel written) or when its
        /// shader has no stencil properties.
        public static Material Add(Material baseMat, int stencilID, StencilOp operation, CompareFunction compareFunction, ColorWriteMask colorWriteMask, int readMask, int writeMask)
        {
            if ((stencilID <= 0 && colorWriteMask == ColorWriteMask.All) || baseMat == null) return baseMat;
            foreach (var p in requiredProperties)
                if (!baseMat.HasProperty(p))
                {
                    Debug.LogWarning("Material " + baseMat.name + " doesn't have " + p + " property", baseMat);
                    return baseMat;
                }

            foreach (var v in variants)
                if (v.Base == baseMat && v.Id == stencilID && v.Operation == operation && v.Compare == compareFunction
                    && v.ReadMask == readMask && v.WriteMask == writeMask && v.ColorMask == colorWriteMask)
                {
                    v.Users++;
                    return v.Material;
                }

            // Writing the stencil clips transparent pixels, so a mask's shape follows its alpha.
            bool alphaClip = operation != StencilOp.Keep && writeMask > 0;
            var material = new Material(baseMat)
            {
                name = $"Stencil Id:{stencilID}, Op:{operation}, Comp:{compareFunction}, WriteMask:{writeMask}, ReadMask:{readMask}, ColorMask:{colorWriteMask} AlphaClip:{alphaClip} ({baseMat.name})",
                hideFlags = HideFlags.HideAndDontSave,
            };
            material.SetFloat("_Stencil", stencilID);
            material.SetFloat("_StencilOp", (int)operation);
            material.SetFloat("_StencilComp", (int)compareFunction);
            material.SetFloat("_StencilReadMask", readMask);
            material.SetFloat("_StencilWriteMask", writeMask);
            material.SetFloat("_ColorMask", (int)colorWriteMask);
            material.SetFloat("_UseUIAlphaClip", alphaClip ? 1f : 0f);
            if (alphaClip) material.EnableKeyword("UNITY_UI_ALPHACLIP");
            else material.DisableKeyword("UNITY_UI_ALPHACLIP");

            variants.Add(new Variant
            {
                Base = baseMat, Material = material, Users = 1, Id = stencilID, Operation = operation, Compare = compareFunction,
                ReadMask = readMask, WriteMask = writeMask, ColorMask = colorWriteMask, AlphaClip = alphaClip,
            });
            return material;
        }

        /// One user less; the material goes with its last user.
        public static void Remove(Material customMat)
        {
            if (customMat == null) return;
            for (int i = 0; i < variants.Count; i++)
            {
                var v = variants[i];
                if (v.Material != customMat) continue;
                if (--v.Users == 0)
                {
                    Destroy(v.Material);
                    variants.RemoveAt(i);
                }
                return;
            }
        }

        public static void ClearAll()
        {
            foreach (var v in variants) Destroy(v.Material);
            variants.Clear();
        }

        static void Destroy(Material m)
        {
            if (Application.isPlaying) Object.Destroy(m);
            else Object.DestroyImmediate(m);
        }
    }

    /// Helpers for stencil masks (Mask) and rect clipping (RectMask2D).
    public class MaskUtilities
    {
        public MaskUtilities() { }

        /// The IMaskable components below the mask (not on its own object) recompute their stencil.
        public static void NotifyStencilStateChanged(Component mask)
        {
            foreach (var c in mask.GetComponentsInChildren<Component>())
            {
                if (c == null || c.gameObject == mask.gameObject) continue;
                if (c is IMaskable m) m.RecalculateMasking();
            }
        }

        /// The IClippable components below the mask (not on its own object) find their clipper again.
        public static void Notify2DMaskStateChanged(Component mask)
        {
            foreach (var c in mask.GetComponentsInChildren<Component>())
            {
                if (c == null || c.gameObject == mask.gameObject) continue;
                if (c is IClippable clippable) clippable.RecalculateClipping();
            }
        }

        /// The nearest canvas at or above start that overrides sorting, or else the topmost
        /// canvas; null without a canvas.
        public static Transform FindRootSortOverrideCanvas(Transform start)
        {
            Canvas found = null;
            for (var t = start; t != null; t = t.parent)
            {
                var c = t.GetComponent<Canvas>();
                if (c == null) continue;
                found = c;
                if (c.overrideSorting) break;
            }
            return found != null ? found.transform : null;
        }

        /// The number of ancestors (strictly above transform, up to and including stopAfter) with
        /// a working stencil mask.
        public static int GetStencilDepth(Transform transform, Transform stopAfter)
        {
            int depth = 0;
            if (transform == stopAfter) return depth;
            var masks = Scratch<Mask>.Take();
            for (var t = transform.parent; t != null; t = t.parent)
            {
                t.GetComponents(masks);
                foreach (var m in masks)
                    if (m != null && m.MaskEnabled() && m.graphic.IsActive())
                    {
                        depth++;
                        break;
                    }
                if (t == stopAfter) break;
            }
            Scratch<Mask>.Return(masks);
            return depth;
        }

        public static bool IsDescendantOrSelf(Transform father, Transform child)
        {
            if (father == null || child == null) return false;
            for (var t = child; t != null; t = t.parent)
                if (t == father) return true;
            return false;
        }

        /// The nearest enabled RectMask2D above the clippable (not on its own object), unless a
        /// sorting-override canvas lies between them.
        public static RectMask2D GetRectMaskForClippable(IClippable clippable)
        {
            var own = clippable.rectTransform;
            if (own == null) return null;
            bool separated = OverridesSorting(own);
            var masks = Scratch<RectMask2D>.Take();
            try
            {
                for (var t = own.parent; t != null; t = t.parent)
                {
                    t.GetComponents(masks);
                    foreach (var m in masks)
                        if (m != null && m.isActiveAndEnabled) return separated ? null : m;
                    if (OverridesSorting(t)) separated = true;
                }
                return null;
            }
            finally { Scratch<RectMask2D>.Return(masks); }
        }

        static bool OverridesSorting(Transform t)
        {
            var c = t.GetComponent<Canvas>();
            return c != null && c.overrideSorting;
        }

        /// The active RectMask2Ds at or above the clipper whose clipping reaches it (none above a
        /// sorting-override canvas), outermost first.
        public static void GetRectMasksForClip(RectMask2D clipper, List<RectMask2D> masks)
        {
            masks.Clear();
            var found = new List<RectMask2D>();
            bool separated = false;
            for (var t = clipper.transform; t != null; t = t.parent)
            {
                if (!separated)
                {
                    t.GetComponents(found);
                    foreach (var m in found)
                        if (m != null && m.IsActive()) masks.Add(m);
                }
                if (OverridesSorting(t)) separated = true;
            }
            masks.Reverse();
        }
    }

    /// Rect clipping in canvas space.
    public static class Clipping
    {
        /// The intersection of the masks' canvas rects, each shrunk by its padding; validRect is
        /// false when there are no masks or the intersection is empty.
        public static Rect FindCullAndClipWorldRect(List<RectMask2D> rectMaskParents, out bool validRect)
        {
            if (rectMaskParents.Count == 0)
            {
                validRect = false;
                return new Rect();
            }
            float xMin = float.MinValue, yMin = float.MinValue, xMax = float.MaxValue, yMax = float.MaxValue;
            foreach (var m in rectMaskParents)
            {
                var r = m.canvasRect;
                var p = m.padding;
                xMin = Mathf.Max(xMin, r.xMin + p.x);
                yMin = Mathf.Max(yMin, r.yMin + p.y);
                xMax = Mathf.Min(xMax, r.xMax - p.z);
                yMax = Mathf.Min(yMax, r.yMax - p.w);
            }
            validRect = xMax > xMin && yMax > yMin;
            return validRect ? new Rect(xMin, yMin, xMax - xMin, yMax - yMin) : new Rect();
        }
    }
}
