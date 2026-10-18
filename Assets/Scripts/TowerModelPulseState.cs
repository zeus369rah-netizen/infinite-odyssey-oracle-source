using System;

namespace OdysseyOracle
{
    public readonly struct TowerModelPose
    {
        public readonly float Recoil,Pitch,CoreScale,CoreLift,CoreYaw,RotorYaw,Glow,Upgrade;
        public readonly bool BoltVisible;
        public TowerModelPose(float recoil,float pitch,float scale,float lift,float coreYaw,float rotorYaw,float glow,float upgrade,bool bolt)
        {Recoil=recoil;Pitch=pitch;CoreScale=scale;CoreLift=lift;CoreYaw=coreYaw;RotorYaw=rotorYaw;Glow=glow;Upgrade=upgrade;BoltVisible=bolt;}
    }

    // # ARCHITECTURE SNAPSHOT
    // Pure cosmetic state. Only accepted consumer hooks admit events; Tick never fires or upgrades.
    // Monotonic host revisions/shot serials make replays inert. No wallet or damage state is stored.
    public sealed class TowerModelPulseState
    {
        readonly int type,id;
        uint shotSequence;
        float shotAge=10,upgradeAge=10,clock,remaining,interval=1;
        public int Tier{get;private set;}=1;
        public int Revision{get;private set;}=-1;
        public bool HasAcceptedState=>Revision>=0;
        public TowerModelPulseState(int type,int towerId)
        {if(type<0||type>=4||towerId<0)throw new ArgumentOutOfRangeException();this.type=type;id=towerId;}
        public bool ApplyAcceptedTier(int tier,int revision)
        {
            if(tier<1||tier>3||revision<0||revision<Revision||HasAcceptedState&&tier<Tier
                ||revision==Revision&&tier!=Tier)return false;
            if(HasAcceptedState&&tier>Tier)upgradeAge=0;
            Tier=tier;Revision=revision;return true;
        }
        public bool OnAcceptedShot(uint serial,float acceptedInterval)
        {
            if(!HasAcceptedState||serial==0||serial<=shotSequence||!Finite(acceptedInterval)||acceptedInterval<.05f||acceptedInterval>10)return false;
            shotSequence=serial;shotAge=0;interval=acceptedInterval;remaining=acceptedInterval;return true;
        }
        public bool ApplyAcceptedCooldown(float secondsRemaining,float acceptedInterval)
        {
            if(!HasAcceptedState||!Finite(secondsRemaining)||!Finite(acceptedInterval)||acceptedInterval<.05f||acceptedInterval>10
                ||secondsRemaining<0||secondsRemaining>acceptedInterval+.001f)return false;
            interval=acceptedInterval;remaining=Math.Min(secondsRemaining,interval);return true;
        }
        public TowerModelPose Tick(float deltaTime,bool animateDecorations)
        {
            if(!Finite(deltaTime)||deltaTime<0||deltaTime>1)deltaTime=0;
            clock=(clock+deltaTime)%4096;shotAge=Math.Min(10,shotAge+deltaTime);upgradeAge=Math.Min(10,upgradeAge+deltaTime);
            remaining=Math.Max(0,remaining-deltaTime);
            float shot=shotAge<.24f?(float)Math.Sin(shotAge/.24f*Math.PI):0;
            float upgrade=Math.Max(0,1-upgradeAge/.65f);
            float charge=1-Math.Min(1,remaining/interval);
            float lift=animateDecorations?(float)Math.Sin(clock*.90+id*.73)*.018f:0;
            float coreYaw=animateDecorations?(type==0?(float)Math.Sin(clock*.55+id)*3:clock*(type==1?8:18)):0;
            float rotorYaw=type==3?(animateDecorations?clock*9:0)+shot*8+upgrade*5:0;
            float amplitude=type==2?.12f:type==0?.085f:type==1?.04f:.025f;
            return new TowerModelPose(-shot*amplitude,shot*(type==2?-1.8f:-.8f),1+charge*.018f+shot*.025f+upgrade*.025f,
                lift,coreYaw,rotorYaw,1+charge*.16f+shot*.9f+upgrade*.5f,upgrade,type!=2||shotAge>=.13f);
        }
        static bool Finite(float value)=>!float.IsNaN(value)&&!float.IsInfinity(value);
    }
}
