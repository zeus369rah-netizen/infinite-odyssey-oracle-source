using System;
using UnityEngine;

namespace OdysseyOracle
{
    // # ARCHITECTURE SNAPSHOT
    // Scalar world-metre offsets, no allocation or scene lookup. Existing
    // arrival floor is clear across x[-2.4,2.4],z[-10,-8.6].
    // Canonical local offsets keep host0 unchanged and all eight capsules apart.
    // Native root checks still own actual changed-world collider acceptance.
    public static class CampaignRefugeSpawns
    {
        public static Vector3 ForAdmission(Vector3 originalSpawn,int slot)
        {
            switch(slot)
            {
                case 0:return originalSpawn;
                case 1:return originalSpawn+new Vector3(-1.2f,0,0);
                case 2:return originalSpawn+new Vector3(1.2f,0,0);
                case 3:return originalSpawn+new Vector3(-2.4f,0,0);
                case 4:return originalSpawn+new Vector3(2.4f,0,0);
                case 5:return originalSpawn+new Vector3(-1.2f,0,1.4f);
                case 6:return originalSpawn+new Vector3(1.2f,0,1.4f);
                case 7:return originalSpawn+new Vector3(0,0,1.4f);
                default:throw new ArgumentOutOfRangeException(nameof(slot));
            }
        }
    }
}
