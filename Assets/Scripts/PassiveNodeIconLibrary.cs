using UnityEngine;

namespace OdysseyOracle
{
    // # ARCHITECTURE SNAPSHOT
    // Explicit asset reference only. Stable twelve-bit order Fire/Water/Earth/Air,
    // three nodes per branch. No loading, unlock inference, tint or gameplay mutation.
    public sealed class PassiveNodeIconLibrary : ScriptableObject
    {
        public string RecipeFingerprint;
        public Sprite[] Icons=new Sprite[12];
        public Sprite Get(int element,int node)=>element>=0&&element<4&&node>=0&&node<3&&Icons!=null&&Icons.Length==12?Icons[element*3+node]:null;
    }
}
