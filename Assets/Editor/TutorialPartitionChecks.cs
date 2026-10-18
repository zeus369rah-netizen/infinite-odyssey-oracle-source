#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Assertions;

namespace OdysseyOracle.Editor
{
    // Root-only exact production partition checks. Pure mesh data; no scene/native asset changes.
    public static class TutorialPartitionChecks
    {
        [Serializable] public sealed class Result {public string status,exception;public int assertions,parts,triangles;public double sourceArea,partitionedArea;}
        public static string Run()
        {
            var result=new Result{status="failed"};
            try {
                Check(result,new[]{new Vector3(-6,0,-5),new Vector3(-6,0,5),new Vector3(6,0,5),new Vector3(6,0,-5)},new[]{0,1,2,0,2,3},Vector3.up,120);
                // Sloped and vertical planes exercise interpolated UVs, winding and shared cell edges.
                Check(result,new[]{new Vector3(-7,-2,-6),new Vector3(-2,4,9),new Vector3(8,3,-4)},new[]{0,1,2},Vector3.Cross(new Vector3(5,6,15),new Vector3(15,5,2)).normalized,-1);
                Check(result,new[]{new Vector3(4,0,-9),new Vector3(4,5,-9),new Vector3(4,5,9),new Vector3(4,0,9)},new[]{0,1,2,0,2,3},Vector3.right,90);
                List<TutorialRenderPartition.Part> empty=TutorialRenderPartition.Partition(new Vector3[0],new Vector3[0],new Vector2[0],new int[0]);
                Demand(empty.Count==0,"Empty input must create no renderer cells.",result);
                Vector3[] point={Vector3.zero,Vector3.zero,Vector3.zero};Vector3[] normals={Vector3.up,Vector3.up,Vector3.up};Vector2[] uvs={Vector2.zero,Vector2.zero,Vector2.zero};
                Demand(TutorialRenderPartition.Partition(point,normals,uvs,new[]{0,1,2}).Count==0,"Zero-area faces must not produce empty cells.",result);
                bool rejected=false;try{TutorialRenderPartition.Partition(point,normals,uvs,new[]{0,1,9});}catch(ArgumentOutOfRangeException){rejected=true;}
                Demand(rejected,"Invalid indices must fail before mesh publication.",result);
                result.status="passed";
            }catch(Exception e){result.exception=e.ToString();}
            return JsonUtility.ToJson(result,true);
        }
        static void Check(Result report,Vector3[] points,int[] indices,Vector3 normal,double expectedArea)
        {
            var normals=new Vector3[points.Length];var uvs=new Vector2[points.Length];
            for(int i=0;i<points.Length;i++){normals[i]=normal;uvs[i]=UV(points[i]);}
            double before=0;for(int i=0;i<indices.Length;i+=3)before+=Area(points[indices[i]],points[indices[i+1]],points[indices[i+2]]);
            if(expectedArea>=0)Demand(Math.Abs(before-expectedArea)<.0001,"Analytic fixture area must match input.",report);
            List<TutorialRenderPartition.Part> parts=TutorialRenderPartition.Partition(points,normals,uvs,indices);double after=0;
            Demand(parts.Count>1,"Wide surfaces must actually span independent cells.",report);
            foreach(TutorialRenderPartition.Part part in parts)
            {
                Demand(part.Positions.Count==part.Normals.Count && part.Positions.Count==part.UVs.Count,"Vertex attributes must remain aligned.",report);
                Demand(part.Indices.Count>0,"Empty renderer cells are forbidden.",report);
                for(int i=0;i<part.Positions.Count;i++)
                {
                    Vector3 p=part.Positions[i],world=p+part.Origin;
                    Demand(p.x>=-.00005f && p.x<=4.00005f && p.z>=-.00005f && p.z<=4.00005f,"Actual cell-local vertices must fit4m XZ bounds.",report);
                    Demand((UV(world)-part.UVs[i]).sqrMagnitude<.0000001f,"Clipped UVs must preserve the original affine mapping.",report);
                    Demand((normal-part.Normals[i]).sqrMagnitude<.0000001f,"Clipping cannot change planar vertex normals.",report);
                }
                for(int i=0;i<part.Indices.Count;i+=3)
                {
                    Vector3 a=part.Positions[part.Indices[i]],b=part.Positions[part.Indices[i+1]],c=part.Positions[part.Indices[i+2]];
                    Demand(Vector3.Dot(Vector3.Cross(b-a,c-a),normal)>0,"All emitted triangles must preserve source face winding.",report);
                    after+=Area(a,b,c);report.triangles++;
                }
            }
            Demand(Math.Abs(after-before)<=Math.Max(.0001,before*.000002),"Area conservation rejects missing faces or duplicated cell interiors.",report);
            report.parts+=parts.Count;report.sourceArea+=before;report.partitionedArea+=after;
        }
        static Vector2 UV(Vector3 p)=>new Vector2(.25f*p.x+.13f*p.y+.07f*p.z,.11f*p.x-.21f*p.y+.33f*p.z);
        static double Area(Vector3 a,Vector3 b,Vector3 c)=>Vector3.Cross(b-a,c-a).magnitude*.5;
        static void Demand(bool value,string message,Result report){Assert.IsTrue(value,message);if(!value)throw new InvalidOperationException(message);report.assertions++;}
    }
}
#endif
