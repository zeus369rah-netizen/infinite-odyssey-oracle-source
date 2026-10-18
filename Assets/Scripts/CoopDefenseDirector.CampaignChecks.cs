#if UNITY_EDITOR
using System;
using System.Text;
using UnityEngine;

namespace OdysseyOracle
{
    public sealed partial class CoopDefenseDirector
    {
        // # ARCHITECTURE SNAPSHOT
        // Root-run only. Actual private Message JSON, no scene, network, profile
        // or gameplay mutation. Actor/source maxima remain511; ONLY drops scope.
        [Serializable] public sealed class CampaignWireReceipt
        {
            public string status,utc;
            public int assertions,failures,unionUtf8Bytes,actualStateUtf8Bytes,progressUtf8Bytes,maxRewardId;
            public bool nativeJsonExecuted,networkExecuted;
            public string[] failureDetails;
        }
        public static CampaignWireReceipt RunCampaignWireAcceptance()
        {
            var result=new CampaignWireReceipt{utc=DateTime.UtcNow.ToString("o")};
            var checks=new DangerChecks();
            try
            {
                var before=BuildDangerWireProbe();
                before.campaign="fedcba9876543210fedcba9876543210";
                before.campaignStage=3;before.campaignEpoch=int.MaxValue;
                before.campaignVersion=CoopCampaignDirector.ProtocolVersion;
                before.personal.level=50;before.personal.xp=0;before.personal.points=37;before.personal.mask=4095;
                for(int i=0;i<128;i++)
                {
                    before.drops[i].id=1408+i;
                    before.personal.claimed[i]=1408+i;
                }
                before.dropId=CampaignQuestState.ScopedDropId(3,511);
                var progress=new CampaignProgressSnapshot{id=ulong.MaxValue,revision=int.MaxValue,level=50,xp=0,points=37,mask=4095,
                    forgeVersion=MatchTowerForgeRules.Version,forgeBank=MatchTowerForgeRules.MaximumBalance,forgeTiers=170,
                    forgeRevision=MatchTowerForgeRules.MaximumRevision,forgeLastRequest=uint.MaxValue,forgeOutcome=(int)MatchForgeOutcome.Applied};
                before.campaignProgress=progress;
                before.sequence=int.MaxValue;
                var json=JsonUtility.ToJson(before);result.nativeJsonExecuted=true;
                result.unionUtf8Bytes=Encoding.UTF8.GetByteCount(json);
                var after=JsonUtility.FromJson<Message>(json);
                CheckMessage(before,after,checks);
                CheckCampaignFields(before,after,checks);
                checks.Check(after!=null&&ValidPersonalPayload(after.personal),"Level50/37unusedpoints and stage3 claim IDs remain real valid arena personal");
                checks.Check(result.unionUtf8Bytes<RoomConnection.MaximumReliablePayloadBytes,"max campaign metadata+128 stage3drops+48actors+48bolts+8health+fullpersonal+progress below64KiB UTF8");
                checks.Check(JsonUtility.ToJson(after)==json,"full010 private DTO byte-stable roundtrip");
                foreach(var actor in before.enemies)checks.Check(actor.id>0&&actor.id<=RemoteDangerPolicy.MaximumActorId,"actor IDs remain stage-local<=511");
                foreach(var bolt in before.hostileBolts)checks.Check(bolt.sourceEnemyId>0&&bolt.sourceEnemyId<=RemoteDangerPolicy.MaximumActorId,"bolt source remains stage-local<=511");
                checks.Check(before.dropId==1535,"only reward identity uses stage3 maximum1535");
                result.maxRewardId=before.dropId;
                before.hasPersonal=false;before.personal=null;before.campaignProgress=null;before.text=string.Empty;
                json=JsonUtility.ToJson(before);result.actualStateUtf8Bytes=Encoding.UTF8.GetByteCount(json);
                after=JsonUtility.FromJson<Message>(json);
                CheckCampaignFields(before,after,checks);
                checks.Check(!HasPersonalPayload(after),"absent arena personal still guarded");
                checks.Check(after.campaignProgress==null||!CampaignProgressSnapshot.Valid(after.campaignProgress),"default-materialized absent refuge progress cannot validate");
                checks.Check(result.actualStateUtf8Bytes<RoomConnection.MaximumReliablePayloadBytes,"actual010 common state below64KiB");
                var envelope=new Message{kind="oracle.campaign.progress.v1",run=before.campaign+".refuge.3",campaign=before.campaign,
                    campaignStage=3,campaignEpoch=int.MaxValue,campaignVersion=CoopCampaignDirector.ProtocolVersion,campaignProgress=progress};
                json=JsonUtility.ToJson(envelope);result.progressUtf8Bytes=Encoding.UTF8.GetByteCount(json);
                var decoded=JsonUtility.FromJson<Message>(json);
                CheckCampaignFields(envelope,decoded,checks);
                checks.Check(decoded.kind==envelope.kind&&decoded.run==envelope.run&&CampaignProgressSnapshot.Valid(decoded.campaignProgress),"actual progress-only envelope roundtrips without arena health");
                checks.Check(result.progressUtf8Bytes<RoomConnection.MaximumReliablePayloadBytes,"progress-only envelope fits transport");
                decoded.campaignProgress.revision=0;
                checks.Check(!CampaignProgressSnapshot.Valid(decoded.campaignProgress),"missing monotonic progress revision fails");
                var absent=JsonUtility.FromJson<Message>("{\"kind\":\"oracle.campaign.progress.v1\"}");
                checks.Check(absent.campaignVersion!=CoopCampaignDirector.ProtocolVersion&&
                    (absent.campaignProgress==null||!CampaignProgressSnapshot.Valid(absent.campaignProgress)),"absent campaign metadata/progress cannot be routed as valid");
            }
            catch(Exception error){checks.Check(false,error.GetType().Name+": "+error.Message);}
            result.assertions=checks.Count;result.failures=checks.Failed;result.failureDetails=checks.Details.ToArray();
            result.status=!result.nativeJsonExecuted?"INCOMPLETE_NATIVE_JSON":checks.Failed==0?"PASSED_NATIVE_010_CAMPAIGN_WIRE_NOT_NETWORK":"FAILED_NATIVE_010_CAMPAIGN_WIRE";
            return result;
        }
        static void CheckCampaignFields(Message before,Message after,DangerChecks checks)
        {
            checks.Check(after!=null,"decoded010 message exists");if(after==null)return;
            checks.Check(before.campaign==after.campaign&&before.campaignStage==after.campaignStage&&
                before.campaignEpoch==after.campaignEpoch&&before.campaignVersion==after.campaignVersion,"nondefault campaign/stage/epoch/version serialized exactly");
            if(before.campaignProgress==null)return;
            var a=before.campaignProgress;var b=after.campaignProgress;
            checks.Check(b!=null,"explicit refuge progress exists");if(b==null)return;
            checks.Check(a.id==b.id&&a.revision==b.revision&&a.level==b.level&&a.xp==b.xp&&a.points==b.points&&a.mask==b.mask,
                "own progress identity/revision/legacy XP fields preserved");
            checks.Check(a.forgeVersion==b.forgeVersion&&a.forgeBank==b.forgeBank&&a.forgeTiers==b.forgeTiers&&a.forgeRevision==b.forgeRevision&&
                a.forgeLastRequest==b.forgeLastRequest&&a.forgeOutcome==b.forgeOutcome,"owned Forge bank/ranks/request proof preserved");
        }
    }
}
#endif
