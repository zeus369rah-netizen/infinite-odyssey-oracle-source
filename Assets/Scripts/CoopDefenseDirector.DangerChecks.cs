#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
namespace OdysseyOracle
{
    public sealed partial class CoopDefenseDirector
    {
        // # ARCHITECTURE SNAPSHOT
        // Editor-only cold private DTO acceptance + pure fixed-ledger probes.
        // No Director instance, GameObjects, reflection, profile or transport.
        [Serializable] public sealed class DangerWireReceipt
        {
            public string status, checkedUtc;
            public int assertions, failures, utf8Bytes, actualStateUtf8Bytes, maximumCounterUtf8Bytes, framedBytes;
            public bool nativeJsonExecuted, networkExecuted, absenceGuarded, nativeNullPersonalBecameObject, textCanonicalized;
            public string[] failureDetails;
            public int actualPersonalUtf8Bytes,actualFramedStateBytes,modeledStateBytesPerSecondPerGuest,modeledHostStateBytesPerSecondSevenGuests;
        }
        sealed class DangerChecks
        {
            internal int Count, Failed;
            internal readonly List<string> Details=new List<string>(64);
            internal void Check(bool yes,string name){Count++;if(!yes){Failed++;if(Details.Count<64)Details.Add(name);}}
        }
        public static DangerWireReceipt RunDangerWireAcceptance()
        {
            var result=new DangerWireReceipt{checkedUtc=DateTime.UtcNow.ToString("o")};
            var c=new DangerChecks();
            try
            {
                var before=BuildDangerWireProbe();
                string json=JsonUtility.ToJson(before);result.nativeJsonExecuted=true;
                result.utf8Bytes=Encoding.UTF8.GetByteCount(json);
                var after=JsonUtility.FromJson<Message>(json);CheckMessage(before,after,c);
                c.Check(result.utf8Bytes<RoomConnection.MaximumReliablePayloadBytes,"48 actors/4 towers/48 bolts/8 public health records + one128-claim personal strictly under64KiB");
                c.Check(JsonUtility.ToJson(after)==json,"whole private DTO byte-stable roundtrip");
                string enemy=JsonUtility.ToJson(before.enemies[0]),hostile=JsonUtility.ToJson(before.hostileBolts[0]);
                foreach(string key in new[]{"q","p","s","l","u","w","t","r","a","d","b","c"})c.Check(enemy.Contains("\""+key+"\":"),"private enemy alias "+key);
                foreach(string key in new[]{"s","g","e","q","l","a","h","o","p","d","n","v","r","t"})c.Check(hostile.Contains("\""+key+"\":"),"private bolt alias "+key);
                c.Check(!enemy.Contains("\"attackSequence\":")&&!hostile.Contains("\"generation\":"),"properties not serialized as duplicate payload");
                before.personal=null;before.hasPersonal=false;before.text=string.Empty;
                string actualStateJson=JsonUtility.ToJson(before);
                result.actualStateUtf8Bytes=Encoding.UTF8.GetByteCount(actualStateJson);
                var actualStateAfter=JsonUtility.FromJson<Message>(actualStateJson);
                CheckMessage(before,actualStateAfter,c);
                result.nativeNullPersonalBecameObject=actualStateAfter!=null&&actualStateAfter.personal!=null;
                result.absenceGuarded=actualStateAfter!=null&&!actualStateAfter.hasPersonal&&!HasPersonalPayload(actualStateAfter);
                result.textCanonicalized=actualStateAfter!=null&&actualStateAfter.text==string.Empty;
                c.Check(result.absenceGuarded,"actual state absent personal cannot route even if serializer creates default object");
                c.Check(result.textCanonicalized,"outgoing empty text canonicalized");
                var validPersonal=BuildDangerWireProbe().personal;
                var envelope=new Message{kind="oracle.personal.v1",partyVersion=PartyProtocolVersion,hasPersonal=false,personal=validPersonal};
                c.Check(!HasPersonalPayload(envelope),"unflagged otherwise valid personal cannot route");
                envelope.hasPersonal=true;c.Check(HasPersonalPayload(envelope),"explicit valid personal routes");
                envelope.kind="oracle.state.v1";c.Check(!HasPersonalPayload(envelope),"wrong kind cannot route flagged personal");
                envelope.kind="oracle.personal.v1";envelope.personal=new PlayerState();c.Check(!HasPersonalPayload(envelope),"serializer default personal cannot mutate progress or selection");
                envelope.personal=validPersonal;int level=validPersonal.level;validPersonal.level=0;c.Check(!HasPersonalPayload(envelope),"invalid level rejected before mutation");validPersonal.level=level;
                float[] returning=validPersonal.returnCooldowns;validPersonal.returnCooldowns=null;c.Check(!HasPersonalPayload(envelope),"missing recovery array rejected before mutation");validPersonal.returnCooldowns=returning;
                c.Check(result.actualStateUtf8Bytes<RoomConnection.MaximumReliablePayloadBytes,"actual SendState strictly under64KiB");
                c.Check(JsonUtility.ToJson(actualStateAfter)==actualStateJson,"actual SendState byte stable");
                string personalJson=JsonUtility.ToJson(new Message{kind="oracle.personal.v1",run=before.run,partyVersion=PartyProtocolVersion,
                    hasPersonal=true,personal=validPersonal});
                result.actualPersonalUtf8Bytes=Encoding.UTF8.GetByteCount(personalJson);
                var personalAfter=JsonUtility.FromJson<Message>(personalJson);
                c.Check(HasPersonalPayload(personalAfter),"actual own personal full envelope valid");CheckPlayer(validPersonal,personalAfter.personal,c);
                c.Check(result.actualPersonalUtf8Bytes<RoomConnection.MaximumReliablePayloadBytes,"own128-claim personal fits reliable cap");
                CheckDangerLedgers(c);
                var wide=BuildDangerWireProbe();
                wide.sequence=int.MaxValue;
                foreach(var actor in wide.enemies)actor.attackSequence=int.MaxValue;
                foreach(var tower in wide.buildings)tower.id=int.MaxValue;
                for(int i=0;i<wide.hostileBolts.Length;i++)
                { var bolt=wide.hostileBolts[i];bolt.generation=bolt.attackSerial=int.MaxValue;wide.hostileBolts[i]=bolt; }
                string maximumCounterJson=JsonUtility.ToJson(wide);
                result.maximumCounterUtf8Bytes=Encoding.UTF8.GetByteCount(maximumCounterJson);
                result.framedBytes=result.utf8Bytes+sizeof(int);
                result.actualFramedStateBytes=result.actualStateUtf8Bytes+sizeof(int);
                result.modeledStateBytesPerSecondPerGuest=result.actualFramedStateBytes*10;
                result.modeledHostStateBytesPerSecondSevenGuests=result.modeledStateBytesPerSecondPerGuest*7;
                CheckMessage(wide,JsonUtility.FromJson<Message>(maximumCounterJson),c);
                c.Check(result.maximumCounterUtf8Bytes<RoomConnection.MaximumReliablePayloadBytes,"maximum int32 counter widths fit64KiB payload");
                CheckNovaPresentation(c);
                var forge=BuildDangerWireProbe().personal;
                c.Check(ValidPersonalPayload(forge),"valid captured accepted Forge state");
                forge.forgeVersion=0;c.Check(!ValidPersonalPayload(forge),"missing Forge capability rejected before mutation");forge.forgeVersion=MatchTowerForgeRules.Version;
                forge.forgeTiers=255;c.Check(!ValidPersonalPayload(forge),"malformed packed Forge tiers rejected");forge.forgeTiers=170;
                forge.forgeBank=-1;c.Check(!ValidPersonalPayload(forge),"negative Forge bank rejected");forge.forgeBank=MatchTowerForgeRules.MaximumBalance;
                forge.forgeLastRequest=0;c.Check(!ValidPersonalPayload(forge),"receipt outcome without request rejected");
                c.Check(TowerProtocolVersion==5&&MaximumPlacedShrines==4&&PartyProtocolVersion==7,"four shared pad/current8 capability exact");
                c.Check(before.players.Length==0&&before.health.Length==8,"common productionshape contains nootherpeer private claim arrays");
                var absent=BuildDangerWireProbe().personal;absent.health=default;
                c.Check(!ValidPersonalPayload(absent),"missing own health cannot route private progress");
                var sync=new Message{kind="oracle.personal.sync.v1",run=before.run,partyVersion=PartyProtocolVersion};
                c.Check(Encoding.UTF8.GetByteCount(JsonUtility.ToJson(sync))<=2048,"actual full personal resync request fits control cap");
                var control=new Message{kind="oracle.cast.v2",run=before.run,partyVersion=PartyProtocolVersion,selectionMask=12,
                    castSequence=uint.MaxValue,selectionSequence=uint.MaxValue,direction=new Vector3(.12345679f,.2345679f,.964111f).normalized};
                c.Check(Encoding.UTF8.GetByteCount(JsonUtility.ToJson(control))<=2048,"actual manualcast union fits control cap");
                c.Check(NovaProtocolVersion==4&&RemoteDangerPolicy.Version==3003,"separate Nova capability preserves mandatory danger version");
            }
            catch(Exception error){c.Check(false,error.GetType().Name+": "+error.Message);}
            result.assertions=c.Count;result.failures=c.Failed;result.failureDetails=c.Details.ToArray();
            result.status=!result.nativeJsonExecuted?"INCOMPLETE_NATIVE_JSON":c.Failed==0?"PASSED_NATIVE_CURRENT8_TOWER5_HEALTH7_WIRE":"FAILED_NATIVE_CURRENT8_TOWER5_HEALTH7_WIRE";
            return result;
        }
        static bool Same(Vector3 a,Vector3 b)=>a.x==b.x&&a.y==b.y&&a.z==b.z;
        static Message BuildDangerWireProbe()
        {
            var m=new Message{kind="oracle.state.v1",run="0123456789abcdef0123456789abcdef",text="wire sentinel",hasPersonal=true,sequence=147,
                result=2,element=3,node=2,dropId=99,activeLanes=8,partyVersion=PartyProtocolVersion,admissionMask=255,geometryMask=255,wave=5,spawned=16,quota=16,running=true,clockStarted=true,enabled=true,
                correctionSequence=int.MaxValue,controlSequence=uint.MaxValue,selectionSequence=uint.MaxValue-1,castSequence=uint.MaxValue-2,selectionMask=12,forgeRequestId=uint.MaxValue,forgeExpectedTier=2,
                forgeExpectedRevision=MatchTowerForgeRules.MaximumRevision,forgeOutcome=(int)MatchForgeOutcome.Applied,
                remaining=123.45678f,shrine=299.12345f,position=new Vector3(-12.123456f,.36f,7.891234f),direction=Vector3.forward,
                dangerVersion=RemoteDangerPolicy.Version,novaVersion=NovaProtocolVersion,towerVersion=TowerProtocolVersion,hostileCount=48,enemies=new EnemyState[OracleCombat.MaxActiveEnemies],buildings=new BuildingState[4],
                players=NoCommonPersonal,health=new PeerHealthState[8],drops=new DropState[128],hostileBolts=new HostileBoltState[48]};
            for(int i=0;i<m.enemies.Length;i++)m.enemies[i]=new EnemyState{id=129+i,lane=i%8,archetype=i%3,shade=i%3==1,flying=i%3==1,
                position=new Vector3(-50.12345f+i,.0123456f,1.2345678f),hp=29.123455f,maximum=60f,attackSequence=33+i,attackPhase=2,
                attackStyle=i%3,attackProgress=.456789f,windupSeconds=.95f,strikeSeconds=.12f,recoverySeconds=3.25f,aggro=true,
                lockedDirection=new Vector3(.12345679f,.2345679f,.964111f).normalized,targetTowerId=i%3==2?1:0,targetPoint=new Vector3(-50.12345f+i,.9f,3.2345678f)};
            for(int i=0;i<4;i++)m.buildings[i]=new BuildingState{id=i+1,element=i%4,owner=i<2?0:ulong.MaxValue,
                tier=i%3+1,tierRevision=MatchTowerForgeRules.MaximumRevision,
                position=WirePad(i),health=79.12345f};
            for(int i=0;i<128;i++)m.drops[i]=new DropState{id=i+1,amount=i%10+1,position=new Vector3(-55.12345f+i*.8f,.0123456f,2.345678f)};
            for(int i=0;i<8;i++)m.health[i]=new PeerHealthState{id=i==0?0:ulong.MaxValue-(ulong)(7-i),present=true,
                hp=124.12345f,shield=1.9999998f,revision=int.MaxValue,report=int.MaxValue,downed=false};
            var p=new PlayerState{id=ulong.MaxValue,health=m.health[7],autoFire=true,preparedElement=-1,selectionMask=9,confirmedElement=3,
                selectionSequence=uint.MaxValue,tokens=3,element=3,combo=1,forgeVersion=MatchTowerForgeRules.Version,
                forgeBank=MatchTowerForgeRules.MaximumBalance,forgeTiers=170,forgeRevision=MatchTowerForgeRules.MaximumRevision,
                forgeLastRequest=uint.MaxValue,forgeOutcome=(int)MatchForgeOutcome.Applied,level=49,xp=17,points=12,mask=4095,
                claimed=new int[128],returnCooldowns=new[]{3.9999998f,2.1234567f,1.2345678f,.9876543f},cooldown=.99999994f,
                comboSeconds=0,novaMask=3,novaSeconds=3.1234567f};
            for(int j=0;j<128;j++)p.claimed[j]=384+j;
            m.personal=p; // Union stress includes one full private envelope, never eight percommonstate.
            for(int i=0;i<48;i++)m.hostileBolts[i]=new HostileBoltState{slot=i,generation=400+i,sourceEnemyId=129+i,attackSerial=33+i,lane=i%8,
                active=i%2==0,impact=i%2!=0,origin=new Vector3(-30.12345f+i,1.2345678f,0),position=new Vector3(-30.12345f+i,1.2345678f,7.123456f),
                direction=new Vector3(.12345679f,.2345679f,.964111f).normalized,normal=-new Vector3(.12345679f,.2345679f,.964111f).normalized,speed=RemoteDangerPolicy.ShadeSpeed,remaining=11.234568f,age=i%2==0?0:.12345678f};
            return m;
        }
        static Vector3 WirePad(int i)
        {switch(i){case 0:return new Vector3(12.934313f,.36f,5.357568f);case 1:return new Vector3(-5.357568f,.36f,12.934313f);
            case 2:return new Vector3(-12.934313f,.36f,-5.357568f);default:return new Vector3(5.357568f,.36f,-12.934313f);}}
        static void CheckHealth(PeerHealthState a,PeerHealthState b,DangerChecks c)
        {
            c.Check(a.id==b.id,"health.id");c.Check(a.present==b.present,"health explicit presence");c.Check(a.hp==b.hp,"health.hp");
            c.Check(a.shield==b.shield,"health.shield");c.Check(a.revision==b.revision,"health.revision");
            c.Check(a.report==b.report,"health.report");c.Check(a.downed==b.downed,"health.downed");
        }
        static void CheckNovaPresentation(DangerChecks c)
        {
            var shot=new PresentationMessage{kind="oracle.shot.v1",run="0123456789abcdef0123456789abcdef",
                eventSequence=int.MaxValue,shotId=int.MaxValue,casterId=ulong.MaxValue,element=3,combo=4,
                origin=new Vector3(-12.123456f,1.65f,7.891234f),direction=new Vector3(.12345679f,.2345679f,.964111f).normalized,
                cosmicNova=true,radius=4.5f,point=new Vector3(-10.123456f,1.2345678f,9.891234f)};
            string json=JsonUtility.ToJson(shot);var result=JsonUtility.FromJson<PresentationMessage>(json);
            c.Check(Encoding.UTF8.GetByteCount(json)<=1024,"Nova presentation stays within existing cosmetic payload cap");
            c.Check(result!=null&&result.cosmicNova&&result.radius==shot.radius&&result.shotId==shot.shotId&&
                result.casterId==shot.casterId&&result.eventSequence==shot.eventSequence&&Same(result.point,shot.point)&&
                Same(result.origin,shot.origin)&&Same(result.direction,shot.direction),"Nova accepted shot and actual impact fields preserve");
            c.Check(JsonUtility.ToJson(result)==json,"Nova presentation private DTO byte-stable roundtrip");
            var valid=BuildDangerWireProbe().personal;
            c.Check(ValidPersonalPayload(valid),"valid captured Nova personal state");
            valid.novaMask=16;c.Check(!ValidPersonalPayload(valid),"out-of-range Nova bits rejected before progress mutation");valid.novaMask=3;
            foreach(float bad in new[]{float.NaN,float.PositiveInfinity,-.001f,4.001f})
            {valid.novaSeconds=bad;c.Check(!ValidPersonalPayload(valid),"invalid Nova clock rejected before progress mutation");}
        }
        static void CheckDangerLedgers(DangerChecks c)
        {
            var attack=new RemoteAttackPresentationLedger();
            c.Check(attack.Apply(1,1,2,.2f),"first committed phase");c.Check(!attack.Apply(1,1,2,.3f),"duplicate snapshot rejected");
            c.Check(!attack.Apply(0,1,2,.3f),"reordered snapshot rejected");c.Check(!attack.Apply(2,0,2,.3f),"older attack rejected");
            c.Check(!attack.Apply(2,1,2,float.NaN),"NaN progress rejected");c.Check(attack.Apply(2,1,3,.1f),"strike accepted");
            attack.Clear();c.Check(attack.Apply(1,1,2,.2f),"phase reset clears old sequence");
            var gate=new RemoteDangerRunGate();string a="aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",b="bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
            c.Check(gate.Prepare(a)&&gate.Accept(a,1),"current run accepted");c.Check(!gate.Accept(a,1),"run snapshot duplicate rejected");
            c.Check(gate.Prepare(b)&&!gate.Accept(a,2)&&gate.Accept(b,1),"new run fences old run");gate.EndRun();
            c.Check(!gate.Prepare(a)&&!gate.Prepare(b),"retired run cannot prepare again");gate.ResetConnection();
            c.Check(gate.Prepare(a),"fresh connection clears bounded run history");
            var ledger=new RemoteBoltLedger();var bolt=BuildDangerWireProbe().hostileBolts[0];bool impact;
            c.Check(ledger.Apply(bolt,out impact)&&!impact,"active bolt accepted");var terminal=bolt;terminal.active=false;terminal.impact=true;terminal.age=.1f;
            c.Check(ledger.Apply(terminal,out impact)&&impact,"terminal produces one impact");
            c.Check(!ledger.Apply(terminal,out impact)&&!impact&&!ledger.Apply(bolt,out impact),"terminal duplicate and resurrection rejected");
            bolt.generation++;c.Check(ledger.Apply(bolt,out impact),"pooled new generation accepted");
            c.Check(!ledger.Apply(terminal,out impact),"pooled old terminal rejected");ledger.Clear();c.Check(ledger.Apply(terminal,out impact)&&impact,"reset clears slot history");
            for(int i=-2;i<52;i++){var bad=bolt;bad.slot=i;c.Check(RemoteDangerPolicy.Bolt(bad)==(i>=0&&i<48),"slot bound");}
            for(int i=-2;i<11;i++){var bad=bolt;bad.lane=i;c.Check(RemoteDangerPolicy.Bolt(bad)==(i>=0&&i<8),"canonical eight lane bound");}
            foreach(float value in new[]{float.NaN,float.PositiveInfinity,float.NegativeInfinity,-.1f,1.1f})
                c.Check(!RemoteDangerPolicy.Phase(2,value,1),"malformed phase progress");
            var invalid=bolt;invalid.direction=Vector3.zero;c.Check(!RemoteDangerPolicy.Bolt(invalid),"zero bolt direction rejected");
            invalid=bolt;invalid.speed+=1f;c.Check(!RemoteDangerPolicy.Bolt(invalid),"forged bolt speed rejected");
            invalid=bolt;invalid.position=new Vector3(float.NaN,0,0);c.Check(!RemoteDangerPolicy.Bolt(invalid),"NaN endpoint rejected");
        }
        static void CheckMessage(Message a,Message b,DangerChecks c)
        {
            c.Check(a!=null&&b!=null,"Message exists");if(a==null||b==null)return;
            c.Check(string.Equals(a.kind,b.kind,StringComparison.Ordinal),"Message.kind");
            c.Check(string.Equals(a.run,b.run,StringComparison.Ordinal),"Message.run");
            c.Check(string.Equals(a.text,b.text,StringComparison.Ordinal),"Message.text");
            c.Check(a.sequence==b.sequence,"Message.sequence");
            c.Check(a.result==b.result,"Message.result");
            c.Check(a.element==b.element,"Message.element");
            c.Check(a.node==b.node,"Message.node");
            c.Check(a.dropId==b.dropId,"Message.dropId");
            c.Check(a.activeLanes==b.activeLanes,"Message.activeLanes");
            c.Check(a.wave==b.wave,"Message.wave");
            c.Check(a.spawned==b.spawned,"Message.spawned");
            c.Check(a.quota==b.quota,"Message.quota");
            c.Check(a.running==b.running,"Message.running");
            c.Check(a.clockStarted==b.clockStarted,"Message.clockStarted");
            c.Check(a.enabled==b.enabled,"Message.enabled");
            c.Check(a.controlSequence==b.controlSequence,"Message.controlSequence");
            c.Check(a.selectionSequence==b.selectionSequence,"Message.selectionSequence");
            c.Check(a.castSequence==b.castSequence,"Message.castSequence");
            c.Check(a.selectionMask==b.selectionMask,"Message.selectionMask");
            c.Check(a.remaining==b.remaining,"Message.remaining");
            c.Check(a.shrine==b.shrine,"Message.shrine");
            c.Check(Same(a.position,b.position),"Message.position");
            c.Check(Same(a.direction,b.direction),"Message.direction");
            c.Check(a.dangerVersion==b.dangerVersion,"Message.dangerVersion");
            c.Check(a.novaVersion==b.novaVersion,"Message.novaVersion private alias");
            c.Check(a.partyVersion==b.partyVersion,"Message.partyVersion");c.Check(a.admissionMask==b.admissionMask,"Message.admissionMask");
            c.Check(a.geometryMask==b.geometryMask,"Message.geometryMask");c.Check(a.correctionSequence==b.correctionSequence,"Message.correctionSequence");
            c.Check(a.health!=null&&b.health!=null&&a.health.Length==b.health.Length,"public health count");
            if(a.health!=null&&b.health!=null)for(int i=0;i<Math.Min(a.health.Length,b.health.Length);i++)CheckHealth(a.health[i],b.health[i],c);
            c.Check(a.towerVersion==b.towerVersion,"Message.towerVersion private alias");
            c.Check(a.forgeRequestId==b.forgeRequestId,"Message.forgeRequestId");
            c.Check(a.forgeExpectedTier==b.forgeExpectedTier,"Message.forgeExpectedTier");
            c.Check(a.forgeExpectedRevision==b.forgeExpectedRevision,"Message.forgeExpectedRevision");
            c.Check(a.forgeOutcome==b.forgeOutcome,"Message.forgeOutcome");
            c.Check(a.hostileCount==b.hostileCount,"Message.hostileCount");
            c.Check(a.enemies!=null&&b.enemies!=null&&a.enemies.Length==b.enemies.Length,"enemies count");
            if(a.enemies!=null&&b.enemies!=null)for(int i=0;i<Math.Min(a.enemies.Length,b.enemies.Length);i++)CheckEnemy(a.enemies[i],b.enemies[i],c);
            c.Check(a.buildings!=null&&b.buildings!=null&&a.buildings.Length==b.buildings.Length,"buildings count");
            if(a.buildings!=null&&b.buildings!=null)for(int i=0;i<Math.Min(a.buildings.Length,b.buildings.Length);i++)CheckBuilding(a.buildings[i],b.buildings[i],c);
            c.Check(a.players!=null&&b.players!=null&&a.players.Length==b.players.Length,"players count");
            if(a.players!=null&&b.players!=null)for(int i=0;i<Math.Min(a.players.Length,b.players.Length);i++)CheckPlayer(a.players[i],b.players[i],c);
            c.Check(a.drops!=null&&b.drops!=null&&a.drops.Length==b.drops.Length,"drops count");
            if(a.drops!=null&&b.drops!=null)for(int i=0;i<Math.Min(a.drops.Length,b.drops.Length);i++)CheckDrop(a.drops[i],b.drops[i],c);
            c.Check(a.hasPersonal==b.hasPersonal,"personal presence bit preserved");if(a.hasPersonal&&b.hasPersonal)CheckPlayer(a.personal,b.personal,c);
            c.Check(a.hostileBolts!=null&&b.hostileBolts!=null&&a.hostileBolts.Length==b.hostileBolts.Length,"hostile count");
            if(a.hostileBolts!=null&&b.hostileBolts!=null)for(int i=0;i<Math.Min(a.hostileBolts.Length,b.hostileBolts.Length);i++)CheckBolt(a.hostileBolts[i],b.hostileBolts[i],c);
        }
        static void CheckEnemy(EnemyState a,EnemyState b,DangerChecks c)
        {
            c.Check(a!=null&&b!=null,"Enemy exists");if(a==null||b==null)return;
            c.Check(a.id==b.id,"Enemy.id");
            c.Check(a.archetype==b.archetype,"Enemy.archetype");c.Check(a.lane==b.lane,"Enemy canonical lane");
            c.Check(a.shade==b.shade,"Enemy.shade");
            c.Check(a.flying==b.flying,"Enemy.flying");
            c.Check(Same(a.position,b.position),"Enemy.position");
            c.Check(a.hp==b.hp,"Enemy.hp");
            c.Check(a.maximum==b.maximum,"Enemy.maximum");
            c.Check(a.attackSequence==b.attackSequence,"Enemy.attackSequence");
            c.Check(a.attackPhase==b.attackPhase,"Enemy.attackPhase");
            c.Check(a.attackStyle==b.attackStyle,"Enemy.attackStyle");
            c.Check(a.attackProgress==b.attackProgress,"Enemy.attackProgress");
            c.Check(a.windupSeconds==b.windupSeconds,"Enemy.windupSeconds");
            c.Check(a.strikeSeconds==b.strikeSeconds,"Enemy.strikeSeconds");
            c.Check(a.recoverySeconds==b.recoverySeconds,"Enemy.recoverySeconds");
            c.Check(a.aggro==b.aggro,"Enemy.aggro");
            c.Check(Same(a.lockedDirection,b.lockedDirection),"Enemy.lockedDirection");
            c.Check(a.targetTowerId==b.targetTowerId,"Enemy.targetTowerId");
            c.Check(Same(a.targetPoint,b.targetPoint),"Enemy.targetPoint");
        }
        static void CheckBuilding(BuildingState a,BuildingState b,DangerChecks c)
        {
            c.Check(a!=null&&b!=null,"Building exists");if(a==null||b==null)return;
            c.Check(a.id==b.id,"Building.id");
            c.Check(a.element==b.element,"Building.element");
            c.Check(a.owner==b.owner,"Building.owner");
            c.Check(Same(a.position,b.position),"Building.position");
            c.Check(a.health==b.health,"Building.health");
            c.Check(a.tier==b.tier,"Building.tier");c.Check(a.tierRevision==b.tierRevision,"Building.tierRevision");
        }
        static void CheckPlayer(PlayerState a,PlayerState b,DangerChecks c)
        {
            c.Check(a!=null&&b!=null,"Player exists");if(a==null||b==null)return;
            c.Check(a.id==b.id,"Player.id");CheckHealth(a.health,b.health,c);
            c.Check(a.autoFire==b.autoFire,"Player.autoFire");
            c.Check(a.preparedElement==b.preparedElement,"Player.preparedElement");
            c.Check(a.selectionMask==b.selectionMask,"Player.selectionMask");
            c.Check(a.confirmedElement==b.confirmedElement,"Player.confirmedElement");
            c.Check(a.selectionSequence==b.selectionSequence,"Player.selectionSequence");
            c.Check(a.tokens==b.tokens,"Player.tokens");
            c.Check(a.forgeVersion==b.forgeVersion,"Player.forgeVersion");c.Check(a.forgeBank==b.forgeBank,"Player.forgeBank");
            c.Check(a.forgeTiers==b.forgeTiers,"Player.forgeTiers");c.Check(a.forgeRevision==b.forgeRevision,"Player.forgeRevision");
            c.Check(a.forgeLastRequest==b.forgeLastRequest,"Player.forgeLastRequest");c.Check(a.forgeOutcome==b.forgeOutcome,"Player.forgeOutcome");
            c.Check(a.element==b.element,"Player.element");
            c.Check(a.combo==b.combo,"Player.combo");
            c.Check(a.level==b.level,"Player.level");
            c.Check(a.xp==b.xp,"Player.xp");
            c.Check(a.points==b.points,"Player.points");
            c.Check(a.mask==b.mask,"Player.mask");
            c.Check(a.cooldown==b.cooldown,"Player.cooldown");
            c.Check(a.comboSeconds==b.comboSeconds,"Player.comboSeconds");
            c.Check(a.novaMask==b.novaMask,"Player.novaMask private alias");
            c.Check(a.novaSeconds==b.novaSeconds,"Player.novaSeconds private alias");
            c.Check(a.claimed!=null&&b.claimed!=null&&a.claimed.Length==b.claimed.Length,"claimed count");
            if(a.claimed!=null&&b.claimed!=null)for(int i=0;i<Math.Min(a.claimed.Length,b.claimed.Length);i++)c.Check(a.claimed[i]==b.claimed[i],"claimed item");
            c.Check(a.returnCooldowns!=null&&b.returnCooldowns!=null&&a.returnCooldowns.Length==b.returnCooldowns.Length,"returnCooldowns count");
            if(a.returnCooldowns!=null&&b.returnCooldowns!=null)for(int i=0;i<Math.Min(a.returnCooldowns.Length,b.returnCooldowns.Length);i++)c.Check(a.returnCooldowns[i]==b.returnCooldowns[i],"returnCooldowns item");
        }
        static void CheckDrop(DropState a,DropState b,DangerChecks c)
        {
            c.Check(a!=null&&b!=null,"Drop exists");if(a==null||b==null)return;
            c.Check(a.id==b.id,"Drop.id");
            c.Check(a.amount==b.amount,"Drop.amount");
            c.Check(Same(a.position,b.position),"Drop.position");
        }
        static void CheckBolt(HostileBoltState a,HostileBoltState b,DangerChecks c)
        {
            c.Check(a.slot==b.slot,"Bolt.slot");
            c.Check(a.generation==b.generation,"Bolt.generation");
            c.Check(a.sourceEnemyId==b.sourceEnemyId,"Bolt.sourceEnemyId");
            c.Check(a.attackSerial==b.attackSerial,"Bolt.attackSerial");
            c.Check(a.lane==b.lane,"Bolt.lane");
            c.Check(a.active==b.active,"Bolt.active");
            c.Check(a.impact==b.impact,"Bolt.impact");
            c.Check(Same(a.origin,b.origin),"Bolt.origin");
            c.Check(Same(a.position,b.position),"Bolt.position");
            c.Check(Same(a.direction,b.direction),"Bolt.direction");
            c.Check(Same(a.normal,b.normal),"Bolt.normal");
            c.Check(a.speed==b.speed,"Bolt.speed");
            c.Check(a.remaining==b.remaining,"Bolt.remaining");
            c.Check(a.age==b.age,"Bolt.age");
        }
    }
}
#endif
