using System;
using Unity.Collections;
using UnityEngine;

namespace OdysseyOracle
{
    public struct PeerCombatTarget
    {
        public ulong PeerId;
        public Vector3 Feet,Aim;
        public bool Targetable;
        public int Lane;
    }

    // # ARCHITECTURE SNAPSHOT
    // Main-thread bridge only. Ledger owns host scalars/tickets; this component
    // owns one persistent eight-target NativeArray and eight validated replicas.
    // World Physics stays on the main thread. No new Update, targets or damage
    // from packets. Director owns sender/run/capability/full-payload admission.
    [DisallowMultipleComponent]
    public sealed class PeerCombatAuthority:MonoBehaviour
    {
        readonly PeerCombatLedger ledger=new PeerCombatLedger();
        readonly PeerHealthReplica[] replicas=new PeerHealthReplica[PeerCombatLedger.Capacity];
        readonly ulong[] replicaIds=new ulong[PeerCombatLedger.Capacity];
        readonly bool[] replicaUsed=new bool[PeerCombatLedger.Capacity];
        readonly RaycastHit[] hits=new RaycastHit[32];
        readonly double[] nextCorrection=new double[PeerCombatLedger.Capacity];
        NativeArray<PeerCombatTarget> targets;
        GameSession session;CoopDefenseDirector director;RoomConnection rooms;OracleRevival revival;
        Func<ulong,Vector3,bool> poseValidator;
        bool initialized,validatorAttached,peacePosesPrepared;
        readonly ulong[] peacePoseIds=new ulong[PeerCombatLedger.Capacity];
        string run;
        public event Action<ulong> HealthChanged;
        public event Action<ulong,Vector3> PoseCorrectionRequired;
        public bool Initialized=>initialized&&targets.IsCreated&&session!=null&&director!=null;
        public string CurrentRunId=>run??string.Empty;
        public bool RunMatches(string expected)=>Initialized&&isActiveAndEnabled&&PeerCombatLedger.ValidRun(expected)&&run==expected&&director.CurrentRunId==run;
        public bool Active=>RunMatches(director!=null?director.CurrentRunId:null)&&director.Running&&director.IsAuthority&&
            rooms!=null&&rooms.Connected&&rooms.IsHost&&session.World!=null&&session.World.IsCoopArena;
        //010-only pose lease: combat Active/targets/tickets remain Running-gated.
        bool PoseValidationActive=>CampaignPeacePoseRules.CanValidate(
            RunMatches(director!=null?director.CurrentRunId:null),
            director!=null&&director.IsAuthority&&rooms!=null&&rooms.Connected&&rooms.IsHost&&
                session!=null&&session.World!=null&&session.World.IsCoopArena,
            director!=null&&director.Running,director!=null&&director.CampaignPeacePoses,peacePosesPrepared);
        public bool BeginCampaignPeacePoses()
        {
            if(!RunMatches(director!=null?director.CurrentRunId:null)||director.Running||!director.CampaignPeacePoses||
                !director.IsAuthority||rooms==null||!rooms.Connected||!rooms.IsHost||session.World==null||!session.World.IsCoopArena)return false;
            if(peacePosesPrepared)return true;
            int count=0;bool ownPresent=false;
            for(int i=0;i<PeerCombatLedger.Capacity;i++)
            {
                var peer=ledger.At(i);if(!peer.Used||!peer.Ready||!ConnectedId(peer.Id))continue;
                peacePoseIds[count++]=peer.Id;ownPresent|=peer.Id==rooms.LocalClientId;
            }
            if(!ownPresent||!CampaignPeacePoseRules.NormalizeDownedAtAcceptedPoses(ledger,peacePoseIds,count,
                director.SimulationClock,Time.realtimeSinceStartupAsDouble))return false;
            // No health/revival publication: accepted StageClear flow resets
            // local recovery presentation. Publishing here could trigger the
            // combat revive mirror's spawn teleport. Existing positions remain.
            peacePosesPrepared=true;return true;
        }
        public void Initialize(GameSession owner,CoopDefenseDirector source)
        {
            if(owner==null||source==null)throw new ArgumentNullException("The current session and Director are required.");
            if(initialized&&(session!=owner||director!=source))throw new InvalidOperationException("One authority owner per component.");
            session=owner;director=source;
            if(poseValidator==null)poseValidator=ValidatePose;
            if(!targets.IsCreated)targets=new NativeArray<PeerCombatTarget>(PeerCombatLedger.Capacity,Allocator.Persistent);
            initialized=true;Attach();
        }
        void Attach()
        {
            RoomConnection current=session!=null?session.Rooms:null;
            if(rooms!=current)
            {
                if(rooms!=null)rooms.ClearArenaPoseValidator(poseValidator);
                rooms=current;validatorAttached=false;
            }
            // A guest publishes finite local poses for HOST validation. Installing
            // this host ledger gate on a guest would reject every outgoing pose.
            if(rooms!=null&&rooms.IsHost&&!validatorAttached){rooms.ArenaPoseValidator=poseValidator;validatorAttached=true;}
            else if(rooms!=null&&!rooms.IsHost&&validatorAttached){rooms.ClearArenaPoseValidator(poseValidator);validatorAttached=false;}
            OracleRevival recovery=session!=null?session.Revival:null;
            if(revival!=recovery)
            {if(revival!=null)revival.ClearPeerCombatAuthority(this);revival=recovery;if(revival!=null)revival.SetPeerCombatAuthority(this);}
        }
        public bool ResetRun(string confirmedRun)
        {
            if(!Initialized||!PeerCombatLedger.ValidRun(confirmedRun)||director.CurrentRunId!=confirmedRun)return false;
            if(run==confirmedRun&&ledger.RunId==confirmedRun)return true;
            ClearRun();run=confirmedRun;ledger.BeginRun(run);Attach();return true;
        }
        public void ClearRun()
        {
            peacePosesPrepared=false;Array.Clear(peacePoseIds,0,peacePoseIds.Length);
            run=null;ledger.Clear();Array.Clear(replicas,0,replicas.Length);Array.Clear(replicaUsed,0,replicaUsed.Length);
            Array.Clear(replicaIds,0,replicaIds.Length);Array.Clear(nextCorrection,0,nextCorrection.Length);
            if(targets.IsCreated)for(int i=0;i<targets.Length;i++)targets[i]=default;
        }
        bool ConnectedId(ulong id)
        {
            if(rooms==null||!rooms.Connected)return false;
            var ids=rooms.ConnectedClientIds;for(int i=0;i<ids.Count&&i<PeerCombatLedger.Capacity;i++)if(ids[i]==id)return true;return false;
        }
        public void ReconcilePeer(ulong id,int slot,bool ready)
        {
            Attach();if(!Active||slot<0||slot>=PeerCombatLedger.Capacity||!ConnectedId(id))return;
            bool created=false;
            if(!ledger.TryGet(id,out var old))
            {
                Vector3 spawn=session.World.GetCoopPlayerSpawn(slot);
                created=ledger.Add(id,slot,spawn.x,spawn.y,spawn.z,Time.realtimeSinceStartupAsDouble);
            }
            else if(old.Slot!=slot)return;
            ledger.SetReady(id,ready);
            if(created||old.Ready!=ready)PublishHealth(id);
        }
        public void RemovePeer(ulong id)
        {
            ledger.Remove(id);for(int i=0;i<replicas.Length;i++)if(replicaUsed[i]&&replicaIds[i]==id){replicaUsed[i]=false;replicas[i]=default;}
        }
        public bool TryGetHealth(ulong id,out PeerCombatLedger.Record record)
        {record=default;return Active&&ledger.TryGet(id,out record);}
        public static bool ValidHealthState(PeerHealthState state)=>PeerCombatLedger.ValidHealthState(state);
        public bool TryCaptureHealth(ulong id,out PeerHealthState state)
        {
            state=new PeerHealthState{id=id};
            // EndDefense stops Running before sending its final state. Reading
            // this retained confirmed-run host ledger grants no attack rights.
            if(!RunMatches(director!=null?director.CurrentRunId:null)||!director.IsAuthority||
                rooms==null||!rooms.Connected||!rooms.IsHost||session.World==null||!session.World.IsCoopArena||
                !ConnectedId(id)||!ledger.TryGet(id,out var p)||!p.Ready)return false;
            state=new PeerHealthState{id=id,present=true,hp=p.Health,revision=p.Revision,report=p.DownReport,downed=p.Downed,
                shield=(float)Math.Max(0d,p.ShieldUntil-director.SimulationClock)};
            return ValidHealthState(state);
        }
        public bool TryApplyHostHealth(string confirmedRun,PeerHealthState state)
        {
            if(!RunMatches(confirmedRun)||!ValidHealthState(state)||!ConnectedId(state.id))return false;
            int slot=-1;
            for(int i=0;i<replicas.Length;i++)if(replicaUsed[i]&&replicaIds[i]==state.id){slot=i;break;}
            if(slot<0)for(int i=0;i<replicas.Length;i++)if(!replicaUsed[i]||!ConnectedId(replicaIds[i])){slot=i;replicas[i]=default;replicas[i].ResetForRun(run,state.id);replicaIds[i]=state.id;replicaUsed[i]=true;break;}
            if(slot<0)return false;
            if(!state.present){replicas[slot].ResetForRun(run,state.id);return true;}
            if(!replicas[slot].TryApply(run,state,director.SimulationClock))return false;
            if(rooms!=null&&state.id==rooms.LocalClientId&&revival!=null&&
                replicas[slot].TryGet(run,director.SimulationClock,out var snapshot))revival.ApplyAcceptedArenaHealth(snapshot);
            return true;
        }
        public bool TryGetPeerHealth(ulong id,out PeerHealthSnapshot snapshot)
        {
            snapshot=default;
            if(!RunMatches(director!=null?director.CurrentRunId:null)||!director.Running||session.World==null||!session.World.IsCoopArena||!ConnectedId(id))return false;
            for(int i=0;i<replicas.Length;i++)if(replicaUsed[i]&&replicaIds[i]==id)return replicas[i].TryGet(run,director.SimulationClock,out snapshot);
            return false;
        }
        void PublishHealth(ulong id)
        {TryCaptureHealth(id,out var state);TryApplyHostHealth(run,state);HealthChanged?.Invoke(id);}
        public bool TryGetPose(ulong id,out Vector3 position)
        {if(PoseValidationActive&&ConnectedId(id)&&ledger.TryGet(id,out var p)&&p.Ready){position=new Vector3(p.X,p.Y,p.Z);return true;}position=default;return false;}
        public void CaptureLocalPose()
        {Attach();if(PoseValidationActive&&session.Player!=null)ValidatePose(rooms.LocalClientId,session.Player.transform.position);}
        bool ValidatePose(ulong id,Vector3 candidate)
        {
            if(session==null||session.World==null||!session.World.IsCoopArena)return true;
            if(!PoseValidationActive||!ConnectedId(id)||!ledger.TryGet(id,out var old)||!old.Ready)return false;
            double now=Time.realtimeSinceStartupAsDouble;Vector3 from=new Vector3(old.X,old.Y,old.Z),delta=candidate-from;
            bool accepted=ledger.CanMove(id,candidate.x,candidate.y,candidate.z,now);
            if(accepted&&delta.sqrMagnitude>.0001f)
            {
                int count=Physics.CapsuleCastNonAlloc(from+Vector3.up*.30f,from+Vector3.up*1.55f,.275f,delta.normalized,hits,delta.magnitude,~(1<<2),QueryTriggerInteraction.Ignore);
                accepted=count<hits.Length;for(int i=0;accepted&&i<count;i++)if(BlocksWorld(hits[i].collider))accepted=false;
            }
            if(accepted)return ledger.CommitPose(id,candidate.x,candidate.y,candidate.z,now);
            if(now>=nextCorrection[old.Slot]){nextCorrection[old.Slot]=now+.25d;PoseCorrectionRequired?.Invoke(id,from);}return false;
        }
        bool BlocksWorld(Collider collider)
        {
            if(collider==null||collider.isTrigger)return false;
            return collider.GetComponentInParent<OraclePlayer>()==null&&collider.GetComponentInParent<RemoteOracleAvatar>()==null&&
                collider.GetComponentInParent<OracleEnemy>()==null&&collider.GetComponentInParent<OracleProjectile>()==null;
        }
        bool Sight(Vector3 origin,Vector3 destination)
        {
            Vector3 delta=destination-origin;if(delta.sqrMagnitude<.0001f)return true;
            int count=Physics.RaycastNonAlloc(origin,delta.normalized,hits,delta.magnitude,~(1<<2),QueryTriggerInteraction.Ignore);
            if(count==hits.Length)return false;for(int i=0;i<count;i++)if(BlocksWorld(hits[i].collider))return false;return true;
        }
        public int CopyPeerCombatTargets(PeerCombatTarget[] destination)
        {
            if(!Active||!targets.IsCreated||destination==null)return 0;int count=0;
            for(int i=0;i<PeerCombatLedger.Capacity;i++)
            {
                var p=ledger.At(i);if(!p.Used||!p.Ready)continue;Vector3 feet=new Vector3(p.X,p.Y,p.Z);int lane=session.World.GetArenaLane(feet);
                var value=new PeerCombatTarget{PeerId=p.Id,Feet=feet,Aim=feet+Vector3.up*1.35f,Lane=lane,
                    Targetable=!p.Downed&&p.Health>0&&director.SimulationClock>=p.ShieldUntil&&CoopPartyCapacity.LaneActive(director.ActiveLaneMask,lane)};
                targets[count]=value;if(count<destination.Length)destination[count]=value;count++;
            }
            for(int i=count;i<targets.Length;i++)targets[i]=default;return Math.Min(count,destination.Length);
        }
        OracleEnemy FindEnemy(int id)
        {if(session.Combat!=null)foreach(var enemy in session.Combat.ActiveEnemies)if(enemy!=null&&enemy.Alive&&enemy.TargetId==id)return enemy;return null;}
        bool Source(int id,Vector3 origin,float range,bool projectile,out int lane)
        {
            lane=-1;if(!Active||!director.ArenaReady||!PeerCombatLedger.Finite(range)||range<=0||range>(projectile?22f:2.5f)||
                !PeerCombatLedger.ValidPosition(origin.x,origin.y,origin.z))return false;
            var enemy=FindEnemy(id);return enemy!=null&&enemy.AttackPhase==OracleEnemy.GuardianAttackPhase.Strike&&enemy.IsRanged==projectile&&
                (origin-enemy.AimPoint).sqrMagnitude<=4f&&director.TryGetEnemyLane(id,out lane)&&CoopPartyCapacity.LaneActive(director.ActiveLaneMask,lane);
        }
        public bool AuthorizePeerAttack(int enemyId,int serial,ulong targetId,Vector3 origin,float range,bool projectile)
        {
            if(!Source(enemyId,origin,range,projectile,out int lane)||!TryGetPose(targetId,out var feet)||session.World.GetArenaLane(feet)!=lane||
                (feet+Vector3.up*1.35f-origin).sqrMagnitude>(range+.5f)*(range+.5f)||!Sight(origin,feet+Vector3.up*1.35f))return false;
            return ledger.Authorize(enemyId,serial,targetId,lane,projectile,director.SimulationClock,origin.x,origin.y,origin.z,range);
        }
        public bool AuthorizeLaneProjectile(int enemyId,int serial,Vector3 origin,float range)
        {return Source(enemyId,origin,range,true,out int lane)&&ledger.AuthorizeProjectile(enemyId,serial,lane,director.SimulationClock,origin.x,origin.y,origin.z,range);}
        public bool ApplyPeerDamage(ulong id,float amount,int enemyId,int serial)
        {
            if(!Active||!director.ArenaReady||!TryGetPose(id,out var feet)||!ledger.TryGet(id,out var before)||!PeerCombatLedger.Finite(amount)||amount<=0||amount>20||
                !ledger.TryGetAttack(enemyId,serial,director.SimulationClock,out var attack))return false;
            int lane=session.World.GetArenaLane(feet);Vector3 origin=new Vector3(attack.X,attack.Y,attack.Z);
            if(!CoopPartyCapacity.LaneActive(director.ActiveLaneMask,lane)||!ledger.CanHit(enemyId,serial,id,lane,director.SimulationClock)||
                (feet+Vector3.up*1.1f-origin).sqrMagnitude>(attack.Range+1f)*(attack.Range+1f)||!Sight(origin,feet+Vector3.up*1.1f))return false;
            OracleRevival.DownCommit fall=default;
            if(amount>=before.Health&&(revival==null||!revival.TryPreparePeerDowned(id,before.DownReport+1,out fall)))return false;
            if(!ledger.Damage(enemyId,serial,id,lane,amount,director.SimulationClock))return false;
            if(amount>=before.Health)revival.CommitPeerDowned(fall);
            PublishHealth(id);return true;
        }
        internal bool TryCommitRevive(ulong id,int report)
        {
            if(!Active||!ConnectedId(id)||!rooms.TryGetAdmissionSlot(id,out int slot)||slot<0||slot>=PeerCombatLedger.Capacity)return false;
            Vector3 spawn=session.World.GetCoopPlayerSpawn(slot);
            return ledger.Revive(id,report,spawn.x,spawn.y,spawn.z,director.SimulationClock,Time.realtimeSinceStartupAsDouble);
        }
        internal void PublishRevival(ulong id)
        {if(!Active||!TryGetPose(id,out var spawn))return;PoseCorrectionRequired?.Invoke(id,spawn);PublishHealth(id);}
        void OnEnable(){if(session!=null&&director!=null)Initialize(session,director);}
        void OnDisable()
        {
            if(rooms!=null)rooms.ClearArenaPoseValidator(poseValidator);rooms=null;validatorAttached=false;
            if(revival!=null)revival.ClearPeerCombatAuthority(this);revival=null;
            ClearRun();if(targets.IsCreated)targets.Dispose();initialized=false;
        }
        void OnDestroy()=>OnDisable();
    }
}
