using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace OdysseyOracle
{
    /// <summary>
    /// Original next-release voxel smith; prepared outside Assets deliberately.
    /// Design references inspected: user clipboard 2178bc95-daeb-42d1-b2c8-c6fe7c3326f4
    /// and 211567eb-4796-4b4c-9423-86f696e18026. They guide the friendly dark-haired,
    /// grey-bearded adult, red scarf, leather apron, gold clasps and lava hammer.
    /// All geometry, pixel patterns and idle motion below are newly authored code.
    /// No reference bitmap, third-party mesh, avatar, skeleton or generated media is used.
    /// Local forward is +Z; boots stand at Y=0; body crown is approximately 1.90 m.
    /// Hover labels, key bindings, currency and shop UI belong to the caller.
    /// Further user angles dca21a2e, 8f6e315d, f290c452 and 97d64a8c were
    /// inspected for rear apron straps, rolled linen and the same color silhouette.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed partial class NpcBlacksmith : MonoBehaviour
    {
        const int Coal=0,Skin=1,Hair=2,Beard=3,Scarf=4,Leather=5,Gold=6,Steel=7,Ember=8,Linen=9;
        const int PaletteCount=10;
        readonly List<Object> resources=new List<Object>();
        OraclePlayer player;
        GameObject visual;
        Transform breathing,head,hammerArm;
        Material emberPaint;

        public Transform InteractionAnchor {get;private set;}
        public Transform CampfireAnchor {get;private set;}
        public Vector3 AimPoint => InteractionAnchor!=null?InteractionAnchor.position:transform.position+Vector3.up*1.35f;
        public string DisplayName => "Blacksmith";
        public bool BuildWorkstation {get;set;}=true;

        public void Initialize(OraclePlayer owner)
        {
            Retire();player=owner;InitializeFluidSmith009();
            visual=new GameObject("Original voxel blacksmith and forge");
            visual.transform.SetParent(transform,false);
            var hide=Pattern("Smith original hammered grain",32,0);
            var cloth=Pattern("Smith original woven cloth",32,1);
            var skin=Pattern("Smith original skin pixels",32,2);
            Material[] paints={
                Paint("Forge charcoal",new Color(.11f,.115f,.12f),.12f,.22f,hide),
                Paint("Warm exposed skin",new Color(.69f,.47f,.31f),0f,.23f,skin),
                Paint("Dark swept hair",new Color(.095f,.071f,.055f),0f,.18f,hide),
                Paint("Grey beard",new Color(.49f,.50f,.47f),0f,.22f,cloth),
                Paint("Brick red scarf",new Color(.50f,.085f,.049f),0f,.17f,cloth),
                Paint("Worn dark leather",new Color(.25f,.135f,.065f),.03f,.24f,hide),
                Paint("Worn gold clasps",new Color(.70f,.48f,.19f),.68f,.42f,hide),
                Paint("Hammer and anvil steel",new Color(.27f,.29f,.28f),.66f,.38f,hide),
                Paint("Lava seams",new Color(1f,.31f,.025f),.05f,.27f,null),
                Paint("Rolled linen sleeves",new Color(.67f,.62f,.50f),0f,.16f,cloth)
            };
            emberPaint=paints[Ember];emberPaint.EnableKeyword("_EMISSION");SetGlow(1f);
            var lower=new Model();BuildLegsAndApronSkirt(lower);Emit("Boots belt and apron skirt",lower,paints,visual.transform);
            breathing=Child("Breathing torso",visual.transform,new Vector3(0f,1.01f,0f));
            var body=new Model();BuildTorsoAndRestingArm(body);Emit("Apron scarf and resting hand",body,paints,breathing);
            head=Child("Friendly smith head",breathing,new Vector3(0f,.56f,0f));
            var face=new Model();BuildHead(face);Emit("Beard face and swept hair",face,paints,head);
            hammerArm=Child("Hammer arm at rest",breathing,new Vector3(.30f,.29f,0f));
            var arm=new Model();BuildHoldingArm(arm);Emit("Closed hammer grip",arm,paints,hammerArm);
            Transform grip=Child("Hammer grip socket",hammerArm,new Vector3(.25f,.03f,.11f));
            var hammer=new Model();BuildHammer(hammer);Emit("Original lava-seamed hammer",hammer,paints,grip);
            InteractionAnchor=Child("Shop interaction anchor",visual.transform,new Vector3(0f,1.32f,.27f));
            var bodyCollider=visual.AddComponent<CapsuleCollider>();
            bodyCollider.center=new Vector3(0f,.92f,0f);bodyCollider.height=1.83f;bodyCollider.radius=.30f;
            if(BuildWorkstation) {
                var workstation=new Model();BuildForgeAndAnvil(workstation);
                Emit("Original anvil and coal forge",workstation,paints,visual.transform);
                towerAnvilCollider=AddCollider("Anvil solid base",new Vector3(1.10f,.43f,.12f),new Vector3(.64f,.86f,.56f));
                AddCollider("Coal forge solid base",new Vector3(1.06f,.29f,1.15f),new Vector3(.86f,.58f,.72f));
                CampfireAnchor=Child("Forge ember audio anchor",visual.transform,new Vector3(1.06f,.68f,1.15f));
                BuildSmallRefugeFurnace(paints);
            }
        }

        void LateUpdate(){TickFluidSmith009();TickSmithFurnace015();}

        void BuildLegsAndApronSkirt(Model s)
        {
            for(int side=-1;side<=1;side+=2) {
                float x=side*.165f;
                s.Box(new Vector3(x,.105f,.063f),new Vector3(.235f,.19f,.355f),.025f,Leather);
                s.Box(new Vector3(x,.033f,.070f),new Vector3(.248f,.052f,.37f),.009f,Coal);
                s.Box(new Vector3(x,.192f,-.035f),new Vector3(.190f,.19f,.198f),.017f,Leather);
                s.Box(new Vector3(x,.36f,-.015f),new Vector3(.197f,.23f,.208f),.015f,Leather);
                s.Box(new Vector3(x,.625f,-.015f),new Vector3(.208f,.34f,.233f),.021f,Leather);
                s.Box(new Vector3(x,.448f,.005f),new Vector3(.232f,.068f,.242f),.008f,Gold);
                s.Box(new Vector3(x,.448f,.134f),new Vector3(.076f,.057f,.018f),.005f,Coal);
                for(int rivet=-1;rivet<=1;rivet++)s.Box(new Vector3(x+rivet*.068f,.451f,.132f),new Vector3(.015f,.015f,.014f),.003f,Gold);
                for(int row=0;row<3;row++)s.Box(new Vector3(x,.186f-row*.031f,.160f+row*.018f),new Vector3(.163f,.022f,.017f),.004f,Coal);
            }
            s.Box(new Vector3(0f,.809f,-.005f),new Vector3(.49f,.16f,.28f),.024f,Leather);
            for(int panel=0;panel<5;panel++) {
                float x=(panel-2)*.086f,h=.40f-Mathf.Abs(panel-2)*.012f;
                s.Box(new Vector3(x,.757f,.182f),new Vector3(.082f,h,.047f),.007f,Leather);
                s.Box(new Vector3(x,.575f,.211f),new Vector3(.072f,.014f,.010f),.002f,Coal);
                s.Box(new Vector3(x,.757f,-.164f),new Vector3(.082f,h*.95f,.043f),.007f,Leather);
            }
            s.Box(new Vector3(0f,.968f,.170f),new Vector3(.546f,.072f,.075f),.009f,Leather);
            s.Frame(new Vector3(0f,.968f,.219f),new Vector2(.078f,.063f),.010f,Gold);
            for(int i=-3;i<=3;i++)if(i!=0)s.Box(new Vector3(i*.065f,.968f,.215f),new Vector3(.014f,.015f,.013f),.003f,Gold);
            s.Box(new Vector3(-.293f,.811f,.042f),new Vector3(.115f,.235f,.13f),.012f,Leather);
            s.Box(new Vector3(-.293f,.911f,.112f),new Vector3(.119f,.051f,.018f),.006f,Coal);
            s.Box(new Vector3(-.293f,.909f,.129f),new Vector3(.024f,.021f,.013f),.003f,Gold);
            s.Rod(new Vector3(-.241f,.936f,.137f),new Vector3(-.241f,.780f,.155f),.014f,Steel);
            s.Rod(new Vector3(-.198f,.914f,.163f),new Vector3(-.185f,.733f,.181f),.017f,Steel);
            s.Box(new Vector3(-.185f,.725f,.180f),new Vector3(.041f,.029f,.023f),.004f,Steel);
        }

        void BuildTorsoAndRestingArm(Model s)
        {
            s.Box(new Vector3(0f,.158f,0f),new Vector3(.526f,.49f,.284f),.042f,Linen);
            s.Box(new Vector3(0f,.106f,.170f),new Vector3(.432f,.463f,.055f),.014f,Leather);
            s.Box(new Vector3(-.067f,.051f,.207f),new Vector3(.173f,.121f,.025f),.009f,Leather);
            s.Box(new Vector3(-.067f,.107f,.224f),new Vector3(.178f,.016f,.012f),.002f,Coal);
            s.Rod(new Vector3(-.185f,.401f,-.163f),new Vector3(.195f,-.026f,-.163f),.057f,Leather);
            s.Rod(new Vector3(.185f,.401f,-.163f),new Vector3(-.195f,-.026f,-.163f),.057f,Leather);
            s.Frame(new Vector3(0f,.193f,-.201f),new Vector2(.073f,.065f),.010f,Gold);
            for(int i=0;i<4;i++)for(int side=-1;side<=1;side+=2)
                s.Box(new Vector3(side*(.164f-i*.046f),.375f-i*.053f,-.197f),new Vector3(.012f,.013f,.010f),.002f,Gold);
            for(int side=-1;side<=1;side+=2) {
                float x=side*.184f;
                s.Rod(new Vector3(x,.418f,-.005f),new Vector3(x,.177f,.184f),.053f,Leather);
                s.Frame(new Vector3(x,.254f,.206f),new Vector2(.068f,.077f),.009f,Gold);
                for(int y=0;y<2;y++)s.Box(new Vector3(x,.193f-y*.039f,.213f),new Vector3(.016f,.016f,.012f),.003f,Gold);
                s.Box(new Vector3(side*.271f,.314f,-.006f),new Vector3(.112f,.152f,.282f),.015f,Coal);
            }
            s.Box(new Vector3(0f,.420f,.008f),new Vector3(.366f,.095f,.302f),.025f,Scarf);
            for(int i=0;i<7;i++) {
                float x=(i-3)*.049f;
                s.Box(new Vector3(x,.402f,.174f+Mathf.Cos(i*.6f)*.010f),new Vector3(.064f,.075f,.045f),.011f,Scarf,
                    Quaternion.Euler(0f,0f,(i-3)*-3f));
            }
            s.Box(new Vector3(.103f,.302f,.199f),new Vector3(.090f,.187f,.038f),.011f,Scarf,Quaternion.Euler(0f,0f,-12f));
            s.Rod(new Vector3(-.29f,.323f,0f),new Vector3(-.370f,.086f,.014f),.175f,Linen);
            s.Box(new Vector3(-.371f,.083f,.017f),new Vector3(.192f,.087f,.193f),.012f,Linen,Quaternion.Euler(0f,0f,-12f));
            s.Rod(new Vector3(-.375f,.045f,.022f),new Vector3(-.405f,-.105f,.071f),.120f,Skin);
            s.Rod(new Vector3(-.403f,-.058f,.052f),new Vector3(-.415f,-.161f,.086f),.137f,Leather);
            for(int row=0;row<2;row++) {
                s.Box(new Vector3(-.409f,-.071f-row*.063f,.124f),new Vector3(.144f,.023f,.023f),.003f,Gold);
                for(int i=-1;i<=1;i++)s.Box(new Vector3(-.409f+i*.044f,-.071f-row*.063f,.139f),new Vector3(.010f,.012f,.010f),.002f,Gold);
            }
            s.Box(new Vector3(-.421f,-.204f,.091f),new Vector3(.112f,.113f,.074f),.015f,Skin);
            for(int finger=0;finger<4;finger++) {
                float x=-.461f+finger*.026f;
                s.Box(new Vector3(x,-.268f,.101f),new Vector3(.022f,.069f,.043f),.005f,Skin,
                    Quaternion.Euler(8f,0f,(finger-1.5f)*4f));
            }
            s.Rod(new Vector3(-.365f,-.183f,.108f),new Vector3(-.349f,-.233f,.143f),.031f,Skin);
        }

        void BuildHead(Model s)
        {
            s.Box(new Vector3(0f,.115f,.0f),new Vector3(.306f,.319f,.267f),.035f,Skin);
            s.Box(new Vector3(0f,-.044f,-.012f),new Vector3(.164f,.104f,.170f),.020f,Skin);
            for(int side=-1;side<=1;side+=2) {
                s.Box(new Vector3(side*.165f,.106f,-.006f),new Vector3(.049f,.086f,.066f),.011f,Skin);
                s.Box(new Vector3(side*.069f,.151f,.138f),new Vector3(.074f,.036f,.014f),.005f,Linen);
                s.Box(new Vector3(side*.067f,.151f,.149f),new Vector3(.024f,.029f,.010f),.004f,Hair);
                s.Box(new Vector3(side*.064f-.005f,.158f,.156f),new Vector3(.008f,.009f,.004f),.001f,Linen);
                s.Box(new Vector3(side*.072f,.192f,.151f),new Vector3(.093f,.026f,.021f),.005f,Hair,
                    Quaternion.Euler(0f,0f,side*-5f));
                s.Box(new Vector3(side*.119f,.080f,.137f),new Vector3(.052f,.048f,.031f),.010f,Skin);
            }
            s.Box(new Vector3(0f,.099f,.162f),new Vector3(.047f,.070f,.051f),.011f,Skin);
            s.Box(new Vector3(0f,.042f,.150f),new Vector3(.133f,.035f,.029f),.009f,Hair);
            s.Box(new Vector3(0f,.017f,.159f),new Vector3(.084f,.014f,.009f),.003f,Leather);
            s.Box(new Vector3(0f,-.005f,.111f),new Vector3(.253f,.117f,.123f),.020f,Hair);
            for(int i=0;i<9;i++) {
                float x=(i-4)*.027f,y=-.013f-Mathf.Cos((i-4)*.29f)*.024f;
                s.Box(new Vector3(x,y,.166f),new Vector3(.039f,.094f+(i%3)*.009f,.052f),.007f,Beard,
                    Quaternion.Euler(0f,0f,(i-4)*-3f));
                if(i%2==0)s.Box(new Vector3(x,y-.037f,.190f),new Vector3(.017f,.051f,.018f),.003f,Linen);
            }
            for(int side=-1;side<=1;side+=2)for(int i=0;i<3;i++)
                s.Box(new Vector3(side*(.118f+i*.005f),.053f-i*.025f,.143f),new Vector3(.044f,.056f,.052f),.008f,i%2==0?Beard:Hair);
            s.Box(new Vector3(0f,.273f,-.014f),new Vector3(.330f,.082f,.287f),.018f,Hair);
            s.Box(new Vector3(0f,.125f,-.134f),new Vector3(.330f,.279f,.060f),.017f,Hair);
            for(int ring=0;ring<3;ring++)for(int i=0;i<10;i++) {
                float a=i*Mathf.PI*.2f;
                float x=Mathf.Cos(a)*(.153f+ring*.006f),z=Mathf.Sin(a)*.131f;
                if(z>.065f && ring>0)continue;
                s.Box(new Vector3(x,.247f-ring*.067f,z),new Vector3(.070f,.078f,.067f),.008f,Hair,
                    Quaternion.Euler(0f,-i*36f,Mathf.Cos(a)*-14f));
            }
            for(int i=0;i<6;i++)s.Box(new Vector3((i-2.5f)*.047f,.283f+(i%2)*.016f,.042f),
                new Vector3(.058f,.079f,.106f),.008f,Hair,Quaternion.Euler(-12f,0f,(i-2.5f)*-7f));
            s.Box(new Vector3(-.133f,.152f,-.085f),new Vector3(.027f,.112f,.040f),.005f,Beard);
            s.Box(new Vector3(.133f,.152f,-.085f),new Vector3(.027f,.112f,.040f),.005f,Beard);
        }

        void BuildHoldingArm(Model s)
        {
            s.Rod(new Vector3(0f,0f,0f),new Vector3(.096f,-.213f,.026f),.175f,Linen);
            s.Box(new Vector3(.099f,-.191f,.029f),new Vector3(.188f,.080f,.182f),.011f,Linen,
                Quaternion.Euler(0f,0f,19f));
            s.Rod(new Vector3(.110f,-.190f,.035f),new Vector3(.233f,-.027f,.098f),.125f,Skin);
            s.Rod(new Vector3(.159f,-.119f,.065f),new Vector3(.222f,-.034f,.098f),.145f,Leather);
            for(int i=0;i<2;i++)s.Box(new Vector3(.184f+i*.037f,-.088f+i*.051f,.154f),
                new Vector3(.133f,.025f,.024f),.004f,Gold,Quaternion.Euler(0f,0f,-34f));
            Vector3 grip=new Vector3(.25f,.03f,.11f);
            s.Box(grip+new Vector3(.036f,-.003f,-.006f),new Vector3(.089f,.117f,.079f),.012f,Skin);
            for(int finger=0;finger<4;finger++) {
                float y=.038f-finger*.025f;
                s.Box(grip+new Vector3(.012f,y,.050f),new Vector3(.071f,.020f,.029f),.004f,Skin);
                s.Box(grip+new Vector3(-.031f,y,.028f),new Vector3(.025f,.020f,.044f),.004f,Skin);
                s.Box(grip+new Vector3(-.031f,y,-.008f),new Vector3(.024f,.020f,.032f),.004f,Skin);
            }
            s.Rod(grip+new Vector3(.074f,-.004f,.033f),grip+new Vector3(.033f,.057f,.069f),.030f,Skin);
            s.Box(grip+new Vector3(.016f,.055f,.064f),new Vector3(.041f,.024f,.030f),.005f,Skin);
        }

        void BuildHammer(Model s)
        {
            s.Box(new Vector3(0f,.039f,0f),new Vector3(.043f,.624f,.043f),.006f,Leather);
            for(int i=0;i<12;i++)s.Box(new Vector3(0f,-.244f+i*.045f,0f),new Vector3(.058f,.017f,.058f),.003f,i%3==0?Gold:Coal);
            s.Box(new Vector3(0f,-.284f,0f),new Vector3(.079f,.048f,.077f),.007f,Steel);
            s.Box(new Vector3(0f,.350f,0f),new Vector3(.090f,.058f,.084f),.010f,Gold);
            s.Box(new Vector3(0f,.486f,0f),new Vector3(.404f,.274f,.272f),.020f,Coal);
            for(int side=-1;side<=1;side+=2)s.Box(new Vector3(side*.187f,.486f,0f),new Vector3(.039f,.246f,.265f),.009f,Steel);
            Vector3[] crack={new Vector3(-.123f,.608f,.141f),new Vector3(-.087f,.555f,.141f),new Vector3(-.106f,.498f,.141f),
                new Vector3(-.045f,.455f,.141f),new Vector3(-.066f,.359f,.141f)};
            for(int i=0;i<crack.Length-1;i++) {
                s.Rod(crack[i],crack[i+1],.010f,Leather);
                s.Rod(crack[i]+Vector3.forward*.006f,crack[i+1]+Vector3.forward*.006f,.0035f,Ember);
                Vector3 a=new Vector3(-crack[i].x,crack[i].y,-crack[i].z-.006f);
                Vector3 b=new Vector3(-crack[i+1].x,crack[i+1].y,-crack[i+1].z-.006f);
                s.Rod(a,b,.0035f,Ember);
            }
            s.Rod(crack[1],new Vector3(.021f,.583f,.148f),.004f,Ember);
            s.Rod(crack[2],new Vector3(-.171f,.460f,.148f),.004f,Ember);
            s.Rod(crack[3],new Vector3(.068f,.412f,.148f),.004f,Ember);
            // A simple original forge lozenge, distinct from the Oracle cuff sigil.
            Vector3 mark=new Vector3(.094f,.503f,.147f);
            s.Rod(mark+Vector3.up*.034f,mark+Vector3.right*.025f,.004f,Ember);
            s.Rod(mark+Vector3.right*.025f,mark-Vector3.up*.034f,.004f,Ember);
            s.Rod(mark-Vector3.up*.034f,mark-Vector3.right*.025f,.004f,Ember);
            s.Rod(mark-Vector3.right*.025f,mark+Vector3.up*.034f,.004f,Ember);
            s.Rod(mark-Vector3.up*.039f,mark+Vector3.up*.039f,.003f,Ember);
            for(int i=0;i<4;i++)s.Rod(new Vector3(-.12f+i*.079f,.628f,-.080f),new Vector3(-.098f+i*.071f,.628f,.108f),.003f,Ember);
        }

        void BuildForgeAndAnvil(Model s)
        {
            Vector3 a=new Vector3(1.10f,0f,.12f);
            s.Box(a+Vector3.up*.275f,new Vector3(.61f,.55f,.54f),.025f,Leather);
            for(int side=-1;side<=1;side+=2) {
                s.Box(a+new Vector3(side*.24f,.30f,.279f),new Vector3(.041f,.51f,.027f),.005f,Coal);
                s.Box(a+new Vector3(side*.27f,.27f,0f),new Vector3(.042f,.055f,.59f),.005f,Steel);
            }
            s.Box(a+Vector3.up*.576f,new Vector3(.57f,.067f,.41f),.011f,Steel);
            s.Box(a+Vector3.up*.690f,new Vector3(.29f,.192f,.29f),.017f,Steel);
            s.Box(a+Vector3.up*.817f,new Vector3(.65f,.104f,.39f),.011f,Steel);
            for(int i=0;i<4;i++)s.Box(a+new Vector3(.338f+i*.057f,.821f+i*.002f,0f),
                new Vector3(.075f,.094f-i*.019f,.30f-i*.067f),.007f,Steel);
            s.Box(a+new Vector3(-.348f,.819f,0f),new Vector3(.096f,.109f,.37f),.008f,Steel);
            s.Box(a+new Vector3(-.226f,.874f,-.101f),new Vector3(.048f,.011f,.050f),.003f,Coal);
            s.Box(a+new Vector3(.037f,.883f,.019f),new Vector3(.199f,.025f,.077f),.006f,Ember);
            Vector3 forge=new Vector3(1.06f,0f,1.15f);
            s.Box(forge+Vector3.up*.24f,new Vector3(.86f,.48f,.72f),.027f,Coal);
            for(int side=-1;side<=1;side+=2) {
                s.Box(forge+new Vector3(side*.384f,.540f,0f),new Vector3(.095f,.123f,.75f),.013f,Steel);
                s.Box(forge+new Vector3(0f,.540f,side*.319f),new Vector3(.75f,.123f,.095f),.013f,Steel);
            }
            for(int i=0;i<20;i++) {
                int col=i%5,row=i/5;
                float h=.047f+((i*7)%5)*.009f;
                s.Box(forge+new Vector3((col-2)*.131f,.515f+h*.5f,(row-1.5f)*.135f),
                    new Vector3(.116f,h,.119f),.009f,i%3==0?Coal:Ember);
            }
            for(int i=0;i<5;i++)s.Box(forge+new Vector3((i-2)*.087f,.650f+(i%3)*.037f,0f),
                new Vector3(.060f,.130f+(i%2)*.065f,.065f),.007f,Ember,Quaternion.Euler(0f,i*31f,(i-2)*8f));
        }

        Transform Child(string label,Transform parent,Vector3 position)
        {
            var t=new GameObject(label).transform;t.SetParent(parent,false);t.localPosition=position;return t;
        }
        BoxCollider AddCollider(string label,Vector3 center,Vector3 size)
        {
            var go=new GameObject(label);go.transform.SetParent(visual.transform,false);go.transform.localPosition=center;
            var collider=go.AddComponent<BoxCollider>();collider.size=size;return collider;
        }
        void Emit(string label,Model model,Material[] paints,Transform parent)
        {
            var mesh=model.Finish(label,paints,out Material[] used);resources.Add(mesh);
            var go=new GameObject(label);go.transform.SetParent(parent,false);
            go.AddComponent<MeshFilter>().sharedMesh=mesh;
            var renderer=go.AddComponent<MeshRenderer>();renderer.sharedMaterials=used;
            renderer.shadowCastingMode=ShadowCastingMode.On;renderer.receiveShadows=true;
            renderer.lightProbeUsage=LightProbeUsage.BlendProbes;renderer.reflectionProbeUsage=ReflectionProbeUsage.Off;
        }
        Material Paint(string label,Color color,float metallic,float smoothness,Texture texture)
        {
            Shader shader=Shader.Find("Universal Render Pipeline/Lit")??Shader.Find("Standard");
            var paint=new Material(shader){name="Original blacksmith / "+label};
            if(paint.HasProperty("_BaseColor"))paint.SetColor("_BaseColor",color);
            if(paint.HasProperty("_Color"))paint.SetColor("_Color",color);
            if(paint.HasProperty("_BaseMap"))paint.SetTexture("_BaseMap",texture);
            if(paint.HasProperty("_MainTex"))paint.SetTexture("_MainTex",texture);
            if(paint.HasProperty("_Metallic"))paint.SetFloat("_Metallic",metallic);
            if(paint.HasProperty("_Smoothness"))paint.SetFloat("_Smoothness",smoothness);
            if(paint.HasProperty("_Glossiness"))paint.SetFloat("_Glossiness",smoothness);
            resources.Add(paint);return paint;
        }
        Texture2D Pattern(string label,int size,int kind)
        {
            var texture=new Texture2D(size,size,TextureFormat.RGBA32,false,true) {
                name=label,filterMode=FilterMode.Point,wrapMode=TextureWrapMode.Repeat,anisoLevel=0
            };
            Color[] pixels=new Color[size*size];
            for(int y=0;y<size;y++)for(int x=0;x<size;x++) {
                uint hash=unchecked((uint)(x*73856093^y*19349663^kind*83492791));
                hash=unchecked((hash^(hash>>13))*1274126177u);
                float n=(hash&255)/255f;
                float v=kind==2?.90f+n*.10f:.76f+n*.24f;
                if(kind==1 && (x+y)%4==0)v-=.08f;
                if(kind==0 && n<.08f)v-=.16f;
                pixels[y*size+x]=new Color(v,v,v,1f);
            }
            texture.SetPixels(pixels);texture.Apply(false,true);resources.Add(texture);return texture;
        }
        void SetGlow(float scale)
        {
            if(emberPaint==null || !emberPaint.HasProperty("_EmissionColor"))return;
            emberPaint.SetColor("_EmissionColor",new Color(3.7f,.57f,.025f)*scale);
        }
        void OnDestroy(){Retire();}
        void Retire()
        {
            DisposeSmithFurnace015();
            DisposeFluidSmith009();
            if(visual!=null) {visual.SetActive(false);Release(visual);}
            for(int i=resources.Count-1;i>=0;i--)if(resources[i]!=null)Release(resources[i]);
            resources.Clear();visual=null;InteractionAnchor=null;CampfireAnchor=null;towerAnvilCollider=null;
            SmallFurnaceReady=SmallFurnaceFlameReady=false;SmallFurnaceQuadCount=0;
            breathing=null;head=null;hammerArm=null;emberPaint=null;
        }
        static void Release(Object value)
        {
            if(Application.isPlaying)Destroy(value);else DestroyImmediate(value);
        }

        // Original bevel mesh builder. One renderer per animated part, compacted
        // to the material slots that part actually uses; never more than ten.
        sealed class Model
        {
            readonly List<Vector3> positions=new List<Vector3>(4096);
            readonly List<Vector3> normals=new List<Vector3>(4096);
            readonly List<Vector2> uv=new List<Vector2>(4096);
            readonly List<int>[] triangles=new List<int>[PaletteCount];
            static readonly Vector3[] Axes={Vector3.right,Vector3.up,Vector3.forward};
            public Model(){for(int i=0;i<PaletteCount;i++)triangles[i]=new List<int>(256);}
            void Face(Vector3[] points,Vector3 normal,int material,Vector3 center,Quaternion rotation)
            {
                int at=positions.Count;
                bool flip=Vector3.Dot(Vector3.Cross(points[1]-points[0],points[2]-points[0]),normal)<0f;
                for(int i=0;i<points.Length;i++) {
                    Vector3 p=points[flip?points.Length-1-i:i];
                    positions.Add(center+rotation*p);normals.Add(rotation*normal);
                    uv.Add(i==0?Vector2.zero:i==1?Vector2.right:i==2?Vector2.one:Vector2.up);
                }
                for(int i=1;i<points.Length-1;i++) {
                    triangles[material].Add(at);triangles[material].Add(at+i);triangles[material].Add(at+i+1);
                }
            }
            public void Box(Vector3 c,Vector3 size,float bevel,int material,Quaternion? rotation=null)
            {
                Quaternion q=rotation??Quaternion.identity;
                Vector3 h=size*.5f;
                float b=Mathf.Clamp(bevel,.0001f,Mathf.Min(h.x,Mathf.Min(h.y,h.z))*.8f);
                Vector3 inset=h-Vector3.one*b;
                for(int a=0;a<3;a++)for(int sign=-1;sign<=1;sign+=2) {
                    int u=(a+1)%3,v=(a+2)%3;
                    Vector3 middle=Axes[a]*(h[a]*sign),du=Axes[u]*inset[u],dv=Axes[v]*inset[v];
                    Face(new[]{middle-du-dv,middle+du-dv,middle+du+dv,middle-du+dv},Axes[a]*sign,material,c,q);
                }
                for(int a=0;a<3;a++)for(int d=a+1;d<3;d++) {
                    int v=3-a-d;
                    for(int sa=-1;sa<=1;sa+=2)for(int sd=-1;sd<=1;sd+=2) {
                        Vector3 p=Axes[a]*(h[a]*sa)+Axes[d]*(inset[d]*sd);
                        Vector3 r=Axes[a]*(inset[a]*sa)+Axes[d]*(h[d]*sd),e=Axes[v]*inset[v];
                        Face(new[]{p-e,r-e,r+e,p+e},(Axes[a]*sa+Axes[d]*sd).normalized,material,c,q);
                    }
                }
                for(int x=-1;x<=1;x+=2)for(int y=-1;y<=1;y+=2)for(int z=-1;z<=1;z+=2)
                    Face(new[]{new Vector3(h.x*x,inset.y*y,inset.z*z),new Vector3(inset.x*x,h.y*y,inset.z*z),
                        new Vector3(inset.x*x,inset.y*y,h.z*z)},new Vector3(x,y,z).normalized,material,c,q);
            }
            public void Rod(Vector3 a,Vector3 b,float width,int material)
            {
                Vector3 d=b-a;if(d.sqrMagnitude<.0000001f)return;
                Box((a+b)*.5f,new Vector3(width,d.magnitude,width),width*.15f,material,Quaternion.FromToRotation(Vector3.up,d));
            }
            public void Frame(Vector3 c,Vector2 size,float thickness,int material)
            {
                Box(c+Vector3.up*size.y*.5f,new Vector3(size.x,thickness,thickness),thickness*.2f,material);
                Box(c-Vector3.up*size.y*.5f,new Vector3(size.x,thickness,thickness),thickness*.2f,material);
                Box(c+Vector3.right*size.x*.5f,new Vector3(thickness,size.y,thickness),thickness*.2f,material);
                Box(c-Vector3.right*size.x*.5f,new Vector3(thickness,size.y,thickness),thickness*.2f,material);
            }
            public Mesh Finish(string label,Material[] palette,out Material[] used)
            {
                var mesh=new Mesh{name=label};
                if(positions.Count>65535)mesh.indexFormat=IndexFormat.UInt32;
                mesh.SetVertices(positions);mesh.SetNormals(normals);mesh.SetUVs(0,uv);
                var selected=new List<Material>(PaletteCount);
                for(int i=0;i<PaletteCount;i++)if(triangles[i].Count>0)selected.Add(palette[i]);
                mesh.subMeshCount=selected.Count;int submesh=0;
                for(int i=0;i<PaletteCount;i++)if(triangles[i].Count>0)mesh.SetTriangles(triangles[i],submesh++,false);
                mesh.RecalculateBounds();mesh.UploadMeshData(true);used=selected.ToArray();return mesh;
            }
        }
    }
}
// # ARCHITECTURE SNAPSHOT
// World metres/Y-up/XZ; existing station mesh/pivots and canonical host roles remain.
// Bounded cached collider/root references only; no new economy/native buffers or quest bits.
// Root owns Unity integration and native acceptance; this is a guarded work candidate.

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Original supported Smith pose/anvil retained; full-height furnace uses cold certified east-alcove position.
// Array Formats: One cold greedy mesh, one static solid and inherited bounded flame; no active-frame allocation additions.
// Dependencies/Pointers: Existing resources/Retire owns furnace geometry and its new ambient record; NPC motion/voice/input/economy remain canonical.
