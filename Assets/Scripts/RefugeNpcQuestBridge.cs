using System;
using UnityEngine;

namespace OdysseyOracle
{
    // # ARCHITECTURE SNAPSHOT
    // Read-only caller bridge, not a second campaign.010 owns host validation,
    // unordered visits, ready/returns and all grants. The008 actor owner registers
    // actual roots and requests a visit only after a real interaction/modal opens.
    public readonly struct RefugeNpcQuestView
    {
        public readonly bool Active,InRefuge;
        public readonly int Stage,VisitMask,TowerMask,Ready,Members;
        public RefugeNpcQuestView(bool active,bool refuge,int stage,int visits,int towers,int ready,int members)
        {Active=active;InRefuge=refuge;Stage=stage;VisitMask=visits;TowerMask=towers;Ready=ready;Members=members;}
    }
    public interface IRefugeNpcQuestBridge
    {
        event Action Changed;
        bool TryReadAccepted(out RefugeNpcQuestView view);
        void RegisterNpcTarget(int role,Transform root,Vector3 feet,Vector3 aim);
        void RequestNpcVisit(int role);
    }
}
