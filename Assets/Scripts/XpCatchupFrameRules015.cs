namespace OdysseyOracle
{
    public static class XpCatchupFrameRules015
    {
        public static XpEntitlement32 Entitlement(in XpFrame015 frame)=>new XpEntitlement32
        {Owner0=frame.Owner0,Owner1=frame.Owner1,ParentId=frame.SourceId,OwnerMask=frame.EligibleMask,Packed=frame.ClaimedMask,Revision=frame.ValueRevision};
        public static bool ShapeEntitlement(in XpFrame015 frame)=>frame.Kind==XpFrameRules015.Entitlement&&
            frame.SourceId<=XpCatchupOrbs015.ParentCapacity&&XpCatchupOrbs015.Valid(Entitlement(frame))&&
            XpCatchupOrbs015.SourceIdentity((int)frame.SourceId,frame.OriginWave,frame.BaseXp)&&
            frame.Position.x==0f&&frame.Position.y==0f&&frame.Position.z==0f&&
            frame.Level==0&&frame.Xp==0&&frame.Points==0&&frame.Mask==0&&frame.ForgeVersion==0&&
            frame.ForgeBalance==0&&frame.ForgeTiers==0&&frame.ForgeRevision==0&&frame.ProgressRevision==0;
    }
}
// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Existing authenticated run/epoch envelope and parent identity, no new geometry or progression authority.
// Array Formats: Kind4 uses same128B layout: parent/baseXP/origin/owners plus packed entitlement/revision; all unused words zero.
// Dependencies/Pointers: Room validates exactVersion2/header/sender first; Director keeps independent source/meta revisions and requires accepted parent claim before child visibility/grant.
