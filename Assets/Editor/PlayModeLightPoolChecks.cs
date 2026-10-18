using System;
using System.Reflection;
using UnityEngine;
using OdysseyOracle;

// ROOT MAY RUN after stopping Play/importing the candidate. This task never invokes it.
// Uses isolated transient objects, actual NativeArrays and the actual pool code;
// does not trigger compilation, change preferences or edit project assets.
public static class PlayModeLightPoolChecks
{
    public static string Run()
    {
        int checks=0;
        Action<bool,string> require=(ok,label)=>{checks++;if(!ok)throw new InvalidOperationException(label);};
        GameObject spellRoot=null,fireRoot=null;
        TutorialFireLightPool fire=null;
        try
        {
            spellRoot=new GameObject("Transient spell pool lifecycle checks"){hideFlags=HideFlags.HideAndDontSave};
            var spell=spellRoot.AddComponent<SpellImpactLighting>();
            spell.RestoreAfterReload();
            Light[] spellIds=Ids(spellRoot);
            require(spellIds.Length==2,"Cold spell pool has exactly two actual Lights");
            spell.Pulse(Vector3.zero,Color.cyan,.5f);
            require(Enabled(spellRoot)==1,"Real pulse enables one actual light");
            spell.PrepareForReload();spell.PrepareForReload();
            Invoke(spell,"Update");
            require(Enabled(spellRoot)==0,"Repeated reload preparation is safe and missing runtime arrays do not throw");
            spell.RestoreAfterReload();require(Same(spellIds,Ids(spellRoot)),"Spell restore retains existing Light instance IDs");
            Field(typeof(SpellImpactLighting),"lights").SetValue(spell,null);
            spell.PrepareForReload();spell.RestoreAfterReload();
            require(Same(spellIds,Ids(spellRoot)),"Legacy lost spell references adopt generated children rather than duplicate Lights");
            spell.Pulse(Vector3.one,Color.yellow,1f);spell.ResetLights();
            require(Enabled(spellRoot)==0,"Rehydrated spell pool pulses and resets safely");

            fireRoot=new GameObject("Transient fire pool lifecycle checks"){hideFlags=HideFlags.HideAndDontSave};
            fire=fireRoot.AddComponent<TutorialFireLightPool>();
            fire.Initialize(fireRoot.transform,null,1700089000);
            Vector3 first=new Vector3(-5f,2f,-1f),second=new Vector3(5f,1f,2f);
            require(fire.RegisterEmitter(first,TutorialFireLightPool.Fixture.Torch,1)&&
                fire.RegisterEmitter(second,TutorialFireLightPool.Fixture.CandleCluster,2),"Actual bounded registry accepts two fixtures");
            Light[] fireIds=Ids(fireRoot);
            require(fireIds.Length==4&&fire.NativeStorageCreated,"Cold fire pool owns four Lights and live native storage");
            for(int cycle=0;cycle<20;cycle++)
            {
                fire.PrepareForReload();fire.PrepareForReload();
                require(!fire.NativeStorageCreated&&fire.RegisteredEmitterCount==2&&Enabled(fireRoot)==0,
                    "Reload preparation disposes native arrays but retains serialized registry and disabled Lights");
                Field(typeof(TutorialFireLightPool),"initialized").SetValue(fire,true);
                Invoke(fire,"Update");
                require(!fire.NativeStorageCreated,"Restored initialized flag cannot cause disposed native access");
                fire.RestoreAfterReload();
                require(fire.NativeStorageCreated&&Same(fireIds,Ids(fireRoot)),"Cold native recreation reuses all four exact Lights");
                require(fire.TryGetEmitterPosition(0,out Vector3 a)&&a==first&&fire.TryGetEmitterPosition(1,out Vector3 b)&&b==second,
                    "Actual rehydrated native emitter positions match retained managed registry");
            }
            if(Application.isPlaying)
            {
                fireRoot.SetActive(false);
                require(!fire.NativeStorageCreated&&fire.RegisteredEmitterCount==2,"Real OnDisable disposes native ownership");
                fireRoot.SetActive(true);
                require(fire.NativeStorageCreated&&Same(fireIds,Ids(fireRoot)),"Real OnEnable restores cold state without Light growth");
            }
            fire.PrepareForReload();Field(typeof(TutorialFireLightPool),"lights").SetValue(fire,null);fire.RestoreAfterReload();
            require(Same(fireIds,Ids(fireRoot)),"Legacy fire reference loss adopts four generated children");
            var extra=new GameObject("Reused tutorial fire spill 3");extra.transform.SetParent(fireRoot.transform,false);
            Light duplicate=extra.AddComponent<Light>();duplicate.enabled=true;
            fire.PrepareForReload();fire.RestoreAfterReload();
            require(duplicate==null||!duplicate.enabled,"Duplicate legacy pool light is disabled before deferred destruction");
            require(fire.ActiveLightCount==0,"Recovery never enables extra spill lights");
            fire.SetLightBudget(99);require(fire.LightBudget==4,"Nearest fixture budget cannot exceed four");
            fire.SetLightBudget(-1);require(fire.LightBudget==0&&fire.ActiveLightCount==0,"Zero budget disables all spill Lights");
            fire.ClearRegistry();require(fire.RegisteredEmitterCount==0,"Explicit geometry rebuild clears retained registry");
            fire.PrepareForReload();require(!fire.NativeStorageCreated,"Final native disposal precedes transient fixture destruction");
            return "PASS: "+checks+" actual Light/native-storage lifecycle checks; reload event itself is emulated, not a live recompilation. Real enable/disable callbacks: "+(Application.isPlaying?"checked":"not run outside Play mode")+".";
        }
        finally
        {
            if(fire!=null)fire.PrepareForReload();
            if(spellRoot!=null)UnityEngine.Object.DestroyImmediate(spellRoot);
            if(fireRoot!=null)UnityEngine.Object.DestroyImmediate(fireRoot);
        }
    }
    private static FieldInfo Field(Type type,string name)=>type.GetField(name,BindingFlags.Instance|BindingFlags.NonPublic)??throw new MissingFieldException(type.Name,name);
    private static void Invoke(object target,string method)=>target.GetType().GetMethod(method,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(target,null);
    private static int Enabled(GameObject root){int count=0;foreach(var light in root.GetComponentsInChildren<Light>(true))if(light.enabled)count++;return count;}
    private static Light[] Ids(GameObject root){var lights=root.GetComponentsInChildren<Light>(true);Array.Sort(lights,(a,b)=>string.CompareOrdinal(a.name,b.name));return lights;}
    private static bool Same(Light[] a,Light[] b){if(a.Length!=b.Length)return false;for(int i=0;i<a.Length;i++)if(a[i]!=b[i])return false;return true;}
}
