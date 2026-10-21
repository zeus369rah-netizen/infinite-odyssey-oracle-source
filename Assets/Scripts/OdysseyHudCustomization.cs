using UnityEngine;

namespace OdysseyOracle
{
    public sealed partial class OdysseyHud
    {
        OracleAvatarCustomizationPanel customizationControls;
        RectTransform customizationControlHost;
        UnityEngine.UI.RawImage customizationPreviewImage;
        UnityEngine.UI.Text customizationPreviewStatus;
        OracleAvatarPreviewLease customizationPreview;
        JourneyPanel customizationReturn=JourneyPanel.Start;
        bool customizationInitialized;
        int customizationOpenedFrame;

        void BuildCustomizationPanel()
        {
            var content=PanelContent(JourneyPanel.Customize,"CustomizeOraclePanel",new Vector2(1150,750));
            MenuLabel(content,"CustomizeOracleTitle","CUSTOMIZE ORACLE",34,Ivory,58);
            MenuLabel(content,"CustomizeOracleCopy","Choose a starting look, then change your hat, clothing, hair and book.",17,Muted,32);
            var row=Rect(content,"AppearanceControlsAndPreview",Vector2.zero,new Vector2(.5f,.5f),Vector2.zero,new Vector2(0,445));Height(row,445);
            customizationControlHost=Rect(row,"AppearanceControls",new Vector2(0,1),new Vector2(0,1),Vector2.zero,new Vector2(592,400));
            customizationControls=customizationControlHost.gameObject.AddComponent<OracleAvatarCustomizationPanel>();customizationControls.enabled=false;
            var preview=Rect(row,"Oracle3DPreview",new Vector2(1,1),new Vector2(1,1),new Vector2(0,-3),new Vector2(340,425));
            customizationPreviewImage=preview.gameObject.AddComponent<UnityEngine.UI.RawImage>();customizationPreviewImage.raycastTarget=false;
            customizationPreviewImage.color=new Color(.025f,.04f,.045f,1);
            customizationPreviewStatus=Label(preview,"AppearancePreviewUnavailable","3D PREVIEW UNAVAILABLE",18,Muted,TextAnchor.MiddleCenter,Vector2.zero,new Vector2(310,90),new Vector2(.5f,.5f));
            customizationPreviewStatus.gameObject.SetActive(false);
            MenuButton(content,"CustomizationBack","BACK",CloseCustomization,50);
        }
        void OpenCustomization(JourneyPanel from)
        {
            if(session==null||IntroActive||LoadingActive||ShopVisible||DownedActive)return;
            AbilityBindings.CancelRebind();if(session.HasStarted)session.Pause();customizationReturn=from;
            if(!customizationInitialized)
            {
                RemoteOracleAvatar previewAvatar=null;
                try{customizationPreview=new OracleAvatarPreviewLease();customizationPreviewImage.texture=customizationPreview.Target;customizationPreviewImage.color=Color.white;previewAvatar=customizationPreview.Avatar;}
                catch(System.Exception failure){customizationPreviewStatus.gameObject.SetActive(true);Debug.LogWarning("Oracle preview unavailable: "+failure.GetType().Name);}
                customizationInitialized=customizationControls.Initialize(customizationControlHost,font,previewAvatar);
                if(!customizationInitialized){DisposeCustomization();return;}
            }
            customizationOpenedFrame=Time.frameCount;SetPanel(JourneyPanel.Customize);customizationControls.Refresh();
        }
        void CloseCustomization()
        {OracleAvatarLocalAppearance.Save();SetPanel(customizationReturn);}
        void SetCustomizationVisible(bool visible)
        {
            if(customizationControls!=null)customizationControls.enabled=visible&&customizationInitialized;
            customizationPreview?.SetVisible(visible&&customizationInitialized);
        }
        void UpdateCustomizationNavigation()
        {
            var keyboard=UnityEngine.InputSystem.Keyboard.current;
            if(Application.isFocused&&!AbilityBindings.IsRebinding&&Time.frameCount>customizationOpenedFrame&&keyboard!=null&&keyboard.escapeKey.wasPressedThisFrame)CloseCustomization();
        }
        void DisposeCustomization()
        {
            if(customizationPreviewImage!=null)customizationPreviewImage.texture=null;
            customizationPreview?.Dispose();customizationPreview=null;customizationInitialized=false;
        }
    }
}

// # ARCHITECTURE SNAPSHOT
// Existing1920x1080 Canvas/JourneyPanel/input owner. One cold retained preview lease, no new Update/input/camera-main owner.
//4-byte appearance controls reuse rigid meshes/property blocks; close/back never resumes or captures the pointer.
