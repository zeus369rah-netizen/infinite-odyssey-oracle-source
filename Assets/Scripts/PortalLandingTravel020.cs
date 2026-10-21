namespace OdysseyOracle
{
    public enum LandingTravelResult020:byte {None,Accepted,Context,Stale,Busy,Locked,Reach,Cancelled,Expired,Destination}
    public struct LandingTravelState020
    {
        public uint OwnerGeneration,HighestNonce,CachedNonce,CachedStory,CachedEndpoint,PendingNonce,Generation,StoryRevision,Endpoint,DestinationGeneration;
        public LandingTravelResult020 CachedResult;
        public byte Phase;
        public double Deadline;
    }
    public static class PortalLandingTravel020
    {
        public const double TimeoutSeconds=30d;
        static bool Clock(double now)=>!double.IsNaN(now)&&!double.IsInfinity(now)&&now>=0d;
        static bool Current(in LandingTravelState020 state,in LandingAdmission020 context,double now)=>Clock(now)&&PortalLandingLayout020.Active(context)&&context.Authority==1&&
            state.Generation==context.Generation&&state.StoryRevision==context.StoryRevision&&state.PendingNonce!=0&&now<state.Deadline&&PortalLandingLayout020.Unlocked(context,(int)state.Endpoint);
        public static void Reset(ref LandingTravelState020 state,uint admittedGeneration){state=default;state.OwnerGeneration=admittedGeneration;}
        public static LandingTravelResult020 Request(ref LandingTravelState020 state,in LandingAdmission020 context,uint nonce,int endpoint,LandingPoint020 acceptedFeet,LandingPoint020 acceptedAim,double now,out bool replayed)
        {
            replayed=false;
            if(!Clock(now)||!PortalLandingLayout020.Active(context)||context.Authority!=1||nonce==0||endpoint<1||endpoint>2)return LandingTravelResult020.Context;
            if(state.OwnerGeneration==0)state.OwnerGeneration=context.Generation;
            if(state.OwnerGeneration!=context.Generation)return LandingTravelResult020.Stale;
            Tick(ref state,context,now);
            if(nonce<=state.HighestNonce)
            {
                if(nonce==state.CachedNonce&&state.CachedEndpoint==(uint)endpoint&&state.CachedStory==context.StoryRevision){replayed=true;return state.CachedResult;}
                return LandingTravelResult020.Stale;
            }
            state.HighestNonce=state.CachedNonce=nonce;state.CachedEndpoint=(uint)endpoint;state.CachedStory=context.StoryRevision;
            LandingTravelResult020 result;
            if(state.Phase!=0)result=LandingTravelResult020.Busy;
            else if(!PortalLandingLayout020.Unlocked(context,endpoint))result=LandingTravelResult020.Locked;
            else if(!PortalLandingQueries020.EndpointReach(context,endpoint,acceptedFeet,acceptedAim))result=LandingTravelResult020.Reach;
            else
            {
                state.PendingNonce=nonce;state.Generation=context.Generation;state.StoryRevision=context.StoryRevision;state.Endpoint=(uint)endpoint;
                state.DestinationGeneration=0;state.Phase=1;state.Deadline=now+TimeoutSeconds;result=LandingTravelResult020.Accepted;
            }
            state.CachedResult=result;return result;
        }
        public static bool ValidateDestination(ref LandingTravelState020 state,in LandingAdmission020 context,uint nonce,uint destination,uint destinationGeneration,bool actualWorldReady,bool supportedSpawn,double now)
        {
            if(state.Phase!=1||state.PendingNonce!=nonce||!Current(state,context,now)||!PortalLandingLayout020.TryEndpoint((int)state.Endpoint,out var endpoint)||endpoint.Destination!=destination||destinationGeneration==0||!actualWorldReady||!supportedSpawn)return false;
            state.DestinationGeneration=destinationGeneration;state.Phase=2;return true;
        }
        public static bool Blackout(ref LandingTravelState020 state,in LandingAdmission020 context,uint nonce,float alpha,double now)
        {
            if(state.Phase!=2||state.PendingNonce!=nonce||!Current(state,context,now)||!PortalLandingLayout020.Finite(alpha)||alpha!=1f||state.DestinationGeneration==0)return false;
            state.Phase=3;return true;
        }
        public static bool Commit(ref LandingTravelState020 state,in LandingAdmission020 context,uint nonce,uint actualDestination,uint actualDestinationGeneration,bool actualWorldReady,bool supportedSpawn,double now,out int endpoint,out uint destinationGeneration)
        {
            endpoint=0;destinationGeneration=0;
            if(state.Phase!=3||state.PendingNonce!=nonce||!Current(state,context,now)||state.DestinationGeneration==0||actualDestination!=state.Endpoint||actualDestinationGeneration!=state.DestinationGeneration||!actualWorldReady||!supportedSpawn)return false;
            endpoint=(int)state.Endpoint;destinationGeneration=state.DestinationGeneration;state.Phase=4;return true;
        }
        public static bool GateClosed(in LandingTravelState020 state,in LandingAdmission020 context,int endpoint,double now)
            =>endpoint<1||endpoint>2||state.Phase!=4||state.Endpoint!=(uint)endpoint||!Current(state,context,now);
        public static void Cancel(ref LandingTravelState020 state,LandingTravelResult020 reason=LandingTravelResult020.Cancelled)
        {
            if(state.CachedNonce==state.PendingNonce)state.CachedResult=reason;
            state.Phase=0;state.PendingNonce=state.Generation=state.StoryRevision=state.Endpoint=state.DestinationGeneration=0;state.Deadline=0;
        }
        public static void Tick(ref LandingTravelState020 state,in LandingAdmission020 context,double now)
        {
            if(state.Phase==0)return;
            if(!Current(state,context,now))Cancel(ref state,Clock(now)&&now>=state.Deadline?LandingTravelResult020.Expired:LandingTravelResult020.Context);
        }
    }
}
// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Explicit source world generation/story revision, trusted accepted feet/aim and destination-instance generation; monotonic real-time seconds.
// Array Formats: One flat bounded request/cache/transition state and enum result; no event closures, heap, save, quest, wire or mode mutation.
// Dependencies/Pointers: Trusted owner supplies actual authority, story bits, region readiness and supported spawn. Requested->destination validated->blackout->one fresh destination-revalidated commit precedes optional gate opening. Cancel/stale/inactive/timeout remains closed; permanent outer caps never open or follow alpha. No destination region is fabricated.
