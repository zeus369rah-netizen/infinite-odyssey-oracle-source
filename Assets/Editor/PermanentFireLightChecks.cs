#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Assertions;

namespace OdysseyOracle.Editor
{
    // # ARCHITECTURE SNAPSHOT
    // Native public-API fixture, root invoked only. Capture before and after camera motion/time/pause;
    // Compare requires the same23 enabled physical fixtures, positions and steady intensities.
    // Never moves the camera, advances gameplay, changes focus or invokes a private Update.
    public static class PermanentFireLightChecks
    {
        [Serializable] public sealed class Snapshot {
            public string status, exception; public int passed,failed,emitters,permanentLights,enabledPermanentLights,enabledDetailLights,budget;
            public Entry[] entries; public string contribution="Enabled Light state is not a measured rendered surface contribution; use root's on/off render diagnostic for brightness.";
        }
        [Serializable] public sealed class Entry { public int index;public string instanceId,name;public Vector3 localPosition;public float intensity,range;public Color color;public bool enabled; }
        public static string Capture() => JsonUtility.ToJson(Read(),true);
        public static string Compare(string previousSnapshotJson)
        {
            Snapshot current=Read();
            try {
                Snapshot previous=JsonUtility.FromJson<Snapshot>(previousSnapshotJson);
                Demand(previous!=null && previous.status=="passed" && current.status=="passed","Both native snapshots must be valid.");
                Demand(previous.entries.Length==current.entries.Length,"Registry/light count changed.");current.passed++;
                for(int i=0;i<previous.entries.Length;i++) {
                    Entry a=previous.entries[i],b=current.entries[i];
                    Demand(a.index==b.index && a.instanceId==b.instanceId && a.name==b.name,"Fixture identity changed at "+i);
                    Demand((a.localPosition-b.localPosition).sqrMagnitude<.00000001f,"Fixture moved with camera at "+i);
                    Demand(a.enabled && b.enabled && Mathf.Abs(a.intensity-b.intensity)<.00001f && Mathf.Abs(a.range-b.range)<.00001f && a.color==b.color,"Permanent illumination changed at "+i);
                    current.passed+=3;
                }
            }catch(Exception e){current.status="failed";current.failed++;current.exception=e.ToString();}
            return JsonUtility.ToJson(current,true);
        }
        public static string CheckBudgetIndependence()
        {
            TutorialFireLightPool pool=Pool();int before=pool.LightBudget;Snapshot snapshot=Read();
            try {
                Demand(snapshot.status=="passed","Valid initial illumination required.");
                foreach(int budget in new[]{0,1,2,4}) {
                    pool.SetLightBudget(budget);
                    Snapshot current=Read();Demand(current.status=="passed","Permanent lights must remain enabled at budget "+budget);snapshot.passed++;
                    for(int i=0;i<snapshot.entries.Length;i++) {
                        Entry a=snapshot.entries[i],b=current.entries[i];
                        Demand(a.instanceId==b.instanceId && (a.localPosition-b.localPosition).sqrMagnitude<.00000001f && a.intensity==b.intensity && b.enabled,"SetLightBudget changed a permanent fixture.");snapshot.passed++;
                    }
                }
            }catch(Exception e){snapshot.status="failed";snapshot.failed++;snapshot.exception=e.ToString();}
            finally{pool.SetLightBudget(before);}
            return JsonUtility.ToJson(snapshot,true);
        }
        static Snapshot Read()
        {
            var result=new Snapshot{status="failed"};
            try {
                TutorialFireLightPool pool=Pool();
                result.emitters=pool.RegisteredEmitterCount;result.permanentLights=pool.PermanentLightCount;
                result.enabledPermanentLights=pool.ActivePermanentLightCount;result.enabledDetailLights=pool.ActiveLightCount;result.budget=pool.LightBudget;
                Demand(result.emitters==23 && result.permanentLights==23 && result.enabledPermanentLights==23,"Exactly23 authored fixtures must be permanently enabled.");result.passed++;
                Demand(result.enabledDetailLights==0,"Old camera-following point lights must be retired, preventing double illumination.");result.passed++;
                var entries=new List<Entry>(23);var ids=new HashSet<string>();
                for(int i=0;i<result.emitters;i++) {
                    Demand(pool.TryGetEmitterPosition(i,out Vector3 position) && pool.TryGetPermanentLight(i,out Light light),"Missing fixture mapping at "+i);
                    pool.TryGetPermanentLight(i,out Light source);
                    Demand(ids.Add(source.GetEntityId().ToString()),"Duplicate Light identity at "+i);
                    Demand(source.type==LightType.Point && source.shadows==LightShadows.None && source.enabled && source.gameObject.activeInHierarchy,"Every fixture must be an enabled shadowless point at "+i);
                    Demand(source.transform.parent==pool.transform && (source.transform.localPosition-position).sqrMagnitude<.00000001f,"Fixture must remain at registered local position at "+i);
                    Demand(source.intensity>0 && source.range>0 && source.renderMode==LightRenderMode.Auto,"A positive automatic point contribution is required at "+i);
                    result.passed+=5;
                    entries.Add(new Entry{index=i,instanceId=source.GetEntityId().ToString(),name=source.name,localPosition=source.transform.localPosition,intensity=source.intensity,range=source.range,color=source.color,enabled=source.enabled && source.gameObject.activeInHierarchy});
                }
                result.entries=entries.ToArray();result.status="passed";
            }catch(Exception e){result.failed++;result.exception=e.ToString();}
            return result;
        }
        static TutorialFireLightPool Pool() {
            GameSession session=GameSession.Instance;
            Demand(session!=null && session.World!=null && !session.World.IsCoopArena,"Initialized tutorial required.");
            TutorialFireLightPool pool=session.World.GetComponentInChildren<TutorialFireLightPool>(false);
            Demand(pool!=null && pool.isActiveAndEnabled,"An active tutorial fixture registry is required.");return pool;
        }
        static void Demand(bool value,string message){Assert.IsTrue(value,message);if(!value)throw new InvalidOperationException(message);}
    }
}
#endif
