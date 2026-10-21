using System;
using Unity.Collections;

using Unity.Jobs;
using Unity.Mathematics;
using OracleNatureBoundary020;

namespace OdysseyOracle
{
    // PSEUDOCODE: own cold fixed buffers for the accepted 69-floor exterior and
    // copied 128-root contact index; schedule boundary/contact math as Burst jobs;
    // retain every handle and input until completion; expose only one validated
    // current-step frame; retire by completing all jobs before releasing memory.
    public sealed class CampaignNatureBindingLease020
    {
        const int ContactCount=128,PairCount=256,PlayerCount=2;
        NativeArray<BoundaryQuad020> boundaryQuads;
        NativeArray<BoundaryEdge020> boundaryEdges;
        NativeArray<BoundaryNode020> boundaryNodes;
        NativeArray<double> boundaryCuts;
        NativeArray<BoundaryPoint020> boundaryPoints;
        NativeArray<BoundaryHeader020> boundaryHeader;
        NativeArray<BoundaryQuery020> boundaryQueries;
        NativeArray<BoundaryResult020> boundaryResults;

        NativeArray<CampaignGardenPlantContact020> plants;
        NativeArray<int> order;
        NativeArray<CampaignGardenContactCell020> cells;
        NativeArray<CampaignGardenContactIndex020> contactIndex;
        NativeArray<CampaignGardenContactPair020> pairA,pairB;
        NativeArray<CampaignGardenContactPlayer020> inputPlayers;
        NativeArray<CampaignGardenContactPlayerState020> playerStates;
        NativeArray<double> plantNext;
        NativeArray<CampaignGardenContactState020> contactState;
        NativeArray<CampaignGardenContactFrame020> contactFrame;

        JobHandle boundaryBuildHandle,contactIndexHandle,contactHandle,boundaryQueryHandle;
        bool boundaryBuildPending,contactIndexPending,contactPending,boundaryQueryPending;
        bool boundaryReady,contactsReady,pairAIsSource=true,cleanupBlocked,disposed;
        BoundaryScope020 boundaryScope;
        CampaignGardenContactScope020 contactScope;
        uint audioEpoch;
        ulong pendingSequence;
        uint pendingStep;

        public string Failure020 {get;private set;}="";
        public bool CleanupBlocked020=>cleanupBlocked;
        public bool BoundaryReady020=>boundaryReady&&!disposed;
        public bool ContactsReady020=>contactsReady&&!disposed;
        public bool FramePending020=>contactPending||boundaryQueryPending;
        public BoundaryScope020 Scope020=>boundaryScope;

        public bool BeginBoundary020(in BoundaryScope020 scope)
        {
            if(disposed||cleanupBlocked||boundaryReady||boundaryBuildPending||
                !WindBoundaryMath020.ScopeValid(scope))return Fail020("Invalid boundary lease scope.");
            boundaryScope=scope;
            try
            {
                boundaryQuads=new NativeArray<BoundaryQuad020>(WindBoundaryMath020.Quads,Allocator.Persistent,NativeArrayOptions.ClearMemory);
                boundaryEdges=new NativeArray<BoundaryEdge020>(WindBoundaryMath020.Edges,Allocator.Persistent,NativeArrayOptions.ClearMemory);
                boundaryNodes=new NativeArray<BoundaryNode020>(WindBoundaryMath020.Nodes,Allocator.Persistent,NativeArrayOptions.ClearMemory);
                boundaryCuts=new NativeArray<double>(WindBoundaryMath020.Cuts,Allocator.Persistent,NativeArrayOptions.ClearMemory);
                boundaryPoints=new NativeArray<BoundaryPoint020>(WindBoundaryMath020.Points,Allocator.Persistent,NativeArrayOptions.ClearMemory);
                boundaryHeader=new NativeArray<BoundaryHeader020>(1,Allocator.Persistent,NativeArrayOptions.ClearMemory);
                boundaryQueries=new NativeArray<BoundaryQuery020>(1,Allocator.Persistent,NativeArrayOptions.ClearMemory);
                boundaryResults=new NativeArray<BoundaryResult020>(1,Allocator.Persistent,NativeArrayOptions.ClearMemory);
                int copied;
                if(!WindBoundaryLayout020.TryCopy(boundaryQuads,
                    boundaryQuads.Length,out copied)||copied!=WindBoundaryMath020.Quads)
                    return Fail020("The accepted layout did not provide exactly 69 version-2 floor rectangles.");
                boundaryBuildHandle=new WindBoundaryBuildJob020
                {
                    Quads=boundaryQuads,Edges=boundaryEdges,Nodes=boundaryNodes,Cuts=boundaryCuts,
                    Points=boundaryPoints,Header=boundaryHeader,Scope=scope
                }.Schedule();
                boundaryBuildPending=true;
                return true;
            }
            catch(Exception error){Failure020=error.Message;return false;}
        }

