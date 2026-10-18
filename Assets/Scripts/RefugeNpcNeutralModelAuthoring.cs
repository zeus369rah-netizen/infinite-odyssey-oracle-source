#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using OdysseyOracle.CampaignNpcs;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace OdysseyOracle
{
    // # ARCHITECTURE SNAPSHOT
    // Cold Editor-only bridge to the frozen ORIGINAL Ilyra/Bram factory. No
    // recipe duplication, animation tick, scene placement, dialogue, collider,
    // voice source or gameplay component is installed on a neutral prefab.
    // Caller lends one palette; lease owns only temporary hierarchy and meshes.
    public static class RefugeNpcNeutralModelAuthoring
    {
        public sealed class NeutralLease:IDisposable
        {
            public GameObject Root {get;private set;}
            readonly List<UnityEngine.Object> transientMeshes;
            internal NeutralLease(GameObject root,List<UnityEngine.Object> meshes){Root=root;transientMeshes=meshes;}
            public void Dispose()
            {
                if(Root!=null)UnityEngine.Object.DestroyImmediate(Root);Root=null;
                foreach(var mesh in transientMeshes)if(mesh!=null)UnityEngine.Object.DestroyImmediate(mesh);
                transientMeshes.Clear();
            }
        }
        public static NeutralLease Create(RefugeNpcRole role,Material borrowedPalette,Scene authoringScene)
        {
            if(Application.isPlaying)throw new InvalidOperationException("Neutral NPC authoring requires stopped Play.");
            if(role!=RefugeNpcRole.Runekeeper&&role!=RefugeNpcRole.Gatekeeper)throw new ArgumentOutOfRangeException(nameof(role));
            if(borrowedPalette==null||!authoringScene.IsValid()||!authoringScene.isLoaded)
                throw new ArgumentException("Borrowed palette and isolated loaded authoring scene required.");
            var meshes=new List<UnityEngine.Object>();GameObject holder=null,root=null;
            try
            {
                holder=new GameObject("Original refuge NPC authoring holder");SceneManager.MoveGameObjectToScene(holder,authoringScene);
                root=CampaignNpcPresentation.CreateNeutralModel(role,holder.transform,borrowedPalette,meshes);
                // Four neutral sockets only. Gaze remains available through the
                // original Gaze head pivot; the old extra gaze alias becomes the
                // primary held-tool socket, attached to its original grip part.
                Unique(root,"Anchor_Mount").name="Feet";
                Unique(root,"Anchor_Interaction").name="Interact";
                Unique(root,"Anchor_Voice").name="Voice";
                Transform hand=Unique(root,"Anchor_Gaze");hand.name="HandTool";
                Transform tool=Unique(root,role==RefugeNpcRole.Runekeeper?"Split crown emerald staff":"Forked copper watch spear");
                hand.SetParent(tool,false);hand.localPosition=Vector3.zero;hand.localRotation=Quaternion.identity;hand.localScale=Vector3.one;
                root.transform.SetParent(null,false);SceneManager.MoveGameObjectToScene(root,authoringScene);
                UnityEngine.Object.DestroyImmediate(holder);holder=null;
                return new NeutralLease(root,meshes);
            }
            catch
            {
                if(root!=null)UnityEngine.Object.DestroyImmediate(root);
                if(holder!=null)UnityEngine.Object.DestroyImmediate(holder);
                foreach(var mesh in meshes)if(mesh!=null)UnityEngine.Object.DestroyImmediate(mesh);
                throw;
            }
        }
        static Transform Unique(GameObject root,string name)
        {
            Transform found=null;
            foreach(var item in root.GetComponentsInChildren<Transform>(true))if(item.name==name)
            {if(found!=null)throw new InvalidOperationException("Duplicate original NPC anchor/part: "+name);found=item;}
            if(found==null)throw new InvalidOperationException("Missing original NPC anchor/part: "+name);
            return found;
        }
    }
}
#endif
