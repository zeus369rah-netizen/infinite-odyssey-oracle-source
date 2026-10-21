using System;
using Unity.Collections;
using Unity.Jobs;
using Unity.Netcode;
using UnityEngine;
using OdysseyOracle.Foundation.Pools015;
#if UNITY_EDITOR
using XpOwnerReload015 = UnityEditor.AssemblyReloadEvents.AssemblyReloadCallback;
#else
using XpOwnerReload015 = System.Action;
#endif

namespace OdysseyOracle
{
    public sealed partial class CoopDefenseDirector
    {
        NativeArray<XpSource64> xpSources015;
        NativeArray<uint> xpDirty015;
        NativeArray<int> xpUrgent015;
        NativeArray<uint> xpValueRevision015;
        JobHandle xpReaders015;
        string xpCampaign015, xpRun015;
        ulong xpRunLow015, xpRunHigh015;
        uint xpSendSequence015, xpReceiveSequence015, xpSourceRevision015;
        int xpWireGeneration015, xpPumpCursor015, xpAppliedProgress015;
        int xpUrgentHead015, xpUrgentTail015, xpUrgentCount015;
        bool xpLedgerReady015, xpFault015;
        static readonly DropState[] NoCampaignDrops015 = Array.Empty<DropState>();
        XpOwnerReload015 xpOwnerReload015;
        bool xpOwnerSubscribed015;
        public bool CampaignXp015Active => CampaignActive && xpLedgerReady015;
        public bool XpConservationFault015 => xpFault015;
        public int XpReservedSources015 { get; private set; }
        public int XpPublishedSources015 { get; private set; }
        public int XpAcceptedClaims015 { get; private set; }
        public long XpPublishedBaseValue015 { get; private set; }
        public long XpAwardedValue015 { get; private set; }
        public bool TryReadXpSource015(int sourceId,out XpSource64 source)
        {
            source=default;
            if(!xpLedgerReady015||sourceId<1||sourceId>xpSources015.Length)return false;
            CompleteXpReaders015();source=xpSources015[sourceId-1];return source.SourceId==sourceId;
        }

