using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Gloomdelve
{
    /// <summary>
    /// Colours a UI Text from 'top' to 'bottom' down its height (the title's gold fading to amber).
    /// Add it next to a Text; Outline / Shadow effects after it keep their own colours.
    /// </summary>
    [RequireComponent(typeof(Text))]
    public class VerticalGradient : BaseMeshEffect
    {
        public Color top = new Color(1f, 0.95f, 0.72f);
        public Color bottom = new Color(0.86f, 0.5f, 0.2f);

        static readonly List<UIVertex> verts = new List<UIVertex>();

        public override void ModifyMesh(VertexHelper vh)
        {
            if (!IsActive() || vh.currentVertCount == 0) return;
            verts.Clear();
            vh.GetUIVertexStream(verts);
            float min = float.MaxValue, max = float.MinValue;
            foreach (var v in verts) { if (v.position.y < min) min = v.position.y; if (v.position.y > max) max = v.position.y; }
            float h = Mathf.Max(0.001f, max - min);
            for (int i = 0; i < verts.Count; i++)
            {
                var v = verts[i];
                Color c = Color.Lerp(bottom, top, (v.position.y - min) / h);
                c.a *= v.color.a / 255f;
                v.color = c;
                verts[i] = v;
            }
            vh.Clear();
            vh.AddUIVertexTriangleStream(verts);
        }
    }
}
