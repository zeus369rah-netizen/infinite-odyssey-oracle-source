using System;
using OdysseyOracle.CampaignNpcs;

namespace OdysseyOracle
{
    public static class NibwickRuntimeRecipe020
    {
        public const int PartCount=17;
        public static NpcRecipe Build()
        {
            var source=NibwickRecipe020.Build();
            var r=new NpcRecipe{Role=source.Role,Name=source.Name,RoleLabel=source.RoleLabel,Identity=source.Identity,
                Torso=source.Torso,Head=source.Head,Mouth=source.Mouth,Eyelids=source.Eyelids,LeftArm=source.LeftArm,RightArm=source.RightArm};
            for(int p=0;p<13;p++)
            {
                if(p==9||p==10)
                {
                    var leg=source.Parts[p];if(leg.Positions.Count!=192||leg.Indices.Count!=276)throw new InvalidOperationException("Frozen original leg solid boundaries changed.");
                    var thigh=Copy(leg,0,24,0,36,default,leg.Pivot,leg.Parent,p==9?"Left rigid thigh":"Right rigid thigh");
                    var align=NibwickRigidLeg020.Align(new NpcVector(0,-.14f,.025f),new NpcVector(0,-1,0));
                    for(int v=0;v<thigh.Positions.Count;v++){thigh.Positions[v]=NibwickRigidLeg020.Rotate(align,thigh.Positions[v]);thigh.Normals[v]=NibwickRigidLeg020.Rotate(align,thigh.Normals[v]);}
                    r.Parts.Add(thigh);
                }
                else r.Parts.Add(source.Parts[p]);
            }
            var knee=new NpcVector(0,-.14f,.025f);var ankle=new NpcVector(0,-.235f,.025f);
            r.Parts.Add(Copy(source.Parts[9],24,24,36,36,knee,source.Parts[9].Pivot+knee,0,"Left rigid calf"));
            r.Parts.Add(Copy(source.Parts[10],24,24,36,36,knee,source.Parts[10].Pivot+knee,0,"Right rigid calf"));
            r.Parts.Add(Copy(source.Parts[9],48,144,72,204,ankle,source.Parts[9].Pivot+ankle,0,"Left planted boot"));
            r.Parts.Add(Copy(source.Parts[10],48,144,72,204,ankle,source.Parts[10].Pivot+ankle,0,"Right planted boot"));
            if(r.Parts.Count!=PartCount||r.Triangles!=source.Triangles)throw new InvalidOperationException("Nibwick runtime partition changed its geometry budget.");
            NpcModelBuilder.Validate(r);return r;
        }
        static NpcPart Copy(NpcPart source,int first,int count,int firstIndex,int indexCount,NpcVector offset,NpcVector pivot,int parent,string name)
        {
            var p=new NpcPart{Name=name,Parent=parent,Pivot=pivot};
            for(int i=first;i<first+count;i++){p.Positions.Add(source.Positions[i]-offset);p.Normals.Add(source.Normals[i]);p.Paints.Add(source.Paints[i]);}
            for(int i=firstIndex;i<firstIndex+indexCount;i++)p.Indices.Add(source.Indices[i]-first);
            return p;
        }
    }
}

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Original local13-part recipe ->17 parent-before-child rigid groups with explicit hip/knee/ankle pivots and unchanged material identity.
// Array Formats: Cold exact partition of two192-vertex legs into thigh/calf/boot; total1392triangles/2916vertices unchanged.
// Dependencies/Pointers: Actual pure builder validates geometry. No extra triangles, external mesh, skin/Animator, actual Assets or per-frame construction; original frozen prototype remains untouched.
