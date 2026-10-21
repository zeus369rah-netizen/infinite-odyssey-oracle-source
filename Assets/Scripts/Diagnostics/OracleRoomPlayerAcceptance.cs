#if DEVELOPMENT_BUILD && UNITY_STANDALONE_WIN && !UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Unity.Services.Authentication;
using Unity.Services.Core;
using UnityEngine;

namespace OdysseyOracle.Diagnostics
{
    // # ARCHITECTURE SNAPSHOT
    // Explicitly armed DEVELOPMENT Windows player only. Each process owns one
    // normal Rooms session. Small atomic work journals coordinate public actions,
    // never game DTOs. No native allocation, transport injection, profile edits,
    // damage/reward grants or OS input. The root operator builds/launches/stops.
    public sealed class OracleRoomPlayerAcceptance : MonoBehaviour
    {
        public const string DiagnosticProductName="Infinite Odyssey Network Diagnostics 007";
        const string AllowedWorkRoot="C:/Users/zeus3/Documents/Codex/2026-10-16/soi/work/";
        const double MaximumSeconds=420,PhaseSeconds=90,CleanupSeconds=25;
        [Serializable] public sealed class Check {public string name,detail;}
        [Serializable] public sealed class Receipt
        {
            public string status="RUNNING",runId,profile,productName,persistentDataPath,error,phase,roomStatus,startedUtc,endedUtc;
            public int index,participants,capacity,campaignEvents,observedPlayerMaximum;
            public bool explicitDevelopmentArm,authInitializedWithFreshProfile,nativeNetworkExecuted,allRosterObserved;
            public bool ownerCleanupRequested,ownerCleanupComplete,stageJoinRejected,eightClientsExecuted,currentRunHealthObserved;
            public int playerErrorCount;public List<string> playerErrors=new List<string>();
            public bool fakeGameDtoInjected,osInputRequested,productionProfileWriteRequested;
            public float observedSimulationSeconds;
            public List<Check> checks=new List<Check>();
        }
        [Serializable] sealed class Manifest {public string runId,productName;public int participants,capacity;}
        [Serializable] sealed class Cancellation {public string runId;}
        [Serializable] sealed class Journal
        {
            public string runId,phase,code,identityHash,error,updatedUtc;
            public int index,participants,slot=-1,playerCount,readyCount;
            public ulong clientId;public Vector3 position;public bool connected,clean;
        }
        static bool armed;
        GameSession owner;RoomConnection rooms;
        Receipt receipt;string directory,runId,phase,profile;int index,participants;
        bool servicesReady,finished,cleanupStarted,selecting,secondTap,castRequested,castAccepted,allRosterRecorded;
        double started,phaseAt,nextTick,nextPublish,nextAction,oneReadySince=-1;
        float defenseStart;ulong departingGuestId;
        readonly Journal[] journals=new Journal[8];
        readonly Dictionary<int,int> originalSlots=new Dictionary<int,int>();
        public string CurrentReceiptJson=>receipt==null?string.Empty:JsonUtility.ToJson(receipt,true);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Arm()
        {
            var args=Environment.GetCommandLineArgs();
            string token;
            try{token=Argument(args,"--odyssey-room-run",false);}
            catch(Exception error){Debug.LogError(error.ToString());Application.Quit(2);return;}
            if(token==null)return;
            if(armed){Debug.LogError("Duplicate development room acceptance arm.");Application.Quit(2);return;}
            armed=true;
            // A separate build product isolates Windows game PlayerPrefs as well
            // as the fresh Authentication profile. Never arm the production build.
            if(Application.productName!=DiagnosticProductName||!Application.isBatchMode)
            {Debug.LogError("Room acceptance requires its separate diagnostic product and batch mode.");Application.Quit(2);return;}
            var go=new GameObject("Oracle development room acceptance");DontDestroyOnLoad(go);
            var test=go.AddComponent<OracleRoomPlayerAcceptance>();
            try{test.Configure(args,token);}catch(Exception e){Debug.LogError(e.ToString());Application.Quit(2);Destroy(go);}
        }
        static string Argument(string[] args,string key,bool required=true)
        {
            string value=null;
            for(int i=0;i<args.Length;i++)if(args[i]==key)
            {if(value!=null||i+1>=args.Length)throw new ArgumentException("Duplicate or missing "+key);value=args[++i];}
            if(required&&value==null)throw new ArgumentException("Missing "+key);return value;
        }
        void Configure(string[] args,string token)
        {
            if(!Token(token))throw new ArgumentException("Use a fresh 32-hex run token.");
            runId=token;
            if(!int.TryParse(Argument(args,"--odyssey-room-index"),out index)||
               !int.TryParse(Argument(args,"--odyssey-room-participants"),out participants)||
               (participants!=2&&participants!=8)||index<0||index>=participants||RoomConnection.MaximumPlayers!=8)
                throw new ArgumentException("Use exactly two or eight processes with the coherent eight-capacity source.");
            directory=Path.GetFullPath(Argument(args,"--odyssey-room-directory")).TrimEnd(Path.DirectorySeparatorChar);
            string allowed=Path.GetFullPath(AllowedWorkRoot).TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar;
            if(!directory.StartsWith(allowed,StringComparison.OrdinalIgnoreCase)||Path.GetFileName(directory)!=runId||!Directory.Exists(directory))
                throw new ArgumentException("Use the root-created fresh run directory under the allowed work root.");
            string manifestPath=Path.Combine(directory,"run.json");
            if(!File.Exists(manifestPath)||new FileInfo(manifestPath).Length>2048)throw new ArgumentException("Missing bounded root manifest.");
            var manifest=JsonUtility.FromJson<Manifest>(File.ReadAllText(manifestPath));
            if(manifest==null||manifest.runId!=runId||manifest.participants!=participants||manifest.capacity!=8||manifest.productName!=DiagnosticProductName)
                throw new ArgumentException("Root run manifest does not match this process.");
            if(File.Exists(PathFor("receipt"))||File.Exists(PathFor("journal")))throw new InvalidOperationException("Never reuse a process journal/profile slot.");
            profile="orct_"+runId.Substring(0,16)+"_"+index;
            started=phaseAt=Time.realtimeSinceStartupAsDouble;phase="initializing";
            receipt=new Receipt{runId=runId,index=index,participants=participants,capacity=8,profile=profile,
                productName=Application.productName,persistentDataPath=Application.persistentDataPath,explicitDevelopmentArm=true,startedUtc=DateTime.UtcNow.ToString("o")};
            Application.logMessageReceived+=OnLog;Save();InitializeServices();
        }
        async void InitializeServices()
        {
            try
            {
                if(UnityServices.State!=ServicesInitializationState.Uninitialized)
                    throw new InvalidOperationException("SDK initialized before isolated test profile; refusing to switch an account/profile.");
                await UnityServices.InitializeAsync(new InitializationOptions().SetProfile(profile));
                if(finished)return;
                receipt.authInitializedWithFreshProfile=true;servicesReady=true;SetPhase("waiting_session");
            }
            catch(Exception error){Fail(error);}
        }
        void Update()
        {
            if(receipt==null||finished)return;
            double now=Time.realtimeSinceStartupAsDouble;if(now<nextTick)return;nextTick=now+.1d;
            try
            {
                if(phase=="cleanup"){TickCleanup(now);return;}
                string cancelPath=Path.Combine(directory,"cancel.json");
                if(File.Exists(cancelPath)&&new FileInfo(cancelPath).Length<=2048)
                {
                    var cancel=JsonUtility.FromJson<Cancellation>(File.ReadAllText(cancelPath));
                    if(cancel!=null&&cancel.runId==runId)throw new OperationCanceledException("Root requested bounded test cleanup.");
                }
                if(now-started>MaximumSeconds)throw new TimeoutException("Finite development room run exceeded its deadline.");
                if(now-phaseAt>PhaseSeconds)throw new TimeoutException("Room phase timed out: "+phase+" / "+(rooms!=null?rooms.Status:"no GameSession"));
                if(!servicesReady)return;
                if(owner==null)
                {
                    owner=GameSession.Instance;if(owner==null||owner.Rooms==null||owner.Player==null||owner.Tutorial==null||owner.Defense==null)return;
                    rooms=owner.Rooms;if(rooms.Busy||rooms.Connected)throw new InvalidOperationException("Start the diagnostic player disconnected.");
                    rooms.CampaignStarted+=OnCampaign;
                    // Public skip performs no reward grant; diagnostic-product
                    // saves are separate even if startup performs normal reads.
                    owner.SkipTutorialToHub();SetPhase("training_skip");return;
                }
                receipt.roomStatus=rooms.Status;
                receipt.observedPlayerMaximum=Math.Max(receipt.observedPlayerMaximum,rooms.PlayerCount);
                if(rooms.Connected)receipt.nativeNetworkExecuted=true;
                ReadJournals();
                for(int i=0;i<participants;i++)if(i!=index&&journals[i]!=null&&!string.IsNullOrEmpty(journals[i].error))
                    throw new InvalidOperationException("Peer "+i+" failed: "+journals[i].error);
                if(phase=="training_skip")
                {
                    if(!owner.Tutorial.Finished){owner.SkipTutorialToHub();return;}
                    if(!owner.IsPlaying)return;
                    if(index==0){rooms.CreateRoom();SetPhase("creating");}else SetPhase("waiting_host");
                }
                else if(index==0)TickHost(now);else TickGuest(now);
                if(now>=nextPublish){nextPublish=now+.5d;Publish();Save();}
            }
            catch(Exception error){Fail(error);}
        }
        void OnCampaign(){receipt.campaignEvents++;}
        void OnLog(string condition,string stack,LogType type)
        {
            if(receipt==null||type!=LogType.Error&&type!=LogType.Exception&&type!=LogType.Assert)return;
            receipt.playerErrorCount++;
            if(receipt.playerErrors.Count<8)receipt.playerErrors.Add(condition.Length>1024?condition.Substring(0,1024):condition);
        }
        void Assert(bool condition,string label,string detail="")
        {if(!condition)throw new InvalidOperationException(label+": "+detail);receipt.checks.Add(new Check{name=label,detail=detail});}
        bool PeerPhase(int peer,string expected)=>journals[peer]!=null&&journals[peer].phase==expected;
        bool AllGuestPhase(string expected)
        {for(int i=1;i<participants;i++)if(!PeerPhase(i,expected))return false;return true;}
        void ConnectedChecks(bool host)
        {
            Assert(rooms.Connected&&!rooms.Busy&&rooms.IsHost==host,"actual_role_connected",rooms.Status);
            Assert(!string.IsNullOrEmpty(rooms.Code)&&IdentityHash().Length==64,"actual_code_and_auth_identity");
            Assert(!rooms.CampaignActive&&receipt.campaignEvents==0,"hub_before_ready");
        }
        bool FullRoster()
        {
            var ids=rooms.ConnectedClientIds;if(rooms.PlayerCount!=participants||ids.Count!=participants)return false;
            int slots=0;
            for(int i=0;i<ids.Count;i++)
            {
                if(!rooms.TryGetAdmissionSlot(ids[i],out int slot)||slot<0||slot>7||(slots&(1<<slot))!=0)return false;
                if((ids[i]==0)!=(slot==0))return false;slots|=1<<slot;
                for(int j=0;j<i;j++)if(ids[j]==ids[i])return false;
            }
            if(!allRosterRecorded){Assert(true,"actual_complete_stable_roster",ids.Count+" accepted members");allRosterRecorded=true;receipt.allRosterObserved=true;}
            return true;
        }
        bool VerifyActualGuests(bool first)
        {
            if(!FullRoster())return false;
            var hashes=new HashSet<string>(StringComparer.Ordinal){IdentityHash()};
            for(int i=1;i<participants;i++)
            {
                var peer=journals[i];if(peer==null||!peer.connected||peer.identityHash==null||peer.identityHash.Length!=64||!hashes.Add(peer.identityHash))return false;
                if(!Contains(rooms.ConnectedClientIds,peer.clientId)||!rooms.TryGetAdmissionSlot(peer.clientId,out int slot)||slot!=peer.slot)return false;
                if(!rooms.TryGetPlayerPose(peer.clientId,out Vector3 p,out _)||(p-peer.position).sqrMagnitude>.05f*.05f)return false;
                if(first)originalSlots[i]=slot;else if(originalSlots[i]!=slot)throw new InvalidOperationException("Survivor/rejoin slot changed for peer "+i);
                if(i==1)departingGuestId=peer.clientId;
            }
            Assert(true,"distinct_actual_auth_ids_and_host_received_poses",participants+" real participants");return true;
        }
        static bool Contains(IReadOnlyList<ulong> ids,ulong id){for(int i=0;i<ids.Count;i++)if(ids[i]==id)return true;return false;}
        bool AllPeerHealthPresent()
        {
            foreach(ulong id in rooms.ConnectedClientIds)
                if(!owner.TryGetPeerHealth(id,out var health)||!health.Present||health.PeerId!=id||health.RunId!=owner.Defense.CurrentRunId||health.Revision<1||
                    float.IsNaN(health.Health)||float.IsInfinity(health.Health)||health.Health<0||health.Health>health.MaximumHealth)return false;
            receipt.currentRunHealthObserved=true;return true;
        }
        void TickHost(double now)
        {
            if(phase=="creating"&&rooms.Connected&&!rooms.Busy){ConnectedChecks(true);SetPhase("join");}
            else if(phase=="join"&&AllGuestPhase("joined")&&VerifyActualGuests(true))
            {rooms.SetCoopReady(true);SetPhase("one_ready");}
            else if(phase=="one_ready")
            {
                if(rooms.CampaignActive)throw new InvalidOperationException("Unready guests failed to block campaign start.");
                if(!rooms.LocalReady||rooms.ReadyPlayerCount!=1){oneReadySince=-1;if(now>=nextAction){rooms.SetCoopReady(true);nextAction=now+1;}return;}
                if(oneReadySince<0)oneReadySince=now;if(now-oneReadySince<2)return;
                Assert(receipt.campaignEvents==0&&rooms.PlayerCount==participants,"one_ready_does_not_start_party");SetPhase("leave_once");
            }
            else if(phase=="leave_once"&&rooms.PlayerCount==participants-1&&PeerPhase(1,"left"))
            {Assert(!rooms.LocalReady&&!rooms.CampaignActive,"departure_clears_ready_without_start");SetPhase("rejoin");}
            else if(phase=="rejoin"&&PeerPhase(1,"rejoined")&&VerifyActualGuests(false))
            {Assert(!rooms.LocalReady&&!rooms.CampaignActive,"rejoin_has_no_old_ready");rooms.SetCoopReady(true);SetPhase("ready");}
            else if(phase=="ready")
            {
                if(!rooms.CampaignActive&&!rooms.LocalReady&&now>=nextAction){rooms.SetCoopReady(true);nextAction=now+1;}
                if(rooms.CampaignActive){Assert(receipt.campaignEvents==1,"single_actual_campaign_event");SetPhase("defense");}
            }
            else if(phase=="defense"&&owner.Defense.ArenaReady&&owner.IsPlaying&&AllPeerHealthPresent())
            {Assert(owner.World.IsCoopArena,"host_actual_arena_barrier");defenseStart=owner.Defense.RemainingSeconds;SetPhase("cast");}
            else if(phase=="cast"){CastOnce(now);if(castAccepted&&AllGuestPhase("cast_ok"))SetPhase("observe");}
            else if(phase=="observe")
            {
                receipt.observedSimulationSeconds=defenseStart-owner.Defense.RemainingSeconds;
                if(receipt.observedSimulationSeconds<8||owner.Defense.AliveCount<1)return;
                Assert(owner.Defense.Running,"actual_timer_and_enemy_spawn");SetPhase("leave_defense");
            }
            else if(phase=="leave_defense"&&rooms.PlayerCount==participants-1&&PeerPhase(1,"left_defense"))
            {Assert(owner.Defense.Running,"host_continues_after_guest_departure");Assert(!owner.TryGetPeerHealth(departingGuestId,out _),"departed_peer_has_no_current_health");SetPhase("reject_join");}
            else if(phase=="reject_join"&&PeerPhase(1,"rejected"))
            {Assert(rooms.PlayerCount==participants-1&&owner.Defense.Running,"actual_defense_join_remains_rejected");receipt.stageJoinRejected=true;SetPhase("cleanup_guests");}
            else if(phase=="cleanup_guests"&&AllGuestPhase("guest_clean")){BeginCleanup();}
        }
        void TickGuest(double now)
        {
            var host=journals[0];if(host==null)return;
            if(phase=="waiting_host"&&host.phase=="join"){rooms.JoinRoom(host.code);SetPhase("joining");}
            else if(phase=="joining"&&rooms.Connected&&!rooms.Busy&&FullRoster())
            {ConnectedChecks(false);owner.Player.Teleport(owner.Player.transform.position+Vector3.right*.25f,owner.Player.transform.eulerAngles.y);SetPhase("joined");}
            else if(phase=="joined"&&index==1&&host.phase=="leave_once"){rooms.LeaveRoom();SetPhase("leaving");}
            else if(phase=="leaving"&&!rooms.Connected&&!rooms.Busy){Assert(true,"actual_first_guest_leave");SetPhase("left");}
            else if(phase=="left"&&host.phase=="rejoin"){rooms.JoinRoom(host.code);SetPhase("rejoining");}
            else if(phase=="rejoining"&&rooms.Connected&&!rooms.Busy&&FullRoster()){ConnectedChecks(false);SetPhase("rejoined");}
            else if((phase=="joined"||phase=="rejoined")&&host.phase=="ready")SetPhase("ready");
            else if(phase=="ready")
            {
                if(!rooms.CampaignActive&&!rooms.LocalReady&&now>=nextAction){rooms.SetCoopReady(true);nextAction=now+1;}
                if(rooms.CampaignActive){Assert(receipt.campaignEvents==1,"single_actual_campaign_event");SetPhase("defense");}
            }
            else if(phase=="defense"&&owner.Defense.ArenaReady&&owner.IsPlaying&&AllPeerHealthPresent())
            {Assert(owner.World.IsCoopArena,"guest_actual_arena_barrier");SetPhase("cast");}
            else if(phase=="cast"){CastOnce(now);if(castAccepted)SetPhase("cast_ok");}
            else if(phase=="cast_ok"&&index==1&&host.phase=="leave_defense"){rooms.LeaveRoom();SetPhase("leaving_defense");}
            else if(phase=="leaving_defense"&&!rooms.Connected&&!rooms.Busy)SetPhase("left_defense");
            else if(phase=="left_defense"&&host.phase=="reject_join"){rooms.JoinRoom(host.code);SetPhase("rejecting_join");}
            else if(phase=="rejecting_join")
            {
                if(rooms.Connected)throw new InvalidOperationException("New connection admitted during actual defense.");
                if(!rooms.Busy&&now-phaseAt>.25)
                {
                    string reason=rooms.Status??"";bool explicitReason=reason.IndexOf("defense",StringComparison.OrdinalIgnoreCase)>=0||reason.IndexOf("refuge",StringComparison.OrdinalIgnoreCase)>=0;
                    Assert(explicitReason&&string.IsNullOrEmpty(rooms.Code),"actual_defense_join_rejection_reason",reason);receipt.stageJoinRejected=true;SetPhase("rejected");
                }
            }
            if(host.phase=="cleanup_guests"&&(phase=="cast_ok"||phase=="rejected"))
            {receipt.ownerCleanupRequested=true;rooms.LeaveRoom();SetPhase("guest_leaving");}
            if(phase=="guest_leaving"&&!rooms.Connected&&!rooms.Busy){receipt.ownerCleanupComplete=true;SetPhase("guest_clean");Save();}
            if(phase=="guest_clean"&&(host.phase=="cleanup"||host.phase=="finished"))Finish(true);
        }
        void CastOnce(double now)
        {
            if(!rooms.Connected||!owner.IsPlaying||!owner.Defense.ArenaReady)return;
            if(!selecting){owner.Combat.SetAutoFire(false);owner.Combat.TrySelectElement(0);selecting=true;nextAction=now+.15;return;}
            if(!secondTap&&owner.Combat.PreparedElement==0&&now>=nextAction){owner.Combat.TrySelectElement(0);secondTap=true;return;}
            if(!castRequested&&owner.Combat.HasConfirmedSelection&&owner.Combat.ConfirmedMask==1)
            {Assert(owner.Combat.CooldownRemaining[0]<=.05f,"unused_confirmed_element");owner.Combat.ManualCast();castRequested=true;return;}
            if(castRequested&&!castAccepted&&owner.Combat.CooldownRemaining[0]>2)
            {Assert(true,"actual_host_accepted_manual_cast_state");castAccepted=true;}
        }
        void BeginCleanup(){if(cleanupStarted)return;cleanupStarted=true;receipt.ownerCleanupRequested=true;rooms?.LeaveRoom();SetPhase("cleanup");}
        void TickCleanup(double now)
        {
            if(rooms==null||(!rooms.Connected&&!rooms.Busy)){receipt.ownerCleanupComplete=true;Finish(string.IsNullOrEmpty(receipt.error));}
            else if(now-phaseAt>CleanupSeconds){receipt.error=(receipt.error??"")+" / room cleanup deadline";Finish(false);}
            else if(now>=nextPublish){nextPublish=now+.5d;Publish();Save();}
        }
        void Fail(Exception error)
        {
            if(finished)return;
            if(receipt==null){Debug.LogError(error.ToString());Application.Quit(2);return;}
            receipt.error=error.GetType().Name+": "+error.Message;receipt.status="FAIL";
            try{BeginCleanup();}catch(Exception cleanup){receipt.error+=" / cleanup: "+cleanup.Message;Finish(false);}
        }
        void SetPhase(string next){phase=next;phaseAt=Time.realtimeSinceStartupAsDouble;receipt.phase=next;Publish();Save();}
        string PathFor(string kind)=>Path.Combine(directory,"peer-"+index+"."+kind+".json");
        static bool Token(string token){if(token==null||token.Length!=32)return false;foreach(char c in token)if(!(c>='0'&&c<='9'||c>='a'&&c<='f'))return false;return true;}
        static string IdentityHash()
        {
            string value=AuthenticationService.Instance.PlayerId;if(string.IsNullOrEmpty(value))throw new InvalidOperationException("No real anonymous player identity.");
            using(var hash=SHA256.Create())return BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(value))).Replace("-","").ToLowerInvariant();
        }
        void Publish()
        {
            if(receipt==null||directory==null)return;
            var journal=new Journal{runId=runId,index=index,participants=participants,phase=phase,error=receipt.error,updatedUtc=DateTime.UtcNow.ToString("o"),clean=receipt.ownerCleanupComplete};
            if(rooms!=null&&rooms.Connected)
            {
                journal.connected=true;journal.clientId=rooms.LocalClientId;journal.code=rooms.Code;journal.playerCount=rooms.PlayerCount;journal.readyCount=rooms.ReadyPlayerCount;
                if(owner!=null&&owner.Player!=null)journal.position=owner.Player.transform.position;
                rooms.TryGetAdmissionSlot(journal.clientId,out journal.slot);journal.identityHash=IdentityHash();
            }
            AtomicWrite(PathFor("journal"),JsonUtility.ToJson(journal));
        }
        void ReadJournals()
        {
            for(int i=0;i<participants;i++)
            {
                if(i==index)continue;string path=Path.Combine(directory,"peer-"+i+".journal.json");if(!File.Exists(path)){journals[i]=null;continue;}
                try
                {
                    if(new FileInfo(path).Length>4096)throw new InvalidOperationException("Oversize coordination file.");
                    var peer=JsonUtility.FromJson<Journal>(File.ReadAllText(path));
                    if(peer==null||peer.runId!=runId||peer.index!=i||peer.participants!=participants||!DateTime.TryParse(peer.updatedUtc,out var updated)||
                        Math.Abs((DateTime.UtcNow-updated.ToUniversalTime()).TotalSeconds)>30){journals[i]=null;continue;}
                    journals[i]=peer;
                }
                catch(IOException){journals[i]=null;}catch(ArgumentException){journals[i]=null;}
            }
        }
        static void AtomicWrite(string path,string json)
        {string temp=path+".tmp";File.WriteAllText(temp,json);if(File.Exists(path))File.Replace(temp,path,null);else File.Move(temp,path);}
        void Save(){if(receipt!=null&&directory!=null)AtomicWrite(PathFor("receipt"),JsonUtility.ToJson(receipt,true));}
        void Finish(bool pass)
        {
            if(finished)return;
            bool accepted=pass&&receipt.nativeNetworkExecuted&&receipt.allRosterObserved&&receipt.currentRunHealthObserved&&castAccepted&&receipt.campaignEvents==1&&receipt.ownerCleanupComplete&&
                receipt.playerErrorCount==0&&(index>1||receipt.stageJoinRejected);
            if(!accepted&&string.IsNullOrEmpty(receipt.error))receipt.error=receipt.playerErrorCount>0?"Player logged an error or exception; inspect playerErrors.":"A required real-network or cleanup observation is missing.";
            receipt.eightClientsExecuted=accepted&&participants==8;
            receipt.status=accepted?(participants==8?"PASS_REAL_EIGHT_PARTICIPANTS":"PASS_REAL_TWO_PARTICIPANTS"):"FAIL";
            receipt.endedUtc=DateTime.UtcNow.ToString("o");phase="finished";receipt.phase=phase;
            if(rooms!=null)rooms.CampaignStarted-=OnCampaign;Application.logMessageReceived-=OnLog;Publish();Save();finished=true;Application.Quit(accepted?0:1);
        }
        void OnApplicationQuit()
        {
            if(finished||receipt==null)return;
            receipt.status="INCOMPLETE_INTERRUPTED";receipt.error="Player quit before bounded cleanup completed.";receipt.endedUtc=DateTime.UtcNow.ToString("o");
            try{rooms?.LeaveRoom();Publish();Save();}catch(Exception e){Debug.LogError(e.ToString());}
        }
    }
}
#endif