        // Returns 0 while the retained build is running, 1 when exact scope is ready, and -1 on failure.
        public int PollBoundaryBuild020(in BoundaryScope020 current)
        {
            if(disposed||cleanupBlocked||!WindBoundaryMath020.ScopeValid(current)||
                !WindBoundaryMath020.Same(boundaryScope,current))return -1;
            if(boundaryReady)return 1;
            if(!boundaryBuildPending||!boundaryBuildHandle.IsCompleted)return 0;
            try
            {
                boundaryBuildHandle.Complete();boundaryBuildPending=false;boundaryBuildHandle=default;
            }
            catch(Exception error){cleanupBlocked=true;Failure020=error.Message;return -1;}
            if(!boundaryHeader.IsCreated||boundaryHeader.Length!=1)return FailStatus020("Boundary header storage is unavailable.");
            var header=boundaryHeader[0];
            if(header.Status!=BoundaryStatus020.Ready||!WindBoundaryMath020.Same(header.Scope,boundaryScope)||
                header.EdgeCount<1||header.EdgeCount>WindBoundaryMath020.Edges||header.NodeCount<1||header.NodeCount>WindBoundaryMath020.Nodes)
                return FailStatus020("Burst boundary build did not publish a bounded ready header for this scope.");
            boundaryReady=true;return 1;
        }

        public bool BeginContacts020(CampaignGardenFoliageLease020 foliage,in CampaignGardenFoliageScope020 actual,uint acceptedAudioEpoch)
        {
            if(disposed||cleanupBlocked||!boundaryReady||contactsReady||contactIndexPending||foliage==null||
                acceptedAudioEpoch==0||!actual.Positive||!CampaignGardenContactMath020.ScopeValid(ToContactScope020(actual)))
                return Fail020("Invalid foliage/contact admission.");
            contactScope=ToContactScope020(actual);audioEpoch=acceptedAudioEpoch;
            try
            {
                plants=new NativeArray<CampaignGardenPlantContact020>(ContactCount,Allocator.Persistent,NativeArrayOptions.ClearMemory);
                order=new NativeArray<int>(ContactCount,Allocator.Persistent,NativeArrayOptions.ClearMemory);
                cells=new NativeArray<CampaignGardenContactCell020>(ContactCount,Allocator.Persistent,NativeArrayOptions.ClearMemory);
                contactIndex=new NativeArray<CampaignGardenContactIndex020>(1,Allocator.Persistent,NativeArrayOptions.ClearMemory);
                pairA=new NativeArray<CampaignGardenContactPair020>(PairCount,Allocator.Persistent,NativeArrayOptions.ClearMemory);
                pairB=new NativeArray<CampaignGardenContactPair020>(PairCount,Allocator.Persistent,NativeArrayOptions.ClearMemory);
                inputPlayers=new NativeArray<CampaignGardenContactPlayer020>(PlayerCount,Allocator.Persistent,NativeArrayOptions.ClearMemory);
                playerStates=new NativeArray<CampaignGardenContactPlayerState020>(PlayerCount,Allocator.Persistent,NativeArrayOptions.ClearMemory);
                plantNext=new NativeArray<double>(ContactCount,Allocator.Persistent,NativeArrayOptions.ClearMemory);
                contactState=new NativeArray<CampaignGardenContactState020>(1,Allocator.Persistent,NativeArrayOptions.ClearMemory);
                contactFrame=new NativeArray<CampaignGardenContactFrame020>(1,Allocator.Persistent,NativeArrayOptions.ClearMemory);
                if(!foliage.TryCopyContacts020(plants,actual))return Fail020("The accepted foliage publisher refused its exact 128-contact copy.");
                contactIndexHandle=new CampaignGardenContactIndexJob020
                {
                    Plants=plants,Order=order,Cells=cells,Index=contactIndex,FirstPairs=pairA,SecondPairs=pairB,
                    Players=playerStates,PlantNext=plantNext,State=contactState,Frame=contactFrame,
                    Scope=contactScope,AudioEpoch=audioEpoch
                }.Schedule();
                contactIndexPending=true;
                return true;
            }
            catch(Exception error){Failure020=error.Message;return false;}
        }

