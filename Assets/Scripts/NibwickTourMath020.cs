using System;

namespace OdysseyOracle
{
    public enum NibwickPhase020 : byte { Inactive, Available, Choosing, AwaitingControl, Walking, AtStop, Speaking, Waiting, Paused, Returning, Finished }
    public enum NibwickCommand020 : byte { None, Interact, TakeTour, SkipStop, EndTour, OtherArrivals, Gardens }
    public enum NibwickVoiceResult020 : byte { None, Accepted, Completed, Interrupted }

    public struct NibwickTourInput020
    {
        public LandingAdmission020 Admission;
        public LandingPoint020 Player, Eye, Aim;
        public float Delta;
        public uint WorldRevision, VoiceToken;
        public byte Admitted, Focused, Control, BlockingMenu, StationMask, Speaking;
        public NibwickCommand020 Command;
        public NibwickVoiceResult020 VoiceResult;
    }
    public struct NibwickTourState020
    {
        public uint Generation, WorldRevision, VoiceToken, PresentedMask, SkipMask;
        public NibwickPhase020 Phase;
        public byte Greeted, Tour, ReturningRoute, AwaitingVoice, StepActive, SwingRight, WaitRemark;
        public int Node, LastLine, RequestedLine;
        public byte OpenPanel, CancelVoice, CanInteract;
        public float WaitAge, StopAge, Yaw, BodyY, StepAge, StepDuration, FromYaw, ToYaw, Speed;
        public LandingPoint020 Position, From, To, LeftFoot, RightFoot, SwingFrom, SwingTo;
        public float LeftYaw, RightYaw, SwingFromYaw, SwingToYaw;
    }

    // Pure value-state commands, revision fences, supported fixed routes and rigid
    // planted/swing feet. Called only by the bounded numeric worker, never a Transform.
    public static class NibwickTourMath020
    {
        public const float BodyRadius = .43f, SupportRadius = .30f, FootRadius = .15f, BodyHeight = 1.12f, MaximumGap = 4f;
        public const float TourSpeed = 2.4f, Acceleration = 4f, StepDistance = .24f, StepDuration = .10f;
        public const float TurnRate = 120f, ResumeGap = 3f;
        const float Rad = .017453292519943295f;
        public static bool Finite(float x) => !float.IsNaN(x) && !float.IsInfinity(x);
        public static float Clamp(float x, float lo, float hi) => Math.Max(lo, Math.Min(hi, x));
        public static float LengthXZ(LandingPoint020 a, LandingPoint020 b)
        { float x=a.X-b.X,z=a.Z-b.Z; return (float)Math.Sqrt(x*x+z*z); }
        public static float Gap(LandingPoint020 a,LandingPoint020 b)
        {float x=a.X-b.X,y=a.Y-b.Y,z=a.Z-b.Z;return (float)Math.Sqrt(x*x+y*y+z*z);}
        static LandingPoint020 Mix(LandingPoint020 a, LandingPoint020 b, float t) => new LandingPoint020(a.X+(b.X-a.X)*t,a.Y+(b.Y-a.Y)*t,a.Z+(b.Z-a.Z)*t);
        static float Ease(float t) { t=Clamp(t,0,1);return t*t*(3-2*t); }
        public static float Angle(float a, float b) { float d=(b-a)%360;return d>180?d-360:d< -180?d+360:d; }
        static LandingPoint020 Foot(LandingPoint020 p,float yaw,bool right)
        { float c=(float)Math.Cos(yaw*Rad),s=(float)Math.Sin(yaw*Rad),x=right?.09f:-.09f;return new LandingPoint020(p.X+c*x+s*.065f,p.Y,p.Z-s*x+c*.065f); }
        static bool Supported(in LandingAdmission020 context, LandingPoint020 p, float radius, out LandingPoint020 result)
        { result=default;if(!PortalLandingQueries020.TryStand(context,p,radius,out float y))return false;result=new LandingPoint020(p.X,y,p.Z);return true; }
        static bool Segment(in LandingAdmission020 context,LandingPoint020 a,LandingPoint020 b)
        {return Math.Abs(a.Y-b.Y)<=.301f&&PortalLandingQueries020.ClearSegment(a,b,BodyRadius,BodyHeight,0);}

