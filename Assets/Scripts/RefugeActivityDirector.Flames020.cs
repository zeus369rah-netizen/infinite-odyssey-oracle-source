using System;
using UnityEngine;

namespace OdysseyOracle
{
    public sealed partial class RefugeActivityDirector
    {
        bool refugeFlamesEarned020,refugeFlamesBlocked020,refugeFlamesDirty020;
        public string RefugeFlamesSaveStatus020{get;private set;}="";
        public bool AllRefugeFlamesOutEarned020=>refugeFlamesEarned020;
        public bool AllRefugeFlamesOutNameVisible020=>refugeFlamesEarned020;
        public uint AllRefugeFlamesOutRevision020{get;private set;}
        void InitializeRefugeFlames020()
        {
            try
            {
                int stored=OracleProgressPrefs015.GetInt(RefugeFlameRules020.PreferenceKey,0);
                if(!RefugeFlameRules020.ValidStored(stored)){refugeFlamesBlocked020=true;RefugeFlamesSaveStatus020="Existing Lights out data was kept; its saving is unavailable.";return;}
                refugeFlamesEarned020=stored==1;
                AllRefugeFlamesOutRevision020=refugeFlamesEarned020?1u:0u;
            }
            catch(Exception){refugeFlamesBlocked020=true;RefugeFlamesSaveStatus020="Lights out data could not be read safely; its saving is unavailable.";}
        }
        internal bool RecordAllRefugeFlamesOut020(DungeonWorld source)
        {
            if(refugeFlamesEarned020||refugeFlamesBlocked020||session==null||source==null||session.World!=source||!source.IsTutorialRefuge||source.IsCoopArena)return false;
            refugeFlamesEarned020=true;AllRefugeFlamesOutRevision020++;refugeFlamesDirty020=true;FlushRefugeFlames020();
            if(notice!=null){notice.text=RefugeFlameRules020.AchievementNotice;noticeUntil=Time.unscaledTime+4f;notice.gameObject.SetActive(true);}
            if(achievementSound!=null&&chime!=null)chime.PlayOneShot(achievementSound);
            return true;
        }
        public bool FlushRefugeFlames020()
        {
            if(refugeFlamesBlocked020)return false;if(!refugeFlamesDirty020)return true;
            try{OracleProgressPrefs015.SetInt(RefugeFlameRules020.PreferenceKey,1);OracleProgressPrefs015.Save();refugeFlamesDirty020=false;RefugeFlamesSaveStatus020="";return true;}
            catch(Exception){RefugeFlamesSaveStatus020="Lights out is earned this session; a later explicit save will retry.";return false;}
        }
    }
}
// # ARCHITECTURE SNAPSHOT
// Coordinate Space: local profile and existing achievement toast/book; accepted World event supplies the final local Air-caster fact.
// Array Formats: separate validated int Oracle.RefugeFlames.v1, three scalar flags and cached literal notice; original20bits/16B discovery payload unchanged.
// Dependencies/Pointers: malformed saves remain untouched; accepted in-memory award suppresses duplicates even when storage fails. Existing explicit flush/disable retries, original chime/notice lifetime and scoped preferences retained; native persistence/UI acceptance pending.
