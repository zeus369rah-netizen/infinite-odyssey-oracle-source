using System;
using System.Collections.Generic;
using UnityEngine;

namespace OdysseyOracle
{
    /// <summary>
    /// Bounded playback of the project's imported Odyssey recordings. Resources are
    /// shared imported clips; this component never generates or duplicates PCM.
    /// Playback starts only after StartAudioFromGesture is called by a UI click.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed partial class GameAudioDirector : MonoBehaviour
    {
        const string PreferencePrefix = "Oracle.Audio.";
        const float NarrationDuck = .25118864f; // -12 dB
        const float MusicCrossfadeSeconds = 3f;
        const int MaximumAmbientEmitters = 32;
        const string BookWritingResource = "Audio/WorldCue/book_write";
        const float BookWritingGain = .035f;
        static readonly float[] CastPitches = {1f,.96f,.88f,1.12f};
        GameSession session;
        AudioSource music, musicNext, campfire, ui, cast, portal;
        readonly AudioSource[] ambientPool = new AudioSource[4];
        readonly int[] ambientIds = { -1, -1, -1, -1 };
        readonly float[] ambientGains = new float[4];
        sealed class AmbientEmitter
        {
            public string Cue;
            public Vector3 Position;
            public float Range, Gain;
        }
        readonly Dictionary<int, AmbientEmitter> ambientEmitters = new Dictionary<int, AmbientEmitter>(MaximumAmbientEmitters);
        readonly AudioSource[] towerPool = new AudioSource[4], cuePool = new AudioSource[4];
        readonly float[] towerNext = new float[4], towerStarted = new float[4], towerGains = new float[4];
        readonly float[] cueNext = new float[4], cueStarted = new float[4], cueGains = new float[4];
        // # ARCHITECTURE SNAPSHOT
        // Fixed managed clip/admission caches; borrow the existing four cue
        // sources. Resources load once in Start after legacy Awake registration.
        readonly AudioClip[] productionClips = new AudioClip[ProductionSoundBank.Count];
        readonly float[] productionNext = new float[ProductionSoundBank.Count];
        readonly bool[] productionSpellBound = new bool[4];
        readonly ProductionCueAdmission cueAdmission = new ProductionCueAdmission();
        AudioClip productionNovaClip;
        bool productionResourcesBound;
        float nextProductionStep, nextRemoteSpell, nextProductionNova, activeCastGain = .13f;
        readonly Dictionary<string, AudioClip> namedClips = new Dictionary<string, AudioClip>(StringComparer.Ordinal);
        readonly Dictionary<string, float> namedNext = new Dictionary<string, float>(StringComparer.Ordinal);
        static readonly string[] CueNames = { "portal_open", "portal_travel", "pickup", "passive_unlock", "wand_upgrade",
            "downed", "revive", "enemy_windup", "enemy_impact", "book_open", "ui_hover", "footstep_stone_01",
            "footstep_stone_02", "footstep_wood_01", "footstep_wood_02", "jump", "land_stone", "land_wood",
            "torch_crackle", "forge_idle", "forge_hit", "crossbow_fire", "element_aura" };
        AudioClip musicClip, fireClip, cueClip;
        readonly AudioClip[] menuMusicClips = new AudioClip[2];
        AudioClip arenaMusicClip;
        int menuMusicIndex;
        bool musicModeKnown, arenaMusicMode, musicTransition, musicSuspended;
        float musicFadeElapsed, musicGain = 1f, musicNextGain, nextAmbientCheck;
        readonly AudioClip[] elementCastClips = new AudioClip[4];
        readonly AudioClip[] elementTowerClips = new AudioClip[8];
        AudioClip supportClip;
        float masterVolume, musicVolume, sfxVolume;
        float duck = 1f, uiStopAt = -1f, nextPlaybackCheck, nextUiAt, nextCastAt, nextTowerAt;
        bool initialized, gestureAuthorized, gameplayMix, campfirePositionSet;
        bool portalActive, applicationPaused;
        float priorListenerVolume, uiGain = .055f, uiProtectedUntil;
        Transform listenerTransform;
        OracleFoley foley;
        OdysseyHud bookHud;
        AudioSource bookWriting;
        AudioClip bookWritingClip;
        bool bookWritingActive;

        public float MasterVolume
        {
            get => masterVolume;
            set => StoreVolume(ref masterVolume,"Master",value);
        }
        public float MusicVolume
        {
            get => musicVolume;
            set => StoreVolume(ref musicVolume,"Music",value);
        }
        public float SfxVolume
        {
            get => sfxVolume;
            set => StoreVolume(ref sfxVolume,"Sfx",value);
        }

        public void Initialize(GameSession owner)
        {
            session = owner;
            if(initialized)return;
            initialized = true;
            priorListenerVolume = AudioListener.volume;
            masterVolume = ReadVolume("Master",.85f);
            musicVolume = ReadVolume("Music",.45f);
            sfxVolume = ReadVolume("Sfx",.75f);
            musicClip = Resources.Load<AudioClip>("Audio/Music/oracle_return_theme");
            menuMusicClips[0] = musicClip;
            menuMusicClips[1] = Resources.Load<AudioClip>("Audio/Music/oracle_return_theme_two");
            arenaMusicClip = musicClip; // Existing provenance-verified Creator music track.
            fireClip = null; // Unverified legacy ambience is preserved outside the shipping graph.
            cueClip = Resources.Load<AudioClip>("Audio/ProductionCue/shrine_confirm");
            LoadStaffSpiralClip();
            music = Source("Odyssey music A",false,128);
            musicNext = Source("Odyssey music B",false,128);
            campfire = Source("Original spatial campfire",true,224);
            campfire.loop = true;
            campfire.clip = fireClip;
            campfire.minDistance = 1.5f;
            campfire.maxDistance = 14f;
            campfire.rolloffMode = AudioRolloffMode.Logarithmic;
            ui = Source("Soft original interface cue",false,80);
            ui.clip = cueClip;
            cast = Source("Original Oracle cast cue",false,96);
            cast.clip = cueClip;
            bookWriting = Source("Quiet 2D book writing", false, 176);
            bookWriting.loop = true;
            SetBookWritingClip(Resources.Load<AudioClip>(BookWritingResource));
            for(int i=0;i<4;i++)
            {
                towerPool[i]=Source("Positional tower voice "+i,true,144);
                cuePool[i]=Source("World cue voice "+i,true,160);
                towerGains[i]=.1f;cueGains[i]=.1f;
                ambientPool[i] = Source("Nearest spatial ambience " + i,true,232);
                ambientPool[i].loop = true;
            }
            portal=Source("Spatial portal idle",true,224);
            portal.loop=true;portal.minDistance=2f;portal.maxDistance=24f;
            portal.clip=Resources.Load<AudioClip>("Audio/RefugeLoops015/portal_idle_overlap015");
            foreach(string name in CueNames)
            {
                AudioClip clip=Resources.Load<AudioClip>("Audio/WorldCue/"+name);
                namedClips[name]=clip;namedNext[name]=0f;
                if(clip!=null)clip.LoadAudioData();
            }
            if(portal.clip!=null)portal.clip.LoadAudioData();
            AttachFoley();
            // The root owns importer configuration and browser verification.
            // Request the opening theme immediately. Browsers may suspend their
            // audio context until the visitor's first permitted interaction.
            if(musicClip!=null)musicClip.LoadAudioData();
            if(menuMusicClips[1]!=null)menuMusicClips[1].LoadAudioData();
            if(arenaMusicClip!=null)arenaMusicClip.LoadAudioData();
            if(fireClip!=null)fireClip.LoadAudioData();
            if(cueClip!=null)cueClip.LoadAudioData();
            gestureAuthorized = true;
            InitializePlaylist009();
            InitializeCauldronAmbience009();
            InitializeAcoustics015();
            PrepareWeatherAmbience020();
            PrepareSupportedFootsteps020();
            ApplyVolumes();
            if(musicClip==null || cueClip==null)
                Debug.LogWarning("Original Odyssey audio is incomplete; missing clips stay silent.");
        }

        void Start() { BindProductionResources(); }

        public void BindProductionResources()
        {
            if (!initialized || productionResourcesBound) return;
            productionResourcesBound = true;
            LoadElementImpactClips();
            PrepareWorkshopAudio015();
            PreparePlayerEffort020();
            PrepareCampaignArrival020();
            for (int i = 0; i < ProductionSoundBank.Count; i++)
            {
                AudioClip clip = Resources.Load<AudioClip>("Audio/ProductionCue/" + ProductionSoundBank.Names[i]);
                productionClips[i] = clip; Prime(clip);
            }
            string[] spells = { "fire_cast", "water_cast", "earth_cast", "air_cast" };
            for (int i = 0; i < 4; i++)
            {
                AudioClip clip = Resources.Load<AudioClip>("Audio/SpellCue/" + spells[i]);
                if (clip == null) continue;
                productionSpellBound[i] = true; SetCastClip(i, clip);
            }
            productionNovaClip = Resources.Load<AudioClip>("Audio/SpellCue/cosmic_nova");
            Prime(productionNovaClip);
            PrepareRefugeSounds015();
            PrepareWindowBreezeAudio015();
            PrepareElementUpgrades011();
        }

        public bool HasProductionCue(ProductionCue cue) => ProductionSoundBank.Valid(cue) &&
            productionClips[(int)cue] != null && productionClips[(int)cue].loadState == AudioDataLoadState.Loaded;

        public bool PlayProductionCue(ProductionCue cue, Vector3? position = null)
        {
            if (!CanPlayCue() || !HasProductionCue(cue) || session == null || !session.HasStarted || session.IsDead || session.Victory) return false;
            bool isInterface = cue == ProductionCue.TowerBuilderOpen || cue == ProductionCue.TowerConfirm ||
                cue == ProductionCue.TowerUpgrade || cue == ProductionCue.ShrineConfirm;
            if (!isInterface && !session.SimulationActive || position.HasValue && !Near(position.Value, 30f)) return false;
            float now = Time.unscaledTime;
            if (now < productionNext[(int)cue] || ProductionSoundBank.Step(cue) && now < nextProductionStep) return false;
            if (!PlayPrioritizedCue(productionClips[(int)cue], position ?? ListenerPosition(), position.HasValue,
                ProductionSoundBank.Gain(cue), ProductionSoundBank.Priority(cue), .05f)) return false;
            productionNext[(int)cue] = now + ProductionSoundBank.Interval(cue);
            if (ProductionSoundBank.Step(cue)) nextProductionStep = now + .5f;
            return true;
        }

        // Called only from accepted non-own shot / actual Nova impact consumers.
        // Current002 has no Nova mechanic: preloaded audio does not create one.
        public void PlayRemoteCast(int element, Vector3 position)
        {
            if (!CanPlayCue() || session == null || !session.SimulationActive || !session.CombatUnlocked ||
                element < 0 || element >= 4 || !Near(position, 30f) || Time.unscaledTime < nextRemoteSpell) return;
            if (PlayPrioritizedCue(elementCastClips[element], position, true, .055f, 1, .05f)) nextRemoteSpell = Time.unscaledTime + .06f;
        }
        public void PlayCosmicNova(Vector3 position)
        {
            if (!CanPlayCue() || session == null || !session.SimulationActive || !Near(position, 30f) || Time.unscaledTime < nextProductionNova) return;
            if (PlayPrioritizedCue(productionNovaClip, position, true, .10f, 2, .08f)) nextProductionNova = Time.unscaledTime + .25f;
        }

        bool PlayPrioritizedCue(AudioClip clip, Vector3 position, bool spatial, float gain, int priority, float cadence)
        {
            if (clip == null || clip.loadState != AudioDataLoadState.Loaded || !Finite(position)) return false;
            int busy = 0;
            for (int i = 0; i < cuePool.Length; i++) if (cuePool[i] != null && (cuePool[i].isPlaying || AcousticVoiceBusy015(i))) busy |= 1 << i;
            float now = Time.unscaledTime;
            int selected = cueAdmission.Select(now, priority, busy);
            if (selected < 0 || cuePool[selected] == null) return false;
            AudioSource source = cuePool[selected]; source.Stop(); source.transform.position = position;
            source.spatialBlend = spatial ? 1f : 0f; source.clip = clip; source.pitch = 1f;
            cueGains[selected] = Mathf.Clamp(gain, 0f, .25f); source.volume = sfxVolume * cueGains[selected];
            cueNext[selected] = now + cadence; cueStarted[selected] = now;
            cueAdmission.Commit(selected, now, priority, Mathf.Min(priority == 3 ? .7f : .25f, clip.length), cadence);
            PlayAcousticVoice015(selected, source); return true;
        }

        /// <summary>Call directly from a visible button's click before entering play.</summary>
        public void StartAudioFromGesture()
        {
            if(!initialized)return;
            RecordGuideGesture015();
            gestureAuthorized = true;
            PlaylistGesture009();
            ResumeMusic();
            nextPlaybackCheck = 0f;
            ApplyVolumes();
            EnsureLoops();
        }

        public void SetGameplayMix(bool active)
        {
            gameplayMix = active;
            if(!active) {portalActive=false;if(portal!=null)portal.Stop();}
            nextPlaybackCheck = 0f;
            ApplyVolumes();
        }

        public void SetCampfirePosition(Vector3 position)
        {
            if(!Finite(position) || campfire==null)return;
            campfire.transform.position = position;
            campfirePositionSet = true;
            nextPlaybackCheck = 0f;
        }

        public void SetListenerTransform(Transform listener) {listenerTransform=listener;InitializeWebAudio015(listener);}
        public void SetPortalPosition(Vector3 position,bool active=true)
        {
            if(portal==null||!Finite(position))return;
            portal.transform.position=position;portalActive=active;nextPlaybackCheck=0f;
            if(!active)portal.Stop();
        }
        public void SetMusicClip(AudioClip clip)
        {
            // Compatibility: root's existing call configures the first peaceful
            // track; it never replaces the arena combat recording.
            SetMenuMusicClip(0,clip);
        }
        public void SetMenuMusicClip(int index,AudioClip clip)
        {
            if(index<0||index>=menuMusicClips.Length)return;
            menuMusicClips[index]=clip;
            if(index==0)musicClip=clip;
            Prime(clip);nextPlaybackCheck=0f;
        }
        public void SetArenaMusicClip(AudioClip clip)
        {
            arenaMusicClip=clip;Prime(clip);nextPlaybackCheck=0f;
        }
        public void SetCueClip(string name,AudioClip clip)
        {
            if(name=="portal_idle") {if(portal!=null){portal.Stop();portal.clip=clip;}nextPlaybackCheck=0f;}
            else if(name!=null&&namedClips.ContainsKey(name))namedClips[name]=clip;
            else return;
            if(clip!=null)clip.LoadAudioData();
        }

        public void PlayCrossbow(Vector3 position) { PlayCue("crossbow_fire",position); }

        // Root registers actual placed world objects. Fixed voices render only
        // the nearest four audible emitters; missing optional recordings stay silent.
        public bool SetAmbientEmitter(int id,string cue,Vector3 position,bool active=true,float range=14f,float gain=.04f)
        {
            if(!active){RemoveAmbientEmitter(id);return true;}
            if(id<0||!Finite(position)||!IsAmbientCue(cue)||float.IsNaN(range)||float.IsInfinity(range)||
                float.IsNaN(gain)||float.IsInfinity(gain))return false;
            if(!ambientEmitters.TryGetValue(id,out var emitter))
            {
                if(ambientEmitters.Count>=MaximumAmbientEmitters)return false;
                emitter=new AmbientEmitter();ambientEmitters.Add(id,emitter);
            }
            emitter.Cue=cue;emitter.Position=position;emitter.Range=Mathf.Clamp(range,2f,30f);
            emitter.Gain=Mathf.Clamp(gain,0f,.1f);nextAmbientCheck=0f;return true;
        }
        public void RemoveAmbientEmitter(int id)
        {
            if(id<0)return;
            ambientEmitters.Remove(id);
            for(int i=0;i<ambientPool.Length;i++)if(ambientIds[i]==id){ambientPool[i].Stop();ambientIds[i]=-1;ambientGains[i]=0f;}
        }
        public void ClearAmbientEmitters()
        {
            RetireCampaignNatureAudio020();
            RetireWeatherAmbience020();
            ambientEmitters.Clear();KeepWindowBreezeRecords015();StopAmbient();
        }
        static bool IsAmbientCue(string cue) => cue=="torch_crackle"||cue=="forge_idle"||cue=="element_aura"||
            cue==WindowDarkCue015||cue==WindowBrightCue015||cue==CandleCrackleCue015||cue==CampaignWindCue020||cue==WeatherCueA020||cue==WeatherCueB020;

        public void PlayUi()
        {
            if(!CanPlayCue() || Time.unscaledTime<nextUiAt || Time.unscaledTime<uiProtectedUntil || cueClip==null)return;
            nextUiAt = Time.unscaledTime + .1f;
            ui.Stop();
            ui.pitch = 1.08f;
            ui.clip=cueClip;uiGain=.055f;
            ui.volume = sfxVolume*uiGain;
            ui.Play();
            // A quiet 110 ms onset of the verified cue is the interface tick.
            // The recording on disk remains byte-identical to the original.
            uiStopAt = Time.unscaledTime+.11f;
        }

        public void PlayCast(int element)
        {
            if(!CanPlayCue() || session==null || !session.IsPlaying || !session.CombatUnlocked ||
                Time.unscaledTime<nextCastAt)return;
            nextCastAt = Time.unscaledTime+.10f;
            cast.Stop();
            int index = Mathf.Clamp(element,0,3);
            cast.clip = elementCastClips[index]!=null?elementCastClips[index]:cueClip;
            cast.pitch = elementCastClips[index]!=null?1f:CastPitches[index];
            activeCastGain = productionSpellBound[index] ? .08f : .13f;
            cast.volume = sfxVolume*activeCastGain;
            cast.Play();
        }

        public void PlayTower(int element)
        {
            PlayTower(element,ListenerPosition());
        }
        public void PlayTower(int element,Vector3 position)
        {
            if(!CanPlayCue()||session==null||!session.SimulationActive||element<0||element>=8||!Near(position,30f))return;
            if (element == 4 && HasProductionCue(ProductionCue.LightningZap)) { PlayProductionCue(ProductionCue.LightningZap, position); return; }
            PlayPooled(towerPool,towerNext,towerStarted,towerGains,elementTowerClips[element]!=null?elementTowerClips[element]:cueClip,
                position,true,.11f,.1f);
        }

        public void PlaySupport()
        {
            if(!CanPlayCue() || session==null || !session.SimulationActive || Time.unscaledTime<nextTowerAt)return;
            nextTowerAt = Time.unscaledTime+.14f;
            PlayPooled(towerPool,towerNext,towerStarted,towerGains,supportClip!=null?supportClip:cueClip,
                ListenerPosition(),false,.09f,.14f);
        }

        public void PlayCue(string name,Vector3? position=null)
        {
            if(!CanPlayCue()||string.IsNullOrEmpty(name))return;
            if(name=="book_open"||name=="book_close"){TryPlayBookSurface015(name=="book_open");return;}
            if(name=="portal_idle"){if(position.HasValue)SetPortalPosition(position.Value);return;}
            if(!namedClips.TryGetValue(name,out AudioClip clip)||clip==null||clip.loadState!=AudioDataLoadState.Loaded||Time.unscaledTime<namedNext[name])return;
            bool isUi=name=="ui_hover"||name=="book_open"||name=="passive_unlock"||name=="wand_upgrade";
            if(!isUi&&(session==null||!session.HasStarted||session.Victory||session.IsDead))return;
            if(position.HasValue&&!Near(position.Value,30f))return;
            float interval=name=="ui_hover"?.1f:name=="book_open"?.25f:name.StartsWith("footstep_",StringComparison.Ordinal)?.18f:
                name.StartsWith("enemy_",StringComparison.Ordinal)?.08f:.3f;
            if(isUi)
            {
                if(name=="ui_hover"&&(Time.unscaledTime<nextUiAt||Time.unscaledTime<uiProtectedUntil))return;
                ui.Stop();ui.clip=clip;ui.pitch=1f;uiGain=name=="ui_hover"?.045f:.14f;ui.volume=sfxVolume*uiGain;ui.Play();uiStopAt=-1f;
                nextUiAt=Time.unscaledTime+.1f;
                if(name!="ui_hover")uiProtectedUntil=Time.unscaledTime+Mathf.Min(.6f,clip.length);
            }
            else if(!PlayPrioritizedCue(clip,position??ListenerPosition(),position.HasValue,
                name.StartsWith("footstep_",StringComparison.Ordinal)?.1f:.14f,
                name.StartsWith("footstep_",StringComparison.Ordinal)?0:name=="enemy_windup"?3:name=="enemy_impact"?2:1,.05f))return;
            namedNext[name]=Time.unscaledTime+interval;
        }

        bool PlayPooled(AudioSource[] sources,float[] next,float[] started,float[] gains,AudioClip clip,Vector3 position,bool spatial,float gain,float cadence)
        {
            if(!ReferenceEquals(sources,towerPool)||clip==null||clip.loadState!=AudioDataLoadState.Loaded||!Finite(position))return false;
            float now=Time.unscaledTime;int busy=0;
            for(int i=0;i<4;i++)if(sources[i]!=null&&(sources[i].isPlaying||AcousticVoiceBusy015(i+4)))busy|=1<<i;
            int selected=towerAdmission015.Select(now,1,busy);
            if(selected<0||sources[selected]==null)return false;
            AudioSource source=sources[selected];source.Stop();source.transform.position=position;
            source.spatialBlend=spatial?1f:0f;source.clip=clip;source.pitch=1f;
            gains[selected]=Mathf.Clamp(gain,0f,.25f);source.volume=sfxVolume*gains[selected];
            next[selected]=now+cadence;started[selected]=now;
            if(!towerAdmission015.Commit(selected,now,1,Mathf.Min(.15f,clip.length),cadence))return false;
            PlayAcousticVoice015(selected+4,source);return true;
        }

        // Root can register newly imported effects without changing recording paths
        // or hardcoding an unverified resource name in this source.
        public void SetCastClip(int element,AudioClip clip)
        {
            if(element>=0 && element<4){elementCastClips[element]=clip;if(clip!=null)clip.LoadAudioData();}
        }
        public void SetTowerClip(int element,AudioClip clip)
        {
            if(element>=0 && element<8){elementTowerClips[element]=clip;if(clip!=null)clip.LoadAudioData();}
        }
        public void SetSupportClip(AudioClip clip) {supportClip=clip;if(clip!=null)clip.LoadAudioData();}

        /// <summary>Imported optional recording only; missing audio keeps the reveal silent.</summary>
        public void SetBookWritingClip(AudioClip clip)
        {
            StopBookWriting();
            bookWritingClip = clip;
            Prime(clip);
            if (bookWriting != null) bookWriting.clip = clip;
        }

        void AttachBookAudio()
        {
            OdysseyHud next = session != null ? session.Hud : null;
            if (next == null) next = null; // Normalize a destroyed Unity object.
            if (ReferenceEquals(next, bookHud)) return;
            DetachBookAudio();
            bookHud = next;
            if (bookHud == null) return;
            bookHud.BookWritingStateChanged += OnBookWritingChanged;
            bookWritingActive = bookHud.IsBookWriting;
        }

        void DetachBookAudio()
        {
            // This delegate is a managed field, so removal is also safe after the
            // Unity object has been destroyed but its managed wrapper remains.
            if (!ReferenceEquals(bookHud, null)) bookHud.BookWritingStateChanged -= OnBookWritingChanged;
            bookHud = null;
            bookWritingActive = false;
            StopBookWriting();
        }

        void OnBookWritingChanged(bool writing)
        {
            bookWritingActive = writing;
            TickBookWriting();
        }

        void TickBookWriting()
        {
            if (bookWriting == null) return;
            bool active = isActiveAndEnabled && CanPlayCue() && bookWritingActive && bookHud != null
                && bookHud.IsBookWriting && bookWritingClip != null;
            if (!active) { StopBookWriting(); return; }
            Prime(bookWritingClip);
            if (bookWritingClip.loadState != AudioDataLoadState.Loaded) return;
            // A visible book intentionally pauses gameplay. Its real reveal state
            // owns this loop; session.Paused must not suppress writing audio.
            if (!bookWriting.isPlaying) bookWriting.Play();
        }

        void StopBookWriting() { if (bookWriting != null && bookWriting.isPlaying) bookWriting.Stop(); }
        void OnEnable() { if (initialized) AttachBookAudio(); }

        void Update()
        {
            if(!initialized)return;
            GuardCampaignNatureAudio020();
            GuardSupportedFootsteps020();
            AttachFoley();
            AttachBookAudio();
            TickBookWriting();
            TickPlaylistTransport015();
            TickMusic(Mathf.Clamp(Time.unscaledDeltaTime,0f,.1f));
            TickCauldronAmbience009(Mathf.Clamp(Time.unscaledDeltaTime,0f,.1f));
            TickWindowBreeze015(Mathf.Clamp(Time.unscaledDeltaTime,0f,.1f));
            TickAcoustics015(Mathf.Clamp(Time.unscaledDeltaTime,0f,.1f));
            TickWeatherAmbience020();
            if(Time.unscaledTime>=nextAmbientCheck){nextAmbientCheck=Time.unscaledTime+.25f;RefreshAmbient();}
            // Guide preview intentionally speaks while its tree is open. Keeping
            // this modest duck across that entire lesson avoids pumping between
            // sentence gaps and works before an IsSpeaking API is available.
            bool guide = IsNarrationDucking009;
            float target = guide ? NarrationDuck : 1f;
            float seconds = guide ? .13f : .65f;
            duck = Mathf.Lerp(duck,target,1f-Mathf.Exp(-Time.unscaledDeltaTime/seconds));
            ApplyVolumes();
            if(uiStopAt>=0f && Time.unscaledTime>=uiStopAt) {ui.Stop();uiStopAt=-1f;}
            if(gestureAuthorized && Time.unscaledTime>=nextPlaybackCheck) {
                nextPlaybackCheck = Time.unscaledTime+.5f;
                EnsureLoops();
            }
        }

        bool CanPlayCue() => initialized && isActiveAndEnabled && gestureAuthorized && Application.isFocused && !applicationPaused
            && masterVolume>0f && sfxVolume>0f;

        void EnsureLoops()
        {
            if(!gestureAuthorized || !Application.isFocused || applicationPaused)return;
            TickMusic(0f);
            bool menu=session!=null&&(!session.HasStarted||session.Opening!=null&&session.Opening.IsIntroPlaying);
            bool world=session!=null&&session.HasStarted&&!session.Paused&&!session.Victory&&!session.IsDead;
            bool fire = CanPlayCue()&&campfirePositionSet&&session!=null&&session.World!=null&&session.World.IsTutorialRefuge&&
                !session.World.IsCoopArena&&Near(campfire.transform.position,14f)&&session.World.CampfireReactionFuel>.001f&&(menu||gameplayMix&&world);
            if(fire && fireClip!=null && !campfire.isPlaying && fireClip.loadState==AudioDataLoadState.Loaded)
                campfire.Play();
            else if(!fire && campfire.isPlaying)campfire.Stop();
            bool hum=CanPlayCue()&&portalActive&&gameplayMix&&world&&session.World!=null&&session.World.PortalReadyForAudio009&&
                Near(portal.transform.position,30f)&&portal.clip!=null&&portal.clip.loadState==AudioDataLoadState.Loaded;
            if(hum&&!portal.isPlaying)portal.Play();else if(!hum&&portal.isPlaying)portal.Stop();
        }

        void ApplyVolumes()
        {
            if(!initialized)return;
            bool audible = gestureAuthorized && Application.isFocused && !applicationPaused;
            // One Master stage includes the separately controlled guide voice.
            AudioListener.volume=audible?masterVolume:0f;
            bool paused = session!=null && session.HasStarted && session.Paused;
            float musicMix = gameplayMix ? 1f : .8f;
            if(paused)musicMix *= .65f;
            float musicLevel=musicVolume*.28f*musicMix*duck;
            if(music!=null)music.volume=musicLevel*musicGain;
            if(musicNext!=null)musicNext.volume=musicLevel*musicNextGain;
            if(campfire!=null)campfire.volume=sfxVolume*.075f*(session?.World?.CampfireReactionFuel??1f)*duck;
            if(portal!=null)portal.volume=sfxVolume*.065f*duck*(session?.World?.PortalReadyForAudio009==true?1f:0f);
            if(ui!=null)ui.volume=sfxVolume*uiGain;
            if(cast!=null)cast.volume=sfxVolume*activeCastGain;
            if(bookWriting!=null)bookWriting.volume=sfxVolume*BookWritingGain*duck;
            ApplyCauldronVolumes009();
            ApplyCampaignIntroVolumes020();
            bool worldAudible=session!=null&&session.SimulationActive;
            for(int i=0;i<4;i++)
            {
                if(towerPool[i]!=null)towerPool[i].volume=worldAudible?sfxVolume*towerGains[i]*AcousticGain015(i+4):0f;
                if(cuePool[i]!=null)cuePool[i].volume=sfxVolume*cueGains[i]*AcousticGain015(i);
                if(ambientPool[i]!=null)ambientPool[i].volume=sfxVolume*WindowBreezeVoiceGain015(ambientIds[i],ambientGains[i])*duck;
            }
            ApplyWeatherAmbienceVolumes020();
        }

        static void Prime(AudioClip clip)
        {
            if(clip!=null&&clip.loadState==AudioDataLoadState.Unloaded)clip.LoadAudioData();
        }

        void TickMusic(float dt)
        {
            if(!MusicEnabled009||musicUserPaused015)return;
            if(!gestureAuthorized||!Application.isFocused||applicationPaused||musicSuspended||music==null||musicNext==null)return;
            if(TickMusicFadeIn009()&&!PlaylistImmediateAdvance015)return;
            if(musicTransition)
            {
                AdvanceMusicCrossfade009();
                if(musicFadeElapsed>=musicFadeDuration009)
                {
                    music.Stop();
                    AudioSource previous=music;music=musicNext;musicNext=previous;
                    musicNext.clip=null;musicGain=1f;musicNextGain=0f;musicTransition=false;
                    PlaylistFadeCompleted009();
                }
                return;
            }
            if(TickPlaylist009())return;
            bool arena=session!=null&&session.HasStarted&&session.World!=null&&session.World.IsCoopArena;
            bool changed=!musicModeKnown||arena!=arenaMusicMode;
            if(changed)
            {
                musicModeKnown=true;arenaMusicMode=arena;
                if(!arena)menuMusicIndex=0;
            }
            AudioClip desired=arenaMusicMode?arenaMusicClip:menuMusicClips[menuMusicIndex];
            if(desired==null&&!arenaMusicMode)
            {
                int other=1-menuMusicIndex;
                if(menuMusicClips[other]!=null){menuMusicIndex=other;desired=menuMusicClips[other];}
            }
            if(desired==null)return;
            Prime(desired);
            if(changed||music.clip!=desired||!music.isPlaying)
            {
                BeginMusicTransition(desired,arenaMusicMode);
                return;
            }
            if(arenaMusicMode||MusicRemaining009(music)>MusicCrossfadeMath009.TriggerSeconds)return;
            int nextIndex=1-menuMusicIndex;
            AudioClip next=menuMusicClips[nextIndex];
            // The first peaceful track repeats if the second asset is unavailable.
            if(next==null||next.loadState!=AudioDataLoadState.Loaded){nextIndex=menuMusicIndex;next=desired;}
            if(BeginMusicTransition(next,false))menuMusicIndex=nextIndex;
        }

        bool BeginMusicTransition(AudioClip clip,bool loop)
        {
            if(clip==null||clip.loadState!=AudioDataLoadState.Loaded||music==null||musicNext==null)return false;
            if(music.clip==clip&&music.isPlaying&&loop){music.loop=true;return true;}
            if(music.clip==null||!music.isPlaying)
            {
                music.Stop();music.clip=clip;music.loop=loop;musicGain=1f;
                musicNext.Stop();musicNext.clip=null;musicNextGain=0f;
                music.volume=0f;music.Play();ApplyMusicIntroOffset009(music,clip);StartMusicFadeIn009(clip);return true;
            }
            musicNext.Stop();musicNext.clip=clip;musicNext.loop=loop;
            musicNext.volume=0f;musicNextGain=0f;musicFadeElapsed=0f;
            musicNext.Play();ApplyMusicIntroOffset009(musicNext,clip);StartMusicCrossfade009(MusicRemaining009(music),clip,music.clip==clip);musicTransition=true;return true;
        }

        void SuspendMusic()
        {
            if(musicSuspended)return;
            if(music!=null)music.Pause();
            if(musicNext!=null)musicNext.Pause();
            musicSuspended=true;
        }
        void ResumeMusic()
        {
            if(!MusicEnabled009||musicUserPaused015||!musicSuspended||!gestureAuthorized||!Application.isFocused||applicationPaused)return;
            if(music!=null)music.UnPause();
            if(musicNext!=null)musicNext.UnPause();
            musicSuspended=false;
        }

        void RefreshAmbient()
        {
            // TickCauldronAmbience validates actual room/fuel/near/talk gates first.
            // Only its hearth survives an accepted mentor dialogue pause.
            bool mentorHearth=session!=null&&session.Paused&&cauldronContextActive009;
            bool world=session!=null&&!session.Victory&&!session.IsDead&&!session.IsDowned&&
                (!session.HasStarted||!session.Paused||mentorHearth);
            if(!CanPlayCue()||!world){StopAmbient();return;}
            Vector3 listener=ListenerPosition();
            for(int slot=0;slot<ambientPool.Length;slot++)
            {
                int selected=-1;float nearest=float.PositiveInfinity;
                foreach(var item in ambientEmitters)
                {
                    if(mentorHearth&&item.Key!=CauldronHearthEmitter009)continue;
                    bool used=false;
                    for(int previous=0;previous<slot;previous++)if(ambientIds[previous]==item.Key){used=true;break;}
                    if(IsOwnedWeatherAmbient020(item.Key)&&!WeatherAmbientAllowed020(item.Key))continue;
                    if(used||item.Value.Gain<=0f||!RefugeAmbientAllowed015(item.Value.Cue)||!namedClips.TryGetValue(item.Value.Cue,out var clip)||clip==null||clip.loadState!=AudioDataLoadState.Loaded)continue;
                    float distance=(item.Value.Position-listener).sqrMagnitude;
                    if(distance>item.Value.Range*item.Value.Range||distance>nearest||
                        selected>=0&&Mathf.Approximately(distance,nearest)&&item.Key>selected)continue;
                    selected=item.Key;nearest=distance;
                }
                AudioSource source=ambientPool[slot];
                if(selected<0){source.Stop();ambientIds[slot]=-1;ambientGains[slot]=0f;continue;}
                AmbientEmitter emitter=ambientEmitters[selected];
                AudioClip desired=namedClips[emitter.Cue];
                if(ambientIds[slot]!=selected||source.clip!=desired){source.Stop();source.clip=desired;}
                ambientIds[slot]=selected;ambientGains[slot]=emitter.Gain;
                source.spatialBlend=IsOwnedCampaignWind020(selected)||IsOwnedWeatherAmbient020(selected)?0f:1f;
                source.transform.position=emitter.Position;source.maxDistance=emitter.Range;source.minDistance=1.25f;
                source.volume=sfxVolume*emitter.Gain*duck;
                if(!source.isPlaying&&!StartWeatherVoiceAligned020(selected,source,desired)&&!StartWindowBreezeVoiceAligned015(slot,selected,source,desired))source.Play();
            }
        }
        void StopAmbient()
        {
            StopWindowBreeze015();
            for(int i=0;i<ambientPool.Length;i++)
            {
                if(ambientPool[i]!=null)ambientPool[i].Stop();ambientIds[i]=-1;ambientGains[i]=0f;
            }
        }

        void AttachFoley()
        {
            if(foley!=null||session==null||session.Player==null)return;
            foley=session.Player.GetComponent<OracleFoley>();
            if(foley==null)foley=session.Player.gameObject.AddComponent<OracleFoley>();
            foley.Initialize(session);
        }
        Vector3 ListenerPosition()=>listenerTransform!=null?listenerTransform.position:
            session!=null&&session.Player!=null&&session.Player.ViewCamera!=null?session.Player.ViewCamera.transform.position:transform.position;
        bool Near(Vector3 position,float range)=>Finite(position)&&(position-ListenerPosition()).sqrMagnitude<=range*range;

        AudioSource Source(string label,bool spatial,int priority)
        {
            var child = new GameObject(label);
            child.transform.SetParent(transform,false);
            var source = child.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = false;
            source.spatialBlend = spatial?1f:0f;
            source.dopplerLevel = 0f;
            source.spread = 0f;
            source.reverbZoneMix = 0f;
            source.priority = priority;
            source.volume = 0f;
            source.minDistance=1.5f;source.maxDistance=30f;source.rolloffMode=AudioRolloffMode.Linear;
            return source;
        }

        void StoreVolume(ref float field,string key,float value)
        {
            if(float.IsNaN(value) || float.IsInfinity(value))return;
            float next = Mathf.Clamp01(value);
            if(Mathf.Approximately(field,next))return;
            field = next;
            if(masterVolume<=0f||sfxVolume<=0f){StopCampaignNaturePlayback020();StopWeatherPlayback020();StopSupportedFootstepPlayback020();SuspendExternalAudio015();}
            PlayerPrefs.SetFloat(PreferencePrefix+key,next);
            PlayerPrefs.Save();
            ApplyVolumes();
        }

        static float ReadVolume(string key,float fallback)
        {
            float value = PlayerPrefs.GetFloat(PreferencePrefix+key,fallback);
            return float.IsNaN(value)||float.IsInfinity(value)?fallback:Mathf.Clamp01(value);
        }

        static bool Finite(Vector3 p) => !float.IsNaN(p.x)&&!float.IsInfinity(p.x)
            && !float.IsNaN(p.y)&&!float.IsInfinity(p.y)&&!float.IsNaN(p.z)&&!float.IsInfinity(p.z);

        void OnApplicationFocus(bool focused)
        {
            if(!initialized)return;
            if(!focused){StopCampaignNaturePlayback020();HoldWeatherPlayback020();StopSupportedFootstepPlayback020();PauseCampaignIntroAudio020();}
            if(!focused){SuspendMusic();StopTransients();StopAmbient();StopCauldronAmbience009();if(campfire!=null)campfire.Stop();if(portal!=null)portal.Stop();}
            else ResumeMusic();
            ApplyVolumes();
        }

        void OnDisable()
        {
            if(!initialized)return;
            RetireCampaignNatureAudio020();
            RetireWeatherAmbience020();
            RetireSupportedFootsteps020();
            StopCampaignIntroAudio020();
            StopPlaylist009();
            DetachBookAudio();
            StopCauldronAmbience009();
            if(music!=null)music.Stop();if(musicNext!=null){musicNext.Stop();musicNext.clip=null;}musicTransition=musicSuspended=false;
            musicGain=1f;musicNextGain=0f;if(campfire!=null)campfire.Stop();if(portal!=null)portal.Stop();StopTransients();StopAmbient();
        }
        void OnApplicationPause(bool paused){applicationPaused=paused;OnApplicationFocus(!paused&&Application.isFocused);}
        void StopTransients(){StopAcousticVoices015();cueAdmission.Clear();StopBookWriting();if(ui!=null)ui.Stop();if(cast!=null)cast.Stop();uiStopAt=-1f;for(int i=0;i<4;i++){if(towerPool[i]!=null)towerPool[i].Stop();if(cuePool[i]!=null)cuePool[i].Stop();}}

        void OnDestroy()
        {
            RetireCampaignNatureAudio020();
            DisposeWeatherAmbience020();
            DisposeSupportedFootsteps020();
            DisposeCampaignIntroAudio020();
            DisposeWindowBreeze015();
            DisposeAcoustics015();
            DisposeCauldronAmbience009();
            StopPlaylist009();
            DetachBookAudio();
            if(bookWriting!=null)Destroy(bookWriting.gameObject);
            // Resources clips are shared assets; only owned source objects retire.
            if(music!=null)Destroy(music.gameObject);
            if(musicNext!=null)Destroy(musicNext.gameObject);
            if(campfire!=null)Destroy(campfire.gameObject);
            if(ui!=null)Destroy(ui.gameObject);
            if(cast!=null)Destroy(cast.gameObject);
            if(portal!=null)Destroy(portal.gameObject);
            for(int i=0;i<4;i++){if(towerPool[i]!=null)Destroy(towerPool[i].gameObject);if(cuePool[i]!=null)Destroy(cuePool[i].gameObject);if(ambientPool[i]!=null)Destroy(ambientPool[i].gameObject);}
            if(initialized)AudioListener.volume=priorListenerVolume;
        }
    }
}

