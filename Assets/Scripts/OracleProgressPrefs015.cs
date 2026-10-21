// # ARCHITECTURE SNAPSHOT
// Coordinate Space: six exact progression key identities; no world/actor mutation.
// Array Formats: Editor-only six cached alias strings and typed hash/presence rows; player path owns no scope arrays.
// Dependencies/Pointers: four original save owners retain logic; Editor alias persists until explicit stopped End, never auto-clears on Play lifecycle.
using UnityEngine;
#if UNITY_EDITOR
using System;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using UnityEditor;
#endif

namespace OdysseyOracle
{
#if UNITY_EDITOR
    [InitializeOnLoad]
#endif
    public static class OracleProgressPrefs015
    {
        public static int GetInt(string key, int defaultValue = 0) => PlayerPrefs.GetInt(Resolve(key), defaultValue);
        public static string GetString(string key, string defaultValue = "") => PlayerPrefs.GetString(Resolve(key), defaultValue);
        public static bool HasKey(string key) => PlayerPrefs.HasKey(Resolve(key));
        public static void SetInt(string key, int value) => PlayerPrefs.SetInt(Resolve(key), value);
        public static void SetString(string key, string value) => PlayerPrefs.SetString(Resolve(key), value);
        public static void DeleteKey(string key) => PlayerPrefs.DeleteKey(Resolve(key));
        public static void Save()
        {
#if UNITY_EDITOR
            if (invalidScope) throw new InvalidOperationException("Retained Editor progression test scope is invalid; preferences are blocked.");
#endif
            PlayerPrefs.Save();
        }
        static string Resolve(string key)
        {
#if UNITY_EDITOR
            if (invalidScope) throw new InvalidOperationException("Retained Editor progression test scope is invalid; preferences are blocked.");
            if (scope != null)
            {
                if(key==RefugeFlameRules020.PreferenceKey)
                {
                    if(flameScopeToken020!=scope.token){flameScopeToken020=scope.token;flameShadowKey020=ShadowPrefix+scope.token+"."+RefugeFlameRules020.PreferenceKey;}
                    return flameShadowKey020;
                }
                int slot = Slot(key);
                if (slot < 0) throw new InvalidOperationException("Unsupported progression key while Editor test profile is active. Do not run the unrelated tutorial reward fixture in this scope.");
                return scope.shadowKeys[slot];
            }
#endif
            return key; // Same reference and API behavior; no alias storage/concatenation in a player.
        }

#if UNITY_EDITOR
        const string StateKey = "Oracle.ProgressPrefs015.ActiveScope.v1";
        const string UsedPrefix = "Oracle.ProgressPrefs015.UsedToken.";
        const string ShadowPrefix = "OdysseyOracle.EditorTest015.";
        static Scope scope;
        static bool invalidScope;
        static string flameScopeToken020,flameShadowKey020;
        [Serializable] public sealed class KeyProof
        { public string key, type, sha256; public bool present; }
        [Serializable] sealed class Scope
        {
            public string token, company, product, evidenceDirectory;
            public int editorPid;
            public string[] shadowKeys;
            public KeyProof[] before;
        }
        [Serializable] public sealed class Evidence
        {
            public string schema = "oracle.progress-prefs015.isolation.v1", status, phase, utc, token, company, product;
            public int editorPid;
            public bool activeAlias, canonicalProfileUntouched, proofPointsMatch, shadowKeysInitiallyAbsent;
            public string[] shadowKeys;
            public KeyProof[] before, after;
            public string limitation = "Six expected typed canonical values/presence compared at proof points; no raw values, canonical rewrites or shadow cleanup. Unrelated/transient preference writes and settings/audio are not audited by this wrapper.";
        }
        static OracleProgressPrefs015() { RestoreScope(); }
        public static bool EditorTestScopeActive015 => scope != null && !invalidScope;
        public static string EditorTestToken015 => scope != null && !invalidScope ? scope.token : "";

