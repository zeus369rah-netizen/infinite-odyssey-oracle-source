using System;
using System.Collections.Generic;
using UnityEngine;

namespace OdysseyOracle
{
    internal enum MonsterArtVersion { OriginalV1, DetailV2 }

    // # ARCHITECTURE SNAPSHOT
    // Cold presentation seam only. V1 remains the owner of pivots, rigid gait,
    // flash admission and base mesh lifetime. Optional V2 owns only mesh overrides.
    // No simulation, identity, palette, footprint, collider or wire state lives here.
    internal static class MonsterVisualFactory
    {
        internal static MonsterVisualLease Create(MonsterVoxelCosmetics.Style style,
            Transform body,Transform leftArm,Transform rightArm,Material stone,
            Material joints,Material rune,List<Renderer> flashes,
            MonsterArtVersion version=MonsterArtVersion.DetailV2)
        {
            if(version!=MonsterArtVersion.OriginalV1&&version!=MonsterArtVersion.DetailV2)
                throw new ArgumentOutOfRangeException(nameof(version));
            MonsterVoxelCosmetics.Lease original=null;IDisposable detail=null;
            try
            {
                original=MonsterVoxelCosmetics.Create(style,body,leftArm,rightArm,stone,joints,rune,flashes);
                if(version==MonsterArtVersion.DetailV2)
                    detail=MonsterVoxelCosmeticsV2.Attach(style,body,leftArm,rightArm);
                return new MonsterVisualLease(original,detail);
            }
            catch
            {
                // Remove renderers before releasing their override meshes.
                try{original?.Dispose();}finally{detail?.Dispose();}
                throw;
            }
        }
    }

    internal sealed class MonsterVisualLease:IDisposable
    {
        internal MonsterVoxelCosmetics.Lease OriginalForMotion {get;private set;}
        IDisposable detail;
        internal MonsterVisualLease(MonsterVoxelCosmetics.Lease original,IDisposable detail)
        {OriginalForMotion=original??throw new ArgumentNullException(nameof(original));this.detail=detail;}
        internal Transform LeftLeg=>OriginalForMotion!=null?OriginalForMotion.LeftLeg:null;
        internal Transform RightLeg=>OriginalForMotion!=null?OriginalForMotion.RightLeg:null;
        internal Transform[] SpiderLegs=>OriginalForMotion!=null?OriginalForMotion.SpiderLegs:null;
        internal void PoseSpider(float gait,float weight,bool flying)
        {OriginalForMotion?.PoseSpider(gait,weight,flying);}
        public void Dispose()
        {
            var original=OriginalForMotion;var decorations=detail;
            OriginalForMotion=null;detail=null;
            try{original?.Dispose();}finally{decorations?.Dispose();}
        }
    }
}
