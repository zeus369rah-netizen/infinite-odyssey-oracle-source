using UnityEngine;

namespace OdysseyOracle
{
    public sealed partial class OdysseyHud
    {
        // # ARCHITECTURE SNAPSHOT
        // Menu-only Canvas reference pixels. One original Unity-supplied Sprite;
        // presentation owns no gameplay, input, network or persistence state.
        private void BuildUnityCredit(RectTransform parent)
        {
            var credit = MenuLabel(parent, "Backend", "MADE WITH UNITY\n" + GraphicsLabel(), 11, Muted, 44);
            var sprite = Resources.Load<Sprite>("Interface/UnityLogo");
            if (sprite == null) return;
            var bounds = Rect(credit.transform, "UnityEngineLogo", new Vector2(.5f, .5f),
                new Vector2(.5f, .5f), new Vector2(-152, 0), new Vector2(36, 31));
            var logo = Paint(bounds, Color.white);
            logo.sprite = sprite;
            logo.preserveAspect = true;
            logo.raycastTarget = false;
        }
    }
}
