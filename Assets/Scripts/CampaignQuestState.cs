using System;

namespace OdysseyOracle
{
    // # ARCHITECTURE SNAPSHOT
    // Pure bounded campaign state: host-owned scalar IDs/flags, three150s target stages (180s admission hard deadline).
    // No Unity objects, wallet IO, transport, native allocations or client authority.
    public enum CampaignPhase { None, Refuge, Preparing, Defense, StageClear, Complete, Failed }
    [Serializable] public sealed class CampaignPeerSnapshot
    {
        public ulong id;
        public int visits, towers;
        public bool ready, participating;
    }
    [Serializable] public sealed class CampaignSnapshot
    {
        public string campaign;
        public int stage, completed, epoch, revision, phase;
        public CampaignPeerSnapshot[] peers;
    }
    public sealed class CampaignQuestState
    {
        public const int StageCount = CampaignStagePacing.StageCount, StageSeconds = CampaignStagePacing.StageSeconds, MaximumCapacity = 8;
        public const int SmithRole = 1, RunekeeperRole = 2, RequiredVisits = 3, FourCoreTowers = 15;
        readonly CampaignPeerSnapshot[] peers = new CampaignPeerSnapshot[MaximumCapacity];
        readonly int capacity;
        int count;
        string activeRun;
        public string CampaignId { get; private set; }
        public CampaignPhase Phase { get; private set; }
        public int StageNumber { get; private set; }
        public int CompletedStages { get; private set; }
        public int Epoch { get; private set; }
        public int Revision { get; private set; }
        public int MemberCount => count;
        public CampaignQuestState(int capacity)
        {
            if (capacity < 1 || capacity > MaximumCapacity) throw new ArgumentOutOfRangeException(nameof(capacity));
            this.capacity = capacity;
        }
        public bool Begin(string campaignId, ulong[] members)
        {
            if (!ValidNonce(campaignId) || !ValidMembers(members, capacity) || members.Length < 1) return false;
            CampaignId = campaignId; StageNumber = 1; CompletedStages = 0;
            Phase = CampaignPhase.Refuge; Epoch = Revision = 1; activeRun = null;
            for (int i = 0; i < peers.Length; i++) peers[i] = null;
            count = 0; ReplaceMembers(members); return true;
        }
        public bool ReplaceMembers(ulong[] members)
        {
            if (Phase == CampaignPhase.None || !ValidMembers(members, capacity)) return false;
            bool changed = members.Length != count;
            for (int i = 0; i < members.Length && !changed; i++) changed = Find(members[i]) == null;
            if (!changed) return false;
            var kept = new CampaignPeerSnapshot[MaximumCapacity]; // Cold membership boundary only.
            for (int i = 0; i < members.Length; i++) kept[i] = Find(members[i]) ?? new CampaignPeerSnapshot { id = members[i] };
            count = members.Length;
            for (int i = 0; i < peers.Length; i++) peers[i] = kept[i];
            if (Phase == CampaignPhase.Preparing) AbortPreparation();
            else if (Phase == CampaignPhase.Refuge)
            {
                // Membership is a new ready context, never a quest/reward reset.
                // Epoch rejects ready requests queued under the old roster.
                for (int i = 0; i < count; i++) peers[i].ready = false;
                Epoch++;
            }
            Revision++; return true;
        }
        public bool AcceptNpcVisit(ulong member, int role)
        {
            var peer = Find(member);
            if (Phase != CampaignPhase.Refuge || peer == null || role < SmithRole || role > RunekeeperRole) return false;
            int bit = 1 << (role - 1);
            if ((peer.visits & bit) != 0) return false;
            peer.visits |= bit; peer.ready = false;
            // This is permission to use four core tower types, never money, XP,
            // an item purchase or an upgraded rank. Host build validation consumes it.
            if (peer.visits == RequiredVisits) peer.towers = FourCoreTowers;
            Revision++; return true;
        }
        public bool SetReady(ulong member, bool ready)
        {
            var peer = Find(member);
            if (Phase != CampaignPhase.Refuge || peer == null || ready &&
                (peer.visits != RequiredVisits || peer.towers != FourCoreTowers)) return false;
            if (peer.ready == ready) return false;
            peer.ready = ready; Revision++; return true;
        }
        public bool AllReady
        {
            get
            {
                if (Phase != CampaignPhase.Refuge || count < 1) return false;
                for (int i = 0; i < count; i++) if (!peers[i].ready || peers[i].visits != RequiredVisits || peers[i].towers != FourCoreTowers) return false;
                return true;
            }
        }
        public bool BeginPreparation()
        {
            if (!AllReady) return false;
            for (int i = 0; i < count; i++) peers[i].participating = true;
            Phase = CampaignPhase.Preparing; activeRun = null; Epoch++; Revision++; return true;
        }
        public bool BeginDefense(string hostRun)
        {
            if (Phase != CampaignPhase.Preparing || !ValidNonce(hostRun) || count < 1) return false;
            activeRun = hostRun; Phase = CampaignPhase.Defense; Epoch++; Revision++; return true;
        }
        public bool AbortPreparation()
        {
            if (Phase != CampaignPhase.Preparing) return false;
            for (int i = 0; i < count; i++) { peers[i].ready = false; peers[i].participating = false; }
            Phase = CampaignPhase.Refuge; activeRun = null; Epoch++; Revision++; return true;
        }
        public bool CompleteDefense(string hostRun, float remaining, bool survived, int acceptedAdmissions = 0)
        {
            if (Phase != CampaignPhase.Defense || hostRun != activeRun ||
                float.IsNaN(remaining) || float.IsInfinity(remaining) || remaining < 0f || remaining > StageSeconds || acceptedAdmissions < 0 || acceptedAdmissions > CampaignStagePacing.AdmissionsPerStage ||
                survived && (remaining != 0f || acceptedAdmissions != CampaignStagePacing.AdmissionsPerStage)) return false;
            if (!survived) Phase = CampaignPhase.Failed;
            else
            {
                CompletedStages = StageNumber;
                Phase = StageNumber == StageCount ? CampaignPhase.Complete : CampaignPhase.StageClear;
            }
            activeRun = null; Epoch++; Revision++; return true;
        }
        public bool ReturnToRefuge(ulong member)
        {
            if (Phase != CampaignPhase.StageClear || Find(member) == null || CompletedStages != StageNumber || StageNumber >= StageCount) return false;
            StageNumber++;
            for (int i = 0; i < count; i++)
            { peers[i].visits = 0; peers[i].ready = false; peers[i].participating = false; }
            Phase = CampaignPhase.Refuge; Epoch++; Revision++; return true;
        }
        public bool CanBuild(ulong member, int type)
        {
            var peer = Find(member);
            return Phase == CampaignPhase.Defense && peer != null && peer.participating &&
                type >= 0 && type < 4 && (peer.towers & (1 << type)) != 0;
        }
        public bool HasVisit(ulong member,int role)
        {var peer=Find(member);return peer!=null&&role>=1&&role<=2&&(peer.visits&(1<<(role-1)))!=0;}
        public bool CanLearn(ulong member) => Phase == CampaignPhase.Refuge && Find(member) != null &&
            (Find(member).visits & (1 << (RunekeeperRole - 1))) != 0;
        CampaignPeerSnapshot Find(ulong id)
        { for (int i = 0; i < count; i++) if (peers[i].id == id) return peers[i]; return null; }
        public CampaignSnapshot Snapshot()
        {
            var snapshot = new CampaignSnapshot { campaign = CampaignId, stage = StageNumber, completed = CompletedStages,
                phase = (int)Phase, epoch = Epoch, revision = Revision, peers = new CampaignPeerSnapshot[count] };
            for (int i = 0; i < count; i++) snapshot.peers[i] = new CampaignPeerSnapshot { id = peers[i].id,
                visits = peers[i].visits, towers = peers[i].towers, ready = peers[i].ready, participating = peers[i].participating };
            return snapshot;
        }
        public static bool ValidSnapshot(CampaignSnapshot s, int capacity)
        {
            if (s == null || capacity < 1 || capacity > MaximumCapacity || !ValidNonce(s.campaign) ||
                s.stage < 1 || s.stage > StageCount || s.completed < 0 || s.completed > StageCount ||
                s.phase < (int)CampaignPhase.Refuge || s.phase > (int)CampaignPhase.Failed || s.epoch < 1 || s.revision < 1 ||
                s.peers == null || s.peers.Length < 1 || s.peers.Length > capacity) return false;
            var phase = (CampaignPhase)s.phase;
            if ((phase == CampaignPhase.Refuge || phase == CampaignPhase.Preparing || phase == CampaignPhase.Defense || phase == CampaignPhase.Failed) && s.completed != s.stage - 1 ||
                phase == CampaignPhase.StageClear && (s.completed != s.stage || s.stage >= StageCount) ||
                phase == CampaignPhase.Complete && (s.stage != StageCount || s.completed != StageCount)) return false;
            for (int i = 0; i < s.peers.Length; i++)
            {
                var p = s.peers[i];
                if (p == null || p.visits < 0 || p.visits > RequiredVisits || p.towers != 0 && p.towers != FourCoreTowers ||
                    p.visits == RequiredVisits && p.towers != FourCoreTowers ||
                    p.participating && (p.visits != RequiredVisits || p.towers != FourCoreTowers) ||
                    p.ready && (p.visits != RequiredVisits || p.towers != FourCoreTowers)) return false;
                for (int j = 0; j < i; j++) if (s.peers[j].id == p.id) return false;
            }
            bool host = false; for (int i=0;i<s.peers.Length;i++) host |= s.peers[i].id == 0;
            return host;
        }
        static bool ValidMembers(ulong[] ids, int maximum)
        {
            if (ids == null || ids.Length < 1 || ids.Length > maximum) return false;
            for (int i = 0; i < ids.Length; i++) for (int j = 0; j < i; j++) if (ids[i] == ids[j]) return false;
            bool host = false; for (int i=0;i<ids.Length;i++) host |= ids[i] == 0;
            return host;
        }
        public const double PreparationTimeoutSeconds=120d;
        public static bool PreparationTimedOut(double start,double now)=>
            !double.IsNaN(start)&&!double.IsInfinity(start)&&start>=0&&
            !double.IsNaN(now)&&!double.IsInfinity(now)&&now>=start&&now-start>=PreparationTimeoutSeconds;
        public static bool ValidNonce(string value)
        {
            if (value == null || value.Length != 32) return false;
            for (int i = 0; i < value.Length; i++) if (!(value[i] >= '0' && value[i] <= '9' || value[i] >= 'a' && value[i] <= 'f')) return false;
            return true;
        }
        // Enemy/attack IDs remain stage-local. Only reward IDs use this bounded
        // campaign namespace, preventing three stage resets from reusing a forge claim.
        public static int ScopedDropId(int stage, int enemyId) => stage >= 1 && stage <= StageCount && enemyId >= 1 && enemyId <= 511
            ? (stage - 1) * 512 + enemyId : 0;
    }
}
