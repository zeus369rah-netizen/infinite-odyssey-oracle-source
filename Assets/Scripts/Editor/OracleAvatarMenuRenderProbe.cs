#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace OdysseyOracle.EditorTools
{
    // Root-only native diagnostic. Requires an already playing Editor; never changes
    // play state, Game view focus, cursor, scene assets or the live avatar preference.
    public static class OracleAvatarMenuRenderProbe
    {
        static OracleAvatarPreviewLease preview;
        static Texture2D grid;
        static string path;
        static int look,appliedFrame;
        static double deadline;
        public static bool IsRunning=>preview!=null;
        public static string LastResult {get;private set;}="NOT_RUN";
        public static void Begin(string absolutePngPath)
        {
            if(IsRunning)throw new InvalidOperationException("Avatar render probe already running.");
            if(!Application.isPlaying||!Path.IsPathRooted(absolutePngPath)||Path.GetExtension(absolutePngPath).ToLowerInvariant()!=".png")
                throw new ArgumentException("Already-playing Editor and absolute PNG path required.");
            path=absolutePngPath;look=0;deadline=EditorApplication.timeSinceStartup+20;
            try
            {
                preview=new OracleAvatarPreviewLease();grid=new Texture2D(OracleAvatarPreviewLease.Width*4,OracleAvatarPreviewLease.Height*2,TextureFormat.RGBA32,false);
                preview.Avatar.ApplyAppearance(OracleAvatarAppearance.StartingLook(0).Packed);preview.SetVisible(true);appliedFrame=Time.frameCount;
                LastResult="RUNNING";EditorApplication.update+=Tick;
            }
            catch(Exception failure){LastResult="FAIL_"+failure.GetType().Name;Cleanup();throw;}
        }
        static void Tick()
        {
            if(!Application.isPlaying||EditorApplication.timeSinceStartup>deadline){LastResult="FAIL_TIMEOUT_OR_PLAY_STOPPED";Cleanup();return;}
            if(Time.frameCount<appliedFrame+2)return;
            try
            {
                var prior=RenderTexture.active;
                try{RenderTexture.active=preview.Target;grid.ReadPixels(new Rect(0,0,OracleAvatarPreviewLease.Width,OracleAvatarPreviewLease.Height),
                    (look%4)*OracleAvatarPreviewLease.Width,(1-look/4)*OracleAvatarPreviewLease.Height,false);}
                finally{RenderTexture.active=prior;}
                look++;
                if(look==8)
                {
                    grid.Apply(false,false);Directory.CreateDirectory(Path.GetDirectoryName(path));File.WriteAllBytes(path,grid.EncodeToPNG());
                    LastResult="PASS_CAPTURE_ONLY_8_LOOKS_2048x1280_VISUAL_APPROVAL_PENDING";Debug.Log(LastResult+" "+path);Cleanup();return;
                }
                preview.Avatar.ApplyAppearance(OracleAvatarAppearance.StartingLook(look).Packed);appliedFrame=Time.frameCount;
            }
            catch(Exception failure){LastResult="FAIL_"+failure.GetType().Name;Debug.LogException(failure);Cleanup();}
        }
        static void Cleanup()
        {
            EditorApplication.update-=Tick;preview?.Dispose();preview=null;
            if(grid!=null)UnityEngine.Object.Destroy(grid);grid=null;
        }
    }
}
#endif

// # ARCHITECTURE SNAPSHOT
// External native acceptance entry only: one512x640 lease + one2048x1280 capture,8 finite looks/two-frame settle/20s deadline.
// No saved preferences, gameplay/native data owners, Editor play state, input focus or source assets are changed.
