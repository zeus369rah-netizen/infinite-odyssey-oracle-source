using System;

namespace OdysseyOracle
{
    [Serializable]
    public struct PeerHealthState
    {
        public ulong id;
        public bool present;
        public float hp,shield;
        public int revision,report;
        public bool downed;
    }

    public readonly struct PeerHealthSnapshot
    {
        public readonly bool Present,Downed;
        public readonly string RunId;
        public readonly ulong PeerId;
        public readonly int Revision,DownReport;
        public readonly float Health,ShieldRemaining;
        public float MaximumHealth=>Present?PeerCombatLedger.MaximumHealth:0f;
        internal PeerHealthSnapshot(string run,PeerHealthState value,float shield)
        {Present=value.present;RunId=run;PeerId=value.id;Revision=value.revision;DownReport=value.report;Health=value.hp;Downed=value.downed;ShieldRemaining=shield;}
    }

    public struct PeerHealthReplica
    {
        string run;ulong id;PeerHealthState state;double receivedAt;
        public bool Present=>state.present;
        public int Revision=>state.revision;
        public int Report=>state.report;
        public bool ResetForRun(string expectedRun,ulong peer)
        {
            if(!PeerCombatLedger.ValidRun(expectedRun))return false;
            run=expectedRun;id=peer;state=default;receivedAt=0;return true;
        }
        public bool TryApply(string messageRun,PeerHealthState value,double now)
        {
            if(run==null||messageRun!=run||value.id!=id||!value.present||!PeerCombatLedger.ValidHealthState(value)||
                !PeerCombatLedger.Finite(now)||now<receivedAt||value.revision<=state.revision||value.report<state.report)return false;
            state=value;receivedAt=now;return true;
        }
        public bool TryGet(string currentRun,double now,out PeerHealthSnapshot snapshot)
        {
            snapshot=default;if(!state.present||run!=currentRun||!PeerCombatLedger.Finite(now)||now<receivedAt)return false;
            snapshot=new PeerHealthSnapshot(run,state,Math.Max(0f,state.shield-(float)(now-receivedAt)));return true;
        }
    }

