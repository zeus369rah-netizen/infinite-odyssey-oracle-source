using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace OdysseyOracle
{
    public sealed partial class OdysseyHud
    {
        const string CampaignContentResource020="CampaignIntro020/content";
        static readonly int CampaignProgressId020=Shader.PropertyToID("_Progress"),CampaignColorId020=Shader.PropertyToID("_ColorProgress"),CampaignAlphaId020=Shader.PropertyToID("_PageAlpha");
        static readonly string[] CampaignCounters020={"1 / 10","2 / 10","3 / 10","4 / 10","5 / 10","6 / 10","7 / 10","8 / 10","9 / 10","10 / 10"};
        readonly CampaignBookState020 campaignBook020=new CampaignBookState020();
        readonly Texture2D[] campaignImages020=new Texture2D[CampaignBookState020.PageCount];
        readonly AudioClip[] campaignVoices020=new AudioClip[CampaignBookState020.PageCount];
        CampaignBookData020 campaignData020;
        RectTransform campaignOverlay020,campaignPaper020,campaignArt020;
        CanvasGroup campaignInput020;
        UnityEngine.UI.RawImage campaignImage020;
        UnityEngine.UI.AspectRatioFitter campaignAspect020;
        UnityEngine.UI.Text campaignTitle020,campaignCounter020,campaignCaption020,campaignPlayLabel020,campaignNextLabel020,campaignSubtitleLabel020;
        UnityEngine.UI.Image campaignCaptionBackground020;
        UnityEngine.UI.Button campaignPrevious020,campaignPlay020,campaignNext020,campaignSubtitles020,campaignSkip020,campaignBack020;
        Material campaignMaterial020;
        bool campaignLoaded020,campaignClosing020,campaignPreferencesBound020,campaignReducedMotion020;
        int campaignCloseFrame020;
        float campaignCloseElapsed020;
        Vector2 campaignCanvasSize020=new Vector2(-1,-1);
        CampaignBookResult020 campaignCloseResult020;
        public bool CampaignBookVisible020=>campaignOverlay020!=null&&campaignOverlay020.gameObject.activeSelf;
        public event System.Action<CampaignBookResult020> CampaignIntroFinished020;

        void BuildCampaignEntry020(RectTransform parent)
        {
            MenuButton(parent,"CampaignPrologue020","CAMPAIGN",OpenCampaignEntry020,36);
            MenuLabel(parent,"CampaignScope020","Illustrated prologue · wider campaign in development",13,Muted,30);
        }
        void BuildCampaignBook020(Transform parent)
        {
            campaignOverlay020=Stretch(parent,"CampaignPrologueOverlay020");
            Paint(campaignOverlay020,new Color(.018f,.013f,.009f,.98f),true);
            campaignInput020=campaignOverlay020.gameObject.AddComponent<CanvasGroup>();
            campaignPaper020=Frame(campaignOverlay020,"CampaignPapyrus020",new Vector2(.5f,.5f),new Vector2(.5f,.5f),Vector2.zero,new Vector2(1580,1000),new Color(.74f,.64f,.46f,1f),new Color(.32f,.22f,.11f,1f));
            campaignBack020=Button(campaignPaper020,"CampaignBack020","BACK",new Vector2(.5f,.5f),new Vector2(.5f,.5f),new Vector2(-690,445),new Vector2(140,42),CancelCampaignBook020);
            campaignTitle020=Label(campaignPaper020,"CampaignPageTitle020","CAMPAIGN",30,new Color(.12f,.075f,.035f),TextAnchor.MiddleCenter,new Vector2(0,445),new Vector2(1120,56),new Vector2(.5f,.5f));
            campaignCounter020=Label(campaignPaper020,"CampaignPageCounter020","",18,new Color(.14f,.085f,.04f),TextAnchor.MiddleCenter,new Vector2(690,445),new Vector2(120,42),new Vector2(.5f,.5f));
            campaignArt020=Rect(campaignPaper020,"CampaignImageBounds020",new Vector2(.5f,.5f),new Vector2(.5f,.5f),new Vector2(0,70),new Vector2(1480,650));
            var imageRect=Stretch(campaignArt020,"CampaignOriginalImage020");
            campaignImage020=imageRect.gameObject.AddComponent<UnityEngine.UI.RawImage>();campaignImage020.raycastTarget=false;campaignImage020.color=Color.white;
            campaignAspect020=imageRect.gameObject.AddComponent<UnityEngine.UI.AspectRatioFitter>();campaignAspect020.aspectMode=UnityEngine.UI.AspectRatioFitter.AspectMode.FitInParent;
            Shader shader=Resources.Load<Shader>("Shaders/OracleCampaignBook020");
            if(shader!=null){campaignMaterial020=new Material(shader);campaignImage020.material=campaignMaterial020;}
            var caption=Rect(campaignPaper020,"CampaignExactSubtitleBackground020",new Vector2(.5f,.5f),new Vector2(.5f,.5f),new Vector2(0,-320),new Vector2(1420,132));
            campaignCaptionBackground020=Paint(caption,new Color(.075f,.045f,.02f,.88f));
            campaignCaption020=Label(caption,"CampaignExactSubtitle020","",20,Ivory,TextAnchor.MiddleCenter,Vector2.zero,new Vector2(1376,112),new Vector2(.5f,.5f));campaignCaption020.supportRichText=false;
            campaignPrevious020=CampaignControl020("CampaignPrevious020","PREVIOUS",-592,PreviousCampaignPage020);
            campaignPlay020=CampaignControl020("CampaignPlayPause020","PAUSE",-296,ToggleCampaignPlayback020);campaignPlayLabel020=campaignPlay020.GetComponentInChildren<UnityEngine.UI.Text>();
            campaignNext020=CampaignControl020("CampaignNext020","NEXT",0,NextCampaignPage020);campaignNextLabel020=campaignNext020.GetComponentInChildren<UnityEngine.UI.Text>();
            campaignSubtitles020=CampaignControl020("CampaignSubtitles020","SUBTITLES",296,ToggleCampaignSubtitles020);campaignSubtitleLabel020=campaignSubtitles020.GetComponentInChildren<UnityEngine.UI.Text>();
            campaignSkip020=CampaignControl020("CampaignSkip020","SKIP",592,SkipCampaignBook020);
            if(interfacePreferences!=null){interfacePreferences.Changed+=RefreshCampaignPreferences020;campaignPreferencesBound020=true;}
            BuildCampaignHubEntry020();
            campaignOverlay020.gameObject.SetActive(false);
        }
        UnityEngine.UI.Button CampaignControl020(string name,string text,float x,UnityEngine.Events.UnityAction action)=>
            Button(campaignPaper020,name,text,new Vector2(.5f,.5f),new Vector2(.5f,.5f),new Vector2(x,-442),new Vector2(246,52),action);

        void OpenCampaignEntry020()
        {
            if(!CampaignEntryAllowed020)return;
            if(interfacePreferences!=null&&interfacePreferences.CampaignIntroSeen020){session.Audio?.StartAudioFromGesture();ShowCampaignGateway020(false);}
            else StartCampaignBook020();
        }
        bool CampaignEntryAllowed020=>built&&session!=null&&!session.HasStarted&&!IntroActive&&!LoadingActive&&(session.Rooms==null||!session.Rooms.Connected&&!session.Rooms.Busy)&&(currentPanel==JourneyPanel.Solo020||currentPanel==JourneyPanel.Start)&&!campaignClosing020;
        bool LoadCampaignBook020()
        {
            if(campaignLoaded020)return true;
            var asset=Resources.Load<TextAsset>(CampaignContentResource020);
            if(asset==null)return false;
            CampaignBookData020 data;
            try{data=JsonUtility.FromJson<CampaignBookData020>(asset.text);}catch(System.ArgumentException){return false;}
            if(data==null||data.schemaVersion!=1||data.frames==null||data.frames.Length!=CampaignBookState020.PageCount||
                !ValidCampaignSeconds020(data.minimumPageSeconds,1f,30f)||!ValidCampaignSeconds020(data.voiceTailSeconds,0f,5f)||
                !ValidCampaignSeconds020(data.drawRevealSeconds,.1f,10f)||!ValidCampaignSeconds020(data.colorRevealSeconds,.1f,10f))return false;
            for(int i=0;i<CampaignBookState020.PageCount;i++)
            {
                var item=data.frames[i];string suffix=(i+1).ToString("00");
                if(item==null||item.index!=i+1||string.IsNullOrEmpty(item.title)||item.title.Length>100||string.IsNullOrEmpty(item.text)||item.text.Length>2000||
                    item.imageResource!="CampaignIntro020/Images/frame"+suffix||item.voiceResource!="CampaignIntro020/Voice/frame"+suffix)return false;
                var image=Resources.Load<Texture2D>(item.imageResource);var clip=Resources.Load<AudioClip>(item.voiceResource);
                if(image==null||clip==null||!ValidCampaignSeconds020(clip.length,.01f,180f))return false;
                campaignImages020[i]=image;campaignVoices020[i]=clip;if(clip.loadState==AudioDataLoadState.Unloaded)clip.LoadAudioData();
                campaignBook020.SetHold(i,Mathf.Max(data.minimumPageSeconds,clip.length+data.voiceTailSeconds));
            }
            campaignData020=data;campaignLoaded020=true;return true;
        }
        static bool ValidCampaignSeconds020(float value,float minimum,float maximum)=>!float.IsNaN(value)&&!float.IsInfinity(value)&&value>=minimum&&value<=maximum;
        void StartCampaignBook020()
        {
            if(!CampaignEntryAllowed020||campaignOverlay020==null)return;
            session.Audio?.StartAudioFromGesture();
            if(!LoadCampaignBook020()||!campaignBook020.Begin()){ShowCampaignGateway020(true);return;}
            HideCampaignHubEntry020();campaignClosing020=false;campaignCloseResult020=CampaignBookResult020.None;campaignReducedMotion020=OracleBookAccessibility.PrefersReducedMotion;
            ShowCampaignOverlay020();campaignArt020.gameObject.SetActive(true);
            campaignPrevious020.gameObject.SetActive(true);campaignNext020.gameObject.SetActive(true);campaignSubtitles020.gameObject.SetActive(true);campaignSkip020.gameObject.SetActive(true);
            (campaignPlay020.transform as RectTransform).anchoredPosition=new Vector2(-296,-442);
            campaignCaptionBackground020.rectTransform.anchoredPosition=new Vector2(0,-320);campaignCaptionBackground020.rectTransform.sizeDelta=new Vector2(1420,132);
            campaignCaption020.rectTransform.sizeDelta=new Vector2(1376,112);
            session.Audio?.BeginCampaignIntroAudio020();BindCampaignPage020();
            if(EventSystem.current!=null)EventSystem.current.SetSelectedGameObject(campaignPlay020.gameObject);
        }
        void ShowCampaignOverlay020()
        {
            session.ReleasePointer();menuRoot.gameObject.SetActive(false);menuShowcase?.SetVisible(false);hudRoot.gameObject.SetActive(false);pauseButton.gameObject.SetActive(false);crosshair.gameObject.SetActive(false);
            campaignOverlay020.gameObject.SetActive(true);campaignOverlay020.SetAsLastSibling();campaignInput020.alpha=1f;campaignInput020.interactable=campaignInput020.blocksRaycasts=true;
            campaignCanvasSize020=new Vector2(-1,-1);FitCampaignBook020();
        }
        void ShowCampaignGateway020(bool unavailable)
        {
            if(campaignOverlay020==null)return;
            session.Audio?.StopCampaignIntroAudio020();campaignBook020.Cancel();campaignClosing020=false;
            ShowCampaignOverlay020();campaignArt020.gameObject.SetActive(false);campaignPrevious020.gameObject.SetActive(false);campaignNext020.gameObject.SetActive(false);campaignSubtitles020.gameObject.SetActive(false);campaignSkip020.gameObject.SetActive(false);
            campaignTitle020.text=unavailable?"PROLOGUE UNAVAILABLE":"CAMPAIGN";campaignCounter020.text="";
            campaignCaptionBackground020.gameObject.SetActive(true);campaignCaptionBackground020.rectTransform.anchoredPosition=new Vector2(0,20);campaignCaptionBackground020.rectTransform.sizeDelta=new Vector2(1260,230);
            campaignCaption020.rectTransform.sizeDelta=new Vector2(1190,190);campaignCaption020.fontSize=24;
            campaignCaptionBackground020.color=new Color(.075f,.045f,.02f,.88f);campaignCaption020.color=Ivory;
            campaignCaption020.text=unavailable?"The illustrated prologue is not ready on this device. Try again, or return to the main menu.":"Replay the illustrated prologue.\nThe wider campaign is still in development.";
            campaignPlayLabel020.text=unavailable?"TRY AGAIN":"REPLAY INTRO";(campaignPlay020.transform as RectTransform).anchoredPosition=new Vector2(0,-290);
            RefreshCampaignHubGateway020(unavailable);
            if(EventSystem.current!=null)EventSystem.current.SetSelectedGameObject(campaignPlay020.gameObject);
        }
        void BindCampaignPage020()
        {
            int page=campaignBook020.Page;var item=campaignData020.frames[page];
            SetCampaignShader020(0f,0f,0f);campaignImage020.texture=campaignImages020[page];campaignAspect020.aspectRatio=(float)campaignImages020[page].width/campaignImages020[page].height;
            campaignTitle020.text=item.title;campaignCounter020.text=CampaignCounters020[page];campaignCaption020.text=item.text;
            campaignPrevious020.interactable=page>0;campaignNextLabel020.text=page==CampaignBookState020.PageCount-1?"FINISH":"NEXT";
            session.Audio?.SetCampaignIntroPage020(page,campaignVoices020[page],campaignBook020.Paused);RefreshCampaignPreferences020();RefreshCampaignPlaybackLabel020();
        }
        bool UpdateCampaignBook020()
        {
            if(!CampaignBookVisible020)return false;
            FitCampaignBook020();
            if(session==null||session.HasStarted||IntroActive||LoadingActive){CloseCampaignBookImmediate020();return false;}
            if(campaignClosing020)
            {
                campaignCloseElapsed020+=Mathf.Clamp(Time.unscaledDeltaTime,0f,.1f);campaignInput020.alpha=1f-Mathf.Clamp01(campaignCloseElapsed020/.22f);
                var keyboard=Keyboard.current;bool released=(Mouse.current==null||!Mouse.current.leftButton.isPressed)&&(keyboard==null||!keyboard.escapeKey.isPressed&&!keyboard.enterKey.isPressed&&!keyboard.spaceKey.isPressed);
                if(campaignCloseElapsed020>=.22f&&Time.frameCount>campaignCloseFrame020+1&&released)
                {
                    var result=campaignCloseResult020;campaignOverlay020.gameObject.SetActive(false);campaignClosing020=false;SetPanel(result==CampaignBookResult020.Complete||result==CampaignBookResult020.Skipped?JourneyPanel.Start:JourneyPanel.Solo020);
                    if(result==CampaignBookResult020.Complete||result==CampaignBookResult020.Skipped)CampaignIntroFinished020?.Invoke(result);
                }
                return true;
            }
            if(!campaignBook020.Active)return true;
            bool wasPaused=campaignBook020.Paused;
            bool suspended=session.Audio!=null&&session.Audio.CampaignIntroAudioSuspended020;
            if(!Application.isFocused||suspended||session.Audio!=null&&session.Audio.CampaignIntroAudioPaused020)campaignBook020.Pause();
            if(!wasPaused&&campaignBook020.Paused){session.Audio?.PauseCampaignIntroAudio020();RefreshCampaignPlaybackLabel020();}
            if(!campaignBook020.Paused)session.Audio?.TickCampaignIntroAudio020(Time.unscaledDeltaTime,campaignBook020.Elapsed);
            bool changed=campaignBook020.Tick(Time.unscaledDeltaTime,Application.isFocused,suspended,session.Audio!=null&&session.Audio.CampaignIntroVoicePending020,session.Audio!=null&&session.Audio.CampaignIntroVoicePlaying020);
            if(!campaignBook020.Active){CloseCampaignBook020(campaignBook020.Result);return true;}
            if(changed)BindCampaignPage020();
            float time=campaignBook020.Elapsed;
            float progress=campaignReducedMotion020?1f:Mathf.Clamp01(time/campaignData020.drawRevealSeconds);
            float color=campaignReducedMotion020?1f:Mathf.Clamp01((time-campaignData020.drawRevealSeconds)/campaignData020.colorRevealSeconds);
            SetCampaignShader020(progress,color,Mathf.Clamp01(time/.25f));return true;
        }
        void SetCampaignShader020(float progress,float color,float alpha)
        {
            if(campaignMaterial020!=null){campaignMaterial020.SetFloat(CampaignProgressId020,progress);campaignMaterial020.SetFloat(CampaignColorId020,color);campaignMaterial020.SetFloat(CampaignAlphaId020,alpha);}
            else campaignImage020.color=new Color(1f,1f,1f,alpha);
        }
        void FitCampaignBook020()
        {
            if(canvas==null||campaignPaper020==null)return;
            var size=(canvas.transform as RectTransform).rect.size;if(size==campaignCanvasSize020)return;campaignCanvasSize020=size;
            float scale=Mathf.Min(1f,Mathf.Min((size.x-48f)/1580f,(size.y-48f)/1000f));campaignPaper020.localScale=Vector3.one*Mathf.Max(.1f,scale);
        }
        void ToggleCampaignPlayback020()
        {
            if(campaignClosing020||!Application.isFocused)return;
            if(!campaignBook020.Active){StartCampaignBook020();return;}
            if(campaignBook020.Paused){session.Audio?.StartAudioFromGesture();campaignBook020.Resume();session.Audio?.ResumeCampaignIntroAudio020();}
            else {campaignBook020.Pause();session.Audio?.PauseCampaignIntroAudio020();}
            RefreshCampaignPlaybackLabel020();
        }
        void RefreshCampaignPlaybackLabel020(){if(campaignPlayLabel020!=null&&campaignBook020.Active)campaignPlayLabel020.text=campaignBook020.Paused?"RESUME":"PAUSE";}
        void PreviousCampaignPage020(){if(!campaignClosing020&&campaignBook020.Previous())BindCampaignPage020();}
        void NextCampaignPage020()
        {
            if(campaignClosing020||!campaignBook020.Active)return;
            if(campaignBook020.Next())BindCampaignPage020();else if(!campaignBook020.Active)CloseCampaignBook020(campaignBook020.Result);
        }
        void SkipCampaignBook020(){if(campaignClosing020||!campaignBook020.Active)return;campaignBook020.Skip();CloseCampaignBook020(campaignBook020.Result);}
        void CancelCampaignBook020(){if(campaignClosing020)return;campaignBook020.Cancel();CloseCampaignBook020(CampaignBookResult020.Cancelled);}
        void EscapeCampaignBook020()
        {
            if(campaignClosing020)return;
            if(campaignBook020.Active&&!campaignBook020.Paused){campaignBook020.Pause();session.Audio?.PauseCampaignIntroAudio020();RefreshCampaignPlaybackLabel020();}
            else CancelCampaignBook020();
        }
        void CloseCampaignBook020(CampaignBookResult020 result)
        {
            if(campaignClosing020)return;
            session.Audio?.StopCampaignIntroAudio020();campaignCloseResult020=result;
            if(result==CampaignBookResult020.Complete||result==CampaignBookResult020.Skipped)interfacePreferences?.MarkCampaignIntroSeen020();
            campaignClosing020=true;campaignCloseFrame020=Time.frameCount;campaignCloseElapsed020=0f;campaignInput020.interactable=false;
            if(EventSystem.current!=null)EventSystem.current.SetSelectedGameObject(null);
        }
        void CloseCampaignBookImmediate020()
        {
            bool visible=CampaignBookVisible020;
            session?.Audio?.StopCampaignIntroAudio020();campaignBook020.Cancel();campaignClosing020=false;
            if(campaignOverlay020!=null)campaignOverlay020.gameObject.SetActive(false);
            if(visible&&session!=null&&!session.HasStarted&&built)SetPanel(JourneyPanel.Start);
        }
        void ToggleCampaignSubtitles020(){if(interfacePreferences!=null)interfacePreferences.SetNpcSubtitlesEnabled(!interfacePreferences.NpcSubtitlesEnabled);}
        void RefreshCampaignPreferences020()
        {
            if(campaignCaption020==null||!campaignBook020.Active)return;
            bool enabled=interfacePreferences==null||interfacePreferences.NpcSubtitlesEnabled;
            campaignCaptionBackground020.gameObject.SetActive(enabled);
            campaignCaption020.fontSize=interfacePreferences!=null?interfacePreferences.NpcSubtitleFontSize:20;
            bool background=interfacePreferences==null||interfacePreferences.NpcSubtitleBackground;
            campaignCaptionBackground020.color=background?new Color(.075f,.045f,.02f,.88f):Color.clear;
            campaignCaption020.color=background?Ivory:new Color(.1f,.055f,.02f,1f);
            campaignSubtitleLabel020.text=enabled?"SUBTITLES: ON":"SUBTITLES: OFF";
        }
        void DisposeCampaignBook020()
        {
            CloseCampaignBookImmediate020();CampaignIntroFinished020=null;
            if(campaignPreferencesBound020&&interfacePreferences!=null)interfacePreferences.Changed-=RefreshCampaignPreferences020;
            campaignPreferencesBound020=false;if(campaignMaterial020!=null)Destroy(campaignMaterial020);
        }
    }
}

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Existing main-menu Canvas reference pixels; separate unmasked story overlay, no world/campaign/reward/room authority.
// Array Formats: Fixed10shared images/voices/counters, cold deserialized10frame table, one pure page state and one owned shader material.
// Dependencies/Pointers: Existing EventSystem/gesture/mix/subtitle preferences retain ownership. Complete/Skip alone marks seen; release-fenced safe Start then exposes a future router event. Root alone imports media/shader, integrates source and accepts native/browser/audio/render behavior.
