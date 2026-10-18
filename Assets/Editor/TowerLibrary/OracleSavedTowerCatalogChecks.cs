#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace OdysseyOracle.EditorAssets
{
    // # ARCHITECTURE SNAPSHOT
    // Root-run, stopped-Editor asset/presentation check. Temporary known six-group
    // roots are destroyed in finally; borrowed catalog assets, scene, wallet,
    // match state and profile are never changed. This is not a network test.
    public static class OracleSavedTowerCatalogChecks
    {
        [Serializable] public sealed class Receipt
        {
            public string status,checkedUtc;
            public int assertions,failures,templates,groupReferences;
            public bool unityExecuted,networkExecuted,sceneChanged,profileChanged;
            public string[] details;
        }
        public static Receipt Run(GameSession session,string receiptPath=null)
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode||EditorApplication.isCompiling||EditorApplication.isUpdating)
                throw new InvalidOperationException("Run this finite catalog check stopped and idle, after binding and saving the scene.");
            var result=new Receipt{checkedUtc=DateTime.UtcNow.ToString("o"),unityExecuted=true};
            var errors=new List<string>();
            Action<bool,string> check=(ok,label)=>{result.assertions++;if(!ok){result.failures++;errors.Add(label);}};
            SavedTowerModelLibrary library=null;GameObject temporary=null;
            try
            {
                OracleSavedTowerCatalogBake.ValidateBound(session);
                OracleSavedTowerCatalogBake.ValidateFrozenFiles(session.TowerModels);
                check(SavedTowerModelLibrary.TryCreate(session.TowerModels,out library,out string why),"validated borrowed provider: "+why);
                if(library==null)throw new InvalidOperationException("Validated provider was not created.");
                for(int type=0;type<4;type++)for(int tier=1;tier<=3;tier++)
                {
                    temporary=new GameObject("Finite saved tower check"){hideFlags=HideFlags.HideAndDontSave};
                    var presentation=temporary.AddComponent<SavedTowerModelPresentation>();
                    presentation.Initialize(library,1,type,tier,10);
                    check(presentation.AcceptedTier==tier,"initial accepted tier");
                    check(presentation.OnAcceptedShot(1,new Vector3(8,1,0),MatchTowerForgeRules.Interval(tier)),"accepted shot visual");
                    check(!presentation.OnAcceptedShot(1,new Vector3(8,1,0),MatchTowerForgeRules.Interval(tier)),"duplicate visual serial rejected");
                    presentation.Tick(.12f,true,true);presentation.Tick(.12f,false,true);
                    MeshFilter[] groups=temporary.GetComponentsInChildren<MeshFilter>(true);
                    check(groups.Length==6,"six known groups only");
                    for(int group=0;group<6;group++)
                    {
                        string name="Tower "+(TowerModelGroup)group;MeshFilter found=null;
                        for(int i=0;i<groups.Length;i++)if(groups[i].name==name)found=groups[i];
                        Mesh expected=session.TowerModels.GetMesh(type,tier,group);
                        check(found!=null&&found.sharedMesh==expected,"exact borrowed group "+name);
                        if(found!=null)check(found.GetComponent<MeshRenderer>().sharedMaterial==session.TowerModels.GetMaterial(type),"exact borrowed palette");
                        if(expected!=null)result.groupReferences++;
                    }
                    check(temporary.GetComponentsInChildren<Light>(true).Length==0,"no model lights");
                    check(temporary.GetComponentsInChildren<Collider>(true).Length==0,"no hard model body colliders");
                    check(temporary.GetComponentsInChildren<Animator>(true).Length==0,"no arbitrary prefab execution");
                    check(!presentation.ApplyAcceptedTier(tier,9),"stale accepted revision rejected");
                    presentation.OnAcceptedDestroyed();
                    foreach(var renderer in temporary.GetComponentsInChildren<MeshRenderer>(true))check(!renderer.enabled,"destroy hides all groups");
                    UnityEngine.Object.DestroyImmediate(temporary);temporary=null;result.templates++;
                }
                library.Dispose();library=null;
                check(session.TowerModels.TryValidate(out _),"borrower disposal retains every saved asset");
            }
            catch(Exception error){check(false,error.GetType().Name+": "+error.Message);}
            finally{if(temporary!=null)UnityEngine.Object.DestroyImmediate(temporary);library?.Dispose();}
            result.details=errors.ToArray();result.status=result.failures==0?"PASSED_NATIVE_SAVED_TOWER_ASSETS":"FAILED_NATIVE_SAVED_TOWER_ASSETS";
            if(!string.IsNullOrEmpty(receiptPath))File.WriteAllText(receiptPath,JsonUtility.ToJson(result,true));
            return result;
        }
    }
}
#endif
