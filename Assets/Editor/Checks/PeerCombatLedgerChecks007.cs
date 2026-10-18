using System;

namespace OdysseyOracle
{
    public static class PeerCombatLedgerChecks007
    {
        const string A="0123456789abcdef0123456789abcdef",B="fedcba9876543210fedcba9876543210";
        static int checks;
        static void Check(bool ok,string detail){checks++;if(!ok)throw new Exception(detail);}
        static PeerCombatLedger Party()
        {
            var l=new PeerCombatLedger();Check(l.BeginRun(A),"real run");
            for(int i=0;i<8;i++){Check(l.Add((ulong)i,i,0,.2f,0,0),"host accepted member");l.SetReady((ulong)i,true);}return l;
        }
        static PeerHealthState State(ulong id,float hp,int revision,int report,bool downed,float shield=0)
            =>new PeerHealthState{id=id,present=true,hp=hp,revision=revision,report=report,downed=downed,shield=shield};
        public sealed class Report {public int Assertions;public long WarmedLedgerAllocatedBytes;public bool NativeAccepted=false;}
        public static Report Run()
        {
            checks=0;var l=new PeerCombatLedger();
            Check(!l.Add(0,0,0,0,0,0),"no fabricated host before run");Check(!l.BeginRun(null)&&!l.BeginRun(new string('x',32)),"invalid run refused");
            l=Party();Check(!l.Add(99,0,0,0,0,0)&&!l.Add(99,8,0,0,0,0),"bounded admission slots");
            Check(!l.CanMove(0,40,.2f,0,.1)&&!l.CanMove(0,5,.2f,0,100),"movement and suspended budget bounded");
            Check(l.CommitPose(0,.9f,.2f,0,.1)&&!l.CanMove(0,1,.2f,0,.05),"monotonic pose anchor");
            l.SetReady(0,false);Check(!l.CanMove(0,1,.2f,0,.2)&&!l.Authorize(1,1,0,0,false,1),"unready not targetable");l.SetReady(0,true);
            Check(!l.Authorize(0,1,0,0,false,1)&&!l.Authorize(512,1,0,0,false,1),"source bounds");
            Check(!l.Authorize(1,1,0,8,false,1)&&!l.Authorize(1,0,0,0,false,1),"lane and positive serial");
            Check(l.Authorize(1,1,0,0,false,1),"host ID zero is real target");
            Check(!l.Damage(1,1,1,0,5,1.1)&&!l.Damage(1,1,0,1,5,1.1),"melee victim and lane locked");
            Check(!l.Damage(1,1,0,0,float.NaN,1.1)&&!l.Damage(1,1,0,0,21,1.1),"bad amount leaves ticket");
            Check(l.Damage(1,1,0,0,5,1.1)&&!l.Damage(1,1,0,0,5,1.2),"one consumed impact");
            Check(l.TryGet(0,out var hp)&&hp.Health==120&&hp.Revision==2&&hp.DownReport==0,"damage exact scalar revision");
            // Ending is an outer authority mode, not a ledger clear. Retained
            // reads must not heal, spend a ticket, or advance a revision.
            Check(l.TryGet(0,out var finalA)&&finalA.Ready&&finalA.Health==120,"ending retains exact ready host record");
            Check(l.TryGet(0,out var finalB)&&finalB.Revision==finalA.Revision&&finalB.DownReport==finalA.DownReport,"readonly ending capture preserves revision/report");
            Check(!l.Damage(1,1,0,0,5,1.3)&&l.TryGet(0,out hp)&&hp.Health==finalA.Health,"readonly ending read never unspends damage ticket");
            Check(l.BeginRun(A)&&l.TryGet(0,out hp)&&hp.Health==120,"duplicate run preparation cannot heal");
            Check(!l.Authorize(1,1,0,0,false,2),"spent source serial tombstone");
            Check(l.AuthorizeProjectile(2,1,0,2,1,1,1,22),"first live bolt");
            Check(l.AuthorizeProjectile(2,2,0,3,7,1,1,22),"overlapping newer bolt");
            Check(l.TryGetAttack(2,1,3.1,out var old)&&old.X==1&&l.TryGetAttack(2,2,3.1,out var fresh)&&fresh.X==7,"origins retained by ticket serial");
            Check(l.Damage(2,1,1,0,5,3.1)&&l.Damage(2,2,2,0,5,3.1),"both same-source live bolts may hit once");
            Check(!l.Damage(2,1,2,0,5,3.2),"interceptor cannot double spend old bolt");
            Check(l.Authorize(3,1,0,0,false,4)&&!l.Damage(3,1,0,0,5,4.401),"melee expiry");
            Check(l.AuthorizeProjectile(4,1,0,5)&&!l.Damage(4,1,0,0,5,9.001),"bolt expiry");
            l=Party();
            for(int i=0;i<7;i++){Check(l.Authorize(10+i,1,0,0,false,1),"lethal sequence authorization");Check(l.Damage(10+i,1,0,0,20,1),"lethal sequence damage");}
            Check(l.TryGet(0,out hp)&&hp.Downed&&hp.Health==0&&hp.DownReport==1&&hp.Revision==8,"HP report revision ticket atomic lethal");
            Check(!l.Authorize(30,1,0,0,false,1.1),"no downed victim ticket");
            Check(!l.Revive(0,2,0,.2f,0,2,2)&&l.TryGet(0,out hp)&&hp.Downed,"wrong fall report cannot revive");
            Check(!l.Revive(0,1,float.NaN,.2f,0,2,2),"invalid revival atomic");
            Check(l.Revive(0,1,6,.2f,0,2,200)&&l.TryGet(0,out hp)&&hp.Health==125&&!hp.Downed&&hp.DownReport==1&&hp.Revision==9,"accepted full restore and same report");
            Check(!l.Revive(0,1,0,.2f,0,2,200),"duplicate revive cannot heal");
            Check(!l.Authorize(30,1,0,0,false,3.999)&&l.Authorize(30,1,0,0,false,4),"shield exact two seconds");
            Check(l.CanMove(0,6.5f,.2f,0,200.1),"accepted revive reanchors realtime movement");
            l.Remove(0);Check(!l.Damage(30,1,0,0,5,4),"departure invalidates victim ticket");
            Check(l.Add(0,0,0,.2f,0,201)&&l.SetReady(0,true)&&!l.Damage(30,1,0,0,5,4.1),"same-run reinsert cannot reuse melee ticket");
            l=Party();for(int i=1;i<=PeerCombatLedger.TicketCapacity;i++)Check(l.AuthorizeProjectile(i,1,0,1),"bounded simultaneous tickets");
            Check(!l.AuthorizeProjectile(97,1,0,1),"ticket saturation fails without serial commitment");
            Check(l.AuthorizeProjectile(97,1,0,5.001),"same serial succeeds after actual expiry frees slot");
            Check(!l.AuthorizeProjectile(1,1,0,5.001),"expired ticket does not erase source tombstone");
            Check(!l.AuthorizeProjectile(98,1,0,4),"simulation clock regression rejected");
            int accepted=0;
            for(int source=1;source<512;source++)
            {
                l=Party();int chosen=source%8;Check(l.Authorize(source,17,(ulong)chosen,chosen,false,3),"all lifetime source IDs");
                for(int candidate=0;candidate<8;candidate++)if(l.Damage(source,17,(ulong)candidate,chosen,1,3)){accepted++;Check(candidate==chosen,"only intended victim");}
                Check(!l.Damage(source,17,(ulong)chosen,chosen,1,3),"each source once");
            }
            Check(accepted==511,"all source IDs consume exactly once");
            var replica=new PeerHealthReplica();
            Check(!replica.TryGet(A,0,out var unknown)&&!unknown.Present&&unknown.MaximumHealth==0,"guest default has no125HP state");
            Check(replica.ResetForRun(A,ulong.MaxValue),"bind exact run and sparse ID");
            Check(!replica.TryApply(B,State(ulong.MaxValue,125,1,0,false),0),"cross run refused");
            Check(!replica.TryApply(A,State(0,125,1,0,false),0),"hostzero cannot alias another peer");
            Check(replica.TryApply(A,State(ulong.MaxValue,125,1,0,false),0),"first actual host health");
            Check(replica.TryApply(A,State(ulong.MaxValue,0,2,1,true),1),"accepted fall");
            Check(!replica.TryApply(A,State(ulong.MaxValue,125,1,0,false),2),"stale alive cannot cancel fall");
            Check(replica.TryApply(A,State(ulong.MaxValue,125,3,1,false,2),2),"accepted revival shield");
            Check(replica.TryGet(A,3,out var alive)&&!alive.Downed&&alive.Health==125&&alive.ShieldRemaining==1,"cached shield declines without same-revision refresh");
            Check(!replica.TryApply(A,State(ulong.MaxValue,125,3,1,false,2),3),"replayed revival cannot replenish shield");
            Check(!replica.TryApply(A,State(ulong.MaxValue,0,2,1,true),3),"old fall cannot undo revive");
            Check(!replica.TryApply(A,State(ulong.MaxValue,0,4,0,true),3),"crossstream report rollback rejected");
            Check(!replica.TryApply(A,State(ulong.MaxValue,0,4,2,false),3),"zeroHP alive contradiction");
            Check(!replica.TryApply(A,State(ulong.MaxValue,125,4,2,true),3),"positiveHP down contradiction");
            Check(!replica.TryApply(A,State(ulong.MaxValue,float.NaN,4,2,true),3)&&replica.Revision==3,"malformed replica leaves all state");
            Check(replica.ResetForRun(B,ulong.MaxValue)&&!replica.TryGet(B,4,out _),"new run clears explicit presence");
            Check(!replica.TryApply(A,State(ulong.MaxValue,0,99,9,true),4),"old epoch high revision refused");
            Check(!PeerCombatLedger.ValidHealthState(new PeerHealthState{id=0,hp=125,revision=1}),"absent default cannot fabricate fullHP");
            Check(PeerCombatLedger.ValidHealthState(new PeerHealthState{id=0}),"canonical absent DTO supported");
            l=Party();for(int i=0;i<3000;i++){l.Clear();l.BeginRun(A);l.Add(0,0,0,.2f,0,0);l.SetReady(0,true);l.AuthorizeProjectile(1,1,0,1);l.Damage(1,1,0,0,1,1);}
            long before=GC.GetAllocatedBytesForCurrentThread();int hits=0;
            for(int i=0;i<10000;i++){l.Clear();l.BeginRun(A);l.Add(0,0,0,.2f,0,0);l.SetReady(0,true);l.AuthorizeProjectile(1,1,0,1);if(l.Damage(1,1,0,0,1,1))hits++;}
            long allocated=GC.GetAllocatedBytesForCurrentThread()-before;
            Check(hits==10000&&allocated==0,"actual warmed scalar path allocates zero");
            return new Report{Assertions=checks,WarmedLedgerAllocatedBytes=allocated};
        }
    }
}
