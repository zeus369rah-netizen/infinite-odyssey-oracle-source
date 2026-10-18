using UnityEngine;

namespace OdysseyOracle
{
    public sealed partial class OdysseyHud
    {
        // # ARCHITECTURE SNAPSHOT
        // One per-frame local read; eight precreated slot rows refresh at10Hz.
        // Health comes only from the public current-run replica, never a default
        // session125 or the host-only ledger. No input, physics, authority writes,
        // remote revive timer/progress, per-frame hierarchy search or subscriptions.
        private sealed class PartyHealthRow
        {
            public RectTransform Root;
            public UnityEngine.UI.Text Name,Value;
            public UnityEngine.UI.Image Fill;
            public bool Seen,HasPeer,Known,Downed,Shielded,IsLocal;
            public ulong Peer;
            public int RoundedHealth=-1,RoundedMaximum=-1;
            public string SlotLabel,OwnLabel;
        }
        private readonly PartyHealthRow[] partyHealthRows=new PartyHealthRow[8];
        private readonly Vector3[] partyMapCorners=new Vector3[4];
        private RectTransform partyHealthRoot;
        private UnityEngine.UI.Text partyLayoutNotice;
        private float nextPartyHealthRefresh;
        private bool partyLayoutAvailable,partyNoticeAvailable;
        private int localHealthFrame=-1,shownLocalHealth=-1,shownLocalMaximum=-1;
        private bool localHealthKnown,shownLocalKnown,localHudHealthTextReady;
        private float localHealth,localHealthMaximum;

        private void ReadLocalHudHealth()
        {
            if(localHealthFrame==Time.frameCount)return;
            localHealthFrame=Time.frameCount;localHealthKnown=false;localHealth=localHealthMaximum=0;
            if(session==null)return;
            if(session.CoopMode&&session.World!=null&&session.World.IsCoopArena)
            {
                // This branch stays unknown after a disconnect, including the
                // frames before any first host payload. Never fall back to HP125.
                var rooms=session.Rooms;
                if(rooms!=null&&rooms.Connected&&session.TryGetPeerHealth(rooms.LocalClientId,out var hp)&&ValidPartyHealth(hp,rooms.LocalClientId))
                {localHealthKnown=true;localHealth=hp.Health;localHealthMaximum=hp.MaximumHealth;}
                return;
            }
            if(FiniteHudHealth(session.HP)&&FiniteHudHealth(session.MaxHP)&&session.MaxHP>0)
            {localHealthKnown=true;localHealth=Mathf.Clamp(session.HP,0,session.MaxHP);localHealthMaximum=session.MaxHP;}
        }
        private float LocalHudHealthFraction
        {
            get{ReadLocalHudHealth();return localHealthKnown?Mathf.Clamp01(localHealth/Mathf.Max(1,localHealthMaximum)):0;}
        }
        private void RefreshLocalHudHealth()
        {
            ReadLocalHudHealth();if(healthFill==null||healthText==null)return;
            healthFill.enabled=localHealthKnown;
            if(localHealthKnown)
            {
                SetFill(healthFill,LocalHudHealthFraction);
                healthFill.color=localHealth<=localHealthMaximum*.25f?new Color(.79f,.33f,.27f):Emerald;
            }
            int hp=localHealthKnown?Mathf.CeilToInt(localHealth):-1,max=localHealthKnown?Mathf.RoundToInt(localHealthMaximum):-1;
            if(!localHudHealthTextReady||shownLocalKnown!=localHealthKnown||shownLocalHealth!=hp||shownLocalMaximum!=max)
            {
                localHudHealthTextReady=true;shownLocalKnown=localHealthKnown;shownLocalHealth=hp;shownLocalMaximum=max;
                healthText.text=localHealthKnown?hp+" / "+max:"HP UNKNOWN";
            }
        }
        private static bool ValidPartyHealth(PeerHealthSnapshot hp,ulong peer)=>hp.Present&&hp.PeerId==peer&&
            !string.IsNullOrEmpty(hp.RunId)&&hp.Revision>=1&&FiniteHudHealth(hp.Health)&&FiniteHudHealth(hp.MaximumHealth)&&
            hp.MaximumHealth>0&&hp.Health>=0&&hp.Health<=hp.MaximumHealth&&hp.Downed==(hp.Health<=0)&&
            FiniteHudHealth(hp.ShieldRemaining)&&hp.ShieldRemaining>=0;
        private static bool FiniteHudHealth(float value)=>!float.IsNaN(value)&&!float.IsInfinity(value);