        void InitializeXp015()
        {
            if (xpLedgerReady015) return;
            try
            {
                xpSources015 = new NativeArray<XpSource64>(PoolXpMath015.SourceCapacity, Allocator.Persistent);
                xpDirty015 = new NativeArray<uint>(PoolXpMath015.SourceCapacity, Allocator.Persistent);
                xpUrgent015 = new NativeArray<int>(PoolXpMath015.SourceCapacity, Allocator.Persistent);
                xpValueRevision015 = new NativeArray<uint>(PoolXpMath015.SourceCapacity, Allocator.Persistent);
                InitializeXpCatchup015();
                InitializeXpPresentation015();
                PrimeXpJobs015();
                xpLedgerReady015 = true;
                if(xpOwnerReload015==null)xpOwnerReload015=DisposeXp015;
                LightPoolLifecycle.SubscribeBeforeReload(xpOwnerReload015);xpOwnerSubscribed015=true;
            }
            catch { DisposeXp015(); throw; }
        }
        void CompleteXpReaders015() { xpReaders015.Complete(); xpReaders015 = default; }
        void PrimeXpJobs015()
        {
            new ClearXpLedgerJob015{Sources=xpSources015,Dirty=xpDirty015}.Schedule().Complete();
            new ClearXpVersions015{Versions=xpValueRevision015}.Schedule().Complete();
            new RebindXpLeaseJob015{Sources=xpSources015,Dirty=xpDirty015,Epoch=1,Generation=1}.Schedule().Complete();
            RebuildXpClusters015();
            new BuildXpOrbMatrices015{Clusters=xpClusters015,Sources=xpEffectiveSources015,ClusterStatus=xpClusterStatus015,
                Matrices=xpNumericMatrices015,ClusterIds=xpClusterIds015,Count=xpMatrixCount015,WrappedSeconds=0,
                RecipientBit=1,AuthenticatedRecipientId=0}.Schedule().Complete();
            new CopyXpMatrices015{Input=xpNumericMatrices015,Count=xpMatrixCount015,SourceIds=xpClusterIds015,Sources=xpEffectiveSources015,Output=xpUnityMatrices015}.Schedule().Complete();
            new FindNearestXpSource015{Sources=xpEffectiveSources015,Cells=xpCells015,ReadyAt=xpReadyAt015,RetryAt=xpRetryAt015,
                Result=xpNearest015,Feet=default,Radius=4f,Now=0f,Recipient=0,MaximumBaseXp=CampaignStagePacing.MaximumBaseXp}.Schedule().Complete();
        }
        void ResetXpLedger015(bool preserve)
        {
            if (!xpLedgerReady015) return;
            CompleteXpReaders015();
            string context = CampaignActive ? campaign.CampaignId : null;
            bool keep = preserve && context != null && context == xpCampaign015;
            if (!keep)
            {
                new ClearXpLedgerJob015 { Sources = xpSources015, Dirty = xpDirty015 }.Schedule().Complete();
                new ClearXpVersions015{Versions=xpValueRevision015}.Schedule().Complete();
                xpFault015 = false;
                XpReservedSources015=XpPublishedSources015=XpAcceptedClaims015=0;
                XpPublishedBaseValue015=XpAwardedValue015=0;
            }
            ResetXpCatchup015(keep);
            xpCampaign015 = context; xpRun015 = null; xpRunLow015 = xpRunHigh015 = 0;
            xpSendSequence015 = xpReceiveSequence015 = 0; xpWireGeneration015 = xpPumpCursor015 = xpAppliedProgress015 = 0;
            xpUrgentHead015 = xpUrgentTail015 = xpUrgentCount015 = 0;
            BumpXpRevision015();
        }
        bool EnsureXpRun015()
        {
            if (!xpLedgerReady015 || xpFault015 || !CampaignActive || !Running || !clockStarted ||
                owner.World == null || !owner.World.IsCoopArena || !ArenaImpactFrameRules.TryRun(runId, out ulong low, out ulong high)) return false;
            if (xpRun015 == runId) return true;
            CompleteXpReaders015();
            xpRun015 = runId; xpRunLow015 = low; xpRunHigh015 = high;
            xpSendSequence015 = xpReceiveSequence015 = 0; xpAppliedProgress015 = 0;
            xpUrgentHead015 = xpUrgentTail015 = xpUrgentCount015 = 0;
            xpWireGeneration015 = IsAuthority ? xpRewardGeneration015 : 0;
            RequeueXpCatchup015();
            if (IsAuthority)
            {
                if (xpWireGeneration015 <= 0) return false;
                new RebindXpLeaseJob015 { Sources = xpSources015, Dirty = xpDirty015,
                    Epoch = campaign.Epoch, Generation = xpWireGeneration015 }.Schedule().Complete();
                BumpXpRevision015();
            }
            return true;
        }
        bool BumpXpRevision015()
        {
            if (xpSourceRevision015 == uint.MaxValue) { xpFault015 = true; return false; }
            xpSourceRevision015++; return true;
        }
        bool TryPrepareAdmissionXp015(int actorId, out XpSource64 reservation)
        {
            reservation = default;
            if (SoloRestartSuppressesCurrentRewards) return true; // Explicit accepted replay cutoff, no new source value.
            if (!EnsureXpRun015() || !TryGetXpWaveContext(out int epoch, out int wave, out int generation)) return false;
            int sourceId = CampaignQuestState.ScopedDropId(campaign.StageNumber, actorId);
            if (sourceId < 1 || sourceId > xpSources015.Length) return false;
            CompleteXpReaders015();
            if (xpSources015[sourceId - 1].SourceId != 0) return false;
            ulong owner0 = 0, owner1 = 0; uint eligible = 0;
            var connected = rooms.ConnectedClientIds;
            for (int i = 0; i < connected.Count; i++)
            {
                ulong id = connected[i];
                if (!peers.TryGetValue(id, out Peer peer) || !peer.ArenaReady ||
                    !rooms.TryGetAdmissionSlot(id, out int slot) || slot < 0 || slot > 1) return false;
                if (slot == 0) owner0 = id; else owner1 = id;
                eligible |= 1u << slot;
            }
            if (eligible == 0 || eligible > 3) return false;
            return XpLedgerMath015.TryReserve(sourceId,wave,campaignAdmissions.WaveAccepted,epoch,generation,
                owner0,owner1,eligible,out reservation); // Flags0: reserved, not visible/claimable.
        }
        bool BindAdmissionXp015(OracleEnemy actor, in XpSource64 reservation)
        {
            if (reservation.SourceId == 0) return SoloRestartSuppressesCurrentRewards;
            if (actor == null || actor.PoolGeneration015 == 0 || !xpLedgerReady015) return false;
            CompleteXpReaders015();
            int index = (int)reservation.SourceId - 1;
            if (xpSources015[index].SourceId != 0 || !actor.BindReward015(new XpAdmissionTicket015(index + 1,
                reservation.RunEpoch, reservation.RewardGeneration, reservation.OriginWave, reservation.BaseXp, actor.PoolGeneration015))) return false;
            xpSources015[index] = reservation;XpReservedSources015++; return BumpXpRevision015();
        }
        internal void RegisterDefeatedEnemyXp015(OracleEnemy actor)
        {
            if (!CampaignActive) { if (actor != null) RegisterXpDrop(actor.TargetId, actor.transform.position, 1); return; }
            if (SoloRestartSuppressesCurrentRewards || !EnsureXpRun015() || !IsAuthority || actor == null || actor.Alive ||
                !actor.TryReadReward015(out XpAdmissionTicket015 ticket) ||
                ticket.RunEpoch != campaign.Epoch || ticket.RewardGeneration != xpWireGeneration015) return;
            CompleteXpReaders015(); int index = ticket.SourceId - 1;
            var source = xpSources015[index];
            if (source.SourceId != ticket.SourceId || source.Flags != 0 || source.BaseXp != ticket.BaseXp ||
                source.OriginWave != ticket.OriginWave || source.RunEpoch != ticket.RunEpoch || source.RewardGeneration != ticket.RewardGeneration) return;
            Vector3 position = actor.transform.position;
            if (!PoolXpMath015.Position(position.x, position.y, position.z)) { xpFault015 = true; return; }
            if(!XpLedgerMath015.Publish(ref source,ticket,position.x,position.y,position.z)){xpFault015=true;return;}
            xpSources015[index] = source; xpDirty015[index] |= source.EligibleMask; BumpXpRevision015();
            xpValueRevision015[index]=1;
            XpPublishedSources015++;XpPublishedBaseValue015+=source.BaseXp;
            MarkXpSourceReady015(index);
        }
        bool TryClaimXp015(ulong sender,int sourceId)=>TryClaimCatchupOrb015(sender,sourceId);
        void ApplyLocalXpProgress015(Peer peer)
        {
            if (peer.ProgressRevision015 == int.MaxValue) { xpFault015 = true; return; }
            int revision = ++peer.ProgressRevision015;
            if (!localForge.ApplyHostState(peer.Forge.Snapshot)) { xpFault015 = true; return; }
            owner.Progression.ApplyAuthoritativeState(peer.Level, peer.Xp, peer.Points, peer.Mask);
            xpAppliedProgress015 = revision;
            CacheAcceptedSoloRestartProgress(peer.Level, peer.Xp, peer.Points, peer.Mask);
        }
        void ApplyLegacyProgressRevision015(PlayerState state)
        {
            // Health/cooldown/Forge receipt handling remains on its original path.
            // A delayed full-personal JSON cannot roll back a newer binary XP progress state.
            if(CampaignActive && state.progressRevision015<=xpAppliedProgress015)return;
            owner.Progression.ApplyAuthoritativeState(state.level,state.xp,state.points,state.mask);
            if(CampaignActive)
            {
                xpAppliedProgress015=state.progressRevision015;
                CacheAcceptedSoloRestartProgress(state.level,state.xp,state.points,state.mask);
            }
        }
        void OnXpFrame015(ulong sender, XpFrame015 frame)
        {
            if (!XpFrameRules015.Shape(frame)||!EnsureXpRun015() || frame.RunLow != xpRunLow015 || frame.RunHigh != xpRunHigh015 ||
                frame.RunEpoch != campaign.Epoch || frame.Sequence <= xpReceiveSequence015 ||!CatchupSourceInStage015(frame.SourceId)) return;
            if(frame.Kind==XpFrameRules015.Entitlement||frame.Kind==XpFrameRules015.EntitlementAck)
            {
                if(IsAuthority){if(frame.RewardGeneration!=xpWireGeneration015)return;}
                else
                {
                    if(sender!=NetworkManager.ServerClientId||xpWireGeneration015!=0&&frame.RewardGeneration!=xpWireGeneration015)return;
                    if(xpWireGeneration015==0)
                    {
                        xpWireGeneration015=frame.RewardGeneration;
                        new RebindXpLeaseJob015{Sources=xpSources015,Dirty=xpDirty015,Epoch=campaign.Epoch,Generation=xpWireGeneration015}.Schedule().Complete();
                    }
                }
                if(ReceiveCatchupFrame015(sender,frame))xpReceiveSequence015=frame.Sequence;return;
            }
            if (IsAuthority)
            {
                if (frame.RewardGeneration != xpWireGeneration015) return;
                if(frame.Kind==XpFrameRules015.Ack)
                {
                    CompleteXpReaders015();int ackIndex=(int)frame.SourceId-1;var value=xpSources015[ackIndex];
                    uint bit=(value.EligibleMask&1u)!=0&&value.Owner0==sender?1u:(value.EligibleMask&2u)!=0&&value.Owner1==sender?2u:0u;
                    if(bit==0||frame.ValueRevision!=xpValueRevision015[ackIndex])return;
                    xpDirty015[ackIndex]&=~bit;xpReceiveSequence015=frame.Sequence;return;
                }
                if(frame.Kind!=XpFrameRules015.Claim)return;
                xpReceiveSequence015 = frame.Sequence; TryClaimXp015(sender, (int)frame.SourceId); return;
            }
            if (sender != NetworkManager.ServerClientId || frame.Kind != XpFrameRules015.Source ||
                !XpFrameRules015.Shape(frame) || (frame.EligibleMask & 1) == 0 && (frame.EligibleMask & 2) == 0) return;
            bool belongs = (frame.EligibleMask & 1) != 0 && frame.Owner0 == rooms.LocalClientId ||
                (frame.EligibleMask & 2) != 0 && frame.Owner1 == rooms.LocalClientId;
            if (!belongs || xpWireGeneration015 != 0 && xpWireGeneration015 != frame.RewardGeneration) return;
            CompleteXpReaders015();
            if (xpWireGeneration015 == 0)
            {
                xpWireGeneration015 = frame.RewardGeneration;
                new RebindXpLeaseJob015 { Sources = xpSources015, Dirty = xpDirty015,
                    Epoch = campaign.Epoch, Generation = xpWireGeneration015 }.Schedule().Complete();
            }
            int index = (int)frame.SourceId - 1; var previous = xpSources015[index]; var source = XpFrameRules015.ToSource(frame);
            if (previous.SourceId != 0 && (!XpLedgerMath015.SameImmutableValue(previous,source)||(previous.ClaimedMask&~source.ClaimedMask)!=0||
                frame.ValueRevision<xpValueRevision015[index])) return;
            xpSources015[index] = source; xpReceiveSequence015 = frame.Sequence; BumpXpRevision015();
            xpValueRevision015[index]=frame.ValueRevision;EnqueueXpAck015(index);
            if (previous.Flags == 0) MarkXpSourceReady015(index);
            if (frame.ProgressRevision > xpAppliedProgress015 &&
                localForge.ApplyHostState(new MatchForgeSnapshot(frame.ForgeVersion, frame.ForgeBalance, frame.ForgeTiers, frame.ForgeRevision)))
            {
                owner.Progression.ApplyAuthoritativeState(frame.Level, frame.Xp, frame.Points, frame.Mask);
                xpAppliedProgress015 = frame.ProgressRevision;
                CacheAcceptedSoloRestartProgress(frame.Level, frame.Xp, frame.Points, frame.Mask);
            }
        }
        internal bool RequestSourceXp015(int sourceId)
        {
            if (!EnsureXpRun015() || sourceId < 1 || sourceId > xpSources015.Length) return false;
            if(!CanRequestCatchupSource015(sourceId))return false;
            if (IsAuthority) return TryClaimXp015(rooms.LocalClientId, sourceId);
            if (xpWireGeneration015 <= 0 || xpSendSequence015 == uint.MaxValue) return false;
            var frame = new XpFrame015 { Magic = XpFrameRules015.Magic, Version = XpFrameRules015.Version, Kind = XpFrameRules015.Claim,
                RunLow = xpRunLow015, RunHigh = xpRunHigh015, RunEpoch = campaign.Epoch, RewardGeneration = xpWireGeneration015,
                Sequence = xpSendSequence015 + 1, SourceId = (uint)sourceId };
            if (!rooms.SendXp015(frame, NetworkManager.ServerClientId)) return false;
            xpSendSequence015++; return true;
        }
        void PumpXp015()
        {
            if (!EnsureXpRun015() || xpSendSequence015 == uint.MaxValue) return;
            xpEntitlementTurn015=!xpEntitlementTurn015;
            if(xpEntitlementTurn015&&PumpCatchupFrame015())return;
            if(!IsAuthority){PumpClientXpAck015();return;}
            CompleteXpReaders015();
            bool urgent = xpUrgentCount015 > 0;
            // At most64 examined slots and one128B send per frame. Dirty bits retain value/delivery on rate limits.
            for (int examined = 0; examined < 64; examined++)
            {
                int index = urgent ? xpUrgent015[xpUrgentHead015] : xpPumpCursor015;
                if (!urgent) xpPumpCursor015 = (xpPumpCursor015 + 1) & 4095;
                var source = xpSources015[index];
                uint pending = xpDirty015[index] & 3u;
                if (pending == 0 || !PoolXpMath015.SourceValid(source, CampaignStagePacing.MaximumBaseXp))
                { if (urgent) { DequeueXpAck015(index); urgent = xpUrgentCount015 > 0; } continue; }
                for (int slot = 0; slot < 2; slot++)
                {
                    uint bit = 1u << slot; if ((pending & bit) == 0) continue;
                    ulong recipient = slot == 0 ? source.Owner0 : source.Owner1;
                    if (recipient == rooms.LocalClientId) { xpDirty015[index] &= ~bit; continue; }
                    if (!peers.TryGetValue(recipient, out Peer peer) || !peer.ArenaReady || peer.ProgressRevision015 == int.MaxValue) continue;
                    var forge = peer.Forge.Snapshot;
                    var frame = new XpFrame015 { Magic = XpFrameRules015.Magic, Version = XpFrameRules015.Version, Kind = XpFrameRules015.Source,
                        RunLow = xpRunLow015, RunHigh = xpRunHigh015, RunEpoch = campaign.Epoch, RewardGeneration = xpWireGeneration015,
                        Sequence = xpSendSequence015 + 1, SourceId = source.SourceId, BaseXp = source.BaseXp, OriginWave = source.OriginWave,
                        ValueRevision=xpValueRevision015[index],
                        Position = new Vector3(source.X, source.Y, source.Z), EligibleMask = source.EligibleMask,
                        Owner0 = source.Owner0, Owner1 = source.Owner1, ClaimedMask = source.ClaimedMask,
                        Level = peer.Level, Xp = peer.Xp, Points = peer.Points, Mask = peer.Mask,
                        ForgeVersion = forge.Version, ForgeBalance = forge.Balance, ForgeTiers = forge.PackedTiers, ForgeRevision = forge.Revision,
                        ProgressRevision = peer.ProgressRevision015 + 1 };
                    if (!rooms.SendXp015(frame, recipient)) return;
                    xpSendSequence015++; peer.ProgressRevision015++;
                    if(urgent)DequeueXpAck015(index); // Keep delivery bit until exact application ACK, not merely transport enqueue.
                    return;
                }
                if (urgent) { DequeueXpAck015(index); urgent = xpUrgentCount015 > 0; }
            }
        }
        void EnqueueXpAck015(int index)
        {
            if ((xpDirty015[index] & 256u) != 0 || xpUrgentCount015 == xpUrgent015.Length) return;
            xpDirty015[index] |= 256u; xpUrgent015[xpUrgentTail015] = index;
            xpUrgentTail015 = (xpUrgentTail015 + 1) & 4095; xpUrgentCount015++;
        }
        void DequeueXpAck015(int index)
        {
            xpDirty015[index] &= ~256u; xpUrgentHead015 = (xpUrgentHead015 + 1) & 4095; xpUrgentCount015--;
        }
        void PumpClientXpAck015()
        {
            if(xpUrgentCount015==0||xpWireGeneration015<=0)return;
            int index=xpUrgent015[xpUrgentHead015];
            var frame=new XpFrame015{Magic=XpFrameRules015.Magic,Version=XpFrameRules015.Version,Kind=XpFrameRules015.Ack,
                RunLow=xpRunLow015,RunHigh=xpRunHigh015,RunEpoch=campaign.Epoch,RewardGeneration=xpWireGeneration015,
                Sequence=xpSendSequence015+1,SourceId=(uint)(index+1),ValueRevision=xpValueRevision015[index]};
            if(!rooms.SendXp015(frame,NetworkManager.ServerClientId))return;
            xpSendSequence015++;DequeueXpAck015(index);xpDirty015[index]=0;
        }
        void LateUpdate()
        {
            if (!xpLedgerReady015) return;
            PumpXp015(); TickXpPresentation015();
        }
        void DisposeXp015()
        {
            if(rooms!=null)rooms.XpMessage015-=OnXpFrame015;
            if(xpOwnerSubscribed015&&xpOwnerReload015!=null)LightPoolLifecycle.UnsubscribeBeforeReload(xpOwnerReload015);
            xpOwnerSubscribed015=false;
            CompleteXpReaders015(); DisposeXpPresentation015(); DisposeXpCatchup015();
            if (xpSources015.IsCreated) xpSources015.Dispose();
            if (xpDirty015.IsCreated) xpDirty015.Dispose();
            if (xpUrgent015.IsCreated) xpUrgent015.Dispose();
            if (xpValueRevision015.IsCreated) xpValueRevision015.Dispose();
            xpLedgerReady015 = false;
        }
    }
}

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Original arena death positions and authenticated campaign/run/wave/source identity; refuge never remaps/autoawards.
// Array Formats: Private persistent4096x64B logical sources and4096dirty uints, scalar actor ticket links, bounded one-frame network publication.
// Dependencies/Pointers: Main-thread sole host transactions; numeric reader fence completes before every ledger mutation/disposal. Pool generation fences, fixed Room transport and original-position rendering borrow this owner; no new hot JSON, value retirement or client-level trust.

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Accepted parent/wave/owner/run identities and original source positions; literal claim-time catch-up children.
// Array Formats: Existing4096source64 ledger,905entitlement32 sidecars/dirty words,4096readonly projection, exact128BVersion2 frames.
// Dependencies/Pointers: Root composes structural clearance and these guarded owner hooks; base-onlyForge, no doublebonus, native/peer/carry/visual/performance acceptance pending.
