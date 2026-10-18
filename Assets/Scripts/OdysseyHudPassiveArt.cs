using UnityEngine;

namespace OdysseyOracle
{
    // # ARCHITECTURE SNAPSHOT
    // Presentation only: explicit serialized catalog reference, existing12-bit order,
    // three cached card summaries. Missing/miswired art keeps the original element
    // emblem/drawn glyph and readable real node/state/effect text. No unlock inference.
    public sealed partial class OdysseyHud
    {
        [SerializeField] private PassiveNodeIconLibrary passiveNodeIconCatalog;
        static readonly string[] PassiveSpriteNames={
            "Passive_Fire_Kindle","Passive_Fire_LivingFlame","Passive_Fire_SolarFocus",
            "Passive_Water_Flow","Passive_Water_ReturningTide","Passive_Water_DeepCurrent",
            "Passive_Earth_HeavyFocus","Passive_Earth_SteadyChannel","Passive_Earth_StoneTempo",
            "Passive_Air_LightStep","Passive_Air_Tailwind","Passive_Air_Skystride"};
        // Short primary effects from frozen meaningful-passives-003. Full authoritative
        // UpgradeDescription remains in the existing scroll details, including caps,
        // own-element training, actual4s recovery, prerequisites and real cost/state.
        static readonly string[] PassivePrimaryEffects={
            "All element damage +10%.","All element damage +12%.","All element damage +15%.",
            "Water-family slows last +0.20 s.","Water-family slows last +0.20 s.","Water-family slows last +0.20 s.",
            "All element damage +6%.","Earth-family interrupts +0.10 s.","Damage +10%; Earth interrupts +0.10 s.",
            "Movement speed +6%.","Movement speed +8%.","Movement speed +10%."};

        // Call before Initialize when GameSession constructs the HUD dynamically.
        // A later explicit swap refreshes existing Images; it creates no new objects.
        public void SetPassiveIconCatalog(PassiveNodeIconLibrary catalog)
        {
            if(passiveNodeIconCatalog==catalog)return;
            passiveNodeIconCatalog=catalog;treeUiSnapshotValid=false;
            if(built && treeNodeButtons!=null)RefreshTree();
        }
        private Sprite PassiveCardSprite(int element,int node)
        {
            if(element<0||element>=4||node<0||node>=3)return null;
            int bitId=element*3+node;
            var sprite=passiveNodeIconCatalog!=null?passiveNodeIconCatalog.Get(element,node):null;
            return sprite!=null && sprite.name==PassiveSpriteNames[bitId]?sprite:OracleElementEmblems.Get(element);
        }
        private static string PassivePrimaryEffect(int element,int node)=>element>=0&&element<4&&node>=0&&node<3
            ?PassivePrimaryEffects[element*3+node]:"Upgrade details unavailable.";
    }
}
