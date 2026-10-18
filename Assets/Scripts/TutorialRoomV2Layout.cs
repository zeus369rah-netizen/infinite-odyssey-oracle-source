using System;
using System.Collections.Generic;

namespace OdysseyOracle
{
    // # ARCHITECTURE SNAPSHOT
    // Original tutorial room metadata in metres, +Y up/+Z forward. Cold immutable scalar records;
    // geometry, audit envelopes, props and guide anchors consume this same layout rather than a plane count.
    // Existing gameplay anchors and42m length remain; optional width/vault parameters affect architecture only.
    // Layout owns returned arrays/envelope records; consumers borrow them read-only, never mutate them per frame.
    public sealed class TutorialRoomV2Layout
    {
        public readonly struct P3
        {
            public readonly float X,Y,Z;
            public P3(float x,float y,float z){X=x;Y=y;Z=z;}
            public static P3 operator +(P3 a,P3 b)=>new P3(a.X+b.X,a.Y+b.Y,a.Z+b.Z);
            public static P3 operator -(P3 a,P3 b)=>new P3(a.X-b.X,a.Y-b.Y,a.Z-b.Z);
            public static P3 operator *(P3 a,float b)=>new P3(a.X*b,a.Y*b,a.Z*b);
        }
        public readonly struct R2
        {
            public readonly float MinU,MinV,MaxU,MaxV;
            public R2(float minU,float minV,float maxU,float maxV){MinU=minU;MinV=minV;MaxU=maxU;MaxV=maxV;}
            public bool Contains(float u,float v)=>u>=MinU&&u<=MaxU&&v>=MinV&&v<=MaxV;
        }
        public enum AnchorId { Arrival,GuideStart,Shrine,ShrineApproach,Smith,SmithApproach,Archive,Portal,PortalApproach,PracticePad,PracticeApproach,PracticeTarget,LoreEmber,LoreWard,LoreElements,Hearth }
        public enum PropId { RunicBookstand,ForgeWorkbench,SuppliesRack,BrazierRing,WallLantern,CrystalReliquary }
        public sealed class Envelope
        {
            public string Name;
            // Axis0: planeX,U=Z,V=Y. Axis1: planeY,U=X,V=Z. Axis2: planeZ,U=X,V=Y.
            public int Axis,OutwardSign;public float Plane,NormalBand=.75f;
            public R2 Rectangle;public R2[] ClearOpenings=new R2[0],SolidGrilleMembers=new R2[0];
        }
        public readonly struct Placement
        {
            public readonly PropId Id;public readonly P3 Position;public readonly float Yaw;
            public readonly string AlignAnchor;
            public Placement(PropId id,P3 position,float yaw=0,string alignAnchor=null){Id=id;Position=position;Yaw=yaw;AlignAnchor=alignAnchor;}
        }
        public readonly float Width,VaultHeight,HalfWidth,CeilingY,PierX;
        public const float StartZ=-14f,EndZ=28f,DoorHalfWidth=2.3f,DoorHeight=5.2f,AlcoveDepth=4.5f,AlcoveHalfLength=2.6f;
        public const float CircleRadius=3.3f,WindowBottom=1.6f,WindowTop=7.8f,WindowHalfWidth=1.9f,RevealInset=.16f;
        public readonly float[] AlcoveCenters={-3f,8f,18f};
        public readonly float[] PierRows={-11.8f,-6.2f,3f,13f,22f,27.3f};
        public readonly R2[] Rooms;
        public readonly Envelope[] Envelopes;
        public readonly Placement[] PropPlacements;
        public float OuterX=>HalfWidth+AlcoveDepth;
        public R2 Bounds=>new R2(-OuterX,StartZ,OuterX,EndZ);
        public TutorialRoomV2Layout(float width=16f,float vaultHeight=10f)
        {
            if(float.IsNaN(width)||float.IsInfinity(width)||width<15f||width>18f||float.IsNaN(vaultHeight)||float.IsInfinity(vaultHeight)||vaultHeight<9.5f||vaultHeight>11f)
                throw new ArgumentOutOfRangeException("RoomV2 supports authored widths15..18m and vaults9.5..11m.");
            Width=width;VaultHeight=vaultHeight;HalfWidth=width*.5f;CeilingY=vaultHeight-.2f;PierX=HalfWidth-1.05f;
            var rooms=new List<R2>{new R2(-HalfWidth,StartZ,HalfWidth,EndZ)};
            foreach(float c in AlcoveCenters)foreach(int side in new[]{-1,1})rooms.Add(side<0?new R2(-OuterX,c-AlcoveHalfLength,-HalfWidth,c+AlcoveHalfLength):new R2(HalfWidth,c-AlcoveHalfLength,OuterX,c+AlcoveHalfLength));
            Rooms=rooms.ToArray();Envelopes=BuildEnvelopes();
            PropPlacements=new[]{
                new Placement(PropId.RunicBookstand,new P3(-HalfWidth-2.4f,0,6.6f),90),
                new Placement(PropId.ForgeWorkbench,new P3(-HalfWidth+1.55f,0,-8f)),
                new Placement(PropId.SuppliesRack,new P3(OuterX-1.2f,0,17.7f),-90),
                new Placement(PropId.BrazierRing,new P3(HalfWidth-1.5f,0,5.2f)),
                new Placement(PropId.WallLantern,new P3(-HalfWidth,6.3f,-8.7f),90,"Anchor_Mount"),
                new Placement(PropId.CrystalReliquary,new P3(-HalfWidth+1.35f,0,18f)),
                new Placement(PropId.WallLantern,new P3(HalfWidth,6.3f,12.2f),-90,"Anchor_Mount"),
                new Placement(PropId.WallLantern,new P3(-HalfWidth,6.3f,21f),90,"Anchor_Mount")
            };
        }
        public P3 Anchor(AnchorId id)
        {
            switch(id){
                case AnchorId.Arrival:case AnchorId.GuideStart:return new P3(0,.2f,-10);
                case AnchorId.Shrine:return new P3(0,0,2);
                case AnchorId.ShrineApproach:return new P3(0,.2f,-2.7f);
                case AnchorId.Smith:return new P3(6.8f,0,-3.6f);
                case AnchorId.SmithApproach:return new P3(6.8f,.2f,-1.8f);
                case AnchorId.Archive:return new P3(-HalfWidth-2.4f,1.1f,6.6f);
                case AnchorId.Portal:return new P3(0,.68f,25.2f);
                case AnchorId.PortalApproach:return new P3(0,.68f,24.3f);
                case AnchorId.PracticePad:return new P3(HalfWidth-2f,0,15.2f);
                case AnchorId.PracticeApproach:return new P3(HalfWidth-2.1f,.2f,12.3f);
                case AnchorId.PracticeTarget:return new P3(HalfWidth-1.9f,0,19f);
                case AnchorId.LoreEmber:return new P3(-3.9f,.96f,3.15f);
                case AnchorId.LoreWard:return new P3(-3.9f,.96f,7.3f);
                case AnchorId.LoreElements:return new P3(8.65f,.86f,-1.55f);
                case AnchorId.Hearth:return new P3(-4,0,5.25f);
                default:throw new ArgumentOutOfRangeException(nameof(id));
            }
        }
        // Clear window rectangle minus every real opaque grille member. This is the audit's exemption
        // set, not the full window: iron bars must still be certified from actual rendered triangles.
        public R2[] ClearRegions(Envelope envelope)
        {
            if(envelope==null)throw new ArgumentNullException(nameof(envelope));
            var regions=new List<R2>(envelope.ClearOpenings);
            foreach(R2 solid in envelope.SolidGrilleMembers){var next=new List<R2>();foreach(R2 r in regions){
                float u0=Math.Max(r.MinU,solid.MinU),u1=Math.Min(r.MaxU,solid.MaxU),v0=Math.Max(r.MinV,solid.MinV),v1=Math.Min(r.MaxV,solid.MaxV);
                if(u1<=u0||v1<=v0){next.Add(r);continue;}
                if(u0>r.MinU)next.Add(new R2(r.MinU,r.MinV,u0,r.MaxV));if(u1<r.MaxU)next.Add(new R2(u1,r.MinV,r.MaxU,r.MaxV));
                if(v0>r.MinV)next.Add(new R2(u0,r.MinV,u1,v0));if(v1<r.MaxV)next.Add(new R2(u0,v1,u1,r.MaxV));
            }regions=next;}return regions.ToArray();
        }
        Envelope[] BuildEnvelopes()
        {
            var list=new List<Envelope>();
            foreach(R2 room in Rooms){list.Add(new Envelope{Name="Floor "+list.Count,Axis=1,OutwardSign=-1,Plane=0,NormalBand=.01f,Rectangle=room});list.Add(new Envelope{Name="Vault "+list.Count,Axis=1,OutwardSign=1,Plane=CeilingY,NormalBand=.7f,Rectangle=room});}
            foreach(int side in new[]{-1,1}){
                var openings=new List<R2>();foreach(float c in AlcoveCenters)openings.Add(new R2(c-DoorHalfWidth,0,c+DoorHalfWidth,DoorHeight));
                list.Add(new Envelope{Name="Hall side "+side,Axis=0,OutwardSign=side,Plane=side*HalfWidth,Rectangle=new R2(StartZ,0,EndZ,CeilingY),ClearOpenings=openings.ToArray()});
            }
            list.Add(new Envelope{Name="Arrival wall",Axis=2,OutwardSign=-1,Plane=-13.95f,Rectangle=new R2(-HalfWidth,0,HalfWidth,CeilingY)});
            list.Add(new Envelope{Name="Portal rear wall",Axis=2,OutwardSign=1,Plane=27.85f,Rectangle=new R2(-HalfWidth,0,HalfWidth,CeilingY)});
            foreach(float c in AlcoveCenters)foreach(int side in new[]{-1,1}){
                float low=side<0?-OuterX:HalfWidth,high=side<0?-HalfWidth:OuterX;
                foreach(int end in new[]{-1,1})list.Add(new Envelope{Name="Alcove flank "+side+"/"+c+"/"+end,Axis=2,OutwardSign=end,Plane=c+end*AlcoveHalfLength,Rectangle=new R2(low,0,high,CeilingY)});
                var panel=new Envelope{Name="Alcove outer "+side+"/"+c,Axis=0,OutwardSign=side,Plane=side*OuterX,Rectangle=new R2(c-AlcoveHalfLength,0,c+AlcoveHalfLength,CeilingY)};
                if(side>0){
                    panel.ClearOpenings=new[]{new R2(c-WindowHalfWidth+RevealInset,WindowBottom+RevealInset,c+WindowHalfWidth-RevealInset,WindowTop-RevealInset)};
                    var grille=new List<R2>();for(int bar=0;bar<6;bar++){float z=c-1.5f+bar*.6f;grille.Add(new R2(z-.05f,WindowBottom+RevealInset,z+.05f,WindowTop-RevealInset));}
                    for(int bar=0;bar<5;bar++){float y=2.35f+bar*1.15f;grille.Add(new R2(c-WindowHalfWidth+RevealInset,y-.05f,c+WindowHalfWidth-RevealInset,y+.05f));}
                    panel.SolidGrilleMembers=grille.ToArray();
                }list.Add(panel);
            }
            return list.ToArray();
        }
    }
}
