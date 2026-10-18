#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEngine;

namespace OdysseyOracle.Editor
{
    // Explicit root-run pure data probe of the ACTUAL production partition method.
    public static class TutorialPartitionPrecisionProbe
    {
        [Serializable] public sealed class BadTriangle
        {
            public int CellX,CellZ,Triangle;public Vector3 A,B,C,Origin,FloatCross,SourceNormal;
            public double DoubleCrossX,DoubleCrossY,DoubleCrossZ,DoubleDot,DoubleArea,FloatDot;
        }
        [Serializable] public sealed class Result
        {
            public string Method="Actual TutorialRenderPartition.Partition output, no native scene writes or tolerance relaxation.";
            public int Triangles,NonpositiveFloatWinding,NonpositiveDoubleWinding;public double Area,DiscardedCandidateAreaUnknown=-1;
            public BadTriangle[] Bad;
        }
        public static string Run()
        {
            Vector3[] points={new Vector3(-6,0,-5),new Vector3(-6,0,5),new Vector3(6,0,5),new Vector3(6,0,-5)};
            var normals=new[]{Vector3.up,Vector3.up,Vector3.up,Vector3.up};var uvs=new[]{Vector2.zero,Vector2.up,Vector2.one,Vector2.right};
            List<TutorialRenderPartition.Part> parts=TutorialRenderPartition.Partition(points,normals,uvs,new[]{0,1,2,0,2,3});var report=new Result();var bad=new List<BadTriangle>();
            foreach(var part in parts)for(int i=0;i<part.Indices.Count;i+=3){
                Vector3 a=part.Positions[part.Indices[i]],b=part.Positions[part.Indices[i+1]],c=part.Positions[part.Indices[i+2]],cross=Vector3.Cross(b-a,c-a);
                double abx=(double)b.x-a.x,aby=(double)b.y-a.y,abz=(double)b.z-a.z,acx=(double)c.x-a.x,acy=(double)c.y-a.y,acz=(double)c.z-a.z;
                double x=aby*acz-abz*acy,y=abz*acx-abx*acz,z=abx*acy-aby*acx,area=Math.Sqrt(x*x+y*y+z*z)*.5;
                double dot=Vector3.Dot(cross,Vector3.up);report.Triangles++;report.Area+=area;if(dot<=0)report.NonpositiveFloatWinding++;if(y<=0)report.NonpositiveDoubleWinding++;
                if((dot<=0||y<=0)&&bad.Count<12)bad.Add(new BadTriangle {CellX=part.X,CellZ=part.Z,Triangle=i/3,A=a,B=b,C=c,Origin=part.Origin,FloatCross=cross,
                    SourceNormal=Vector3.up,DoubleCrossX=x,DoubleCrossY=y,DoubleCrossZ=z,DoubleDot=y,DoubleArea=area,FloatDot=dot});
            }
            report.Bad=bad.ToArray();return JsonUtility.ToJson(report,true);
        }
    }
}
#endif
