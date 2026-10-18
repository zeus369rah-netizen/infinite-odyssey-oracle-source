using System;
using OdysseyOracle.Art;
using UnityEngine;

namespace OdysseyOracle
{
    // # ARCHITECTURE SNAPSHOT
    // Optional cold adapter only. It replaces boxes inside EXISTING PartSpec
    // meshes, before caching/footprint construction, and retains every existing
    // label/anchor/channel/origin. No original source array is mutated. The V1
    // exporter/source fingerprint remains frozen and is not edited by this stage.
    internal static class MonsterDetailAdapterV2
    {
        internal static MonsterVoxelCosmetics.PartSpec[] Apply(MonsterVoxelCosmetics.Style style,MonsterVoxelCosmetics.PartSpec[] source)
        {
            var recipe=MonsterDetailRecipesV2.Get((MonsterDetailStyleV2)(int)style);
            if(source==null||source.Length!=recipe.BaseParts)throw new InvalidOperationException("Monster source part count changed.");
            int baseBoxes=0;for(int i=0;i<source.Length;i++)baseBoxes+=source[i].Boxes.Length;
            if(baseBoxes*12!=recipe.BaseTriangles)throw new InvalidOperationException("Monster source triangle count changed.");
            var output=(MonsterVoxelCosmetics.PartSpec[])source.Clone();
            var scratch=new MonsterDetailBoxV2[MonsterDetailRecipesV2.MaximumReplacementBoxes];
            int applied=0;
            for(int partIndex=0;partIndex<source.Length;partIndex++)
            {
                var part=source[partIndex];int added=0;
                for(int opIndex=0;opIndex<recipe.OperationCount;opIndex++)
                {
                    var op=recipe.GetOperation(opIndex);if(!String.Equals(op.Part,part.Label,StringComparison.Ordinal))continue;
                    if(part.Label=="Left leg"||part.Label=="Right leg"||part.Label.StartsWith("Spider leg ",StringComparison.Ordinal)||
                        op.BoxIndex<0||op.BoxIndex>=part.Boxes.Length)throw new InvalidOperationException("Foot/leg ownership or source box changed.");
                    added+=op.ReplacementBoxes-1;
                }
                if(added==0)continue;
                var boxes=new MonsterVoxelCosmetics.BoxSpec[part.Boxes.Length+added];int write=0;
                for(int boxIndex=0;boxIndex<part.Boxes.Length;boxIndex++)
                {
                    int match=-1;
                    for(int opIndex=0;opIndex<recipe.OperationCount;opIndex++)
                    {
                        var op=recipe.GetOperation(opIndex);
                        if(op.BoxIndex!=boxIndex||!String.Equals(op.Part,part.Label,StringComparison.Ordinal))continue;
                        if(match!=-1)throw new InvalidOperationException("Duplicate detail operation.");match=opIndex;
                    }
                    var original=part.Boxes[boxIndex];
                    if(match<0){boxes[write++]=original;continue;}
                    // Current recipes target axis-aligned boxes only. Retaining
                    // all six source extrema therefore retains exact part bounds,
                    // including cold gait/arm-sweep/trigger/navigation caches.
                    if(original.Euler!=Vector3.zero)throw new InvalidOperationException("Detail target rotation changed; review bounds first.");
                    int count=MonsterDetailRecipesV2.WriteNormalizedBoxes(recipe.GetOperation(match),scratch);applied++;
                    for(int i=0;i<count;i++)
                    {
                        var b=scratch[i];
                        boxes[write++]=new MonsterVoxelCosmetics.BoxSpec(original.Center+Vector3.Scale(new Vector3(b.X,b.Y,b.Z),original.Size),
                            Vector3.Scale(new Vector3(b.Width,b.Height,b.Depth),original.Size),original.Euler);
                    }
                }
                if(write!=boxes.Length)throw new InvalidOperationException("Detail output size mismatch.");
                output[partIndex]=new MonsterVoxelCosmetics.PartSpec(part.Label,part.Anchor,part.Channel,part.Origin,boxes);
            }
            if(applied!=recipe.OperationCount)throw new InvalidOperationException("Unmatched monster detail operation.");
            return output;
        }
    }
}
