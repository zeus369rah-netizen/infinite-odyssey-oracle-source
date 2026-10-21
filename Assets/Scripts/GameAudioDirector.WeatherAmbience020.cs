// PSEUDOCODE: cold-load four borrowed rain takes; admit only current unheld night-storm audio; crossfade at most two existing ambient voices; amortize exact-scope shelter probes; retire only owned state.
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace OdysseyOracle
{
    public enum WeatherAmbienceState020
    {
        Uninitialized,InactiveHub,Held,Muted,Disabled,GraphicsDisabled,MissingClip,LoadingClip,FailedClip,
        NoAmbientCapacity,ClearWeather,FadingOut,PoolWithheld,Playing,ScopeRejected
    }
    public struct WeatherAmbienceSnapshot020
    {
        public WeatherAmbienceState020 State,ClipState;
        public int MissingMask,PlayingVoices,Code,Slot,Take,ShelterSampleMask,BlockedMask;
        public uint Probes;
        public ulong Epoch,Bag;
        public float Envelope,Shelter;
        public bool GainOnlyMuffle,RaySaturated;
    }
    public sealed partial class GameAudioDirector
    {
        const string WeatherCueA020="campaign_weather_rain_a020",WeatherCueB020="campaign_weather_rain_b020";
        const string WeatherEnabledKey020=PreferencePrefix+"WeatherAmbienceEnabled020";
        static readonly string[] WeatherResources020={"CampaignHub020/Weather/rainPour01","CampaignHub020/Weather/rainPour02","CampaignHub020/Weather/rainPour03","CampaignHub020/Weather/rainPour04"};
        readonly AudioClip[] weatherClips020=new AudioClip[4],weatherVoiceClips020=new AudioClip[2];
        readonly AmbientEmitter[] weatherRecords020=new AmbientEmitter[2];
        readonly int[] weatherIds020={-1,-1};
        readonly double[] weatherStarts020=new double[2];
        readonly AudioLowPassFilter[] weatherFilters020=new AudioLowPassFilter[4];
        readonly RaycastHit[] weatherHits020=new RaycastHit[16];
        NativeArray<WeatherAmbienceProbe020> weatherProbe020;
        JobHandle weatherProbeFence020;
        WeatherAmbienceScope020 weatherScope020;
        DungeonWorld weatherWorld020;
        Transform weatherRoot020;
        WeatherAmbienceSnapshot020 weatherReadout020;
        ulong weatherEpochHigh020,weatherBag020;
        uint weatherProbeSequence020,weatherSeed020;
        double weatherLastClock020,weatherNextProbe020,weatherSwitch020,weatherTransition020;
        float weatherVolume020=.65f,weatherEnvelope020,weatherShelter020=1f;
        Vector3 weatherShelterPoint020;
        uint weatherShelterCell020;
        int weatherShelterHeight020;
        bool weatherEnabled020=true,weatherPrepared020,weatherClockKnown020,weatherProbePending020,weatherPlaylist020,weatherCrossfade020,weatherShelterCellKnown020;
        int weatherCurrent020,weatherOrdinal020,weatherSlot020=4,weatherPrevious020=-1,weatherBagPrevious020=-1,weatherSample020,weatherSampleMask020,weatherBlocked020,weatherFrame020=-1;
        byte weatherCode020;

        public bool WeatherAmbienceEnabled020
        {
            get=>weatherEnabled020;
            set{if(weatherEnabled020==value)return;weatherEnabled020=value;PlayerPrefs.SetInt(WeatherEnabledKey020,value?1:0);PlayerPrefs.Save();}
        }
        public float WeatherAmbienceVolume020
        {
            get=>weatherVolume020;
            set
            {
                if(!WeatherAmbienceRules020.Finite(value))return;
                float next=Mathf.Clamp01(value);if(Mathf.Approximately(next,weatherVolume020))return;
                weatherVolume020=next;PlayerPrefs.SetFloat(PreferencePrefix+"WeatherAmbienceVolume020",next);PlayerPrefs.Save();
                if(next<=0)StopWeatherPlayback020();ApplyVolumes();
            }
        }
        public WeatherAmbienceSnapshot020 WeatherAmbienceReadout020=>weatherReadout020;

        // Cold binding only. Clips remain shared imported assets; no PCM or clip ownership transfers.
        public void SetWeatherAmbienceRainClip020(AudioClip clip)=>SetWeatherAmbienceRainClip020(0,clip);
        public bool SetWeatherAmbienceRainClip020(int slot,AudioClip clip)
        {
            if(slot<0||slot>=4)return false;
            StopWeatherPlayback020();weatherClips020[slot]=clip;Prime(clip);return true;
        }
        void PrepareWeatherAmbience020()
        {
            if(weatherPrepared020)return;
            weatherPrepared020=true;
            weatherEnabled020=PlayerPrefs.GetInt(WeatherEnabledKey020,1)!=0;
            weatherVolume020=ReadVolume("WeatherAmbienceVolume020",.65f);
            for(int i=0;i<4;i++){weatherClips020[i]=Resources.Load<AudioClip>(WeatherResources020[i]);Prime(weatherClips020[i]);}
            for(int i=0;i<2;i++)weatherRecords020[i]=new AmbientEmitter{Cue=i==0?WeatherCueA020:WeatherCueB020,Range=30,Gain=0};
            namedClips[WeatherCueA020]=null;namedClips[WeatherCueB020]=null;
            weatherProbe020=new NativeArray<WeatherAmbienceProbe020>(1,Allocator.Persistent);
#if !UNITY_WEBGL || UNITY_EDITOR
            for(int i=0;i<4;i++)if(ambientPool[i]!=null)
            {var filter=ambientPool[i].gameObject.AddComponent<AudioLowPassFilter>();filter.lowpassResonanceQ=1;filter.cutoffFrequency=18000;filter.enabled=false;weatherFilters020[i]=filter;}
#else
            weatherReadout020.GainOnlyMuffle=true;
#endif
        }

        bool ReadWeatherAdmission020(out DungeonWorld world,out Transform root,out uint generation,out uint revision)
        {
            world=null;root=null;generation=revision=0;
            if(session==null||!ReferenceEquals(GameSession.Instance,session)||!session.isActiveAndEnabled||!session.CampaignHubActive020)return false;
            world=session.World;if(world==null||!world.isActiveAndEnabled||!world.CampaignHubActive020)return false;
            generation=world.CampaignHubGeneration020;revision=world.WorldRevision020;
            if(generation==0||revision==0||!world.TryGetPortalLandingPrototype020(generation,out var snapshot)||snapshot.Generation!=generation)return false;
            root=snapshot.Root;
            return root!=null&&root.gameObject.activeInHierarchy&&world.transform.localToWorldMatrix.Equals(Matrix4x4.identity)&&root.localToWorldMatrix.Equals(Matrix4x4.identity);
        }
        bool WeatherScopeCurrent020(in WeatherAmbienceScope020 scope)
        {
            return scope.Same(weatherScope020)&&ReadWeatherAdmission020(out var world,out var root,out uint generation,out uint revision)&&
                ReferenceEquals(world,weatherWorld020)&&ReferenceEquals(root,weatherRoot020)&&generation==scope.Generation&&revision==scope.Revision&&
                EntityId.ToULong(session.GetEntityId())==scope.Session&&EntityId.ToULong(world.GetEntityId())==scope.World&&EntityId.ToULong(root.GetEntityId())==scope.Root;
        }
        bool BindWeather020(DungeonWorld world,Transform root,uint generation,uint revision)
        {
            RetireWeatherAmbience020();
            if(weatherEpochHigh020==ulong.MaxValue)return false;
            weatherWorld020=world;weatherRoot020=root;
            weatherScope020=new WeatherAmbienceScope020{Session=EntityId.ToULong(session.GetEntityId()),World=EntityId.ToULong(world.GetEntityId()),Root=EntityId.ToULong(root.GetEntityId()),Epoch=++weatherEpochHigh020,Generation=generation,Revision=revision};
            if(!WeatherScopeCurrent020(weatherScope020))return false;
            for(int i=0;i<2;i++)
            {
                int chosen=-1;for(int j=0;j<8;j++)if(!ambientEmitters.ContainsKey(2147483300+j)){chosen=2147483300+j;break;}
                if(chosen<0||ambientEmitters.Count>=MaximumAmbientEmitters){RetireWeatherAmbience020();return false;}
                weatherIds020[i]=chosen;ambientEmitters.Add(chosen,weatherRecords020[i]);
            }
            weatherSeed020=WeatherAmbienceRules020.Mix((uint)weatherScope020.Root^(uint)(weatherScope020.Root>>32)^generation^revision);
            weatherOrdinal020=0;weatherBag020=0;weatherSlot020=4;weatherPrevious020=weatherBagPrevious020=-1;
            return true;
        }
        int WeatherRecordIndex020(int id)
        {
            for(int i=0;i<2;i++)if(id==weatherIds020[i]&&id>=0&&weatherRecords020[i]!=null&&ambientEmitters.TryGetValue(id,out var record)&&
                ReferenceEquals(record,weatherRecords020[i])&&record.Cue==(i==0?WeatherCueA020:WeatherCueB020))return i;
            return -1;
        }
        bool IsOwnedWeatherAmbient020(int id)=>WeatherRecordIndex020(id)>=0;
        bool WeatherAmbientAllowed020(int id)=>weatherFrame020==Time.frameCount&&IsOwnedWeatherAmbient020(id)&&WeatherScopeCurrent020(weatherScope020);

        void TickWeatherAmbience020()
        {
            if(!weatherPrepared020)return;
            weatherFrame020=-1;
            if(!ReadWeatherClips020())
            {RetireWeatherAmbience020();weatherReadout020.State=weatherReadout020.ClipState;return;}
            if(!ReadWeatherAdmission020(out var world,out var root,out uint generation,out uint revision))
            {RetireWeatherAmbience020();weatherReadout020.State=WeatherAmbienceState020.InactiveHub;return;}
            if(!WeatherScopeCurrent020(weatherScope020)||WeatherRecordIndex020(weatherIds020[0])!=0||WeatherRecordIndex020(weatherIds020[1])!=1)
            {
                if(!BindWeather020(world,root,generation,revision)){weatherReadout020.State=WeatherAmbienceState020.NoAmbientCapacity;return;}
            }
            var atmosphere=session.CampaignAtmosphere020;
            double clock=0;uint clockGeneration=0,clockRevision=0;bool held=true;
#if UNITY_EDITOR
            bool clockReady=atmosphere!=null&&atmosphere.TryReadEditorNatureClock020(out clock,out clockGeneration,out clockRevision,out held);
#else
            bool clockReady=atmosphere!=null&&atmosphere.TryReadPresentationClock020(out clock,out clockGeneration,out clockRevision,out held);
#endif
            if(!clockReady||clockGeneration!=generation||clockRevision!=revision||!CampaignCycleRules020.TryView(clock,generation,revision,out var cycle))
            {StopWeatherPlayback020();weatherClockKnown020=false;weatherReadout020.State=WeatherAmbienceState020.ScopeRejected;return;}
            bool audible=CanPlayCue()&&GuideGestureObserved015&&session.IsPlaying&&!session.Paused&&!session.IsDead&&!session.IsDowned&&!session.Victory&&session.Player!=null&&session.Player.isActiveAndEnabled;
            if(held||!audible)
            {HoldWeatherPlayback020();weatherLastClock020=clock;weatherClockKnown020=false;weatherReadout020.State=held||session.Paused?WeatherAmbienceState020.Held:WeatherAmbienceState020.Muted;return;}
            if(!weatherClockKnown020){weatherLastClock020=clock;weatherNextProbe020=clock;weatherClockKnown020=true;}
            if(!WeatherAmbienceRules020.TryDelta(weatherLastClock020,clock,false,out float delta))
            {StopWeatherPlayback020();weatherLastClock020=clock;weatherClockKnown020=false;weatherReadout020.State=WeatherAmbienceState020.Held;return;}
            weatherLastClock020=clock;
            bool effects=session.Graphics!=null&&session.Graphics.WeatherEffectsEnabled020;
            if(!effects){StopWeatherPlayback020();weatherReadout020.State=WeatherAmbienceState020.GraphicsDisabled;return;}
            if(weatherVolume020<=0){StopWeatherPlayback020();weatherReadout020.State=WeatherAmbienceState020.Muted;return;}
            bool target=WeatherAmbienceRules020.Enabled(cycle.Night==1,cycle.Storm==1,effects,weatherEnabled020,weatherVolume020);
            weatherEnvelope020=WeatherAmbienceRules020.Fade(weatherEnvelope020,target,delta);
            if(weatherEnvelope020<=0&&!target)
            {StopWeatherPlayback020();weatherReadout020.State=!weatherEnabled020?WeatherAmbienceState020.Disabled:WeatherAmbienceState020.ClearWeather;return;}
            Vector3 listener=ListenerPosition();
            if(!Finite(listener)||!WeatherScopeCurrent020(weatherScope020))
            {RetireWeatherAmbience020();weatherReadout020.State=WeatherAmbienceState020.ScopeRejected;return;}
            if(!AcousticEnabled015)
            {CompleteWeatherProbe020(false,listener);weatherSampleMask020=weatherBlocked020=0;weatherShelterCellKnown020=false;weatherShelter020=0;}
            else if(target)TickWeatherShelter020(listener,clock,delta);else CompleteWeatherProbe020(false,listener);
            if(!weatherPlaylist020)BeginWeatherTake020(0,clock);
            if(target&&clock>=weatherSwitch020)BeginWeatherTake020(1-weatherCurrent020,clock);
            float transition=weatherCrossfade020?WeatherAmbienceRules020.Blend(clock,weatherTransition020):1;
            if(weatherCrossfade020&&transition>=1){StopWeatherRecordVoice020(1-weatherCurrent020);weatherCrossfade020=false;}
            float gain=WeatherAmbienceRules020.OpenGain*weatherVolume020*weatherEnvelope020*Mathf.Lerp(1,WeatherAmbienceRules020.ShelterGain,weatherShelter020);
            for(int i=0;i<2;i++)if(WeatherRecordIndex020(weatherIds020[i])==i)
            {
                weatherRecords020[i].Position=listener;
                float factor=i==weatherCurrent020?(weatherCrossfade020?WeatherAmbienceRules020.EqualPower(transition):1):(weatherCrossfade020?WeatherAmbienceRules020.EqualPower(1-transition):0);
                weatherRecords020[i].Gain=gain*factor;
            }
            weatherFrame020=Time.frameCount;
            weatherReadout020.State=target?WeatherAmbienceState020.PoolWithheld:WeatherAmbienceState020.FadingOut;
            weatherReadout020.Epoch=weatherScope020.Epoch;weatherReadout020.Bag=weatherBag020;weatherReadout020.Code=weatherCode020;weatherReadout020.Slot=weatherSlot020-1;
            weatherReadout020.Take=WeatherAmbienceRules020.Take(weatherCode020,weatherSlot020-1);weatherReadout020.Envelope=weatherEnvelope020;weatherReadout020.Shelter=weatherShelter020;
        }

        bool ReadWeatherClips020()
        {
            int missing=0;WeatherAmbienceState020 clipState=WeatherAmbienceState020.Playing;
            for(int i=0;i<4;i++)
            {
                var clip=weatherClips020[i];
                if(clip==null){missing|=1<<i;clipState=WeatherAmbienceState020.MissingClip;}
                else if(clip.loadState==AudioDataLoadState.Failed){missing|=1<<i;if(clipState!=WeatherAmbienceState020.MissingClip)clipState=WeatherAmbienceState020.FailedClip;}
                else if(clip.loadState!=AudioDataLoadState.Loaded){missing|=1<<i;if(clipState==WeatherAmbienceState020.Playing)clipState=WeatherAmbienceState020.LoadingClip;}
                else if(!WeatherAmbienceRules020.ValidClip(clip.length,clip.samples,clip.frequency)){missing|=1<<i;if(clipState!=WeatherAmbienceState020.MissingClip)clipState=WeatherAmbienceState020.FailedClip;}
            }
            weatherReadout020.MissingMask=missing;
            weatherReadout020.ClipState=clipState;
            return missing==0;
        }

        void BeginWeatherTake020(int index,double clock)
        {
            if(weatherSlot020>=4)
            {
                if(weatherOrdinal020>=256){weatherBag020++;weatherOrdinal020=0;weatherBagPrevious020=weatherPrevious020;}
                weatherCode020=WeatherAmbienceRules020.Code(weatherOrdinal020++,weatherSeed020,weatherBag020,weatherBagPrevious020);weatherPrevious020=weatherCode020;weatherSlot020=0;
            }
            int take=WeatherAmbienceRules020.Take(weatherCode020,weatherSlot020++);
            StopWeatherRecordVoice020(index);
            weatherVoiceClips020[index]=weatherClips020[take];weatherStarts020[index]=clock;
            namedClips[index==0?WeatherCueA020:WeatherCueB020]=weatherVoiceClips020[index];
            weatherCrossfade020=weatherPlaylist020;weatherTransition020=clock;weatherCurrent020=index;weatherPlaylist020=true;
            weatherSwitch020=clock+weatherVoiceClips020[index].length-WeatherAmbienceRules020.CrossfadeSeconds;nextAmbientCheck=0;
        }
        bool StartWeatherVoiceAligned020(int id,AudioSource voice,AudioClip clip)
        {
            int index=WeatherRecordIndex020(id);
            if(index<0||voice==null||clip==null||clip!=weatherVoiceClips020[index]||!weatherPlaylist020)return false;
            double offset=System.Math.Max(0,weatherLastClock020-weatherStarts020[index]);
            int sample=(int)System.Math.Min(clip.samples-1,System.Math.Floor(offset*clip.frequency));
            voice.timeSamples=sample;voice.pitch=1;voice.Play();return true;
        }
        void ApplyWeatherAmbienceVolumes020()
        {
            int playing=0;
            for(int i=0;i<4;i++)
            {
                int index=WeatherRecordIndex020(ambientIds[i]);var voice=ambientPool[i];
                bool owned=index>=0&&voice!=null&&weatherVoiceClips020[index]!=null&&voice.clip==weatherVoiceClips020[index];
                if(owned)
                {
                    // Master remains solely at AudioListener; SFX and narration duck occur once here.
                    voice.volume=sfxVolume*weatherRecords020[index].Gain*duck;
                    if(voice.isPlaying&&weatherRecords020[index].Gain>0)playing++;
                }
                var filter=weatherFilters020[i];
                if(filter!=null){filter.enabled=owned&&AcousticEnabled015&&weatherShelter020>.01f;if(owned)filter.cutoffFrequency=Mathf.Lerp(18000,1800,weatherShelter020);}
            }
            weatherReadout020.PlayingVoices=playing;
            if(playing>0&&weatherReadout020.State==WeatherAmbienceState020.PoolWithheld)weatherReadout020.State=WeatherAmbienceState020.Playing;
        }
        void TickWeatherShelter020(Vector3 listener,double clock,float delta)
        {
            if(weatherShelterCellKnown020&&(listener-weatherShelterPoint020).sqrMagnitude>1.5f*1.5f)
            {weatherSampleMask020=weatherBlocked020=0;weatherShelterCellKnown020=false;}
            CompleteWeatherProbe020(true,listener);
            weatherShelter020=WeatherAmbienceRules020.Approach(weatherShelter020,WeatherAmbienceRules020.Shelter(weatherSampleMask020,weatherBlocked020),delta,.6f);
            if(weatherProbePending020||clock<weatherNextProbe020||weatherProbeSequence020==uint.MaxValue||!weatherProbe020.IsCreated)return;
            weatherNextProbe020=WeatherAmbienceRules020.NextProbe(clock);
            var job=new WeatherAmbienceProbeJob020{Result=weatherProbe020,Scope=weatherScope020,Position=(float3)listener,Sample=weatherSample020,Sequence=++weatherProbeSequence020};
            weatherSample020=(weatherSample020+1)%3;weatherProbeFence020=job.Schedule();weatherProbePending020=true;
        }
        void CompleteWeatherProbe020(bool consume,Vector3 listener)
        {
            if(!weatherProbePending020||consume&&!weatherProbeFence020.IsCompleted)return;
            weatherProbeFence020.Complete();weatherProbePending020=false;
            if(!consume)return;
            var request=weatherProbe020[0];
            if(request.Valid==0||request.Sequence!=weatherProbeSequence020||!WeatherScopeCurrent020(request.Scope)||
                ((Vector3)request.Origin-listener-Vector3.up*.12f).sqrMagnitude>.75f*.75f)
            {weatherSampleMask020=weatherBlocked020=0;return;}
            // Main-thread Object adapter only: one actual bounded PhysX ray, never a claim of a numeric-only shelter hit.
            int hits=Physics.RaycastNonAlloc((Vector3)request.Origin,(Vector3)request.Direction,weatherHits020,8f,~(1<<2),QueryTriggerInteraction.Ignore);
            bool blocked=hits>=weatherHits020.Length;
            for(int i=0;i<hits&&i<weatherHits020.Length;i++)
            {
                var collider=weatherHits020[i].collider;if(collider==null)continue;
                var item=collider.transform;
                if(session.Player!=null&&item.IsChildOf(session.Player.transform))continue;
                if(item.IsChildOf(weatherRoot020)||item.IsChildOf(weatherWorld020.transform))blocked=true;
            }
            if(!WeatherScopeCurrent020(request.Scope)){weatherSampleMask020=weatherBlocked020=0;return;}
            if(!weatherShelterCellKnown020||request.Morton!=weatherShelterCell020||request.Height!=weatherShelterHeight020)
            {weatherSampleMask020=weatherBlocked020=0;}
            weatherShelterCellKnown020=true;weatherShelterCell020=request.Morton;weatherShelterHeight020=request.Height;weatherShelterPoint020=(Vector3)request.Origin-Vector3.up*.12f;
            int bit=1<<request.Sample;weatherSampleMask020|=bit;if(blocked)weatherBlocked020|=bit;else weatherBlocked020&=~bit;
            weatherReadout020.Probes++;weatherReadout020.ShelterSampleMask=weatherSampleMask020;weatherReadout020.BlockedMask=weatherBlocked020;weatherReadout020.RaySaturated=hits>=weatherHits020.Length;
        }
        void StopWeatherRecordVoice020(int index)
        {
            if(index<0||index>=2)return;
            if(WeatherRecordIndex020(weatherIds020[index])==index)weatherRecords020[index].Gain=0;
            for(int i=0;i<4;i++)if(WeatherRecordIndex020(ambientIds[i])==index&&ambientPool[i]!=null&&ambientPool[i].clip==weatherVoiceClips020[index])
            {ambientPool[i].Stop();ambientIds[i]=-1;ambientGains[i]=0;if(weatherFilters020[i]!=null)weatherFilters020[i].enabled=false;}
        }
        void StopWeatherPlayback020()
        {
            HoldWeatherPlayback020();weatherPlaylist020=weatherCrossfade020=false;
        }
        void HoldWeatherPlayback020()
        {
            CompleteWeatherProbe020(false,default);
            for(int i=0;i<2;i++)StopWeatherRecordVoice020(i);
            weatherFrame020=-1;weatherEnvelope020=0;weatherShelter020=1;weatherSampleMask020=weatherBlocked020=0;weatherShelterCellKnown020=false;
            weatherReadout020.PlayingVoices=0;weatherReadout020.Envelope=0;weatherReadout020.Shelter=1;weatherReadout020.ShelterSampleMask=weatherReadout020.BlockedMask=0;
        }
        void RetireWeatherAmbience020()
        {
            StopWeatherPlayback020();
            for(int i=0;i<2;i++){if(WeatherRecordIndex020(weatherIds020[i])==i)ambientEmitters.Remove(weatherIds020[i]);weatherIds020[i]=-1;weatherVoiceClips020[i]=null;}
            weatherScope020=default;weatherWorld020=null;weatherRoot020=null;weatherClockKnown020=false;weatherReadout020.Epoch=0;
        }
        void DisposeWeatherAmbience020()
        {
            RetireWeatherAmbience020();
            if(weatherProbe020.IsCreated)weatherProbe020.Dispose();
            for(int i=0;i<4;i++)if(weatherFilters020[i]!=null){Destroy(weatherFilters020[i]);weatherFilters020[i]=null;}
        }
    }
}
// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Exact admitted Session/World/root generation/revision and existing held atmosphere seconds; current local listener only.
// Array Formats: Four borrowed clips, two of32 reused ambient records, at most two of existing4ambient voices, one NativeArray probe and16ray hits; no per-frame managed allocation.
// Dependencies/Pointers: Graphics weather master plus persisted Sound controls; native/editor owned lowpass or explicit WebGL gain-only muffle. Shared clips, other voices, player pose, timeScale, global settings and capture clock remain under their existing owners. Root owns native listening, import, occlusion, pool and WebGL acceptance.
