#if UNITY_EDITOR
using System;
using System.Globalization;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace OdysseyOracle.Editor
{
    // # ARCHITECTURE SNAPSHOT
    // Explicit batch-only executeMethod job. SessionState survives Play domain reload.
    // No active job key means no startup hook or gameplay action. Saved scene/catalog
    // are opened, never edited/saved. Public driver owns its virtual input and cleanup.
    [InitializeOnLoad]
    public static class OracleGuideBatchLauncher
    {
        const string Prefix="Oracle004.ExplicitGuideBatch.",Scene="Assets/Scenes/OracleTrial.unity";
        const double WallLimit=320;
        static bool attached;
        [Serializable] public sealed class Receipt
        {
            public string status,reason,startedUtc,endedUtc,scene=Scene,driverReceiptPath;
            public int exitCode,driverBeginCalls;public double wallSeconds;
            public bool batchMode,observedFocused,playStopped,sceneOrCatalogSaved,osFocusRequested,privateStateBypass;
            public OracleGuideEndToEndDriver.Receipt driver;
        }
        static OracleGuideBatchLauncher()
        {
            // A normal user's Editor session is never autoloaded into this job.
            if(Application.isBatchMode&&SessionState.GetBool(Prefix+"Active",false))Attach();
        }
        public static void Execute()
        {
            if(!Application.isBatchMode){Debug.LogError("OracleGuideBatchLauncher requires an explicit batchmode executeMethod invocation.");return;}
            if(SessionState.GetBool(Prefix+"Active",false)){Attach();return;}
            string path=ReceiptPath();
            SessionState.SetString(Prefix+"Receipt",path);
            SessionState.SetString(Prefix+"DriverPath",Path.ChangeExtension(path,"driver.json"));
            SessionState.SetString(Prefix+"Started",DateTime.UtcNow.ToString("o"));
            SessionState.SetString(Prefix+"Phase","WAIT_PLAY");SessionState.SetString(Prefix+"Result","");
            SessionState.SetString(Prefix+"DriverResult","");SessionState.SetBool(Prefix+"Focused",false);
            SessionState.SetInt(Prefix+"BeginCalls",0);SessionState.SetBool(Prefix+"Active",true);
            Attach();
            try
            {
                if(EditorApplication.isPlaying||EditorApplication.isPaused||EditorApplication.isCompiling||EditorApplication.isUpdating)
                {Finish("REJECTED","Requires a clean stopped batch Editor; no active Play/compile/import is replaced.",2);return;}
                if(!File.Exists(Path.Combine(Directory.GetParent(Application.dataPath).FullName,Scene)))
                {Finish("FAILED","Actual saved OracleTrial scene is missing.",4);return;}
                EditorSceneManager.OpenScene(Scene,OpenSceneMode.Single);
                EditorApplication.EnterPlaymode();
            }
            catch(Exception exception){Finish("FAILED",exception.ToString(),4);}
        }
        static void Attach(){if(attached)return;attached=true;EditorApplication.update+=Tick;}
        static void Tick()
        {
            if(!Application.isBatchMode||!SessionState.GetBool(Prefix+"Active",false))
            {EditorApplication.update-=Tick;attached=false;return;}
            try
            {
                string phase=SessionState.GetString(Prefix+"Phase","");
                if(Elapsed()>WallLimit)
                {
                    if(phase=="STOPPING"){WriteStoredResult(false);Exit(4);return;}
                    Finish("INCOMPLETE","320-second launcher wall limit reached; no lesson or focus bypass was attempted.",2);return;
                }
                if(phase=="STOPPING")
                {if(!EditorApplication.isPlaying&&!EditorApplication.isPlayingOrWillChangePlaymode){WriteStoredResult(true);Exit(SessionState.GetInt(Prefix+"ExitCode",4));}return;}
                if(EditorApplication.isCompiling||EditorApplication.isUpdating)return;
                if(phase=="WAIT_PLAY")
                {if(!EditorApplication.isPlaying)return;SessionState.SetString(Prefix+"Phase","WAIT_MENU");phase="WAIT_MENU";}
                if(phase=="WAIT_MENU")
                {
                    if(!EditorApplication.isPlaying){Finish("INCOMPLETE","Play exited before the normal menu became ready.",2);return;}
                    GameSession owner=GameSession.Instance;if(owner==null||owner.Opening==null)return;
                    SessionState.SetBool(Prefix+"Focused",Application.isFocused);
                    if(!Application.isFocused)
                    {Finish("BLOCKED_FOCUS","Actual batch Application.isFocused is false. Tutorial clocks/narration require focus; no OS focus, SkipIntro or private bypass is used.",3);return;}
                    if(owner.Opening.IsIntroPlaying||owner.Opening.IsTransitioning)return;
                    if(owner.HasStarted){Finish("REJECTED","Saved scene did not reach a fresh normal menu; no reset or skip is substituted.",2);return;}
                    if(SessionState.GetInt(Prefix+"BeginCalls",0)!=0){Finish("INCOMPLETE","Driver was already invoked; reload never retries the campaign.",2);return;}
                    SessionState.SetInt(Prefix+"BeginCalls",1);SessionState.SetString(Prefix+"Phase","RUN_DRIVER");
                    string json=OracleGuideEndToEndDriver.Begin(owner,SessionState.GetString(Prefix+"DriverPath",""));
                    if(!OracleGuideEndToEndDriver.IsRunning)FinishFromDriver(json);
                    return;
                }
                if(phase=="RUN_DRIVER")
                {
                    if(OracleGuideEndToEndDriver.IsRunning)return;
                    string json=OracleGuideEndToEndDriver.CurrentReceiptJson();
                    var value=JsonUtility.FromJson<OracleGuideEndToEndDriver.Receipt>(json);
                    if(value==null||value.status=="NOT_STARTED")
                    {
                        string path=SessionState.GetString(Prefix+"DriverPath","");
                        if(File.Exists(path))json=File.ReadAllText(path);
                    }
                    FinishFromDriver(json);
                }
            }
            catch(Exception exception){Finish("FAILED",exception.ToString(),4);}
        }
        static void FinishFromDriver(string json)
        {
            var driver=JsonUtility.FromJson<OracleGuideEndToEndDriver.Receipt>(json);
            if(driver==null||driver.status=="NOT_STARTED")
            {Finish("INCOMPLETE","Driver domain was reset without a terminal receipt. No automatic retry.",2);return;}
            SessionState.SetString(Prefix+"DriverResult",json);
            Finish(driver.status,driver.reason,driver.status=="PASSED"?0:driver.status=="BLOCKED_FOCUS"?3:2);
        }
        static void Finish(string status,string reason,int code)
        {
            if(OracleGuideEndToEndDriver.IsRunning)
            {
                OracleGuideEndToEndDriver.Cancel();
                SessionState.SetString(Prefix+"DriverResult",OracleGuideEndToEndDriver.CurrentReceiptJson());
            }
            var receipt=new Receipt{status=status,reason=reason,exitCode=code,batchMode=Application.isBatchMode,
                startedUtc=SessionState.GetString(Prefix+"Started",""),endedUtc=DateTime.UtcNow.ToString("o"),wallSeconds=Elapsed(),
                driverBeginCalls=SessionState.GetInt(Prefix+"BeginCalls",0),observedFocused=SessionState.GetBool(Prefix+"Focused",false),
                driverReceiptPath=SessionState.GetString(Prefix+"DriverPath","")};
            string driverJson=SessionState.GetString(Prefix+"DriverResult","");
            if(!string.IsNullOrEmpty(driverJson))receipt.driver=JsonUtility.FromJson<OracleGuideEndToEndDriver.Receipt>(driverJson);
            SessionState.SetString(Prefix+"Result",JsonUtility.ToJson(receipt,true));SessionState.SetInt(Prefix+"ExitCode",code);
            SessionState.SetString(Prefix+"Phase","STOPPING");WriteStoredResult(!EditorApplication.isPlaying);
            if(EditorApplication.isPlaying)EditorApplication.ExitPlaymode();
            else if(!EditorApplication.isPlayingOrWillChangePlaymode)Exit(code);
        }
        static void WriteStoredResult(bool stopped)
        {
            string text=SessionState.GetString(Prefix+"Result","");if(string.IsNullOrEmpty(text))return;
            var receipt=JsonUtility.FromJson<Receipt>(text);receipt.playStopped=stopped;receipt.wallSeconds=Elapsed();
            string path=SessionState.GetString(Prefix+"Receipt","");if(string.IsNullOrEmpty(path))return;
            Directory.CreateDirectory(Path.GetDirectoryName(path));File.WriteAllText(path,JsonUtility.ToJson(receipt,true));
        }
        static void Exit(int code)
        {
            SessionState.SetBool(Prefix+"Active",false);SessionState.SetString(Prefix+"Phase","");
            EditorApplication.update-=Tick;attached=false;
            EditorApplication.Exit(code);
        }
        static double Elapsed()
        {
            DateTime value;if(!DateTime.TryParse(SessionState.GetString(Prefix+"Started",""),CultureInfo.InvariantCulture,DateTimeStyles.RoundtripKind,out value))return WallLimit+1;
            return Math.Max(0,(DateTime.UtcNow-value.ToUniversalTime()).TotalSeconds);
        }
        static string ReceiptPath()
        {
            string project=Directory.GetParent(Application.dataPath).FullName;
            string workspace=Directory.GetParent(Directory.GetParent(project).FullName).FullName;
            string path=Path.Combine(workspace,"work/native-004-guide-batch.json");
            string[] args=Environment.GetCommandLineArgs();
            for(int i=0;i<args.Length-1;i++)if(args[i]=="-oracleGuideReceipt")path=args[i+1];
            path=Path.GetFullPath(path);string allowed=Path.GetFullPath(Path.Combine(workspace,"work"))+Path.DirectorySeparatorChar;
            if(!path.StartsWith(allowed,StringComparison.OrdinalIgnoreCase)||!path.EndsWith(".json",StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("Batch receipt must be JSON inside the current workspace work folder.");
            return path;
        }
    }
}
#endif
