using System;
using static OdysseyOracle.CampaignNpcs.NpcModelBuilder;

namespace OdysseyOracle.CampaignNpcs
{
    public static class CampaignNpcRecipes
    {
        static int Part(NpcRecipe r,string name,int parent,float x,float y,float z){r.Parts.Add(new NpcPart{Name=name,Parent=parent,Pivot=new NpcVector(x,y,z)});return r.Parts.Count-1;}
        static NpcModelBuilder Model(NpcRecipe r,int index)=>new NpcModelBuilder(r.Parts[index]);
        public static NpcRecipe Build(RefugeNpcRole role)
        {
            if(role!=RefugeNpcRole.Runekeeper&&role!=RefugeNpcRole.Gatekeeper)throw new ArgumentOutOfRangeException(nameof(role));
            bool rune=role==RefugeNpcRole.Runekeeper;byte skin=rune?PaleSkin:Skin,cloth=rune?Ivory:Charcoal,trim=rune?Gold:Copper;
            var r=new NpcRecipe{Role=role,Name=rune?"Ilyra":"Bram",RoleLabel=rune?"Runekeeper":"Gatekeeper",
                Identity=rune?"Ivory stepped robe, blue hood, emerald scroll seal and split-crown staff.":"Broad charcoal coat, copper shoulder plates, teal lantern and forked watch spear."};
            int lower=Part(r,"Grounded boots and layered skirt",-1,0,0,0);var m=Model(r,lower);
            for(int side=-1;side<=1;side+=2){float x=side*.14f;
                m.Bevel(x,.10f,.06f,.22f,.20f,.34f,.025f,Charcoal);m.Box(x,.025f,.08f,.23f,.05f,.35f,trim);
                m.Box(x,.33f,-.01f,.17f,.30f,.19f,Charcoal);m.Box(x,.54f,0,.20f,.19f,.22f,cloth);
                m.Box(x,.16f,.22f,.16f,.035f,.024f,trim);
            }
            if(rune)for(int i=-2;i<=2;i++){
                m.Box(i*.083f,.68f,.105f,.082f,.55f-Math.Abs(i)*.025f,.13f,Ivory);m.Box(i*.083f,.415f,.18f,.078f,.025f,.018f,Blue);
                m.Box(i*.083f,.69f,-.12f,.08f,.52f,.09f,Blue);
            }else{
                m.Bevel(0,.68f,0,.49f,.36f,.28f,.025f,Charcoal);m.Box(-.17f,.65f,.17f,.18f,.40f,.045f,Charcoal);m.Box(.17f,.65f,.17f,.18f,.40f,.045f,Charcoal);
                for(int side=-1;side<=1;side+=2){m.Box(side*.195f,.61f,.20f,.022f,.33f,.018f,Copper);m.Box(side*.29f,.69f,0,.10f,.18f,.11f,Hair);}
            }
            r.Torso=Part(r,"Breathing fitted torso",-1,0,.98f,0);m=Model(r,r.Torso);
            m.Bevel(0,.16f,0,rune?.44f:.55f,.44f,.28f,.035f,cloth);m.Box(0,-.03f,.16f,rune?.46f:.57f,.07f,.045f,trim);
            m.Box(0,.17f,.16f,.105f,.39f,.04f,rune?Blue:Teal);m.Box(0,.34f,.18f,.06f,.04f,.025f,rune?Emerald:Copper);
            m.Box(0,.38f,0,.32f,.095f,.29f,rune?Blue:Teal);m.Box(.10f,.26f,.19f,.085f,.19f,.025f,rune?Blue:Teal);
            for(int side=-1;side<=1;side+=2){m.Bevel(side*(rune?.25f:.30f),.31f,0,rune?.13f:.20f,.13f,.30f,.016f,trim);
                for(int row=0;row<3;row++)m.Box(side*.17f,.25f-row*.09f,.18f,.016f,.016f,.014f,trim);}
            // One diagonal sash is a closed solid; it never detaches from breathing torso.
            m.Rod(new NpcVector(-.20f,.35f,.18f),new NpcVector(.20f,.02f,.18f),.035f,trim);
            r.Head=Part(r,"Gaze head",r.Torso,0,.56f,0);m=Model(r,r.Head);
            m.Bevel(0,.11f,.015f,.30f,.31f,.27f,.028f,skin);m.Box(0,-.055f,0,.14f,.10f,.15f,skin);
            m.Box(0,.27f,-.015f,rune?.36f:.33f,.09f,.29f,rune?Blue:Hair);m.Box(0,.115f,-.135f,.33f,.28f,.045f,rune?Blue:Hair);
            for(int side=-1;side<=1;side+=2){
                m.Box(side*.167f,.10f,0,.04f,.08f,.06f,skin);m.Box(side*.067f,.147f,.155f,.068f,.034f,.012f,EyeWhite);
                m.Box(side*.068f,.148f,.164f,.023f,.027f,.01f,rune?Emerald:Teal);m.Box(side*.067f,.15f,.171f,.011f,.019f,.007f,Ink);
                m.Box(side*.071f,.193f,.157f,.084f,.023f,.018f,Hair);m.Box(side*.115f,.064f,.15f,.04f,.035f,.02f,skin);
                if(rune){m.Box(side*.16f,.06f,-.09f,.07f,.28f,.07f,Hair);for(int row=0;row<4;row++)m.Box(side*.17f,-.07f-row*.05f,-.04f,.065f,.045f,.075f,row%2==0?Ivory:Gold);}
                else {m.Box(side*.14f,.22f,-.04f,.065f,.10f,.12f,Hair);m.Box(side*.132f,-.012f,.07f,.055f,.065f,.13f,Hair);}
            }
            m.Box(0,.092f,.18f,.045f,.07f,.045f,skin);m.Box(0,.03f,.161f,.104f,.012f,.013f,skin);
            if(rune){m.Box(0,.28f,.14f,.13f,.04f,.035f,Gold);m.Crystal(0,.28f,.17f,.027f,.065f,Emerald);}
            else {m.Box(-.095f,.10f,.17f,.012f,.012f,.006f,Copper);m.Box(0,-.03f,.11f,.15f,.043f,.09f,Hair);}
            r.Mouth=Part(r,"Voice RMS jaw opening",r.Head,0,.012f,.165f);m=Model(r,r.Mouth);m.Box(0,0,0,.072f,.013f,.012f,Ink);
            r.Eyelids=Part(r,"Blink skin lids",r.Head,0,.15f,.18f);m=Model(r,r.Eyelids);for(int side=-1;side<=1;side+=2)m.Box(side*.067f,0,0,.073f,.041f,.008f,skin);
            r.LeftArm=Arm(r,true,skin,cloth,trim,rune);r.RightArm=Arm(r,false,skin,cloth,trim,rune);
            if(rune)RunekeeperTools(r);else GatekeeperTools(r);
            Validate(r);return r;
        }
        static int Arm(NpcRecipe r,bool left,byte skin,byte cloth,byte trim,bool rune)
        {
            int side=left?-1:1,index=Part(r,left?"Left shoulder and grip":"Right shoulder and grip",r.Torso,side*(rune?.27f:.33f),.29f,0);var m=Model(r,index);
            m.Bevel(side*.025f,-.13f,0,.16f,.29f,.20f,.018f,cloth);m.Box(side*.03f,-.27f,.015f,.17f,.065f,.21f,trim);
            m.Rod(new NpcVector(side*.03f,-.29f,.02f),new NpcVector(side*.12f,-.43f,.10f),.10f,skin);
            m.Box(side*.12f,-.43f,.11f,.115f,.12f,.09f,skin);for(int finger=0;finger<3;finger++)m.Box(side*.12f,-.40f-finger*.032f,.165f,.09f,.023f,.026f,skin);
            return index;
        }
        static void RunekeeperTools(NpcRecipe r)
        {
            int staff=Part(r,"Split crown emerald staff",r.RightArm,.12f,-.43f,.10f);var m=Model(r,staff);
            m.Rod(new NpcVector(0,-.80f,0),new NpcVector(0,1.13f,0),.04f,Hair);
            for(int i=0;i<4;i++)m.Box(0,-.13f+i*.30f,0,.065f,.045f,.065f,Gold);
            m.Rod(new NpcVector(0,1,0),new NpcVector(-.13f,1.22f,0),.035f,Gold);m.Rod(new NpcVector(0,1,0),new NpcVector(.13f,1.22f,0),.035f,Gold);
            m.Crystal(0,1.22f,0,.087f,.25f,JadeLight);m.Box(0,1.06f,0,.12f,.045f,.12f,Gold);
            int scroll=Part(r,"Bound ivory scroll and seal",r.LeftArm,-.12f,-.43f,.16f);m=Model(r,scroll);
            m.Bevel(0,.02f,0,.24f,.23f,.075f,.01f,Ivory);m.Box(0,.02f,.045f,.045f,.23f,.02f,Blue);
            m.Box(-.13f,.02f,0,.055f,.26f,.09f,Gold);m.Box(.13f,.02f,0,.055f,.26f,.09f,Gold);m.Crystal(0,.02f,.065f,.033f,.075f,Emerald);
            for(int row=0;row<4;row++)m.Box(.05f,.095f-row*.045f,.045f,.065f,.009f,.008f,Blue);
        }
        static void GatekeeperTools(NpcRecipe r)
        {
            int spear=Part(r,"Forked copper watch spear",r.LeftArm,-.12f,-.43f,.10f);var m=Model(r,spear);
            m.Rod(new NpcVector(0,-.80f,0),new NpcVector(0,1.28f,0),.045f,Hair);m.Box(0,1.05f,0,.12f,.07f,.10f,Copper);
            m.Crystal(0,1.30f,0,.075f,.36f,Copper);m.Rod(new NpcVector(-.12f,1.12f,0),new NpcVector(0,1.30f,0),.025f,Teal);
            m.Rod(new NpcVector(.12f,1.12f,0),new NpcVector(0,1.30f,0),.025f,Teal);for(int i=0;i<5;i++)m.Box(0,-.12f+i*.055f,0,.062f,.018f,.062f,Copper);
            int lantern=Part(r,"Caged teal watch lantern",r.RightArm,.12f,-.43f,.10f);m=Model(r,lantern);
            m.Rod(new NpcVector(0,-.03f,0),new NpcVector(0,-.16f,0),.024f,Copper);m.Bevel(0,-.20f,0,.24f,.07f,.24f,.014f,Copper);
            m.Bevel(0,-.48f,0,.22f,.06f,.22f,.012f,Copper);m.Crystal(0,-.35f,0,.067f,.21f,LanternLight);
            for(int x=-1;x<=1;x+=2)for(int z=-1;z<=1;z+=2)m.Box(x*.09f,-.35f,z*.09f,.021f,.26f,.021f,Charcoal);
            m.Box(0,-.35f,.11f,.15f,.025f,.02f,Teal);m.Box(0,-.52f,0,.09f,.05f,.09f,Copper);
        }
    }
}