        public static bool ValidateRoute(in LandingAdmission020 context)
        {
            if(!PortalLandingLayout020.Active(context)||context.Authority!=1||PortalLandingTour020.PointCount<6||PortalLandingTour020.PointCount>64)return false;
            LandingPoint020 previous=default;uint mask=0;
            for(int n=0;n<PortalLandingTour020.PointCount;n++)
            {
                if(!PortalLandingTour020.TryPoint(n,out var raw,out var stop)||!Supported(context,raw,SupportRadius,out var point))return false;
                if(stop>=LandingTourStop020.Shrine&&stop<=LandingTourStop020.ExitOverlook)mask|=1u<<((int)stop-2);
                if(n>0)
                {
                    float distance=LengthXZ(previous,point);int count=(int)Math.Ceiling(distance/.10f);
                    if(count<1)count=1;if(count>1024)return false;
                    LandingPoint020 from=previous;
                    for(int i=1;i<=count;i++)
                    {
                        var sample=Mix(previous,point,(float)i/count);
                        // Tread interpolation is only a probe. Publish the actual
                        // admitted floor, and validate both directions for return.
                        if(!Supported(context,sample,SupportRadius,out var to)||!Segment(context,from,to)||!Segment(context,to,from))return false;
                        from=to;
                    }
                }
                previous=point;
            }
            return mask==31u;
        }

        public static bool CanTalk(in NibwickTourInput020 input,in NibwickTourState020 state)
        {
            if(input.Control!=1||input.Focused!=1||input.BlockingMenu!=0||Gap(input.Player,state.Position)>3.25f)return false;
            var target=new LandingPoint020(state.Position.X,state.BodyY+.83f,state.Position.Z);
            float dx=target.X-input.Eye.X,dy=target.Y-input.Eye.Y,dz=target.Z-input.Eye.Z;
            float square=dx*dx+dy*dy+dz*dz,aim=input.Aim.X*input.Aim.X+input.Aim.Y*input.Aim.Y+input.Aim.Z*input.Aim.Z;
            return square>.01f&&square<3.8f*3.8f&&aim>.99f&&aim<1.01f&&
                (dx*input.Aim.X+dy*input.Aim.Y+dz*input.Aim.Z)/Math.Sqrt(square*aim)>.82d&&
                PortalLandingQueries020.ClearSegment(input.Eye,target,0,0,0);
        }
        static void Line(ref NibwickTourState020 state,int line)
        { state.VoiceToken++;if(state.VoiceToken==0)state.VoiceToken=1;state.LastLine=state.RequestedLine=line;state.AwaitingVoice=1; }
        static void Pause(ref NibwickTourState020 state)
        {state.Phase=NibwickPhase020.Paused;state.StepActive=0;state.CancelVoice=1;state.AwaitingVoice=0;}
        static void SettleFoot(in LandingAdmission020 context,ref LandingPoint020 foot,float dt)
        {
            if(!PortalLandingQueries020.TryFloor(foot.X,foot.Z,foot.Y+.31f,out float floor))return;
            float move=Clamp(floor-foot.Y,-1.8f*dt,1.8f*dt);
            foot=new LandingPoint020(foot.X,foot.Y+move,foot.Z);
        }
        static void StopPose(in LandingAdmission020 context,ref NibwickTourState020 state,float dt)
        {state.StepActive=0;state.Speed=Math.Max(0,state.Speed-Acceleration*dt);SettleFoot(context,ref state.LeftFoot,dt);SettleFoot(context,ref state.RightFoot,dt);Body(context,ref state,dt);}
        static void Body(in LandingAdmission020 context,ref NibwickTourState020 state,float dt)
        {
            float ceiling=Math.Max(state.Position.Y,Math.Max(state.LeftFoot.Y,state.RightFoot.Y))+.31f;
            if(!PortalLandingQueries020.TryFloor(state.LeftFoot.X,state.LeftFoot.Z,ceiling,out float left)||
                !PortalLandingQueries020.TryFloor(state.RightFoot.X,state.RightFoot.Z,ceiling,out float right))return;
            float target=Math.Min(left,right);
            if(state.StepActive==1)target=Math.Min(target,state.To.Y);
            state.BodyY+=Clamp(target-state.BodyY,-2f*dt,2f*dt);
        }