        static CampaignGardenContactScope020 ToContactScope020(in CampaignGardenFoliageScope020 s)=>
            new CampaignGardenContactScope020
            {
                WorldToken=s.WorldToken,Epoch=s.Epoch,Generation=s.Generation,WorldRevision=s.WorldRevision,
                GeometryRevision=s.GeometryRevision,FoliageRevision=s.FoliageRevision
            };

        // Returns 0 while indexing runs, 1 when the private index is ready, and -1 on failure.
        public int PollContactIndex020(in CampaignGardenFoliageScope020 current,uint currentAudioEpoch)
        {
            if(disposed||cleanupBlocked||!CampaignGardenContactMath020.ScopeValid(contactScope)||
                !CampaignGardenContactMath020.Same(contactScope,ToContactScope020(current))||currentAudioEpoch!=audioEpoch)return -1;
            if(contactsReady)return 1;
            if(!contactIndexPending||!contactIndexHandle.IsCompleted)return 0;
            try
            {
                contactIndexHandle.Complete();contactIndexPending=false;contactIndexHandle=default;
            }
            catch(Exception error){cleanupBlocked=true;Failure020=error.Message;return -1;}
            if(!contactIndex.IsCreated||contactIndex.Length!=1||contactIndex[0].Ready!=1||
                !CampaignGardenContactMath020.Same(contactScope,contactIndex[0].Scope))
                return FailStatus020("The cold Morton index did not publish its exact contact scope.");
            contactsReady=true;return 1;
        }

        public bool ScheduleFrame020(in CampaignGardenContactPlayer020 player,in BoundaryQuery020 query,
            float delta,bool held,bool allowEvents,bool allowMotion,ulong sequence,uint step)
        {
            if(disposed||cleanupBlocked||!boundaryReady||!contactsReady||FramePending020||
                sequence==0||sequence==ulong.MaxValue||step==0||step==uint.MaxValue||audioEpoch==0||
                !math.isfinite(delta)||delta<0f||!inputPlayers.IsCreated||!boundaryQueries.IsCreated)
                return Fail020("Invalid nature frame schedule.");
            var q=query;
            q.Scope=boundaryScope;q.Step=step;q.Accepted=player.Accepted;q.Held=(byte)(held?1:0);
            q.Player=player.Accepted==1?player.PlayerId:0UL;
            q.Position=player.Accepted==1?new BoundaryPoint020(player.FeetWorld.x,player.FeetWorld.z):default;
            inputPlayers[0]=player;inputPlayers[1]=default;boundaryQueries[0]=q;
            var pairsSource=pairAIsSource?pairA:pairB;
            var pairsTarget=pairAIsSource?pairB:pairA;
            try
            {
                contactHandle=new CampaignGardenContactJob020
                {
                    Plants=plants,Order=order,Cells=cells,Index=contactIndex,
                    SourcePairs=pairsSource,TargetPairs=pairsTarget,InputPlayers=inputPlayers,
                    Players=playerStates,PlantNext=plantNext,State=contactState,Frame=contactFrame,
                    Scope=contactScope,Sequence=sequence,AudioEpoch=audioEpoch,Step=step,Delta=delta,
                    Held=(byte)(held?1:0),AllowEvents=(byte)(allowEvents?1:0),AllowMotion=(byte)(allowMotion?1:0)
                }.Schedule();
                contactPending=true;pendingSequence=sequence;pendingStep=step;
                boundaryQueryHandle=new WindBoundaryNearestJob020
                {
                    Header=boundaryHeader,Edges=boundaryEdges,Nodes=boundaryNodes,
                    Queries=boundaryQueries,Results=boundaryResults
                }.Schedule(1,1);
                boundaryQueryPending=true;
                return true;
            }
            catch(Exception error){Failure020=error.Message;return false;}
        }