        public static string BeginEditorTestScope015(string uniqueGuidN, string freshAbsoluteEvidenceDirectory)
        {
            RequireStopped();
            if (scope != null || invalidScope) throw new InvalidOperationException("An active or invalid retained scope already exists; finish that scope explicitly before another Begin.");
            if (!ValidToken(uniqueGuidN)) throw new ArgumentException("Use a nonzero lowercase GUID in exact N format.");
            if (SessionState.GetBool(UsedPrefix + uniqueGuidN, false)) throw new ArgumentException("Token was already used in this Editor session.");
            string folder = WorkDirectory(freshAbsoluteEvidenceDirectory);
            if (Directory.Exists(folder) || File.Exists(folder)) throw new ArgumentException("Evidence directory must be fresh.");
            string[] shadows = BuildShadows(uniqueGuidN);
            for (int index = 0; index < 6; index++) if (PlayerPrefs.HasKey(shadows[index])) throw new InvalidOperationException("A shadow key already exists; use a fresh GUID. Nothing was deleted or copied.");
            var next = new Scope { token = uniqueGuidN, company = Application.companyName, product = Application.productName,
                editorPid = EditorPid(), shadowKeys = shadows, before = CaptureCanonical(), evidenceDirectory = folder };
            Directory.CreateDirectory(folder);
            // Persist proof metadata, never canonical values. Set alias before any possible next Play/reload.
            SessionState.SetString(StateKey, JsonConvert.SerializeObject(next));
            SessionState.SetBool(UsedPrefix + uniqueGuidN, true);
            scope = next;
            return WriteProof("begin");
        }
        public static string VerifyEditorTestScope015()
        { RequireActive(); return WriteProof("verify-" + Guid.NewGuid().ToString("N")); }
        public static string EndEditorTestScope015()
        {
            RequireStopped(); RequireActive();
            string path = WriteProof("end"); // Proof follows owner OnDisable writes after root Stop.
            SessionState.EraseString(StateKey);
            scope = null; invalidScope = false;
            return path; // Keep all shadow keys/evidence. Later cleanup must be explicitly shadow-only.
        }
        public static string GetEditorShadowKey015(int index)
        { RequireActive(); if ((uint)index >= 6) throw new ArgumentOutOfRangeException(nameof(index)); return scope.shadowKeys[index]; }

        public static string GetEditorFlameShadowKey020(){RequireActive();return Resolve(RefugeFlameRules020.PreferenceKey);}

