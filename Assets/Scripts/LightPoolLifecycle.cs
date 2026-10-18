using System;
using UnityEngine;
#if UNITY_EDITOR
using ReloadCallback = UnityEditor.AssemblyReloadEvents.AssemblyReloadCallback;
#else
using ReloadCallback = System.Action;
#endif

namespace OdysseyOracle
{
    // Cold lifecycle operations only. Serialized references are authoritative; exact
    // generated direct-child names support migration from the previous readonly arrays.
    // No scene-wide searches, authored-light discovery, jobs or per-frame allocation.
    public static class LightPoolLifecycle
    {
        public static void Ensure(Transform root,ref Light[] lights,int capacity,string prefix)
        {
            if(root==null)return;
            if(lights==null||lights.Length!=capacity)
            {
                Light[] previous=lights;lights=new Light[capacity];
                if(previous!=null)Array.Copy(previous,lights,Math.Min(previous.Length,capacity));
            }
            for(int slot=0;slot<capacity;slot++)
            {
                string name=prefix+slot;
                Light light=lights[slot];
                for(int earlier=0;earlier<slot;earlier++)if(light==lights[earlier])light=null;
                if(light==null)
                {
                    Transform child=root.Find(name);
                    if(child!=null)light=child.GetComponent<Light>();
                    for(int earlier=0;earlier<slot;earlier++)if(light==lights[earlier])light=null;
                }
                if(light==null)
                {
                    var source=new GameObject(name);source.transform.SetParent(root,false);light=source.AddComponent<Light>();
                }
                light.gameObject.name=name;light.transform.SetParent(root,false);
                light.type=LightType.Point;light.shadows=LightShadows.None;
                light.enabled=false;lights[slot]=light;
            }
            // Retire duplicate legacy generated lights from an earlier failed reload.
            // Unrelated direct children and arbitrary scene lights are never touched.
            for(int childIndex=root.childCount-1;childIndex>=0;childIndex--)
            {
                Transform child=root.GetChild(childIndex);bool generated=false,kept=false;
                for(int slot=0;slot<capacity;slot++)
                {generated|=child.name==prefix+slot;kept|=lights[slot]!=null&&child==lights[slot].transform;}
                if(!generated||kept)continue;
                Light duplicate=child.GetComponent<Light>();if(duplicate==null)continue;
                duplicate.enabled=false;DestroyOwned(child.gameObject);
            }
        }
        public static void Disable(Light[] lights)
        {if(lights!=null)for(int i=0;i<lights.Length;i++)if(lights[i]!=null)lights[i].enabled=false;}
        public static void Destroy(Light[] lights)
        {
            if(lights==null)return;
            for(int i=0;i<lights.Length;i++)if(lights[i]!=null){DestroyOwned(lights[i].gameObject);lights[i]=null;}
        }
        public static void SubscribeBeforeReload(ReloadCallback callback)
        {
#if UNITY_EDITOR
            UnityEditor.AssemblyReloadEvents.beforeAssemblyReload-=callback;
            UnityEditor.AssemblyReloadEvents.beforeAssemblyReload+=callback;
#endif
        }
        public static void UnsubscribeBeforeReload(ReloadCallback callback)
        {
#if UNITY_EDITOR
            UnityEditor.AssemblyReloadEvents.beforeAssemblyReload-=callback;
#endif
        }
        private static void DestroyOwned(UnityEngine.Object value)
        {if(value==null)return;if(Application.isPlaying)UnityEngine.Object.Destroy(value);else UnityEngine.Object.DestroyImmediate(value);}
    }
}
