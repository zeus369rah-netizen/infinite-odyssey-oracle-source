using Unity.Collections;
using Unity.Jobs;
using Unity.Netcode;
using UnityEngine;
using OdysseyOracle.Foundation.Pools015;

namespace OdysseyOracle
{
    public sealed partial class CoopDefenseDirector
    {
        NativeArray<XpEntitlement32> xpEntitlements015;
        NativeArray<uint> xpEntitlementDirty015;
        NativeArray<XpSource64> xpEffectiveSources015;
        int xpEntitlementCursor015;
        bool xpEntitlementTurn015;
        public int XpPublishedChildSources015{get;private set;}
        public int XpEntitledChildOrbs015{get;private set;}
        public int XpAcceptedChildClaims015{get;private set;}
        public bool TryReadXpEntitlement015(int parentId,out XpEntitlement32 value)
        {
            value=default;if(!xpEntitlements015.IsCreated||parentId<1||parentId>XpCatchupOrbs015.ParentCapacity)return false;
            CompleteXpReaders015();value=xpEntitlements015[parentId-1];return XpCatchupOrbs015.Valid(value);
        }
        void InitializeXpCatchup015()
        {
            if(CampaignStagePacing.LastRewardId(8)!=XpCatchupOrbs015.ParentCapacity)throw new System.InvalidOperationException("Catchup source schema differs from campaign.");
            xpEntitlements015=new NativeArray<XpEntitlement32>(XpCatchupOrbs015.ParentCapacity,Allocator.Persistent);
            xpEntitlementDirty015=new NativeArray<uint>(XpCatchupOrbs015.ParentCapacity,Allocator.Persistent);
            xpEffectiveSources015=new NativeArray<XpSource64>(PoolXpMath015.SourceCapacity,Allocator.Persistent);
            new ClearXpCatchup015{Entitlements=xpEntitlements015,Dirty=xpEntitlementDirty015}.Schedule().Complete();
        }
        void ResetXpCatchup015(bool keep)
        {
            CompleteXpReaders015();
            if(!keep){new ClearXpCatchup015{Entitlements=xpEntitlements015,Dirty=xpEntitlementDirty015}.Schedule().Complete();XpPublishedChildSources015=XpEntitledChildOrbs015=XpAcceptedChildClaims015=0;}
            else new ClearXpVersions015{Versions=xpEntitlementDirty015}.Schedule().Complete();
            xpEntitlementCursor015=0;xpEntitlementTurn015=false;
        }
        void RequeueXpCatchup015()
        {
            CompleteXpReaders015();xpEntitlementCursor015=0;xpEntitlementTurn015=false;
            if(IsAuthority)new RequeueXpCatchup015{Entitlements=xpEntitlements015,Dirty=xpEntitlementDirty015}.Schedule().Complete();
            else new ClearXpVersions015{Versions=xpEntitlementDirty015}.Schedule().Complete();
        }
        void ScheduleXpCatchupProjection015()
        {xpReaders015=new ProjectXpCatchupSources015{Sources=xpSources015,Entitlements=xpEntitlements015,Projection=xpEffectiveSources015}.Schedule(xpReaders015);}
        bool CatchupSourceInStage015(uint sourceId)=>XpCatchupOrbs015.InStage((int)sourceId,campaign.StageNumber);
        bool CanRequestCatchupSource015(int sourceId)
        {
            CompleteXpReaders015();int parentId=XpCatchupOrbs015.ParentId(sourceId);if(parentId==0)return false;
            var source=xpSources015[sourceId-1];uint effective=XpCatchupOrbs015.EffectiveMask(source,xpSources015[parentId-1],xpEntitlements015[parentId-1]);
            return (effective&1)!=0&&source.Owner0==rooms.LocalClientId||(effective&2)!=0&&source.Owner1==rooms.LocalClientId;
        }
        bool PrepareCatchupChild015(in XpSource64 parent,in XpEntitlement32 next,int tier,out XpSource64 child,out bool create)
        {
            child=default;create=false;uint required=1u<<(tier-1);
            if(((XpCatchupOrbs015.TierMask(next,0)|XpCatchupOrbs015.TierMask(next,1))&required)==0)return true;
            if(!XpCatchupOrbs015.TryChild(parent,tier,out child))return false;
            var previous=xpSources015[(int)child.SourceId-1];
            if(previous.SourceId==0){create=true;return true;}
            return PoolXpMath015.SourceValid(previous,CampaignStagePacing.MaximumBaseXp)&&XpCatchupOrbs015.IsChildOf(previous,parent);
        }
        void CommitCatchupChild015(in XpSource64 child,bool create)
        {
            if(!create)return;int index=(int)child.SourceId-1;xpSources015[index]=child;xpValueRevision015[index]=1;
            xpDirty015[index]|=child.EligibleMask;EnqueueXpAck015(index);MarkXpSourceReady015(index);
            XpPublishedSources015++;XpPublishedChildSources015++;XpPublishedBaseValue015+=child.BaseXp;
        }
        bool TryClaimCatchupOrb015(ulong sender,int sourceId)
        {
            if(SoloRestartSuppressesCurrentRewards||!EnsureXpRun015()||!IsAuthority||!owner.SimulationActive||sourceId<1||sourceId>xpSources015.Length||
                !CatchupSourceInStage015((uint)sourceId)||!peers.TryGetValue(sender,out Peer peer)||!peer.ArenaReady)return false;
            CompleteXpReaders015();int index=sourceId-1,parentId=XpCatchupOrbs015.ParentId(sourceId),parentIndex=parentId-1;
            var source=xpSources015[index];var parent=xpSources015[parentIndex];var entitlement=xpEntitlements015[parentIndex];
            int slot=(source.EligibleMask&1)!=0&&source.Owner0==sender?0:(source.EligibleMask&2)!=0&&source.Owner1==sender?1:-1;
            if(slot<0||!PoolXpMath015.SourceValid(source,CampaignStagePacing.MaximumBaseXp))return false;uint bit=1u<<slot;
            if((source.ClaimedMask&bit)!=0)
            {
                xpDirty015[index]|=bit;EnqueueXpAck015(index);
                if(sourceId==parentId&&XpCatchupOrbs015.Valid(entitlement))xpEntitlementDirty015[parentIndex]|=bit;
                return true;
            }
            if(owner.Revival!=null&&owner.Revival.IsPeerDowned(sender)||!rooms.TryGetPlayerPose(sender,out Vector3 feet,out _)||
                !PickupReachRules.WithinReach((feet-new Vector3(source.X,source.Y,source.Z)).sqrMagnitude,peer.Mask))return false;
            Transform collector=sender==rooms.LocalClientId&&owner.Player!=null?owner.Player.transform:null;
            if(!xpClaimVisibility.IsClear(feet+Vector3.up*.95f,new Vector3(source.X,source.Y+.4f,source.Z),collector)||
                !XpCatchupOrbs015.TryQuote(source,parent,entitlement,slot,sender,peer.Level,campaign.Epoch,xpWireGeneration015,out var quote)||
                xpValueRevision015[index]<1||xpValueRevision015[index]>=3||xpSourceRevision015==uint.MaxValue||peer.ProgressRevision015==int.MaxValue||
                !XpLedgerMath015.AdvanceProgress(peer.Level,peer.Xp,peer.Points,quote.TotalXp,out int level,out int xp,out int points)||
                !XpCatchupOrbs015.TryMark(ref source,parent,entitlement,quote))return false;
            XpEntitlement32 next=entitlement;XpSource64 a=default,b=default,c=default;bool ca=false,cb=false,cc=false;
            bool isBase=sourceId==parentId;
            if(isBase)
            {
                // Resolve with pre-advance host level, on the already marked local parent copy.
                if(!XpCatchupOrbs015.TryResolve(source,entitlement,slot,sender,peer.Level,out next)||
                    !PrepareCatchupChild015(source,next,1,out a,out ca)||!PrepareCatchupChild015(source,next,2,out b,out cb)||
                    !PrepareCatchupChild015(source,next,3,out c,out cc)||!peer.Forge.TryCreditAcceptedDrop(parentId))return false;
            }
            peer.Level=level;peer.Xp=xp;peer.Points=points;xpSources015[index]=source;xpDirty015[index]|=source.EligibleMask;xpValueRevision015[index]++;
            if(isBase)
            {
                xpEntitlements015[parentIndex]=next;xpEntitlementDirty015[parentIndex]|=next.OwnerMask;
                uint unlocked=XpCatchupOrbs015.TierMask(next,slot);XpEntitledChildOrbs015+=(unlocked&1)!=0?1:0;XpEntitledChildOrbs015+=(unlocked&2)!=0?1:0;XpEntitledChildOrbs015+=(unlocked&4)!=0?1:0;
                CommitCatchupChild015(a,ca);CommitCatchupChild015(b,cb);CommitCatchupChild015(c,cc);
            }
            if(!isBase)XpAcceptedChildClaims015++;
            XpAcceptedClaims015++;XpAwardedValue015+=quote.TotalXp;EnqueueXpAck015(index);BumpXpRevision015();
            if(sender==rooms.LocalClientId)ApplyLocalXpProgress015(peer);return true;
        }
        bool ReceiveCatchupFrame015(ulong sender,in XpFrame015 frame)
        {
            CompleteXpReaders015();int index=(int)frame.SourceId-1;if(index<0||index>=xpEntitlements015.Length)return false;
            var previous=xpEntitlements015[index];
            if(IsAuthority)
            {
                if(frame.Kind!=XpFrameRules015.EntitlementAck||!XpCatchupOrbs015.Valid(previous)||frame.ValueRevision!=previous.Revision)return false;
                uint bit=XpCatchupOrbs015.AckBit(previous,sender,frame.ValueRevision);
                if(bit==0)return false;xpEntitlementDirty015[index]&=~bit;return true;
            }
            if(sender!=NetworkManager.ServerClientId||frame.Kind!=XpFrameRules015.Entitlement||!XpCatchupFrameRules015.ShapeEntitlement(frame))return false;
            var next=XpCatchupFrameRules015.Entitlement(frame);
            bool belongs=(next.OwnerMask&1)!=0&&next.Owner0==rooms.LocalClientId||(next.OwnerMask&2)!=0&&next.Owner1==rooms.LocalClientId;
            if(!belongs||!XpCatchupOrbs015.Monotonic(previous,next))return false;
            var parent=xpSources015[index];
            if(parent.SourceId!=0&&(parent.SourceId!=next.ParentId||parent.EligibleMask!=next.OwnerMask||parent.Owner0!=next.Owner0||parent.Owner1!=next.Owner1))return false;
            xpEntitlements015[index]=next;xpEntitlementDirty015[index]=1;
            if(previous.Packed!=next.Packed||previous.Revision!=next.Revision)BumpXpRevision015();return true;
        }
        bool PumpCatchupFrame015()
        {
            CompleteXpReaders015();
            for(int examined=0;examined<64;examined++)
            {
                int index=xpEntitlementCursor015++;if(xpEntitlementCursor015==XpCatchupOrbs015.ParentCapacity)xpEntitlementCursor015=0;
                var value=xpEntitlements015[index];uint dirty=xpEntitlementDirty015[index];if(dirty==0||!XpCatchupOrbs015.Valid(value))continue;
                if(!IsAuthority)
                {
                    var ack=new XpFrame015{Magic=XpFrameRules015.Magic,Version=XpFrameRules015.Version,Kind=XpFrameRules015.EntitlementAck,RunLow=xpRunLow015,RunHigh=xpRunHigh015,
                        RunEpoch=campaign.Epoch,RewardGeneration=xpWireGeneration015,Sequence=xpSendSequence015+1,SourceId=value.ParentId,ValueRevision=value.Revision};
                    if(!rooms.SendXp015(ack,NetworkManager.ServerClientId))return true;xpSendSequence015++;xpEntitlementDirty015[index]=0;return true;
                }
                var parent=xpSources015[index];if(!XpCatchupOrbs015.Matches(value,parent))continue;
                for(int slot=0;slot<2;slot++)
                {
                    uint bit=1u<<slot;if((dirty&bit)==0)continue;ulong recipient=slot==0?value.Owner0:value.Owner1;
                    if(recipient==rooms.LocalClientId){xpEntitlementDirty015[index]&=~bit;continue;}
                    if(!peers.TryGetValue(recipient,out Peer peer)||!peer.ArenaReady)continue;
                    var frame=new XpFrame015{Magic=XpFrameRules015.Magic,Version=XpFrameRules015.Version,Kind=XpFrameRules015.Entitlement,RunLow=xpRunLow015,RunHigh=xpRunHigh015,
                        RunEpoch=campaign.Epoch,RewardGeneration=xpWireGeneration015,Sequence=xpSendSequence015+1,SourceId=value.ParentId,ValueRevision=value.Revision,
                        BaseXp=parent.BaseXp,OriginWave=parent.OriginWave,EligibleMask=value.OwnerMask,Owner0=value.Owner0,Owner1=value.Owner1,ClaimedMask=value.Packed};
                    if(!rooms.SendXp015(frame,recipient))return true;xpSendSequence015++;return true;
                }
            }
            return false;
        }
        void DisposeXpCatchup015()
        {
            CompleteXpReaders015();if(xpEffectiveSources015.IsCreated)xpEffectiveSources015.Dispose();
            if(xpEntitlements015.IsCreated)xpEntitlements015.Dispose();if(xpEntitlementDirty015.IsCreated)xpEntitlementDirty015.Dispose();
        }
    }
}
// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Actual accepted source/run/epoch and original pickup point; per-owner pre-advance host level resolves immutable bonus tiers.
// Array Formats:905entitlement32/dirty uints,4096readonly projection; same128B typed metadata/ACK frames, bounded64slot scans and existing persistent writer.
// Dependencies/Pointers: Sole XP owner retains authority/reach/LOS/downed/replay fences; synchronous preflight then base-onlyForge/individualXP commits. Reader fence precedes sidecar/source writes/disposal; no new sockets/hotJSON/visual-owner replacement.
