using System;

namespace OdysseyOracle
{
    public static class CampaignTravel015
    {
        public const double BaseMetresPerSecond=95d/32d;
        public const double GateWindupReachMetres=1.9d;
        public const double CommonSeconds=20d,SwarmSeconds=10d,BossSeconds=40d;
        public const double SpiderCycleSeconds=2.4d,SpiderFastSeconds=.45d,SpiderFastStride=1.35d,SpiderSlowStride=.95d;
        public const double SpiderMeanStride=(SpiderFastSeconds*SpiderFastStride+(SpiderCycleSeconds-SpiderFastSeconds)*SpiderSlowStride)/SpiderCycleSeconds;
        public const float MinimumScale=.1f,MaximumScale=4f;
        public static double TargetSeconds(int acceptedGlobalWave)
        {
            if(!CampaignBalance015.ValidWave(acceptedGlobalWave))throw new ArgumentOutOfRangeException(nameof(acceptedGlobalWave));
            int local=(acceptedGlobalWave-1)%CampaignStagePacing.WavesPerStage;
            return local==4?BossSeconds:local==2?SwarmSeconds:CommonSeconds;
        }
        public static bool TrySpeedScale(int acceptedGlobalWave,int archetype,float portalX,float portalZ,float coreX,float coreZ,
            out float scale,out double travelledMetres,out double targetSeconds)
        {
            scale=0f;travelledMetres=targetSeconds=0d;
            if(!CampaignBalance015.ValidWave(acceptedGlobalWave)||archetype<0||archetype>2||
                !Finite(portalX)||!Finite(portalZ)||!Finite(coreX)||!Finite(coreZ))return false;
            double dx=(double)portalX-coreX,dz=(double)portalZ-coreZ;
            double distance=Math.Sqrt(dx*dx+dz*dz);
            if(!Finite(distance)||distance<=GateWindupReachMetres)return false;
            double seconds=TargetSeconds(acceptedGlobalWave),travel=distance-GateWindupReachMetres;
            double mean=archetype==2?SpiderMeanStride:1d;
            double raw=(travel/seconds)/(BaseMetresPerSecond*mean);
            if(!Finite(raw)||raw<MinimumScale||raw>MaximumScale)return false;
            float converted=(float)raw;
            if(!Finite(converted)||converted<MinimumScale||converted>MaximumScale)return false;
            scale=converted;travelledMetres=travel;targetSeconds=seconds;return true;
        }
        public static bool TryCreateProfile(int acceptedGlobalWave,int ordinal,float portalX,float portalZ,float coreX,float coreZ,
            out CurrentDefensePacing.EnemyProfile profile)
        {
            profile=default;
            if(!CampaignBalance015.ValidWave(acceptedGlobalWave)||ordinal<0||ordinal>=CampaignStagePacing.AdmissionsForWave(acceptedGlobalWave))return false;
            var seed=CampaignStagePacing.EnemyForWave(acceptedGlobalWave,ordinal);
            if(!TrySpeedScale(acceptedGlobalWave,seed.Archetype,portalX,portalZ,coreX,coreZ,out float speed,out _,out _))return false;
            // Seed SpeedScale is legacy metadata only: actual campaign admissions
            // always receive the computed travel scale, including non-heavy boss admissions.
            profile=new CurrentDefensePacing.EnemyProfile(seed.Archetype,seed.Flying,seed.Health,speed,seed.DamageScale);return true;
        }
        static bool Finite(double value)=>!double.IsNaN(value)&&!double.IsInfinity(value);
    }
}
// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Canonical world-XZ portal/core coordinates and remaining centreline distance to1.9metre windup reach; time in seconds.
// Array Formats: Scalar finite policy and existing immutable EnemyProfile value; no Unity Object/native/managed allocation in admission math.
// Dependencies/Pointers: Host supplies exact accepted wave/ordinal and actual World anchors before spawn/reservation; unchanged95/32 movement and2.4second spider cadence consume validated0.1..4 scale.
