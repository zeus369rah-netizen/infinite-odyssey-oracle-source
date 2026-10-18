using System;
using UnityEngine;

namespace OdysseyOracle
{
    // # ARCHITECTURE SNAPSHOT
    // Optional adapter: compile only alongside008's frozen interface. It owns
    // no quest state, subscriptions, wallet, actors or transport of its own.
    public sealed class CampaignNpcQuestBridge010:IRefugeNpcQuestBridge
    {
        readonly CoopCampaignDirector campaign;
        public CampaignNpcQuestBridge010(CoopCampaignDirector campaign)
        {this.campaign=campaign!=null?campaign:throw new ArgumentNullException(nameof(campaign));}
        public event Action Changed
        {add{campaign.StateChanged+=value;}remove{if(campaign!=null)campaign.StateChanged-=value;}}
        public bool TryReadAccepted(out RefugeNpcQuestView view)
        {
            view=default;if(campaign==null||!campaign.Active)return false;
            view=new RefugeNpcQuestView(true,campaign.InRefuge,campaign.StageNumber,campaign.LocalVisitMask,
                campaign.LocalTowerMask,campaign.ReadyPlayerCount,campaign.MemberCount);return true;
        }
        public void RegisterNpcTarget(int role,Transform root,Vector3 feet,Vector3 aim)
        {if(campaign!=null)campaign.RegisterNpcTarget(role,root,feet,aim);}
        public void RequestNpcVisit(int role){if(campaign!=null)campaign.RequestNpcVisit(role);}
    }
}
