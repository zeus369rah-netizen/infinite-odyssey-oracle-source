using UnityEngine;
using UnityEngine.UI;

namespace OdysseyOracle
{
    public sealed partial class OdysseyHud
    {
        static readonly string[] LabDigits={"0","1","2","3","4","5","6","7","8","9"};
        static readonly string[] LabPrototypeLabels={"GUARDIAN GOLEM","STALKER","SHADE","SPIDER"};
        sealed class LabNumber
        {
            internal readonly Text[] Digits=new Text[7];
            internal int LastValue=-2;
            internal void Set(float number)
            {
                bool overflow=number>99999.99f;
                int value=overflow?10000000:Mathf.RoundToInt(Mathf.Clamp(number,0f,99999.99f)*100f);
                if(value==LastValue)return;LastValue=value;
                int remaining=Mathf.Min(value,9999999);
                for(int index=6;index>=0;index--){int digit=remaining%10;remaining/=10;Digits[index].text=overflow&&index==0?">":index<4&&remaining==0&&digit==0?" ":LabDigits[digit];}
            }
        }
        bool trainingLabBuilt;float nextTrainingLabUi;
        readonly Button[] labPrototypeButtons=new Button[4];readonly Text[] labPrototypeTexts=new Text[4];
        readonly LabNumber[] labMenuNumbers=new LabNumber[8],labLiveNumbers=new LabNumber[4];
        Button labSpawn,labDespawn,labTowerToggle,labMonsterToggle;Text labStatus,labLock,labTowerLabel,labMonsterLabel;
        RectTransform labLiveRoot;
        public bool IsTrainingLabVisible=>trainingLabBuilt&&currentPanel==JourneyPanel.TrainingLab;
        public void InitializeTrainingLab()
        {
            if(trainingLabBuilt||!built||session==null)return;
            var page=PanelContent(JourneyPanel.TrainingLab,"OfflineTrainingLab",new Vector2(1060,1000));
            MenuLabel(page,"LabTitle","OFFLINE TRAINING LAB",30,Ivory,42);
            MenuLabel(page,"LabScope","One stationary dummy and one free tier-one Water tower. Choose a prototype, then SPAWN DUMMY.\nNo chase, XP, Forge charge, currency, drops or achievements. Co-op is unavailable.",16,Muted,60);
            for(int prototype=0;prototype<4;prototype++)
            {int choice=prototype;labPrototypeButtons[prototype]=MenuButton(page,"LabPrototype"+prototype,LabPrototypeLabels[prototype],()=>{session.TrainingLab.SelectPrototype(choice);RefreshTrainingLab(true);},34);labPrototypeTexts[prototype]=labPrototypeButtons[prototype].GetComponentInChildren<Text>();}
            labLock=MenuLabel(page,"LabPrototypeLock","PROTOTYPE CHOICE UNLOCKED",16,Gold,26);
            labSpawn=MenuButton(page,"LabExplicitSpawn","SPAWN DUMMY",()=>{session.TrainingLab.SpawnDummy();RefreshTrainingLab(true);},42);
            labDespawn=MenuButton(page,"LabExplicitDespawn","DESPAWN DUMMY & TOWER",()=>{session.TrainingLab.DespawnDummy();RefreshTrainingLab(true);},42);
            labTowerToggle=MenuButton(page,"LabTowerAttackToggle","TOWER ATTACK: OFF",()=>{session.TrainingLab.SetTowerAttacking(!session.TrainingLab.TowerAttacking);RefreshTrainingLab(true);},38);
            labTowerLabel=labTowerToggle.GetComponentInChildren<Text>();
            labMonsterToggle=MenuButton(page,"LabMonsterAttackToggle","MONSTER ATTACK TOWER: OFF",()=>{session.TrainingLab.SetMonsterAttackingTower(!session.TrainingLab.MonsterAttackingTower);RefreshTrainingLab(true);},38);
            labMonsterLabel=labMonsterToggle.GetComponentInChildren<Text>();
            string[] labels={"DUMMY HP (CURRENT)","DUMMY HP (MAX)","TOWER HP (CURRENT)","TOWER HP (MAX)","LAST DAMAGE TO DUMMY","TOTAL DAMAGE TO DUMMY","LAST DAMAGE TO TOWER","TOTAL DAMAGE TO TOWER"};
            for(int i=0;i<labels.Length;i++)labMenuNumbers[i]=BuildLabNumber(page,labels[i],740,490,24);
            labStatus=MenuLabel(page,"LabActualStatus","No actors until you press SPAWN DUMMY.",15,Muted,42);
            MenuButton(page,"LabReturnToTest","RETURN TO TEST & LOCK MOUSE",()=>session.TrainingLab.Close(true),46);
            labLiveRoot=Frame(hudRoot,"ActualTrainingLabMeters",new Vector2(0,1),new Vector2(0,1),new Vector2(20,-150),new Vector2(420,190),Panel,MutedGold);
            Label(labLiveRoot,"LiveLabScope","TRAINING LAB | ACTUAL HP / DAMAGE",15,Gold,TextAnchor.UpperLeft,new Vector2(12,-10),new Vector2(400,24),new Vector2(0,1));
            string[] liveLabels={"DUMMY HP","TOWER HP","LAST DUMMY HIT","LAST TOWER HIT"};
            for(int i=0;i<4;i++)
            {
                var row=Rect(labLiveRoot,"LabLiveRow"+i,new Vector2(0,1),new Vector2(0,1),new Vector2(12,-40-i*31),new Vector2(396,28));
                labLiveNumbers[i]=BuildLabDigits(row,liveLabels[i],396,264);
            }
            trainingLabBuilt=true;panels[JourneyPanel.TrainingLab].gameObject.SetActive(false);labLiveRoot.gameObject.SetActive(false);
            lastCanvasSize=Vector2.zero;
        }
        LabNumber BuildLabNumber(RectTransform parent,string title,float width,float numberX,float height)
        {
            var row=Rect(parent,title,Vector2.zero,new Vector2(.5f,.5f),Vector2.zero,new Vector2(width,height));Height(row,height);
            return BuildLabDigits(row,title,width,numberX);
        }
        LabNumber BuildLabDigits(RectTransform row,string title,float width,float numberX)
        {
            Label(row,"Name",title,15,Muted,TextAnchor.UpperLeft,Vector2.zero,new Vector2(numberX-12,24),new Vector2(0,1));
            var number=new LabNumber();
            for(int digit=0;digit<7;digit++)
            {
                float x=numberX+digit*15+(digit>=5?8:0);
                number.Digits[digit]=Label(row,"Digit"+digit,"0",16,Ivory,TextAnchor.UpperCenter,new Vector2(x,0),new Vector2(15,24),new Vector2(0,1));
            }
            Label(row,"Decimal",".",16,Ivory,TextAnchor.UpperCenter,new Vector2(numberX+75,0),new Vector2(8,24),new Vector2(0,1));
            return number;
        }
        public bool ShowTrainingLab()
        {
            if(!trainingLabBuilt||session.TrainingLab==null||!session.TrainingLab.CanOpen||IntroActive||LoadingActive||DownedActive||ShopVisible)return false;
            AbilityBindings.CancelRebind();session.Pause();SetPanel(JourneyPanel.TrainingLab);RefreshTrainingLab(true);return IsTrainingLabVisible;
        }
        public void HideTrainingLab(){if(IsTrainingLabVisible)SetPanel(JourneyPanel.None);}
        void RefreshTrainingLab(bool force=false)
        {
            if(!trainingLabBuilt||session.TrainingLab==null)return;
            var lab=session.TrainingLab;bool showLive=lab.HasActors&&session.IsPlaying;
            if(labLiveRoot.gameObject.activeSelf!=showLive)labLiveRoot.gameObject.SetActive(showLive);
            if(IsTrainingLabVisible&&!lab.CanOpen){HideTrainingLab();return;}
            if(!force&&Time.unscaledTime<nextTrainingLabUi)return;nextTrainingLabUi=Time.unscaledTime+.1f;
            var snapshot=lab.ReadSnapshot();
            if(IsTrainingLabVisible)
            {
                for(int i=0;i<4;i++)
                {labPrototypeButtons[i].interactable=!snapshot.HasActors;labPrototypeTexts[i].color=snapshot.Prototype==i?Gold:Ivory;}
                labLock.text=snapshot.HasActors?"PROTOTYPE LOCKED | DESPAWN TO CHANGE":"PROTOTYPE CHOICE UNLOCKED";
                labLock.color=snapshot.HasActors?Gold:Emerald;
                labSpawn.interactable=!snapshot.HasActors;labDespawn.interactable=snapshot.HasActors;
                labTowerToggle.interactable=labMonsterToggle.interactable=snapshot.HasActors;
                labTowerLabel.text=snapshot.TowerAttacking?"TOWER ATTACK: ON":"TOWER ATTACK: OFF";
                labMonsterLabel.text=snapshot.MonsterAttackingTower?"MONSTER ATTACK TOWER: ON":"MONSTER ATTACK TOWER: OFF";
                labTowerLabel.color=snapshot.TowerAttacking?Emerald:Muted;labMonsterLabel.color=snapshot.MonsterAttackingTower?Emerald:Muted;
                labMenuNumbers[0].Set(snapshot.DummyHealth);labMenuNumbers[1].Set(snapshot.DummyMaximum);labMenuNumbers[2].Set(snapshot.TowerHealth);labMenuNumbers[3].Set(snapshot.TowerMaximum);
                labMenuNumbers[4].Set(snapshot.LastDummyDamage);labMenuNumbers[5].Set(snapshot.TotalDummyDamage);labMenuNumbers[6].Set(snapshot.LastTowerDamage);labMenuNumbers[7].Set(snapshot.TotalTowerDamage);
                labStatus.text=lab.Status;
            }
            if(showLive)
            {labLiveNumbers[0].Set(snapshot.DummyHealth);labLiveNumbers[1].Set(snapshot.TowerHealth);labLiveNumbers[2].Set(snapshot.LastDummyDamage);labLiveNumbers[3].Set(snapshot.LastTowerDamage);}
        }
    }
}

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Existing responsive HUD Canvas/menu, real current lab actor values; logical colors differ from element colors.
// Array Formats: Four cached prototype buttons; eight menu/four live numeric glyph rows, each fixed7 digits (two decimals), refreshed at10Hz.
// Dependencies/Pointers: Controller methods own actions; digit updates reuse ten static strings (no numeric formatting/LINQ/boxing). Cold UI construction only; actual UGUI/native performance still requires root profiling.
