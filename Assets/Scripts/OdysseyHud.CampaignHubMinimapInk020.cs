using UnityEngine;
using UnityEngine.UI;

namespace OdysseyOracle
{
    public sealed partial class OdysseyHud
    {
        private RectTransform campaignHubPlayerGlyphRect020;

        private RectTransform CreateCampaignHubPlayerGlyph020(RectTransform parent)
        {
            if (parent == null) return null;
            var node = new GameObject("CampaignHubPlayerDirection020", typeof(RectTransform), typeof(CanvasRenderer));
            var rect = node.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = new Vector2(.5f, .5f);
            rect.anchorMax = new Vector2(.5f, .5f);
            rect.pivot = new Vector2(.5f, .5f);
            rect.sizeDelta = new Vector2(18f, 18f);
            rect.anchoredPosition = Vector2.zero;
            rect.localRotation = Quaternion.identity;
            rect.SetAsLastSibling();

            var glyph = node.AddComponent<CampaignHubPlayerGlyph020>();
            glyph.raycastTarget = false;
            glyph.color = Color.white;
            return rect;
        }

        private bool UpdateCampaignHubPlayerGlyph020(Vector3 player, float yaw, Rect bounds)
        {
            if (campaignHubPlayerGlyphRect020 == null || campaignHubMapSurface020 == null)
                return false;

            Rect mapRect = campaignHubMapSurface020.rect;
            if (!CampaignHubInkMapGraphic020.TryProjectPoint020(mapRect, bounds,
                    new Vector2(player.x, player.z), out Vector2 projectedLocal))
                return false;

            Vector2 projectedAnchored = CampaignHubPlayerGlyph020.ToAnchoredPosition020(mapRect,
                campaignHubPlayerGlyphRect020.anchorMin, campaignHubPlayerGlyphRect020.anchorMax,
                campaignHubPlayerGlyphRect020.pivot, projectedLocal);
            if (float.IsNaN(projectedAnchored.x) || float.IsInfinity(projectedAnchored.x)
                || float.IsNaN(projectedAnchored.y) || float.IsInfinity(projectedAnchored.y))
                return false;

            if ((campaignHubPlayerGlyphRect020.anchoredPosition - projectedAnchored).sqrMagnitude > .0001f)
                campaignHubPlayerGlyphRect020.anchoredPosition = projectedAnchored;

            float desiredZ = -yaw;
            if (Mathf.Abs(Mathf.DeltaAngle(campaignHubPlayerGlyphRect020.localEulerAngles.z, desiredZ)) > .05f)
                campaignHubPlayerGlyphRect020.localRotation = Quaternion.Euler(0f, 0f, desiredZ);
            return true;
        }
    }
}

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Existing local minimap UI rectangle; player world XZ maps into anchored local XY and yaw maps to negative local Z rotation.
// Array Formats: One cold-created 18x18 UI glyph mesh; per-HUD refresh changes only cached RectTransform anchored position and rotation.
// Dependencies/Pointers: Called only after the existing positive generation/root/player/bounds admission; glyph parent lifetime is the retained campaign map surface.
