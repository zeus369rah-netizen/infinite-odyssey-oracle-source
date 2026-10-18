using System;
using UnityEngine;
#if UNITY_EDITOR
using ReloadCallback = UnityEditor.AssemblyReloadEvents.AssemblyReloadCallback;
#else
using ReloadCallback = System.Action;
#endif

namespace OdysseyOracle
{
    /// <summary>Two reusable lights; serialized ownership survives a script reload.</summary>
    [DisallowMultipleComponent]
    public sealed class SpellImpactLighting : MonoBehaviour
    {
        private const int Capacity=2;
        [SerializeField] private Light[] lights=new Light[Capacity];
        [NonSerialized] private float[] remaining,energy;
        [NonSerialized] private int next;
        [NonSerialized] private ReloadCallback reloadAction;
        [NonSerialized] private bool reloadSubscribed;

        private void Awake()=>RestoreAfterReload();
        private void OnEnable()=>RestoreAfterReload();
        public void RestoreAfterReload()
        {
            bool cold=remaining==null||energy==null||remaining.Length!=Capacity||energy.Length!=Capacity||
                lights==null||lights.Length!=Capacity;
            if(!cold)for(int i=0;i<Capacity;i++)if(lights[i]==null)cold=true;
            if(cold)
            {
                remaining=new float[Capacity];energy=new float[Capacity];next=0;
                LightPoolLifecycle.Ensure(transform,ref lights,Capacity,"Spell impact pool ");
                for(int i=0;i<Capacity;i++)lights[i].range=3.2f;
            }
            if(reloadAction==null)reloadAction=PrepareForReload;
            if(!reloadSubscribed){LightPoolLifecycle.SubscribeBeforeReload(reloadAction);reloadSubscribed=true;}
        }
        public void Pulse(Vector3 position,Color color,float size)
        {
            if(!Finite(position)||float.IsNaN(size)||float.IsInfinity(size))return;
            RestoreAfterReload();
            int index=next;next=(next+1)%Capacity;
            Light light=lights[index];if(light==null)return;
            light.transform.position=position+Vector3.up*.12f;light.color=color;
            energy[index]=Mathf.Clamp(1.8f+size,1.8f,3.5f);light.intensity=energy[index];
            remaining[index]=.22f;light.enabled=true;
        }
        private void Update()
        {
            bool playing=GameSession.Instance!=null&&GameSession.Instance.IsPlaying;
            if(lights==null||remaining==null||energy==null)return;
            int safeCount=Math.Min(Capacity,Math.Min(lights.Length,Math.Min(remaining.Length,energy.Length)));
            for(int i=0;i<safeCount;i++)
            {
                Light light=lights[i];if(light==null)continue;
                light.enabled=playing&&remaining[i]>0f;
                if(!playing||remaining[i]<=0f)continue;
                remaining[i]=Mathf.Max(0f,remaining[i]-Time.deltaTime);
                light.intensity=energy[i]*remaining[i]/.22f;
            }
        }
        public void ResetLights()
        {
            if(remaining!=null)Array.Clear(remaining,0,remaining.Length);
            if(energy!=null)Array.Clear(energy,0,energy.Length);
            LightPoolLifecycle.Disable(lights);next=0;
        }
        public void PrepareForReload()
        {
            ResetLights();
            if(reloadAction!=null)LightPoolLifecycle.UnsubscribeBeforeReload(reloadAction);
            reloadSubscribed=false;
            remaining=energy=null;
        }
        private void OnDisable()=>PrepareForReload();
        private void OnDestroy(){PrepareForReload();LightPoolLifecycle.Destroy(lights);}
        private static bool Finite(Vector3 value)=>!float.IsNaN(value.x)&&!float.IsInfinity(value.x)&&
            !float.IsNaN(value.y)&&!float.IsInfinity(value.y)&&!float.IsNaN(value.z)&&!float.IsInfinity(value.z);
    }
}