        private void BuildPartyHealth()
        {
            partyHealthRoot=Frame(hudRoot,"AcceptedPartyHealth",new Vector2(0,1),new Vector2(0,1),
                new Vector2(300,-148),new Vector2(270,30+8*34),Panel,MutedGold);
            Label(partyHealthRoot,"AcceptedPartyHealthHeading","PARTY VITALS",17,Gold,TextAnchor.MiddleLeft,
                new Vector2(10,-3),new Vector2(250,25),new Vector2(0,1));
            for(int slot=0;slot<partyHealthRows.Length;slot++)
            {
                var row=partyHealthRows[slot]=new PartyHealthRow{SlotLabel="ORACLE "+(slot+1),OwnLabel="YOU | "+(slot+1)};
                row.Root=Rect(partyHealthRoot,"AcceptedAdmissionSlot"+slot,new Vector2(0,1),new Vector2(0,1),
                    new Vector2(10,-29-slot*34),new Vector2(250,32));
                row.Name=Label(row.Root,"PeerSlotLabel",row.SlotLabel,16,Ivory,TextAnchor.UpperLeft,
                    Vector2.zero,new Vector2(116,24),new Vector2(0,1));
                row.Value=Label(row.Root,"ConfirmedHealthOrUnknown","HP UNKNOWN",15,Muted,TextAnchor.UpperRight,
                    Vector2.zero,new Vector2(134,24),new Vector2(1,1));
                var bar=Rect(row.Root,"ConfirmedPeerHealthBar",new Vector2(0,1),new Vector2(0,1),new Vector2(0,-25),new Vector2(250,4));
                Paint(bar,new Color(.16f,.12f,.12f,1));row.Fill=Paint(Stretch(bar,"AcceptedHealthFill"),Emerald);
                row.Fill.enabled=false;row.Root.gameObject.SetActive(false);
            }
            partyHealthRoot.gameObject.SetActive(false);
            partyLayoutNotice=Label(hudRoot,"PartyHealthNarrowViewportNotice","PARTY VITALS | WIDEN VIEW",15,Muted,TextAnchor.UpperLeft,
                new Vector2(36,-370),new Vector2(300,24),new Vector2(0,1));
            partyLayoutNotice.gameObject.SetActive(false);
        }
        private void UpdatePartyHealth()
        {
            if(partyHealthRoot==null||session==null)return;
            var rooms=session.Rooms;
            bool show=session.HasStarted&&session.CoopMode&&session.World!=null&&session.World.IsCoopArena&&rooms!=null&&rooms.Connected&&
                !session.Paused&&!session.IsDead&&!session.IsDowned&&!session.Victory&&!IntroActive&&!LoadingActive&&currentPanel==JourneyPanel.None;
            if(!show)
            {
                partyHealthRoot.gameObject.SetActive(false);partyLayoutNotice.gameObject.SetActive(false);
                ClearPartyHealthRows();nextPartyHealthRefresh=0;return;
            }
            if(Time.unscaledTime<nextPartyHealthRefresh)return;
            nextPartyHealthRefresh=Time.unscaledTime+.1f;
            for(int i=0;i<partyHealthRows.Length;i++)partyHealthRows[i].Seen=false;
            var roster=rooms.ConnectedClientIds;
            int count=Mathf.Min(roster.Count,partyHealthRows.Length);
            for(int i=0;i<count;i++)
            {
                ulong peer=roster[i];
                if(!rooms.TryGetAdmissionSlot(peer,out int slot)||slot<0||slot>=partyHealthRows.Length)continue;
                var row=partyHealthRows[slot];if(row.Seen)continue;row.Seen=true;
                bool local=peer==rooms.LocalClientId;
                if(!row.HasPeer||row.Peer!=peer||row.IsLocal!=local)
                {
                    row.HasPeer=true;row.Peer=peer;row.IsLocal=local;row.Known=false;row.RoundedHealth=row.RoundedMaximum=-1;
                    row.Name.text=local?row.OwnLabel:row.SlotLabel;row.Value.text="HP UNKNOWN";row.Value.color=Muted;row.Fill.enabled=false;
                }
                bool known=session.TryGetPeerHealth(peer,out var hp)&&ValidPartyHealth(hp,peer);
                UpdatePartyHealthRow(row,known,hp);row.Root.gameObject.SetActive(true);
            }
            for(int i=0;i<partyHealthRows.Length;i++)if(!partyHealthRows[i].Seen)ClearPartyHealthRow(partyHealthRows[i]);
            FitPartyHealth();
            partyHealthRoot.gameObject.SetActive(partyLayoutAvailable&&count>0);
            partyLayoutNotice.gameObject.SetActive(!partyLayoutAvailable&&partyNoticeAvailable&&count>0);
        }
        private void UpdatePartyHealthRow(PartyHealthRow row,bool known,PeerHealthSnapshot hp)
        {
            int value=known?Mathf.CeilToInt(hp.Health):-1,max=known?Mathf.RoundToInt(hp.MaximumHealth):-1;
            bool downed=known&&hp.Downed,shield=known&&hp.ShieldRemaining>0;
            if(row.Known!=known||row.Downed!=downed||row.Shielded!=shield||row.RoundedHealth!=value||row.RoundedMaximum!=max)
            {
                row.Known=known;row.Downed=downed;row.Shielded=shield;row.RoundedHealth=value;row.RoundedMaximum=max;
                row.Value.text=!known?"HP UNKNOWN":downed?"DOWNED":shield?"PROTECTED":value+" / "+max;
                row.Value.color=!known?Muted:downed?new Color(1,.55f,.43f):shield?Gold:Ivory;
            }
            row.Fill.enabled=known;
            if(known){SetFill(row.Fill,hp.Health/hp.MaximumHealth);row.Fill.color=downed?new Color(.79f,.33f,.27f):shield?Gold:Emerald;}
        }
        private void ClearPartyHealthRows(){for(int i=0;i<partyHealthRows.Length;i++)if(partyHealthRows[i]!=null)ClearPartyHealthRow(partyHealthRows[i]);}
        private static void ClearPartyHealthRow(PartyHealthRow row)
        {
            if(row.Root.gameObject.activeSelf)row.Root.gameObject.SetActive(false);
            row.HasPeer=row.Known=row.Seen=false;row.Peer=0;row.RoundedHealth=row.RoundedMaximum=-1;row.Fill.enabled=false;
            // Zero is a valid host ID; HasPeer, never this cleared scalar, is presence.
        }
        private void FitPartyHealth()
        {
            if(minimapRoot==null||hudRoot==null){partyLayoutAvailable=false;return;}
            minimapRoot.GetWorldCorners(partyMapCorners);
            Vector3 mapRight=hudRoot.InverseTransformPoint(partyMapCorners[2]);
            Vector3 mapBottom=hudRoot.InverseTransformPoint(partyMapCorners[0]);
            float width=hudRoot.rect.width,height=hudRoot.rect.height;
            float left=mapRight.x-hudRoot.rect.xMin+16,top=148;
            // Fixed slots remain fixed even if a middle member leaves. The layout
            // reserves all8 rows and never overlaps reticle, title or bottom vitals.
            float panelHeight=30+8*34;
            partyLayoutAvailable=left+270<=width*.5f-72&&top+panelHeight<=height-280;
            if(partyLayoutAvailable)
            {
                var position=new Vector2(left,-top);
                if((partyHealthRoot.anchoredPosition-position).sqrMagnitude>.01f)partyHealthRoot.anchoredPosition=position;
            }
            float noticeTop=hudRoot.rect.yMax-mapBottom.y+(session.Graphics!=null&&session.Graphics.PerformanceOverlayVisible?104:16);
            partyNoticeAvailable=noticeTop+24<=height-280;
            var noticePosition=new Vector2(36,-noticeTop);
            if((partyLayoutNotice.rectTransform.anchoredPosition-noticePosition).sqrMagnitude>.01f)
                partyLayoutNotice.rectTransform.anchoredPosition=noticePosition;
        }
    }
}
