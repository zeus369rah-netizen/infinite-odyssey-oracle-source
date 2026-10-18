using System;
using UnityEngine;

namespace OdysseyOracle
{
    /// <summary>Optional four-sprite presentation cache. Missing slices retain the existing drawn glyphs.</summary>
    public static class OracleElementEmblems
    {
        static readonly string[] Names = { "Fire", "Water", "Earth", "Air" };
        static readonly Sprite[] Sprites = new Sprite[4];
        static bool loaded;

        public static Sprite Get(int element)
        {
            if (element < 0 || element >= Sprites.Length) return null;
            if (!loaded)
            {
                loaded = true;
                var slices = Resources.LoadAll<Sprite>("Interface/ElementEmblems");
                for (var sprite = 0; sprite < slices.Length; sprite++)
                    for (var index = 0; index < Names.Length; index++)
                        if (slices[sprite] != null && string.Equals(slices[sprite].name, Names[index], StringComparison.Ordinal))
                            Sprites[index] = slices[sprite];
            }
            return Sprites[element];
        }
    }

    // # ARCHITECTURE SNAPSHOT: immutable optional Resource sprites, indexed by
    // existing player element0..3. HUD owns UI objects; this cache owns no textures,
    // simulation, input or native handles. One cold load; no per-frame lookup arrays.
}