    // # ARCHITECTURE SNAPSHOT
    // Host-owned fixed scalar state. Poses use realtime seconds; tickets, health
    // and shields use cumulative simulation seconds. No Unity/native/transport.
    // Source serial gates survive spent/expired ticket reuse for the whole run.
    // HP, down report, revision and ticket consumption commit in one method.
    public sealed class PeerCombatLedger
    {
        public const int Capacity=8,SourceCapacity=512,TicketCapacity=96;
        public const float MaximumHealth=125f;
        public struct Record
        {
            public ulong Id;
            public int Slot,Revision,DownReport;
            public bool Used,Ready,Downed;
            public float X,Y,Z,Health;
            public double PoseTime,ShieldUntil;
        }
        public readonly struct Attack
        {
            public readonly int Lane;
            public readonly bool Projectile;
            public readonly ulong Target;
            public readonly float X,Y,Z,Range;
            internal Attack(int lane,bool projectile,ulong target,float x,float y,float z,float range)
            {Lane=lane;Projectile=projectile;Target=target;X=x;Y=y;Z=z;Range=range;}
        }
        struct Source {public int Serial;public double NextAttack;}
        struct Ticket
        {
            public bool Used,Spent;
            public int Source,Serial;
            public Attack Attack;
            public double StartedAt,Expires;
        }
        readonly Record[] peers=new Record[Capacity];
        readonly Source[] sources=new Source[SourceCapacity];
        readonly Ticket[] tickets=new Ticket[TicketCapacity];
        double simulationFloor;
        public string RunId {get;private set;}
        public static bool Finite(double v)=>!double.IsNaN(v)&&!double.IsInfinity(v);
        public static bool ValidRun(string run)
        {
            if(run==null||run.Length!=32)return false;
            for(int i=0;i<run.Length;i++)if(!(run[i]>='0'&&run[i]<='9'||run[i]>='a'&&run[i]<='f'))return false;
            return true;
        }
        public static bool ValidHealthState(PeerHealthState s)
        {
            if(!s.present)return s.hp==0f&&s.shield==0f&&s.revision==0&&s.report==0&&!s.downed;
            return Finite(s.hp)&&s.hp>=0f&&s.hp<=MaximumHealth&&s.revision>=1&&s.report>=0&&
                Finite(s.shield)&&s.shield>=0f&&s.shield<=2f&&s.downed==(s.hp<=0f)&&(!s.downed||s.report>=1&&s.shield==0f);
        }
        public bool BeginRun(string run)
        {if(!ValidRun(run))return false;if(RunId==run)return true;Clear();RunId=run;return true;}
        public void Clear()
        {Array.Clear(peers,0,peers.Length);Array.Clear(sources,0,sources.Length);Array.Clear(tickets,0,tickets.Length);simulationFloor=0;RunId=null;}
        int Index(ulong id){for(int i=0;i<Capacity;i++)if(peers[i].Used&&peers[i].Id==id)return i;return -1;}
        public bool Add(ulong id,int slot,float x,float y,float z,double poseNow)
        {
            if(RunId==null||slot<0||slot>=Capacity||!ValidPosition(x,y,z)||!Finite(poseNow)||poseNow<0)return false;
            for(int i=0;i<Capacity;i++)if(peers[i].Used&&(peers[i].Id==id||peers[i].Slot==slot))return false;
            for(int i=0;i<Capacity;i++)if(!peers[i].Used)
            {peers[i]=new Record{Used=true,Id=id,Slot=slot,X=x,Y=y,Z=z,PoseTime=poseNow,Health=MaximumHealth,Revision=1};return true;}
            return false;
        }
        public bool TryGet(ulong id,out Record record)
        {int i=Index(id);record=i>=0?peers[i]:default;return i>=0;}
        public Record At(int index)=>index>=0&&index<Capacity?peers[index]:default;
        public void Remove(ulong id)
        {
            int i=Index(id);if(i>=0)peers[i]=default;
            for(int t=0;t<TicketCapacity;t++)if(tickets[t].Used&&!tickets[t].Attack.Projectile&&tickets[t].Attack.Target==id)tickets[t].Spent=true;
        }
        public bool SetReady(ulong id,bool ready){int i=Index(id);if(i<0)return false;peers[i].Ready=ready;return true;}
        public static bool ValidPosition(float x,float y,float z)=>Finite(x)&&Finite(y)&&Finite(z)&&(double)x*x+(double)z*z<=78d*78d&&y>=-1f&&y<=12f;
        public bool CanMove(ulong id,float x,float y,float z,double now)
        {
            if(!TryGet(id,out var p)||!p.Ready||!ValidPosition(x,y,z)||!Finite(now)||now<p.PoseTime)return false;
            double dt=Math.Min(.25d,now-p.PoseTime),dx=x-p.X,dz=z-p.Z;
            double horizontal=(p.Downed?.5d:12d)*dt+.18d,vertical=18d*dt+.22d;
            return dx*dx+dz*dz<=horizontal*horizontal&&Math.Abs(y-p.Y)<=vertical;
        }
        public bool CommitPose(ulong id,float x,float y,float z,double now)
        {if(!CanMove(id,x,y,z,now))return false;int i=Index(id);peers[i].X=x;peers[i].Y=y;peers[i].Z=z;peers[i].PoseTime=now;return true;}
        bool Clock(double now)=>RunId!=null&&Finite(now)&&now>=simulationFloor;
        bool AuthorizeCore(int source,int serial,ulong target,int lane,bool projectile,double now,float x,float y,float z,float range,bool named)
        {
            if(source<1||source>=SourceCapacity||serial<1||lane<0||lane>=8||!Clock(now)||!ValidPosition(x,y,z)||
                !Finite(range)||range<=0||range>(projectile?22f:2.5f)||serial<=sources[source].Serial||now<sources[source].NextAttack)return false;
            if(named&&(!TryGet(target,out var p)||!p.Ready||p.Downed||p.Health<=0||now<p.ShieldUntil))return false;
            int slot=-1;
            for(int i=0;i<TicketCapacity;i++)if(!tickets[i].Used||tickets[i].Spent||now>tickets[i].Expires){slot=i;break;}
            if(slot<0)return false;
            tickets[slot]=new Ticket{Used=true,Source=source,Serial=serial,Attack=new Attack(lane,projectile,target,x,y,z,range),StartedAt=now,Expires=now+(projectile?4d:.4d)};
            sources[source]=new Source{Serial=serial,NextAttack=now+(projectile?1d:.4d)};simulationFloor=now;return true;
        }
        public bool Authorize(int source,int serial,ulong target,int lane,bool projectile,double now,float x=0,float y=0,float z=0,float range=2f)
            =>AuthorizeCore(source,serial,target,lane,projectile,now,x,y,z,range,true);
        public bool AuthorizeProjectile(int source,int serial,int lane,double now,float x=0,float y=0,float z=0,float range=22f)
            =>AuthorizeCore(source,serial,0,lane,true,now,x,y,z,range,false);
        int TicketIndex(int source,int serial,double now)
        {
            if(source<1||source>=SourceCapacity||serial<1||!Clock(now))return -1;
            for(int i=0;i<TicketCapacity;i++)if(tickets[i].Used&&!tickets[i].Spent&&tickets[i].Source==source&&tickets[i].Serial==serial&&
                now>=tickets[i].StartedAt&&now<=tickets[i].Expires)return i;
            return -1;
        }
        public bool TryGetAttack(int source,int serial,double now,out Attack attack)
        {int i=TicketIndex(source,serial,now);attack=i>=0?tickets[i].Attack:default;return i>=0;}
        public bool CanHit(int source,int serial,ulong target,int lane,double now)
        {
            if(!TryGet(target,out var p)||!p.Ready||p.Downed||p.Health<=0||p.Revision==int.MaxValue||now<p.ShieldUntil)return false;
            int i=TicketIndex(source,serial,now);if(i<0)return false;
            var a=tickets[i].Attack;return lane==a.Lane&&(a.Projectile||a.Target==target);
        }
        public bool Damage(int source,int serial,ulong target,int lane,float amount,double now)
        {
            if(!Finite(amount)||amount<=0||amount>20||!CanHit(source,serial,target,lane,now))return false;
            int p=Index(target),t=TicketIndex(source,serial,now);
            bool lethal=amount>=peers[p].Health;if(lethal&&peers[p].DownReport==int.MaxValue)return false;
            Record next=peers[p];next.Health=Math.Max(0,next.Health-amount);next.Downed=next.Health<=0;next.Revision++;
            if(lethal)next.DownReport++;
            tickets[t].Spent=true;peers[p]=next;simulationFloor=now;return true;
        }
        public bool Revive(ulong id,int expectedReport,float x,float y,float z,double now,double poseNow)
        {
            int i=Index(id);
            if(i<0||!peers[i].Ready||!peers[i].Downed||peers[i].DownReport!=expectedReport||peers[i].Revision==int.MaxValue||
                !ValidPosition(x,y,z)||!Clock(now)||!Finite(poseNow)||poseNow<peers[i].PoseTime)return false;
            var next=peers[i];next.Health=MaximumHealth;next.Downed=false;next.ShieldUntil=now+2;next.X=x;next.Y=y;next.Z=z;next.PoseTime=poseNow;next.Revision++;
            peers[i]=next;simulationFloor=now;return true;
        }
    }
}
