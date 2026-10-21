using System;
using UnityEngine;

namespace OdysseyOracle
{
    public sealed partial class RefugeActivityDirector
    {
        const string DiscoveryKey = "Oracle.Discovery.v1";
        DiscoveryProgress discovery = DiscoveryProgress.Empty;
        bool discoverySaveBlocked, discoverySaveDirty;
        readonly string[] discoveryNotices = new string[DiscoveryProgress.AchievementCount];
        public string DiscoverySaveStatus { get; private set; } = "";
        public bool HearthStickHeld => (discovery.QuestFlags & DiscoveryProgress.StickHeld) != 0;
        public bool HearthStickPlaced => (discovery.QuestFlags & DiscoveryProgress.StickPlaced) != 0;
        public bool FirstSpellUpgradeAccepted => (discovery.QuestFlags & DiscoveryProgress.SpellUpgradeEver) != 0;
        public bool FirstTowerUpgradeAccepted => (discovery.QuestFlags & DiscoveryProgress.TowerUpgradeEver) != 0;
        void InitializeDiscoveryProgress()
        {
            for (int i = 0; i < discoveryNotices.Length; ++i)
                discoveryNotices[i] = "ACHIEVEMENT · " + DiscoveryCatalog.AchievementName(i);
            try
            {
                string raw = OracleProgressPrefs015.GetString(DiscoveryKey, "");
                if (raw.Length == 0) return;
                if (raw.Length > 512) { BlockDiscoverySave(); return; }
                DiscoveryProgress loaded = JsonUtility.FromJson<DiscoveryProgress>(raw);
                if (!loaded.Valid) { BlockDiscoverySave(); return; }
                discovery = loaded;
            }
            catch (Exception) { BlockDiscoverySave(); }
        }
        void BlockDiscoverySave()
        {
            discoverySaveBlocked = true;
            DiscoverySaveStatus = "Existing discovery data could not be read safely. It was kept; new discovery saving is unavailable.";
        }
        public bool IsAchievementEarned(int row) => row==RefugeFlameRules020.AchievementRow?AllRefugeFlamesOutEarned020:discovery.Earned(row, unlocked);
        public bool IsAchievementNameVisible(int row) => row==RefugeFlameRules020.AchievementRow?AllRefugeFlamesOutNameVisible020:discovery.NameVisible(row, unlocked);
        public bool HasReadDiscoveryBook(int id) => (uint)id < DiscoveryProgress.BookCount &&
            (discovery.ReadBooks & (1u << id)) != 0;
        public bool RecordPhysicalBookRead(int id)
        {
            if (discoverySaveBlocked || (uint)id >= DiscoveryProgress.BookCount) return false;
            bool first = !HasReadDiscoveryBook(id);
            if (!discovery.Read(id)) return false;
            SaveDiscoveryProgress();
            if (first) ShowDiscoveryAward(5 + id);
            return true;
        }
        public bool TryPickupHearthStick()
        {
            if (discoverySaveBlocked || !discovery.PickupStick()) return false;
            SaveDiscoveryProgress(); return true;
        }
        public bool TryPlaceHearthStick()
        {
            if (discoverySaveBlocked || !discovery.PlaceStick()) return false;
            SaveDiscoveryProgress(); ShowDiscoveryAward(17); return true;
        }
        // Only an already accepted local progression owner calls this adapter.
        public void RecordAcceptedIntroUpgrade(bool tower)
        {
            if (discoverySaveBlocked || !discovery.RecordAcceptedUpgrade(tower)) return;
            SaveDiscoveryProgress(); ShowDiscoveryAward(tower ? 19 : 18);
        }
        public bool FlushDiscoveryProgress()
        {
            if (discoverySaveBlocked) return false;
            if (!discoverySaveDirty) return true;
            return SaveDiscoveryProgress();
        }
        bool SaveDiscoveryProgress()
        {
            discoverySaveDirty = true;
            try
            {
                OracleProgressPrefs015.SetString(DiscoveryKey, JsonUtility.ToJson(discovery)); OracleProgressPrefs015.Save();
                discoverySaveDirty = false; DiscoverySaveStatus = ""; return true;
            }
            catch (Exception)
            {
                DiscoverySaveStatus = "Discovery is kept for this session, but could not be stored. A later explicit save will retry.";
                return false;
            }
        }
        void ShowDiscoveryAward(int row)
        {
            if (notice == null || (uint)row >= discoveryNotices.Length) return;
            notice.text = discoveryNotices[row]; noticeUntil = Time.unscaledTime + 4f; notice.gameObject.SetActive(true);
            if (achievementSound != null && chime != null) chime.PlayOneShot(achievementSound);
        }
    }
}

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Local browser-profile facts and existing screen notification; no world/economy mutation.
// Array Formats: One16B discovery payload,20 cached notices; old five bits/key untouched; JSON only on accepted events/explicit retry.
// Dependencies/Pointers: Existing Activity owns audio/HUD; unreadable saves remain untouched; accepted live bits suppress repeat awards after a storage failure.

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: local profile facts only.
// Array Formats: existing DiscoveryProgress16B/20hint bits are byte-for-byte unchanged; row20 delegates to separate flame key without using another discovery bit.
// Dependencies/Pointers: new named read-only achievement API is coordinated with badge UI; existing discovery save/key/read/upgrade methods remain canonical.