        // Returns 0 while either fence runs, 1 with a coherent contact/result pair, and -1 on failure.
        public int PollFrame020(out CampaignGardenContactFrame020 frame,out BoundaryResult020 result)
        {
            frame=default;result=default;
            if(disposed||cleanupBlocked||!FramePending020)return -1;
            if(contactPending&&!contactHandle.IsCompleted||boundaryQueryPending&&!boundaryQueryHandle.IsCompleted)return 0;
            try
            {
                if(contactPending){contactHandle.Complete();contactPending=false;contactHandle=default;}
                if(boundaryQueryPending){boundaryQueryHandle.Complete();boundaryQueryPending=false;boundaryQueryHandle=default;}
            }
            catch(Exception error){cleanupBlocked=true;Failure020=error.Message;return -1;}
            if(!contactFrame.IsCreated||contactFrame.Length!=1||!boundaryResults.IsCreated||boundaryResults.Length!=1)return FailStatus020("Frame output storage is unavailable.");
            frame=contactFrame[0];result=boundaryResults[0];
            if(frame.Ready!=1||frame.Failure!=0||frame.TokenCount>CampaignGardenContactMath020.TokenCapacity||
                frame.Sequence!=pendingSequence||frame.Step!=pendingStep||frame.AudioEpoch!=audioEpoch||
                !CampaignGardenContactMath020.Same(frame.Scope,contactScope))
                return FailStatus020("Contact frame did not match its retained sequence, epoch, and scope.");
            pairAIsSource=!pairAIsSource;pendingSequence=0;pendingStep=0;
            return 1;
        }

        static void Release<T>(ref NativeArray<T> value,ref Exception first) where T:unmanaged
        {
            if(!value.IsCreated)return;
            try{value.Dispose();value=default;}
            catch(Exception error){if(first==null)first=error;}
        }

        static void Complete(ref JobHandle handle,ref bool pending,ref Exception first)
        {
            if(!pending)return;
            try{handle.Complete();handle=default;pending=false;}
            catch(Exception error){if(first==null)first=error;}
        }

        public bool TryRetire020(out string failure)
        {
            failure="";
            if(disposed)return true;
            Exception first=null;
            Complete(ref boundaryBuildHandle,ref boundaryBuildPending,ref first);
            Complete(ref contactIndexHandle,ref contactIndexPending,ref first);
            Complete(ref contactHandle,ref contactPending,ref first);
            Complete(ref boundaryQueryHandle,ref boundaryQueryPending,ref first);
            if(first!=null){cleanupBlocked=true;Failure020=first.Message;failure=Failure020;return false;}

            Release(ref boundaryQuads,ref first);Release(ref boundaryEdges,ref first);Release(ref boundaryNodes,ref first);
            Release(ref boundaryCuts,ref first);Release(ref boundaryPoints,ref first);Release(ref boundaryHeader,ref first);
            Release(ref boundaryQueries,ref first);Release(ref boundaryResults,ref first);
            Release(ref plants,ref first);Release(ref order,ref first);Release(ref cells,ref first);Release(ref contactIndex,ref first);
            Release(ref pairA,ref first);Release(ref pairB,ref first);Release(ref inputPlayers,ref first);Release(ref playerStates,ref first);
            Release(ref plantNext,ref first);Release(ref contactState,ref first);Release(ref contactFrame,ref first);
            if(first!=null){cleanupBlocked=true;Failure020=first.Message;failure=Failure020;return false;}
            disposed=true;boundaryReady=contactsReady=false;audioEpoch=0;boundaryScope=default;contactScope=default;
            Failure020="";return true;
        }

        bool Fail020(string message){Failure020=message;return false;}
        int FailStatus020(string message){Failure020=message;return -1;}
    }
}
// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Exact version-2 69-floor world-XZ exterior and accepted Y-up foliage roots; actual player capsule, no synthetic peer.
// Array Formats: Eight persistent boundary arrays (69/512/1023/140/1024/1/1/1) plus eleven owned contact arrays (128 roots, 128 order/cells, 1 index/state/frame, two 256-pair banks, 2 inputs/states, 128 cooldowns).
// Dependencies/Pointers: GameSession validates actual Session/World/root/publisher/atmosphere/audio identity before and after fences; this owner performs no Unity Object discovery, audio call, clock advancement, mesh upload, or per-frame allocation.


