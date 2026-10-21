using UnityEngine;
namespace OdysseyOracle
{
    public sealed class LanternSwingCatalog : ScriptableObject
    {
        public Mesh SourceCombined, FixedSupport, HangingBody;
        public Vector3 HingeLocal;
        public int SplitVersion = 1;
        public bool Supports(Mesh original) => original != null && SplitVersion == 1 && SourceCombined == original && FixedSupport != null && HangingBody != null &&
            FixedSupport.subMeshCount == original.subMeshCount && HangingBody.subMeshCount == original.subMeshCount &&
            HingeLocal.sqrMagnitude > 0f && HangingBody.bounds.size.sqrMagnitude > 0f;
    }
}
// # ARCHITECTURE SNAPSHOT
// Coordinate Space: original normalized mesh space and existing exact Anchor_HangingLoop pivot.
// Array Formats: explicit saved references to original source and two separately baked render meshes; no native/runtime ownership.
// Dependencies/Pointers: root-owned cold baker and optional TutorialRoomV2Catalog reference; original prefab/mesh/materials remain preserved.