        static void RequireStopped()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isPlaying || EditorApplication.isCompiling || EditorApplication.isUpdating)
                throw new InvalidOperationException("Begin/End require a stopped, quiescent Editor. End is explicit only after root Stop and owner disable saves.");
        }
        static void RequireActive()
        { if (scope == null || invalidScope) throw new InvalidOperationException("No valid retained Editor progression test scope is active."); }
        static bool ValidToken(string value) => value != null && value.Length == 32 && Guid.TryParseExact(value, "N", out Guid parsed) &&
            parsed != Guid.Empty && string.Equals(value, parsed.ToString("N"), StringComparison.Ordinal);
        static int EditorPid()
        { using (var process = System.Diagnostics.Process.GetCurrentProcess()) return process.Id; }
        static void RestoreScope()
        {
            string raw = SessionState.GetString(StateKey, "");
            if (string.IsNullOrEmpty(raw)) return;
            invalidScope = true;
            try
            {
                Scope saved = JsonConvert.DeserializeObject<Scope>(raw);
                if (saved == null || !ValidToken(saved.token) || saved.editorPid != EditorPid() || saved.company != Application.companyName || saved.product != Application.productName ||
                    saved.shadowKeys == null || saved.shadowKeys.Length != 6 || saved.before == null || saved.before.Length != 6) return;
                string[] expected = BuildShadows(saved.token);
                for (int index = 0; index < 6; index++) if (saved.shadowKeys[index] != expected[index] || saved.before[index] == null ||
                    saved.before[index].key != Canonical(index) || saved.before[index].type != (IsString(index) ? "string" : "int") ||
                    saved.before[index].sha256 == null || saved.before[index].sha256.Length != 64) return;
                saved.evidenceDirectory = WorkDirectory(saved.evidenceDirectory);
                scope = saved; invalidScope = false;
            }
            catch (Exception) { /* Retain invalidScope: never silently fall back to canonical keys. */ }
        }
        static string[] BuildShadows(string token)
        {
            var result = new string[6]; string prefix = ShadowPrefix + token + ".";
            for (int index = 0; index < 6; index++) result[index] = prefix + Canonical(index);
            return result; // Cold Begin/reload only; these exact strings are reused by every mapped API call.
        }
        static KeyProof[] CaptureCanonical()
        {
            var result = new KeyProof[6];
            for (int index = 0; index < 6; index++)
            {
                string key = Canonical(index); bool present = PlayerPrefs.HasKey(key), text = IsString(index);
                string type = text ? "string" : "int";
                string value = !present ? "ABSENT" : text ? PlayerPrefs.GetString(key, "") : PlayerPrefs.GetInt(key, 0).ToString(CultureInfo.InvariantCulture);
                byte[] bytes = Encoding.UTF8.GetBytes(type + "\n" + (present ? "PRESENT\n" : "") + value);
                string digest;
                using (SHA256 hash = SHA256.Create()) digest = BitConverter.ToString(hash.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant();
                result[index] = new KeyProof { key = key, type = type, present = present, sha256 = digest };
            }
            return result;
        }
        static string WriteProof(string phase)
        {
            RequireActive(); KeyProof[] after = CaptureCanonical();
            bool same = scope.editorPid == EditorPid() && scope.company == Application.companyName && scope.product == Application.productName;
            for (int index = 0; index < 6; index++) same &= scope.before[index].present == after[index].present && scope.before[index].type == after[index].type && scope.before[index].sha256 == after[index].sha256;
            var proof = new Evidence { status = same ? "ROOT_VERIFIED_ISOLATED_TEST_PROFILE" : "FAIL_CANONICAL_PROFILE_PROOF", phase = phase,
                utc = DateTime.UtcNow.ToString("o"), token = scope.token, company = Application.companyName, product = Application.productName,
                editorPid = EditorPid(), activeAlias = true, canonicalProfileUntouched = same, proofPointsMatch = same,
                shadowKeysInitiallyAbsent = true, shadowKeys = scope.shadowKeys, before = scope.before, after = after };
            string path = Path.Combine(scope.evidenceDirectory, phase + ".json");
            if (File.Exists(path)) throw new InvalidOperationException("Evidence path already exists; it will not be overwritten.");
            File.WriteAllText(path, JsonConvert.SerializeObject(proof, Formatting.Indented)); return path;
        }
        static string WorkDirectory(string folder)
        {
            if (string.IsNullOrWhiteSpace(folder) || !Path.IsPathRooted(folder)) throw new ArgumentException("Use an absolute evidence directory under existing soi/work.");
            string project = Directory.GetParent(Application.dataPath).FullName;
            string workspace = Directory.GetParent(Directory.GetParent(project).FullName).FullName;
            string root = Path.GetFullPath(Path.Combine(workspace, "work")) + Path.DirectorySeparatorChar;
            string full = Path.GetFullPath(folder);
            if (!full.StartsWith(root, StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("Evidence must stay under canonical soi/work.");
            return full;
        }
        static bool IsString(int index) => index == 0 || index == 1 || index == 3;
        static int Slot(string key)
        {
            if (key == "OdysseyOracle.TownProgression.v1") return 0;
            if (key == "OdysseyOracle.TownProgression.v1.backup") return 1;
            if (key == "Oracle.Achievements.v1") return 2;
            if (key == "Oracle.Discovery.v1") return 3;
            if (key == "OdysseyOracle.Rewards.tutorial_guardian_v1.Xp") return 4;
            if (key == "OdysseyOracle.Rewards.tutorial_guardian_v1.Heartstone") return 5;
            return -1;
        }
        static string Canonical(int index)
        {
            switch (index)
            {
                case 0: return "OdysseyOracle.TownProgression.v1";
                case 1: return "OdysseyOracle.TownProgression.v1.backup";
                case 2: return "Oracle.Achievements.v1";
                case 3: return "Oracle.Discovery.v1";
                case 4: return "OdysseyOracle.Rewards.tutorial_guardian_v1.Xp";
                case 5: return "OdysseyOracle.Rewards.tutorial_guardian_v1.Heartstone";
                default: throw new ArgumentOutOfRangeException(nameof(index));
            }
        }
#endif
    }
}
// # ARCHITECTURE SNAPSHOT
// Coordinate Space: exactly six canonical progression keys; unsupported keys fail closed only while Editor scope is active.
// Array Formats: six cold precomputed aliases and typed presence/SHA256 proofs; no per-get namespace concatenation or player scope arrays.
// Dependencies/Pointers: SessionState restores alias before first access/Awake and survives reload; explicit stopped End follows owner disable saves, retains test keys and clears only alias metadata. Canonical values are read, never copied/deleted/rewritten by scope controls.

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: one explicitly recognized separate flame profile key under the retained current Editor scope.
// Array Formats: legacy six cached aliases/proof rows/schema unchanged; one cached auxiliary alias allocated cold on first flame-key access per scope token.
// Dependencies/Pointers: invalid scopes still fail closed; new key never bypasses active test alias. Root separately captures typed canonical flame-key before/after, because legacy six-key proof does not include this auxiliary key.
