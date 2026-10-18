using UnityEngine;
using UnityEngine.UI;

namespace OdysseyOracle
{
    // User preference only; networking resolves a stable-slot default unless a user explicitly customizes.
    public static class OracleAvatarLocalAppearance
    {
        const string Preference="oracle.avatar.cosmetic.v1";
        static bool loaded,custom,dirty;
        static OracleAvatarAppearance selected;
        public static OracleAvatarAppearance Resolve(int admissionSlot)
        {Load();return custom?selected:OracleAvatarAppearance.StartingLook(admissionSlot);}
        public static bool Select(uint packed)
        {
            if(!OracleAvatarAppearance.TryDecode(packed,out var value))return false;
            Load();selected=value;custom=true;dirty=true;PlayerPrefs.SetInt(Preference,(int)packed);return true;
        }
        public static void UseSlotDefault(){Load();custom=false;dirty=true;PlayerPrefs.DeleteKey(Preference);}
        public static void Save(){if(!dirty)return;PlayerPrefs.Save();dirty=false;}
        static void Load()
        {
            if(loaded)return;loaded=true;int packed=PlayerPrefs.GetInt(Preference,-1);
            custom=packed>=0&&OracleAvatarAppearance.TryDecode((uint)packed,out selected);
        }
    }
    // Root mounts this in an existing menu/canvas and passes its font and optional preview.
    // No Update/OnGUI polling, new canvas, input ownership, camera or EventSystem is introduced.
    public sealed class OracleAvatarCustomizationPanel:MonoBehaviour
    {
        static readonly string[] Looks={"Teal Oracle","Crimson Oracle","Cobalt Oracle","Violet Oracle","Ivory Oracle","Amber Oracle","Verdant Oracle","Midnight Oracle"};
        static readonly string[] HatLabels={"Hat: Spire","Hat: Hooked","Hat: Broad","Hat: Hood only"};
        static readonly string[] HatColors={"Hat color: Teal","Hat color: Crimson","Hat color: Cobalt","Hat color: Violet","Hat color: Ivory","Hat color: Amber","Hat color: Verdant","Hat color: Midnight"};
        static readonly string[] RobeColors={"Robe color: Teal","Robe color: Crimson","Robe color: Cobalt","Robe color: Violet","Robe color: Ivory","Robe color: Amber","Robe color: Verdant","Robe color: Midnight"};
        static readonly string[] Garments={"Clothing: Long robe","Clothing: Split coat","Clothing: Tabard"};
        static readonly string[] Shoulders={"Shoulders: Cloth","Shoulders: Gold plate","Shoulders: Layered gold"};
        static readonly string[] Hair={"Hair: Silver","Hair: Black","Hair: Brown","Hair: Copper","Hair: Gold","Hair: Violet","Hair: Ash","Hair: Teal"};
        static readonly string[] Book={"Book: Hidden","Book: Equipped"};
        readonly Text[] values=new Text[7];
        OracleAvatarAppearance current;
        RemoteOracleAvatar preview;
        RectTransform controlRoot;
        bool initialized;
        public bool Initialize(RectTransform parent,Font font,RemoteOracleAvatar optionalPreview=null)
        {
            if(initialized||parent==null||font==null)return false;
            preview=optionalPreview;
            controlRoot=(RectTransform)new GameObject("Oracle appearance controls",typeof(RectTransform)).transform;
            controlRoot.SetParent(parent,false);controlRoot.anchorMin=controlRoot.anchorMax=controlRoot.pivot=new Vector2(0,1);controlRoot.sizeDelta=new Vector2(592,400);
            for(int i=0;i<OracleAvatarAppearance.StartingLooks;i++)
            {
                int captured=i;
                var button=MakeButton(controlRoot,font,Looks[i],new Vector2(16+(i%4)*142,-16-(i/4)*38),new Vector2(136,32),out _);
                button.GetComponent<Image>().color=OracleAvatarModel.LookColor(i);button.onClick.AddListener(()=>ChooseLook(captured));
            }
            MakeButton(controlRoot,font,"Stable party color",new Vector2(16,-94),new Vector2(276,32),out _).onClick.AddListener(UsePartyDefault);
            for(int i=0;i<values.Length;i++)
            {
                int captured=i;
                MakeButton(controlRoot,font,string.Empty,new Vector2(16,-136-i*36),new Vector2(560,30),out values[i]).onClick.AddListener(()=>Cycle(captured));
            }
            initialized=true;Refresh();return true;
        }
        public void Refresh()
        {
            if(!initialized)return;
            int slot=0;var room=RoomConnection.Instance;if(room!=null)room.TryGetAdmissionSlot(room.LocalClientId,out slot);
            current=OracleAvatarLocalAppearance.Resolve(slot);
            values[0].text=HatLabels[current.HatStyle];values[1].text=HatColors[current.HatColor];values[2].text=RobeColors[current.RobeColor];
            values[3].text=Garments[current.ClothingStyle];values[4].text=Shoulders[current.ShoulderStyle];values[5].text=Hair[current.HairColor];values[6].text=Book[current.BookVisible?1:0];
            if(preview!=null)preview.ApplyAppearance(current.Packed);
        }
        void ChooseLook(int index){OracleAvatarLocalAppearance.Select(OracleAvatarAppearance.StartingLook(index).Packed);Refresh();}
        void UsePartyDefault(){OracleAvatarLocalAppearance.UseSlotDefault();Refresh();}
        void Cycle(int field)
        {
            if(field==0)current=current.WithHatStyle((current.HatStyle+1)%OracleAvatarAppearance.HatStyles);
            else if(field==1)current=current.WithHatColor((current.HatColor+1)%OracleAvatarAppearance.Colors);
            else if(field==2)current=current.WithRobeColor((current.RobeColor+1)%OracleAvatarAppearance.Colors);
            else if(field==3)current=current.WithClothingStyle((current.ClothingStyle+1)%OracleAvatarAppearance.ClothingStyles);
            else if(field==4)current=current.WithShoulderStyle((current.ShoulderStyle+1)%OracleAvatarAppearance.ShoulderStyles);
            else if(field==5)current=current.WithHairColor((current.HairColor+1)%OracleAvatarAppearance.Colors);
            else if(field==6)current=current.WithBook(!current.BookVisible);
            else return;
            OracleAvatarLocalAppearance.Select(current.Packed);Refresh();
        }
        static Button MakeButton(RectTransform parent,Font font,string label,Vector2 position,Vector2 size,out Text text)
        {
            var node=new GameObject("Oracle appearance option",typeof(RectTransform),typeof(Image),typeof(Button));
            var rect=(RectTransform)node.transform;rect.SetParent(parent,false);rect.anchorMin=rect.anchorMax=rect.pivot=new Vector2(0,1);rect.anchoredPosition=position;rect.sizeDelta=size;
            node.GetComponent<Image>().color=new Color(.08f,.12f,.13f,.96f);
            var child=new GameObject("Label",typeof(RectTransform),typeof(Text));var childRect=(RectTransform)child.transform;
            childRect.SetParent(rect,false);childRect.anchorMin=Vector2.zero;childRect.anchorMax=Vector2.one;childRect.offsetMin=new Vector2(6,2);childRect.offsetMax=new Vector2(-6,-2);
            text=child.GetComponent<Text>();text.font=font;text.text=label;text.fontSize=14;text.alignment=TextAnchor.MiddleCenter;text.color=Color.white;text.raycastTarget=false;
            var button=node.GetComponent<Button>();button.targetGraphic=node.GetComponent<Image>();return button;
        }
        void OnEnable(){if(controlRoot!=null)controlRoot.gameObject.SetActive(true);}
        void OnDisable(){OracleAvatarLocalAppearance.Save();if(controlRoot!=null)controlRoot.gameObject.SetActive(false);}
        void OnDestroy(){OracleAvatarLocalAppearance.Save();if(controlRoot!=null)Destroy(controlRoot.gameObject);}
    }
}

// # ARCHITECTURE SNAPSHOT
// Main-thread cold UGUI/preference adapter; exact4-byte appearance remains cosmetic and palette/style bounded.
// Root owns menu visibility/input/preview lifecycle. Button listeners/objects allocate once at initialization; no per-frame work is added.
