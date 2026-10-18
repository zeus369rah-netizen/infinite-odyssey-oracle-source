#if UNITY_EDITOR
using System;
using UnityEngine;
using UnityEngine.Assertions;

namespace OdysseyOracle.Editor
{
    public static class TutorialPartitionLocalPrecisionChecks
    {
        public static string Run()
        {
            // Distinct world vertices exceed the existing nondegenerate area threshold.
            // Both round to exactly the SAME uploaded vertex after subtracting origin(-4,0,-4).
            Vector3 a=new Vector3(-3e-8f,0,-3e-8f),b=new Vector3(-9e-8f,0,-9e-8f),c=new Vector3(-4,0,-3.333333f),origin=new Vector3(-4,0,-4);
            Require(Vector3.Cross(b-a,c-a).sqrMagnitude>=1e-16f,"World-space sliver must exercise the old guard.");
            Vector3 la=a-origin,lb=b-origin,lc=c-origin;
            Require(la.x==4&&la.z==4&&lb.x==4&&lb.z==4,"Fixture must reproduce actual32-bit localization collapse.");
            Require(Vector3.Cross(lb-la,lc-la).sqrMagnitude==0,"Uploaded local triangle has exactly zero area.");
            var points=new[]{a,b,c};var normals=new[]{Vector3.up,Vector3.up,Vector3.up};var uvs=new[]{Vector2.zero,Vector2.up,Vector2.one};
            var result=TutorialRenderPartition.Partition(points,normals,uvs,new[]{0,1,2});
            Require(result.Count==0,"Collapsed sliver must not create an empty or zero-area render cell.");
            // The original full-area/UV/strict-winding fixture remains an independent required gate.
            return "4 exact localization regression assertions passed; run unchanged TutorialPartitionChecks.Run() and the native precision probe too.";
        }
        static void Require(bool value,string message){Assert.IsTrue(value,message);if(!value)throw new InvalidOperationException(message);}
    }
}
#endif
