using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace OdysseyOracle
{
    // # ARCHITECTURE SNAPSHOT
    // Explicit tier1 placement preview. It has no accepted state, recoil, upgrade or audio hooks.
    // Six cached groups swap immutable shared meshes on type selection, never during frame motion.
    public sealed class TowerModelPreview:MonoBehaviour
    {
        readonly MeshFilter[] filters=new MeshFilter[6];
        readonly MeshRenderer[] renderers=new MeshRenderer[6];
        TowerModelLibrary library;
        int selected=-1;
        public void Initialize(TowerModelLibrary sharedLibrary,Material transparentMaterial)
        {
            if(library!=null||sharedLibrary==null||transparentMaterial==null)throw new ArgumentException();
            library=sharedLibrary;
            for(int i=0;i<6;i++)
            {
                var child=new GameObject("Tier1 preview "+(TowerModelGroup)i);child.layer=2;child.transform.SetParent(transform,false);
                filters[i]=child.AddComponent<MeshFilter>();renderers[i]=child.AddComponent<MeshRenderer>();
                renderers[i].sharedMaterial=transparentMaterial;renderers[i].shadowCastingMode=ShadowCastingMode.Off;
                renderers[i].receiveShadows=false;renderers[i].lightProbeUsage=LightProbeUsage.Off;renderers[i].reflectionProbeUsage=ReflectionProbeUsage.Off;
            }
            SetSelectedType(0);
        }
        public bool SetSelectedType(int type)
        {
            if(library==null||type<0||type>=4)return false;
            if(type==selected)return true;selected=type;
            var template=library.Get(type,1);
            for(int i=0;i<6;i++)
            {
                filters[i].sharedMesh=template.Groups[i];renderers[i].enabled=template.Groups[i]!=null;
                filters[i].transform.localPosition=i==0?Vector3.zero:TowerModelLibrary.Vector(template.Recipe.HeadPivot);
            }
            return true;
        }
    }
}