        public static void Step(in NibwickTourInput020 input,ref NibwickTourState020 state)
        {
            state.OpenPanel=state.CancelVoice=state.CanInteract=0;state.RequestedLine=0;
            float dt=input.Delta;
            if(input.Admitted!=1||!PortalLandingLayout020.Active(input.Admission)||input.Admission.Authority!=1||input.WorldRevision==0||
                !Finite(dt)||dt<=0||dt>.1f||!PortalLandingLayout020.Finite(input.Player)||!PortalLandingLayout020.Finite(input.Eye)||!PortalLandingLayout020.Finite(input.Aim))
            {if(state.Generation!=0)state.CancelVoice=1;state.Phase=NibwickPhase020.Inactive;state.Generation=0;return;}
            if(state.Generation!=input.Admission.Generation||state.WorldRevision!=input.WorldRevision)
            {
                uint token=state.VoiceToken;state=default;state.VoiceToken=token;state.CancelVoice=1;
                if(!PortalLandingTour020.TryPoint(0,out var home,out _)||!Supported(input.Admission,home,SupportRadius,out var actual))return;
                state.Position=actual;state.BodyY=actual.Y;state.Yaw=180;state.LeftFoot=Foot(actual,180,false);state.RightFoot=Foot(actual,180,true);
                if(!Supported(input.Admission,state.LeftFoot,FootRadius,out state.LeftFoot)||!Supported(input.Admission,state.RightFoot,FootRadius,out state.RightFoot))return;
                state.LeftYaw=state.RightYaw=180;state.Generation=input.Admission.Generation;state.WorldRevision=input.WorldRevision;
                state.Phase=NibwickPhase020.Available;state.Node=1;
            }
            if(!PortalLandingLayout020.Finite(state.Position)||!PortalLandingLayout020.Finite(state.LeftFoot)||!PortalLandingLayout020.Finite(state.RightFoot)||!Finite(state.Yaw)||!Finite(state.BodyY))
            {state.Generation=0;state.Phase=NibwickPhase020.Inactive;state.CancelVoice=1;return;}
            if(input.Focused!=1||input.BlockingMenu==1)
            {if(state.Phase!=NibwickPhase020.Available&&state.Phase!=NibwickPhase020.Paused)Pause(ref state);StopPose(input.Admission,ref state,dt);return;}
            if(state.AwaitingVoice==1&&input.VoiceToken==state.VoiceToken)
            {
                if(input.VoiceResult==NibwickVoiceResult020.Interrupted){Pause(ref state);return;}
                if(input.VoiceResult==NibwickVoiceResult020.Completed)
                {
                    state.AwaitingVoice=0;
                    if(state.Phase==NibwickPhase020.Speaking){int done=StopAt(state.Node);if(done>=0)state.PresentedMask|=1u<<done;state.Phase=NibwickPhase020.Walking;state.Node++;}
                    else if(state.Phase==NibwickPhase020.Finished){state.Tour=0;state.Phase=NibwickPhase020.Returning;state.ReturningRoute=1;state.Node=PortalLandingTour020.PointCount-2;}
                }
            }
            state.CanInteract=CanTalk(input,state)?(byte)1:(byte)0;
            if(input.Command==NibwickCommand020.Interact&&state.CanInteract==1)
            {
                StopPose(input.Admission,ref state,dt);state.Phase=NibwickPhase020.Choosing;state.OpenPanel=1;
                int line=state.Greeted==0?1:state.Tour==1?11:2;state.Greeted=1;Line(ref state,line);return;
            }
            if(input.Command!=NibwickCommand020.None&&(state.Phase==NibwickPhase020.Choosing||state.Phase==NibwickPhase020.Paused))
            {
                switch(input.Command)
                {
                    case NibwickCommand020.TakeTour:
                        if(state.Tour==0&&state.Node!=1)break;
                        state.Tour=1;state.ReturningRoute=0;state.Phase=NibwickPhase020.AwaitingControl;Line(ref state,3);return;
                    case NibwickCommand020.SkipStop:
                        if(state.Tour==1){int stop=NextStop(state.Node);if(stop>=0)state.SkipMask|=1u<<stop;state.Phase=NibwickPhase020.AwaitingControl;state.CancelVoice=1;state.AwaitingVoice=0;return;}break;
                    case NibwickCommand020.EndTour:
                        state.CancelVoice=1;state.AwaitingVoice=0;
                        if(state.Tour==1||state.Node>1||state.ReturningRoute==1){state.Tour=0;state.Phase=NibwickPhase020.Returning;if(state.ReturningRoute==0)state.Node=Math.Max(0,state.Node-1);state.ReturningRoute=1;}
                        else state.Phase=NibwickPhase020.Available;return;
                    case NibwickCommand020.OtherArrivals:state.Phase=NibwickPhase020.Choosing;Line(ref state,14);return;
                    case NibwickCommand020.Gardens:state.Phase=NibwickPhase020.Choosing;Line(ref state,15);return;
                }
            }
            if(state.Phase==NibwickPhase020.Available||state.Phase==NibwickPhase020.Choosing||state.Phase==NibwickPhase020.Paused||state.Phase==NibwickPhase020.Speaking||state.Phase==NibwickPhase020.Finished)
            {StopPose(input.Admission,ref state,dt);return;}
            if(state.Phase==NibwickPhase020.AwaitingControl)
            {
                StopPose(input.Admission,ref state,dt);
                if(input.Control==1&&state.AwaitingVoice==0&&Gap(input.Player,state.Position)<=MaximumGap)state.Phase=NibwickPhase020.Walking;
                return;
            }
            if(state.Phase==NibwickPhase020.Waiting)
            {
                StopPose(input.Admission,ref state,dt);state.WaitAge+=dt;
                if(input.Control==1&&Gap(input.Player,state.Position)<=ResumeGap){state.Phase=NibwickPhase020.Walking;state.WaitAge=0;state.WaitRemark=0;}
                else if(state.WaitAge>=20)Pause(ref state);
                return;
            }
            if(state.Phase==NibwickPhase020.AtStop)
            {
                StopPose(input.Admission,ref state,dt);state.StopAge+=dt;
                if(state.AwaitingVoice==1||state.StopAge<.35f)return;
                int stop=StopAt(state.Node);
                if(stop<0){state.Phase=NibwickPhase020.Walking;state.Node++;return;}
                uint bit=1u<<stop;
                if((state.SkipMask&bit)!=0||(input.StationMask&bit)==0){state.Phase=NibwickPhase020.Walking;state.Node++;return;}
                state.Phase=NibwickPhase020.Speaking;Line(ref state,stop+4);return;
            }
            bool returning=state.Phase==NibwickPhase020.Returning;
            if(!returning&&state.Phase!=NibwickPhase020.Walking)return;
            if(input.Control!=1&&!returning){StopPose(input.Admission,ref state,dt);return;}
            if(!returning&&Gap(input.Player,state.Position)>MaximumGap)
            {
                StopPose(input.Admission,ref state,dt);state.Phase=NibwickPhase020.Waiting;state.WaitAge=0;
                if(state.WaitRemark==0&&state.AwaitingVoice==0){state.WaitRemark=1;Line(ref state,10);}return;
            }
            if(state.Node<0||state.Node>=PortalLandingTour020.PointCount)
            {
                StopPose(input.Admission,ref state,dt);state.Phase=NibwickPhase020.Finished;
                if(state.AwaitingVoice==0)Line(ref state,9);return;
            }
            PortalLandingTour020.TryPoint(state.Node,out var target,out _);
            if(LengthXZ(state.Position,target)<.035f&&state.StepActive==0)
            {
                if(returning){if(state.Node==0){state.Phase=NibwickPhase020.Available;state.ReturningRoute=0;state.Node=1;state.SkipMask=state.PresentedMask=0;}else state.Node--;return;}
                if(StopAt(state.Node)>=0){state.Phase=NibwickPhase020.AtStop;state.StopAge=0;}else state.Node++;
                return;
            }
            if(!Move(input.Admission,target,ref state,dt))
            {StopPose(input.Admission,ref state,dt);state.Phase=NibwickPhase020.Paused;state.CancelVoice=1;state.AwaitingVoice=0;}
        }
        public static int StopAt(int node)
        {return PortalLandingTour020.TryPoint(node,out _,out var stop)&&stop>=LandingTourStop020.Shrine&&stop<=LandingTourStop020.ExitOverlook?(int)stop-2:-1;}
        public static int NextStop(int node)
        {for(int i=Math.Max(0,node);i<PortalLandingTour020.PointCount;i++){int stop=StopAt(i);if(stop>=0)return stop;}return -1;}

