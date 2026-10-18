#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace OdysseyOracle.Editor
{
    // # ARCHITECTURE SNAPSHOT
    // Explicit root-run real UI observer. Existing OracleUiAcceptanceFixture owns
    // text/mask/contrast checks; this adds safe branch focus, actual Sprite bindings
    // and actual Game-view PNGs. No lesson/point/profile/combat/private-field writes.
    // Scale uses its public owner one frame at a time and is restored before finish.
    public static class OraclePassiveNativeAcceptance
    {
        const string PendingKey="Oracle.PassiveNative.PendingScale.v1",ReportKey="Oracle.PassiveNative.Report.v1";
        const string CatalogPath="Assets/Art/Interface/PassiveNodeIcons_v1/PassiveNodeIconLibrary.asset";
        static readonly float[] NormalScales={1f,1.35f},WithCompact={.75f,1f,1.35f};
        static readonly Vector3[] Corners=new Vector3[4];
        static GameSession session;static OracleInterfacePreferences preferences;static Canvas canvas;static RectTransform tree;
        static PassiveNodeIconLibrary catalog;static GameObject previousSelection;static Report report;
        static float[] scales;static float originalScale;static int phase,index,lastFrame,waitFrames,initialWidth,initialHeight;
        static bool running,restoreOnly;static double deadline,screenshotDeadline,restoreReadyAt,restoreDeadline;static string folder,runId;
        [Serializable] public sealed class State
        {
            public bool paused,autoFire,tutorialFinished;public int prepared,confirmed,selected,combatSelected,level,xp,points,mask,wallet,permanent,lesson,guideIndex;
            public int[] wands,elementLevels;public string heading,instruction,subtitle;
        }
        [Serializable] public sealed class IconObservation
        {
            public int bitId,node;public string actualSprite,expectedSprite,cardText;public bool matchesCatalog,imageActive,fallbackVisible;
            public Rect screenRect,labelScreenRect;public Vector2 sourcePixels;public float minimumLabelStylePixels;
        }
        [Serializable] public sealed class Sample
        {
            public int frame,branch,screenWidth,screenHeight,pngWidth,pngHeight;public float uiScale,smallestPanelTextPixels;
            public string title,state,fullEffects,pngPath,pngSha256,minimumTextObject;public Rect minimumTextScreenRect;
            public float minimumFontStyleScreenPixels;public bool screenshotVerified,iconsAt32px,iconsAt64px;
            public OracleUiAcceptanceFixture.Sample actualBounds;public IconObservation[] icons;
        }
        [Serializable] public sealed class Report
        {
            public string startedUtc,finishedUtc,status,failure="",scaleSaveStatus;public bool completed,scaleRestored,pauseRestored,stateUnchanged,interrupted;
            public State original,after;public float originalScale;public List<Sample> samples=new List<Sample>();
            public string scope="Actual current Game viewport only. Normal passive panel, public UI focus only. No OS/Game-view resize, fake point/state, Learn click, guide advance, profile restore or browser acceptance. Font-style pixels are estimated from actual Text size tags and screen transform, not measured glyph ink. Missing learned/affordable states stay deferred.";
        }
        [Serializable] sealed class Pending {public float scale;}
        public static string Begin(string absoluteExistingWorkDirectory,bool includeCompact75=false)
        {
            if(running)throw new InvalidOperationException("Passive audit is already running; Cancel then wait for restoration.");Bind();
            if(!string.IsNullOrEmpty(SessionState.GetString(PendingKey,""))||!string.IsNullOrEmpty(OracleUiAcceptanceFixture.PendingRestoreJson()))throw new InvalidOperationException("Resolve earlier public preference restoration before a new sweep.");
            RequirePauseAndFinished();
            // This public one-shot also refuses a concurrent existing preference matrix.
            OracleUiAcceptanceFixture.CaptureVisibleNow("passive audit preflight: original pause menu");
            string work=Path.GetFullPath(Path.Combine(Application.dataPath,"../../../work"))+Path.DirectorySeparatorChar;
            if(string.IsNullOrWhiteSpace(absoluteExistingWorkDirectory)||!Path.IsPathRooted(absoluteExistingWorkDirectory))throw new ArgumentException("Use an existing absolute output directory below this repository's work folder.");
            folder=Path.GetFullPath(absoluteExistingWorkDirectory);
            if(!folder.StartsWith(work,StringComparison.OrdinalIgnoreCase)||!Directory.Exists(folder))throw new ArgumentException("Use an existing absolute output directory below this repository's work folder.");
            catalog=AssetDatabase.LoadAssetAtPath<PassiveNodeIconLibrary>(CatalogPath);
            if(!catalog||catalog.Icons==null||catalog.Icons.Length!=12)throw new InvalidOperationException("Actual baked twelve-node catalog is unavailable; no fabricated Sprite observation.");
            originalScale=preferences.UiScale;previousSelection=EventSystem.current!=null?EventSystem.current.currentSelectedGameObject:null;
            report=new Report{startedUtc=DateTime.UtcNow.ToString("o"),status="opening actual normal passive panel without purchase actions",original=ReadState(),originalScale=originalScale};
            SessionState.SetString(PendingKey,JsonUtility.ToJson(new Pending{scale=originalScale}));
            scales=includeCompact75?WithCompact:NormalScales;runId=Guid.NewGuid().ToString("N");index=phase=waitFrames=0;restoreOnly=false;
            initialWidth=Screen.width;initialHeight=Screen.height;session.Hud.ShowSkillTree(false);Start();return Summary();
        }
        public static string Cancel()
        {if(running){report.status="cancelled; restoring original scale and pause menu";phase=10;waitFrames=0;}return Summary();}
        public static string RestorePendingScale()
        {
            if(running)throw new InvalidOperationException("Current audit owns restoration.");Bind();RequirePauseAndFinished();
            string pending=SessionState.GetString(PendingKey,"");if(string.IsNullOrEmpty(pending))return "No pending passive scale restore.";
            var saved=JsonUtility.FromJson<Pending>(pending);if(saved==null||float.IsNaN(saved.scale)||saved.scale<.75f||saved.scale>1.35f)throw new InvalidOperationException("Invalid pending scale; no owner preference changed.");
            originalScale=saved.scale;restoreOnly=true;report=new Report{startedUtc=DateTime.UtcNow.ToString("o"),status="restoring scale after interrupted session; original session/profile preservation remains unverified",originalScale=originalScale};
            initialWidth=Screen.width;initialHeight=Screen.height;previousSelection=EventSystem.current!=null?EventSystem.current.currentSelectedGameObject:null;
            phase=10;waitFrames=0;Start();return Summary();
        }
        public static string CurrentReportJson()=>report==null?SessionState.GetString(ReportKey,"{}"):JsonUtility.ToJson(report,true);
        public static string Summary()=>JsonUtility.ToJson(new SummaryData{running=running,status=report?.status??"not run",samples=report?.samples.Count??0,scaleRestored=report!=null&&report.scaleRestored,pauseRestored=report!=null&&report.pauseRestored,stateUnchanged=report!=null&&report.stateUnchanged,pendingRestore=!string.IsNullOrEmpty(SessionState.GetString(PendingKey,""))},true);
        [Serializable] sealed class SummaryData {public bool running,scaleRestored,pauseRestored,stateUnchanged,pendingRestore;public int samples;public string status;}
        static void Bind()
        {
            if(!EditorApplication.isPlaying||EditorApplication.isPaused||EditorApplication.isCompiling||EditorApplication.isUpdating)throw new InvalidOperationException("Settled running Editor required; game pause is supported, Editor Pause is not.");
            session=GameSession.Instance;if(!session||!session.Hud||!session.Combat||!session.Progression)throw new InvalidOperationException("Actual live session/HUD/combat/progression required.");
            var owners=session.Hud.GetComponentsInChildren<OracleInterfacePreferences>(true);if(owners.Length!=1)throw new InvalidOperationException("Exactly one real public interface-preference owner required.");
            preferences=owners[0];canvas=preferences.GetComponent<Canvas>();if(!canvas)throw new InvalidOperationException("Actual overlay Canvas unavailable.");
            tree=Find(canvas.transform,"FourElementPassiveTree") as RectTransform;if(!tree)throw new InvalidOperationException("Actual passive tree was not built.");
        }
        static void RequirePauseAndFinished()
        {
            var pause=Find(canvas.transform,"PausePanel");
            if(!session.HasStarted||!session.Paused||session.IsDead||session.IsDowned||session.Victory||session.CoopMode||session.Rooms!=null&&session.Rooms.Connected||
                session.Tutorial==null||!session.Tutorial.Finished||session.Shop!=null&&session.Shop.Visible||session.Loading!=null&&session.Loading.IsLoading||
                session.Opening!=null&&(session.Opening.IsIntroPlaying||session.Opening.IsTransitioning)||!pause||!pause.gameObject.activeInHierarchy||AbilityBindings.IsRebinding)
                throw new InvalidOperationException("Start from the normal actual Pause menu in an offline, alive, completed-tutorial session. Other gameplay/guide/modal states are deferred, never skipped or reset by the fixture.");
        }
        static void Start()
        {
            running=true;lastFrame=-1;deadline=EditorApplication.timeSinceStartup+90;
            EditorApplication.update+=Tick;EditorApplication.playModeStateChanged+=PlayChanged;AssemblyReloadEvents.beforeAssemblyReload+=BeforeReload;
        }
        static void Tick()
        {
            if(!running)return;
            if(!EditorApplication.isPlaying||!session||!preferences||!canvas){Interrupt("Runtime disappeared before restoration. Pending original scale retained.");return;}
            if(EditorApplication.isPaused||Time.frameCount==lastFrame)
            {
                if(EditorApplication.timeSinceStartup>(phase>=11?restoreDeadline:deadline))Interrupt("Simulation frames stopped before restoration. Pending original scale retained for explicit recovery.");
                return;
            }
            lastFrame=Time.frameCount;
            try
            {
                if(phase<10&&EditorApplication.timeSinceStartup>deadline)throw new TimeoutException("Finite passive audit deadline reached.");
                if(phase<10&&(Screen.width!=initialWidth||Screen.height!=initialHeight))throw new InvalidOperationException("Viewport changed during sweep; actual viewports require separate runs.");
                if(phase<10&&(!session.Paused||!tree.gameObject.activeInHierarchy||session.Hud.IsGuidePreviewVisible))throw new InvalidOperationException("Actual normal passive panel was changed while observing.");
                if(phase==0){preferences.SetUiScale(scales[index/4]);phase=1;waitFrames=2;return;}
                if(phase==1){if(waitFrames-->0)return;Select("ElementTab"+(index%4));phase=2;waitFrames=2;return;}
                if(phase==2){if(waitFrames-->0)return;Select("UpgradeCard0");phase=3;waitFrames=2;return;}
                if(phase==3)
                {
                    if(waitFrames-->0)return;var sample=Capture(index%4);report.samples.Add(sample);
                    sample.pngPath=Path.Combine(folder,"passive-"+runId+"-scale"+Mathf.RoundToInt(preferences.UiScale*100)+"-branch"+sample.branch+"-"+Screen.width+"x"+Screen.height+".png");
                    if(File.Exists(sample.pngPath))throw new InvalidOperationException("Screenshot destination already exists; no overwrite.");
                    ScreenCapture.CaptureScreenshot(sample.pngPath);screenshotDeadline=EditorApplication.timeSinceStartup+5;phase=4;return;
                }
                if(phase==4)
                {
                    var sample=report.samples[report.samples.Count-1];
                    if(!VerifyPng(sample)){if(EditorApplication.timeSinceStartup>screenshotDeadline)throw new TimeoutException("Actual Game-view PNG did not finish; no screenshot accepted.");return;}
                    index++;if(index>=scales.Length*4){report.completed=true;phase=10;}else phase=0;return;
                }
                if(phase==10){preferences.SetUiScale(originalScale);session.Hud.ClosePanels();restoreReadyAt=EditorApplication.timeSinceStartup+.5;restoreDeadline=restoreReadyAt+5;phase=11;waitFrames=2;return;}
                if(phase==11)
                {
                    if(EditorApplication.timeSinceStartup>restoreDeadline)throw new TimeoutException("Finite scale/pause restoration deadline reached.");
                    if(waitFrames-->0||EditorApplication.timeSinceStartup<restoreReadyAt)return;
                    report.scaleRestored=Mathf.Abs(preferences.UiScale-originalScale)<.0001f;var pause=Find(canvas.transform,"PausePanel");report.pauseRestored=session.Paused&&pause&&pause.gameObject.activeInHierarchy;
                    if(!report.scaleRestored||!report.pauseRestored)return;
                    if(previousSelection&&previousSelection.activeInHierarchy&&EventSystem.current!=null)EventSystem.current.SetSelectedGameObject(previousSelection);
                    report.after=ReadState();report.stateUnchanged=!restoreOnly&&JsonUtility.ToJson(report.original)==JsonUtility.ToJson(report.after);report.scaleSaveStatus=preferences.SaveStatus;
                    report.status=restoreOnly?"original scale restored; interrupted old-session/profile evidence remains deferred":report.completed?"real passive sweep captured and scale/pause restored; inspect PNGs, state and actual bounds before acceptance":"cancelled/failed sweep; scale and pause restored";
                    report.finishedUtc=DateTime.UtcNow.ToString("o");SessionState.EraseString(PendingKey);Save();Detach();
                }
            }
            catch(Exception error)
            {
                report.failure=error.ToString();if(phase>=10){Interrupt("Restoration failed; pending scale retained.");return;}
                report.status="observation failed; restoring original scale/pause";phase=10;waitFrames=0;
            }
        }
        static Sample Capture(int branch)
        {
            var existing=JsonUtility.FromJson<OracleUiAcceptanceFixture.Report>(OracleUiAcceptanceFixture.CaptureVisibleNow("real passive branch"+branch));
            if(existing==null||existing.samples.Count!=1||existing.samples[0].scope!="FourElementPassiveTree")throw new InvalidOperationException("Existing real-UI observer did not inspect the actual tree.");
            var result=new Sample{frame=Time.frameCount,branch=branch,screenWidth=Screen.width,screenHeight=Screen.height,uiScale=preferences.UiScale,
                actualBounds=existing.samples[0],smallestPanelTextPixels=existing.samples[0].smallestTextPixels,title=TextOf("FocusedUpgradeTitle"),state=TextOf("FocusedUpgradeState"),fullEffects=TextOf("FocusedUpgradeFullEffects"),icons=new IconObservation[3]};
            result.minimumFontStyleScreenPixels=float.MaxValue;
            foreach(var text in tree.GetComponentsInChildren<Text>(false))
            {
                if(!text.isActiveAndEnabled||text.canvasRenderer.cull||text.color.a<=.01f||string.IsNullOrWhiteSpace(text.text))continue;
                float pixels=MinimumStylePixels(text);
                if(pixels<result.minimumFontStyleScreenPixels){result.minimumFontStyleScreenPixels=pixels;result.minimumTextObject=text.name;result.minimumTextScreenRect=ScreenRect(text.rectTransform);}
            }
            if(result.minimumFontStyleScreenPixels==float.MaxValue)result.minimumFontStyleScreenPixels=0;
            bool all32=true,all64=true;
            for(int node=0;node<3;node++)
            {
                var card=Find(tree,"UpgradeCard"+node);var imageNode=card?Find(card,"OptionalAtlasEmblem"):null;var image=imageNode?imageNode.GetComponent<Image>():null;
                var label=card?card.GetComponentInChildren<Text>(true):null;var fallback=card?Find(card,"DrawnElementFallback"):null;var expected=catalog.Get(branch,node);
                if(!card||!image||!label||!expected)throw new InvalidOperationException("Actual card/catalog components missing; art UI integration is not accepted.");
                Rect pixels=ScreenRect(image.rectTransform);
                result.icons[node]=new IconObservation{bitId=branch*3+node,node=node,actualSprite=image.sprite?image.sprite.name:"<none>",expectedSprite=expected.name,
                    matchesCatalog=image.sprite==expected,imageActive=image.isActiveAndEnabled,fallbackVisible=fallback&&fallback.gameObject.activeInHierarchy,
                    screenRect=pixels,labelScreenRect=ScreenRect(label.rectTransform),sourcePixels=image.sprite?image.sprite.rect.size:Vector2.zero,cardText=label.text,minimumLabelStylePixels=MinimumStylePixels(label)};
                all32&=Mathf.Abs(pixels.width-32)<1&&Mathf.Abs(pixels.height-32)<1;all64&=Mathf.Abs(pixels.width-64)<1&&Mathf.Abs(pixels.height-64)<1;
            }
            result.iconsAt32px=all32;result.iconsAt64px=all64;return result;
        }
        static State ReadState()
        {
            var c=session.Combat;var p=session.Progression;var t=session.Tutorial;var s=session.Shop;
            return new State{paused=session.Paused,prepared=c.PreparedElement,confirmed=c.ConfirmedMask,autoFire=c.AutoFireEnabled,selected=p.SelectedElement,combatSelected=c.SelectedSpell,elementLevels=(int[])p.Levels.Clone(),
                level=p.Level,xp=p.Experience,points=p.SkillPoints,mask=p.UpgradeMask,wallet=s!=null?s.Heartcrystals:0,permanent=s!=null?s.PermanentPassiveMask:0,
                wands=s!=null?new[]{s.WandLevel(0),s.WandLevel(1),s.WandLevel(2),s.WandLevel(3)}:Array.Empty<int>(),tutorialFinished=t.Finished,lesson=(int)t.CurrentLesson,guideIndex=t.CurrentGuideIndex,heading=t.Heading,instruction=t.Instruction,subtitle=t.Subtitle};
        }
        static void Select(string name){var target=Find(tree,name);var button=target?target.GetComponent<Button>():null;if(!button||!button.isActiveAndEnabled||!button.interactable)throw new InvalidOperationException("Actual non-purchase focus control unavailable: "+name);button.Select();}
        static string TextOf(string name){var target=Find(tree,name);var text=target?target.GetComponent<Text>():null;return text?text.text:"<missing>";}
        static Transform Find(Transform root,string name){var nodes=root.GetComponentsInChildren<Transform>(true);if(nodes.Length>8192)throw new InvalidOperationException("Finite UI hierarchy bound exceeded.");Transform found=null;foreach(var node in nodes)if(node.name==name){if(found)throw new InvalidOperationException("Ambiguous real UI name: "+name);found=node;}return found;}
        static Rect ScreenRect(RectTransform rect){rect.GetWorldCorners(Corners);Vector2 min=new Vector2(float.MaxValue,float.MaxValue),max=new Vector2(float.MinValue,float.MinValue);for(int i=0;i<4;i++){Vector2 p=RectTransformUtility.WorldToScreenPoint(canvas.worldCamera,Corners[i]);min=Vector2.Min(min,p);max=Vector2.Max(max,p);}return Rect.MinMaxRect(min.x,min.y,max.x,max.y);}
        static float MinimumStylePixels(Text text)
        {
            int minimum=text.resizeTextForBestFit?text.cachedTextGenerator.fontSizeUsedForBestFit:text.fontSize;
            if(text.supportRichText)for(int start=0,tags=0;tags<64;tags++)
            {
                start=text.text.IndexOf("<size=",start,StringComparison.OrdinalIgnoreCase);if(start<0)break;
                int end=text.text.IndexOf('>',start+6);if(end<0||end-start>16)break;
                if(int.TryParse(text.text.Substring(start+6,end-start-6),out int size)&&size>0)minimum=Mathf.Min(minimum,size);start=end+1;
            }
            return text.rectTransform.rect.height>0?ScreenRect(text.rectTransform).height/text.rectTransform.rect.height*minimum:0;
        }
        static bool VerifyPng(Sample sample)
        {
            if(!File.Exists(sample.pngPath))return false;byte[] header=new byte[24],tail=new byte[12];
            try
            {
                using(var stream=File.OpenRead(sample.pngPath))
                {
                    if(stream.Length<36||stream.Read(header,0,header.Length)!=header.Length)return false;
                    stream.Seek(-12,SeekOrigin.End);if(stream.Read(tail,0,tail.Length)!=tail.Length)return false;
                    if(tail[0]!=0||tail[1]!=0||tail[2]!=0||tail[3]!=0||tail[4]!=73||tail[5]!=69||tail[6]!=78||tail[7]!=68||tail[8]!=174||tail[9]!=66||tail[10]!=96||tail[11]!=130)return false;
                    stream.Position=0;using(var sha=SHA256.Create())sample.pngSha256=BitConverter.ToString(sha.ComputeHash(stream)).Replace("-","").ToLowerInvariant();
                }
            }
            catch(IOException){return false;}
            if(header[0]!=137||header[1]!=80||header[2]!=78||header[3]!=71||header[4]!=13||header[5]!=10||header[6]!=26||header[7]!=10)throw new InvalidOperationException("Screenshot file is not a PNG.");
            sample.pngWidth=BigEndian(header,16);sample.pngHeight=BigEndian(header,20);sample.screenshotVerified=sample.pngWidth==sample.screenWidth&&sample.pngHeight==sample.screenHeight;
            if(!sample.screenshotVerified)throw new InvalidOperationException("PNG dimensions differ from recorded actual viewport.");return true;
        }
        static int BigEndian(byte[] b,int i)=>(b[i]<<24)|(b[i+1]<<16)|(b[i+2]<<8)|b[i+3];
        static void PlayChanged(PlayModeStateChange state){if(state==PlayModeStateChange.ExitingPlayMode)Interrupt("Play stopped before normal restoration; original scale is pending.");}
        static void BeforeReload(){if(running)Interrupt("Assembly reload interrupted native passive observation; no successful restore claimed.");}
        static void Interrupt(string status){if(report!=null){report.interrupted=true;report.status=status;report.finishedUtc=DateTime.UtcNow.ToString("o");Save();}Detach();}
        static void Save(){if(report!=null)SessionState.SetString(ReportKey,JsonUtility.ToJson(report));}
        static void Detach(){EditorApplication.update-=Tick;EditorApplication.playModeStateChanged-=PlayChanged;AssemblyReloadEvents.beforeAssemblyReload-=BeforeReload;running=false;session=null;preferences=null;canvas=null;tree=null;catalog=null;previousSelection=null;}
    }
}
#endif