// # ARCHITECTURE SNAPSHOT
// Book audio is a single scene-owned 2D source, driven by the HUD reveal callback
// on Unity main thread. Resources clips are shared; no PCM copy or native
// container ownership. Repeated playback checks reuse source/delegate state.

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Existing refuge geometry/world axes and support pivots unchanged; elemental numeric state applies only to the supported refuge.
// Array Formats: Existing owner arrays remain canonical; fixed512-node/8word reaction state supplies bounded scalar presentation gains.
// Dependencies/Pointers: Host accepted single Fire/Earth/Wind shots only; existing Object adapters retain materials/lights/lifetime, no rewards/saves or shrine/portal gameplay mutation.

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: host accepted World contacts and existing run/epoch/shot identity; no damage/HP/path permission changes.
// Array Formats: existing bounded launch receipts add three-bit landing dedupe; fixed256B shared wire unchanged; new64B arena frame through existing NGO pipeline.
// Dependencies/Pointers: sole host/network/audio owners retain authority, lifetime and gesture/focus policies; actual peer/audio acceptance pending.

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Existing2D music/uGUI only; world/spatial/campaign authority unchanged.
// Array Formats: Additive fixed18 playlist masks/rows and at most2 optional clip leases; existing two-source fade/duck/volume/focus remain canonical.
// Dependencies/Pointers: Root verifies/imports/binds recordings; pending assets use honest existing-theme fallback. Cold boundary loads only, no PCM/new sources; native listening/memory/browser/UI acceptance pending.

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Actual refuge/world audio anchors only; no geometry/gameplay mutation.
// Array Formats: O(1) read-only cauldron frame,2 cold sources, existing4 ambient voices; cached narration bool.
// Dependencies/Pointers: Existing Audio/World/Activity lifetimes/accepted state remain canonical; work-only SFX integration, native/listening pending.
// # ARCHITECTURE SNAPSHOT
// Coordinate Space:Unity World listener/emitter metres; existing settings use local uGUI.
// Array Formats:Existing pools remain fixed;8voice leases/2queries/16hits per ray; audio-local Morton refresh keys.
// Dependencies/Pointers:Audio owns native reader fences/low-pass/backend leases; accepted World/tower lifetime hooks invalidate acoustic revisions. No narration/gameplay authority replaced.

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Existing guide/menu Canvas and accepted real tutorial/world/session owners.
// Array Formats: Cold fixed presentation stores and cached labels; no new gameplay/native memory ownership.
// Dependencies/Pointers: Work-only original UI packet; real pointer/audio/practice/reward/network authorities retained. Native/browser acceptance pending.

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Existing non-spatial two-source music; accepted session start read only.
// Array Formats: Fixed18-bit consumed cycle and scalar independent user/focus holds;14saved keyboard slots.
// Dependencies/Pointers: Music Off/user pause retain public source sample positions/fade state. One first accepted start may advance an actually playing opening; ordinary menu/gesture callbacks preserve history. Root native/browser checks pending.
// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Existing accepted elemental shop/UI/listener or cosmetic atlas space; no authority or saved-key changes.
// Array Formats: Four bounded clip/image identities and accepted-element bits; existing12models/fourcuevoices and workshop mesh.
// Dependencies/Pointers: Root follow-up imports four retained generated cues; baseline/replay/request failure stays silent, no hot loads/new audio sources or media/provider work here.


// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Existing player world metres/Y-up, listener-relative audio and lower-left HUD vitals Canvas.
// Array Formats: One scalar stamina state, cold cached UI/clip references and same four shared cue voices; no save, wire, collision or authority fields change.
// Dependencies/Pointers: OraclePlayer owns controlled stamina transitions and existing motion smoothing. Exhaustion locks until fresh recovered press; root owns all Assets/import/runtime/browser acceptance.

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Existing accepted weather presentation clock and local listener.
// Array Formats: Existing32ambient records/4ambient voices; at most2 borrowed weather records/voices.
// Dependencies/Pointers: New partial owns only weather state, borrowed clips and bounded probe adapter; no new AudioSource/PCM, independent clock or setting/focus mutation.

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Actual current grounded player event and admitted World/root; separate from weather FX clock/preferences.
// Array Formats:16borrowed clips, private72support rows+1result, existing4cue sources/LRU.
// Dependencies/Pointers: Frozen weather9e83 composition preserved; new partial alone owns footstep state and native data. No extra source, controller/camera/physics/geometry or menu setting mutation.