        static bool Move(in LandingAdmission020 context,LandingPoint020 target,ref NibwickTourState020 state,float dt)
        {
            float remainingDistance=LengthXZ(state.Position,target);
            int nextNode=state.Node+(state.Phase==NibwickPhase020.Returning?-1:1);
            bool brake=state.Phase==NibwickPhase020.Returning?state.Node==0:StopAt(state.Node)>=0;
            if(PortalLandingTour020.TryPoint(nextNode,out var after,out _))
            {
                float ax=target.X-state.Position.X,az=target.Z-state.Position.Z,bx=after.X-target.X,bz=after.Z-target.Z;
                float length=(float)Math.Sqrt((ax*ax+az*az)*(bx*bx+bz*bz));
                if(length>.00001f&&(ax*bx+az*bz)/length<.819152f)brake=true;
            }
            float targetSpeed=brake?Math.Min(TourSpeed,(float)Math.Sqrt(2*Acceleration*Math.Max(0,remainingDistance))):TourSpeed;
            state.Speed+=Clamp(targetSpeed-state.Speed,-Acceleration*dt,Acceleration*dt);
            float budget=dt;
            // dt<=.1 and minimum regular step time=.1 bound this to one full
            // step plus a remainder; third pass only resolves a short endpoint.
            for(int pass=0;pass<3&&budget>1e-7f;pass++)
            {
            if(state.StepActive==0)
            {
                float x=target.X-state.Position.X,z=target.Z-state.Position.Z,d=(float)Math.Sqrt(x*x+z*z);
                if(d<.001f)return true;
                float yaw=(float)Math.Atan2(x,z)/Rad,turn=Angle(state.Yaw,yaw);
                float distance=Math.Abs(turn)>35?0:Math.Min(StepDistance,d);
                var raw=new LandingPoint020(state.Position.X+x/d*distance,state.Position.Y,state.Position.Z+z/d*distance);
                if(!Supported(context,raw,SupportRadius,out var to)||!Segment(context,state.Position,to))return false;
                float duration=distance>0?Math.Max(.005f,distance/TourSpeed):StepDuration;
                float turnStep=TurnRate*duration;
                state.From=state.Position;state.To=to;state.FromYaw=state.Yaw;state.ToYaw=state.Yaw+Clamp(turn,-turnStep,turnStep);
                state.SwingRight=state.SwingRight==0?(byte)1:(byte)0;
                state.SwingFrom=state.SwingRight==1?state.RightFoot:state.LeftFoot;
                state.SwingFromYaw=state.SwingRight==1?state.RightYaw:state.LeftYaw;
                var foot=Foot(to,state.ToYaw,state.SwingRight==1);
                if(!Supported(context,foot,FootRadius,out state.SwingTo))return false;
                state.SwingToYaw=state.ToYaw;state.StepAge=0;
                state.StepDuration=duration;state.StepActive=1;
            }
            bool turning=LengthXZ(state.From,state.To)<.000001f;
            float rate=turning?1:state.Speed/TourSpeed;
            if(rate<.000001f)return true;
            float consumed=Math.Min(budget,(state.StepDuration-state.StepAge)/rate);
            state.StepAge=Math.Min(state.StepDuration,state.StepAge+consumed*rate);budget=Math.Max(0,budget-consumed);
            float t=state.StepAge/state.StepDuration,e=t;
            state.Position=Mix(state.From,state.To,e);state.Yaw=state.FromYaw+Angle(state.FromYaw,state.ToYaw)*e;
            float high=Math.Max(state.SwingFrom.Y,state.SwingTo.Y)+.065f;
            LandingPoint020 footPosition;
            if(t<.20f)footPosition=new LandingPoint020(state.SwingFrom.X,state.SwingFrom.Y+(high-state.SwingFrom.Y)*Ease(t/.20f),state.SwingFrom.Z);
            else if(t<.80f){var flat=Mix(state.SwingFrom,state.SwingTo,Ease((t-.20f)/.60f));footPosition=new LandingPoint020(flat.X,high,flat.Z);}
            else footPosition=new LandingPoint020(state.SwingTo.X,high+(state.SwingTo.Y-high)*Ease((t-.80f)/.20f),state.SwingTo.Z);
            float footYaw=state.SwingFromYaw+Angle(state.SwingFromYaw,state.SwingToYaw)*e;
            if(state.SwingRight==1){state.RightFoot=footPosition;state.RightYaw=footYaw;}else{state.LeftFoot=footPosition;state.LeftYaw=footYaw;}
            if(t>=1)state.StepActive=0;
            }
            Body(context,ref state,dt);
            return true;
        }
    }
}

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Current admitted Portal Landing metres/Y-up, world planted feet and positive-yaw rigid root; supported37-node route, no mirroring or teleport catch-up.
// Array Formats: One blittable input/state command record, eleven finite phases, two planted/swing foot records and scalar voice/revision fences; immutable builder route/query tables.
// Dependencies/Pointers: Numeric worker only, no Unity Object, allocation, authority/reward/save mutation or audio queue. Default/no-data closes; world revisions cancel; actual accepted E/opt-in produces one exact line request through the existing voice owner.
