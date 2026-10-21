namespace OdysseyOracle
{
    // Immutable finite decomposition; no room-filling body collider or duplicate interior floor.
    public static class CampaignTavernLayout020
    {
        public const int Revision=1,ObstacleCount=18,PreviousObstacleCount=137,CombinedObstacleCount=154,FloorWitnessCount=2;
        public const int RemovedPreviousIndex=87;
        public const uint RemovedBodyKey=302u,FirstKey=8000u;public const byte ColliderKind=10;
        public const float ClearDoorWidth=2f,ClearDoorHeight=3.5f,ClearHallWidth=11.6f,ClearHallDepth=9f;
        public static LandingPoint020 MerchantFeet=>new LandingPoint020(-21.4f,0f,33.3f);
        public const float MerchantYaw=180f;
        public static LandingPoint020 CustomerFeet=>new LandingPoint020(-21.4f,0f,30.3f);
        public static bool TryFuturePartyStand(int player,out LandingPoint020 feet)
        {feet=new LandingPoint020(player==0?-25.5f:-24.5f,0f,27.2f);return player>=0&&player<2;}
        public static bool IsObstacle(in LandingBox020 box)=>box.Kind==ColliderKind&&box.Key>=FirstKey&&box.Key<FirstKey+(uint)ObstacleCount;
        // Witnesses clip existing contour bands18/20; callers must not emit another floor mesh/collider.
        public static bool TryInteriorFloorWitness(int index,out LandingBox020 value)
        {
            value=default;if(index<0||index>=FloorWitnessCount)return false;
            float az=index==0?25.5f:30f,bz=index==0?30f:34.5f;
            if(!PortalLandingLayout020.TryMorton(-25f,(az+bz)*.5f,out uint key))return false;
            value=new LandingBox020(-30.8f,-.8f,az,-19.2f,0f,bz,index==0?18u:20u,1,0,key,0f,1f,0f);return true;
        }
        public static bool TryObstacle(int index,out LandingBox020 value)
        {
            switch(index)
            {
                case 0:value=new LandingBox020(-32f,0f,24f,-30.8f,8.6f,36f,8000u,ColliderKind,0,617092u,0f,1f,0f);return true; // west-wall
                case 1:value=new LandingBox020(-19.2f,0f,24f,-18f,8.6f,36f,8001u,ColliderKind,0,618385u,0f,1f,0f);return true; // east-wall
                case 2:value=new LandingBox020(-30.8f,0f,34.5f,-19.2f,8.6f,36f,8002u,ColliderKind,0,639474u,0f,1f,0f);return true; // back-wall
                case 3:value=new LandingBox020(-30.8f,0f,24f,-26.34f,8.6f,25.5f,8003u,ColliderKind,0,616539u,0f,1f,0f);return true; // front-left-wall
                case 4:value=new LandingBox020(-23.66f,0f,24f,-19.2f,8.6f,25.5f,8004u,ColliderKind,0,617550u,0f,1f,0f);return true; // front-right-wall
                case 5:value=new LandingBox020(-26.34f,3.55f,24f,-23.66f,8.6f,25.5f,8005u,ColliderKind,0,616794u,0f,1f,0f);return true; // above-door-wall
                case 6:value=new LandingBox020(-26.49f,0f,24.63f,-26.19f,3.6f,24.97f,8006u,ColliderKind,0,616734u,0f,1f,0f);return true; // left-door-post
                case 7:value=new LandingBox020(-23.81f,0f,24.63f,-23.51f,3.6f,24.97f,8007u,ColliderKind,0,617483u,0f,1f,0f);return true; // right-door-post
                case 8:value=new LandingBox020(-26.49f,3.55f,24.63f,-23.51f,3.85f,24.97f,8008u,ColliderKind,0,616794u,0f,1f,0f);return true; // door-lintel
                case 9:value=new LandingBox020(-26.47f,0f,24.74f,-26.15f,3.42f,27.01f,8009u,ColliderKind,0,616766u,0f,1f,0f);return true; // fixed-inward-open-leaf
                case 10:value=new LandingBox020(-32.3f,8.6f,23.7f,-17.7f,13.3f,36.3f,8010u,ColliderKind,0,617424u,0f,1f,0f);return true; // roof-only-envelope
                case 11:value=new LandingBox020(-23.36f,0f,31.04f,-19.44f,1.33f,32.16f,8011u,ColliderKind,0,618220u,0f,1f,0f);return true; // east-counter
                case 12:value=new LandingBox020(-29.75f,0f,27.95f,-27.75f,0.88f,29.05f,8012u,ColliderKind,0,617049u,0f,1f,0f);return true; // west-table-a
                case 13:value=new LandingBox020(-29.75f,0f,31.75f,-27.75f,0.88f,32.85f,8013u,ColliderKind,0,639059u,0f,1f,0f);return true; // west-table-b
                case 14:value=new LandingBox020(-30.46f,0f,28.19f,-29.84f,1.17f,28.81f,8014u,ColliderKind,0,616989u,0f,1f,0f);return true; // a-west-chair
                case 15:value=new LandingBox020(-27.31f,0f,28.19f,-26.69f,1.17f,28.81f,8015u,ColliderKind,0,617240u,0f,1f,0f);return true; // a-east-chair
                case 16:value=new LandingBox020(-30.46f,0f,31.99f,-29.84f,1.17f,32.61f,8016u,ColliderKind,0,638999u,0f,1f,0f);return true; // b-west-chair
                case 17:value=new LandingBox020(-27.31f,0f,31.99f,-26.69f,1.17f,32.61f,8017u,ColliderKind,0,639250u,0f,1f,0f);return true; // b-east-chair
                default:value=default;return false;
            }
        }
        // Finite outer merge preserves all previous fixture/column/core values, excluding only full-body302.
        public static bool TryMergedObstacleSlot(int index,out int previous,out int tavern)
        {
            previous=tavern=-1;if(index<0||index>=CombinedObstacleCount)return false;int before=0;
            if(index==87){tavern=3;return true;}if(index>87)before++;
            if(index==88){tavern=6;return true;}if(index>88)before++;
            if(index==89){tavern=9;return true;}if(index>89)before++;
            if(index==90){tavern=5;return true;}if(index>90)before++;
            if(index==91){tavern=8;return true;}if(index>91)before++;
            if(index==92){tavern=14;return true;}if(index>92)before++;
            if(index==93){tavern=12;return true;}if(index>93)before++;
            if(index==94){tavern=0;return true;}if(index>94)before++;
            if(index==95){tavern=15;return true;}if(index>95)before++;
            if(index==96){tavern=10;return true;}if(index>96)before++;
            if(index==97){tavern=7;return true;}if(index>97)before++;
            if(index==98){tavern=4;return true;}if(index>98)before++;
            if(index==99){tavern=11;return true;}if(index>99)before++;
            if(index==100){tavern=1;return true;}if(index>100)before++;
            if(index==112){tavern=16;return true;}if(index>112)before++;
            if(index==113){tavern=13;return true;}if(index>113)before++;
            if(index==114){tavern=17;return true;}if(index>114)before++;
            if(index==115){tavern=2;return true;}if(index>115)before++;
            previous=index-before;if(previous>=RemovedPreviousIndex)previous++;return previous>=0&&previous<PreviousObstacleCount;
        }
    }
}
// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Existing world/Y-up tavern(-25,0,30),scale1; clear11.6x9m hall,2m doorway, fixed inward-open leaf and existing TownY0 floor.
// Array Formats:18 stable kind10/key8000..8017 solid values,154-slot Morton outer merge and2 clipped source-floor witnesses; no Object, heap, frame containers or persisted positions.
// Dependencies/Pointers: Root replaces only old full-body302, uses this same stream for Stand/Move/actual colliders, skips kind10 generic visual boxes and reuses old floor meshes/colliders. Future party points are proposals only; real session/world/generation/input owners retain authority.
