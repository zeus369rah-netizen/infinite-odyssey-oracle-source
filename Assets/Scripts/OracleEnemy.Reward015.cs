namespace OdysseyOracle
{
    public sealed partial class OracleEnemy
    {
        XpAdmissionTicket015 rewardTicket015;
        partial void ResetRewardAssociation015() { rewardTicket015 = default; }
        internal bool BindReward015(in XpAdmissionTicket015 ticket)
        {
            if (!ticket.Valid || !Alive || PoolGeneration015 != ticket.PoolGeneration || rewardTicket015.Valid) return false;
            rewardTicket015 = ticket; return true;
        }
        internal bool TryReadReward015(out XpAdmissionTicket015 ticket)
        { ticket = rewardTicket015; return ticket.Valid && ticket.PoolGeneration == PoolGeneration015; }
    }
}

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Pooled actor scalar generation and immutable admitted source/wave/value identity; no transforms or authority changes.
// Array Formats: One bounded value ticket per existing48actor; no managed/native allocation.
// Dependencies/Pointers: Pool helper declares/calls partial reset before activation/retirement and exposes nonwrapping PoolGeneration015; Director alone reserves/publishes/claims value.
