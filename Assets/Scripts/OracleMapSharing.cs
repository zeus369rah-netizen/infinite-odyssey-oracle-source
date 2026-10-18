using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace OdysseyOracle
{
    /// <summary>
    /// Bounded data-sharing client. A device UID/display name is not authentication.
    /// Downloading validates data only; this component never replaces the running world.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class OracleMapSharing : MonoBehaviour
    {
        public const string ServiceUrl = "https://oracles-return.zeusra.chatgpt.site";
        [SerializeField] private string serviceUrl = ServiceUrl;
        public const int MaximumPayloadBytes = 32768;
        public const int RequestTimeoutSeconds = 20;
        private const string UidPreference = "OdysseyOracle.MapSharing.DeviceUid";
        private const string NamePreference = "OdysseyOracle.MapSharing.DisplayName";
        private const string EditPreferencePrefix = "OdysseyOracle.MapSharing.EditKey.";
        private static readonly UTF8Encoding StrictUtf8 = new UTF8Encoding(false, true);

        [Serializable] private sealed class CreateRequest
        { public string uid, author, title, layout; }
        [Serializable] private sealed class UpdateRequest
        { public string uid, author, title, layout, code, editKey; }
        [Serializable] private sealed class UploadResponse
        { public string code, editKey, author, title; public bool updated; }
        [Serializable] private sealed class MapResponse
        { public string code, author, title, layout; }
        [Serializable] private sealed class ErrorResponse
        { public string error, message; }

        private UnityWebRequest activeRequest;
        private Coroutine activeRoutine;
        private int requestTicket;
        private string localUid, displayName;
        public bool Busy { get; private set; }
        public bool LastRequestSucceeded { get; private set; }
        public string Status { get; private set; } = "No map request has been made.";
        public string LastCode { get; private set; } = "";
        public string LastAuthor { get; private set; } = "";
        public string LastTitle { get; private set; } = "";
        public CircularMapLayout LastLayout { get; private set; }
        public string LastLayoutText => LastLayout != null ? LastLayout.ExportCanonical() : "";
        public string DeviceUid { get { EnsureIdentity(); return localUid; } }
        public string DisplayName { get { EnsureIdentity(); return displayName; } }
        public string ResolvedServiceUrl
        {
            get
            {
#if UNITY_WEBGL && !UNITY_EDITOR
                if (Uri.TryCreate(Application.absoluteURL, UriKind.Absolute, out var page) &&
                    (page.Scheme == Uri.UriSchemeHttps || page.Scheme == Uri.UriSchemeHttp))
                    return page.GetLeftPart(UriPartial.Authority);
#endif
                return Uri.TryCreate(serviceUrl, UriKind.Absolute, out var configured) && configured.Scheme == Uri.UriSchemeHttps
                    ? configured.GetLeftPart(UriPartial.Authority) : ServiceUrl;
            }
        }

        private void Awake() => EnsureIdentity();
        public void Initialize() => EnsureIdentity();

        public bool SetDisplayName(string name)
        {
            if (!ValidLabel(name, 48, out var clean)) { Status = "Use a display name of 1–48 characters."; return false; }
            EnsureIdentity();
            displayName = clean;
            try { PlayerPrefs.SetString(NamePreference, clean); PlayerPrefs.Save(); }
            catch (Exception) { Status = "Display name changed for this session; this browser could not save it."; return true; }
            return true;
        }

        public bool HasSavedEditAccess(string code)
        {
            if (!ValidCode(code)) return false;
            try { return ValidEditKey(PlayerPrefs.GetString(EditPreferencePrefix + code, "")); }
            catch (Exception) { return false; }
        }

        public bool Upload(string title, string layout, string code = null)
        {
            EnsureIdentity();
            string key = null;
            if (!string.IsNullOrEmpty(code) && ValidCode(code))
                try { key = PlayerPrefs.GetString(EditPreferencePrefix + code, ""); } catch (Exception) { }
            return Upload(localUid, displayName, title, layout, code, key);
        }

        /// <summary>Optional code/edit key updates an owned map. Keys stay outside exported layout data.</summary>
        public bool Upload(string uid, string author, string title, string layout, string code = null, string editKey = null)
        {
            if (Busy) return false;
            LastRequestSucceeded = false;
            if (!isActiveAndEnabled) { Status = "Map sharing is not active."; return false; }
            if (!ValidUid(uid)) { Status = "Map upload needs a valid device UID."; return false; }
            if (!ValidLabel(author, 48, out var cleanAuthor)) { Status = "Author must contain 1–48 characters."; return false; }
            if (!ValidLabel(title, 80, out var cleanTitle)) { Status = "Map title must contain 1–80 characters."; return false; }
            if (!CircularMapLayout.TryParse(layout, out var parsed, out var error)) { Status = error; return false; }
            var updating = !string.IsNullOrEmpty(code);
            if (updating && !ValidCode(code)) { Status = "A map code must contain exactly four digits, including any leading zero."; return false; }
            if (updating && !ValidEditKey(editKey)) { Status = "This device has no valid edit key for that map."; return false; }
            if (!updating && !string.IsNullOrEmpty(editKey)) { Status = "An edit key is used only when updating a map code."; return false; }
            var canonical = parsed.ExportCanonical();
            string json = updating
                ? JsonUtility.ToJson(new UpdateRequest { uid = uid, author = cleanAuthor, title = cleanTitle, layout = canonical, code = code, editKey = editKey })
                : JsonUtility.ToJson(new CreateRequest { uid = uid, author = cleanAuthor, title = cleanTitle, layout = canonical });
            var bytes = StrictUtf8.GetBytes(json);
            if (bytes.Length > MaximumPayloadBytes) { Status = "Map upload exceeds the 32 KiB limit."; return false; }
            var response = new BoundedResponse();
            var request = new UnityWebRequest(ResolvedServiceUrl + "/upload", UnityWebRequest.kHttpVerbPOST)
            {
                uploadHandler = new UploadHandlerRaw(bytes), downloadHandler = response,
                timeout = RequestTimeoutSeconds, redirectLimit = 0
            };
            request.SetRequestHeader("Content-Type", "application/json; charset=utf-8");
            request.SetRequestHeader("Accept", "application/json");
            BeginRequest(request, response, true, updating ? code : null, parsed);
            return true;
        }

        public bool Download(string code)
        {
            if (Busy) return false;
            LastRequestSucceeded = false;
            if (!isActiveAndEnabled) { Status = "Map sharing is not active."; return false; }
            if (!ValidCode(code)) { Status = "Enter exactly four digits; leading zeros are part of the code."; return false; }
            var response = new BoundedResponse();
            var request = new UnityWebRequest(ResolvedServiceUrl + "/api/maps/" + code, UnityWebRequest.kHttpVerbGET)
            { downloadHandler = response, timeout = RequestTimeoutSeconds, redirectLimit = 0 };
            request.SetRequestHeader("Accept", "application/json");
            BeginRequest(request, response, false, code, null);
            return true;
        }

        public void Cancel()
        {
            if (!Busy && activeRequest == null) return;
            requestTicket++;
            if (activeRoutine != null) StopCoroutine(activeRoutine);
            activeRoutine = null;
            if (activeRequest != null) { activeRequest.Abort(); activeRequest.Dispose(); activeRequest = null; }
            Busy = false;
            LastRequestSucceeded = false;
            Status = "Map request canceled.";
        }

        private void OnDisable() => Cancel();
        private void OnDestroy() => Cancel();

        private void BeginRequest(UnityWebRequest request, BoundedResponse response, bool upload, string requestedCode, CircularMapLayout pendingLayout)
        {
            activeRequest = request;
            Busy = true;
            Status = upload ? "Uploading map…" : "Downloading map " + requestedCode + "…";
            activeRoutine = StartCoroutine(Transfer(request, response, upload, requestedCode, pendingLayout, ++requestTicket));
        }

        private IEnumerator Transfer(UnityWebRequest request, BoundedResponse response, bool upload, string requestedCode,
            CircularMapLayout pendingLayout, int ticket)
        {
            UnityWebRequestAsyncOperation operation = null;
            try { operation = request.SendWebRequest(); }
            catch (Exception) { Status = "The map request could not be started."; }
            if (operation == null) { Finish(request); yield break; }
            var startedAt = Time.realtimeSinceStartup;
            var timedOut = false;
            while (!operation.isDone)
            {
                if (Time.realtimeSinceStartup - startedAt >= RequestTimeoutSeconds)
                { timedOut = true; request.Abort(); break; }
                yield return null;
            }
            if (ticket != requestTicket) yield break;
            if (timedOut) Status = "Map request timed out after 20 seconds.";
            else if (response.ExceededLimit) Status = "The map service response exceeded the 32 KiB limit.";
            else
            {
                try
                {
                    var text = response.ReadText();
                    if (request.result != UnityWebRequest.Result.Success || request.responseCode < 200 || request.responseCode >= 300)
                        Status = FailureMessage(request.responseCode, text);
                    else LastRequestSucceeded = upload ? AcceptUpload(text, requestedCode, pendingLayout) : AcceptDownload(text, requestedCode);
                }
                catch (Exception) { Status = "The map service returned an unreadable response."; }
            }
            Finish(request);
        }

        private bool AcceptUpload(string text, string requestedCode, CircularMapLayout parsed)
        {
            var result = JsonUtility.FromJson<UploadResponse>(text);
            if (result == null || !ValidCode(result.code) || !ValidEditKey(result.editKey) || parsed == null
                || (requestedCode != null && result.code != requestedCode)
                || !ValidLabel(result.author, 48, out var author) || !ValidLabel(result.title, 80, out var title))
            { Status = "The map service returned invalid upload metadata."; return false; }
            var keySaved = true;
            try { PlayerPrefs.SetString(EditPreferencePrefix + result.code, result.editKey); PlayerPrefs.Save(); }
            catch (Exception) { keySaved = false; }
            LastCode = result.code;
            LastAuthor = author;
            LastTitle = title;
            LastLayout = parsed;
            Status = "Map " + result.code + (requestedCode == null ? " uploaded." : " updated.")
                + (keySaved ? " Edit access is saved on this device." : " This browser could not save edit access.");
            return true;
        }

        private bool AcceptDownload(string text, string requestedCode)
        {
            var result = JsonUtility.FromJson<MapResponse>(text);
            if (result == null || result.code != requestedCode || !ValidLabel(result.author, 48, out var author)
                || !ValidLabel(result.title, 80, out var title)
                || !CircularMapLayout.TryParse(result.layout, out var parsed, out _))
            { Status = "The downloaded map did not pass layout validation."; return false; }
            LastCode = result.code;
            LastAuthor = author;
            LastTitle = title;
            LastLayout = parsed;
            Status = "Map " + result.code + " downloaded and validated. It has not been applied to the live world.";
            return true;
        }

        private void Finish(UnityWebRequest request)
        {
            if (activeRequest == request) activeRequest = null;
            request.Dispose();
            activeRoutine = null;
            Busy = false;
        }

        private void EnsureIdentity()
        {
            if (ValidUid(localUid) && !string.IsNullOrEmpty(displayName)) return;
            var storedUid = "";
            var storedName = "";
            try { storedUid = PlayerPrefs.GetString(UidPreference, ""); storedName = PlayerPrefs.GetString(NamePreference, ""); }
            catch (Exception) { }
            localUid = ValidUid(storedUid) ? storedUid : Guid.NewGuid().ToString("N");
            displayName = ValidLabel(storedName, 48, out var clean) ? clean : "Oracle " + localUid.Substring(0, 4).ToUpperInvariant();
            try { PlayerPrefs.SetString(UidPreference, localUid); PlayerPrefs.SetString(NamePreference, displayName); PlayerPrefs.Save(); }
            catch (Exception) { Status = "Map identity is available for this session; this browser could not save it."; }
        }

        private static bool ValidLabel(string value, int maximum, out string clean)
        {
            clean = value == null ? "" : value.Trim();
            if (clean.Length < 1 || clean.Length > maximum) return false;
            for (var i = 0; i < clean.Length; i++) if (char.IsControl(clean[i]) || char.IsSurrogate(clean[i])) return false;
            return true;
        }

        private static bool ValidUid(string value)
        {
            if (value == null || value.Length < 8 || value.Length > 96) return false;
            for (var i = 0; i < value.Length; i++)
            {
                var c = value[i];
                var alphanumeric = c >= 'a' && c <= 'z' || c >= 'A' && c <= 'Z' || c >= '0' && c <= '9';
                if (!alphanumeric && (i == 0 || c != '_' && c != '-')) return false;
            }
            return true;
        }

        private static bool ValidCode(string code)
        {
            if (code == null || code.Length != 4) return false;
            for (var i = 0; i < code.Length; i++) if (code[i] < '0' || code[i] > '9') return false;
            return true;
        }

        private static bool ValidEditKey(string value)
        {
            if (value == null || value.Length != 64) return false;
            for (var i = 0; i < value.Length; i++)
                if (!(value[i] >= '0' && value[i] <= '9' || value[i] >= 'a' && value[i] <= 'f')) return false;
            return true;
        }

        private static string FailureMessage(long status, string text)
        {
            var detail = "";
            try
            {
                var response = JsonUtility.FromJson<ErrorResponse>(text);
                if (response != null && ValidLabel(response.message, 200, out var message)) detail = " · " + message.Replace('<', '[').Replace('>', ']');
            }
            catch (Exception) { }
            return status > 0 ? "Map service returned HTTP " + status + detail : "Map service could not be reached. Check the connection and try again.";
        }

        private sealed class BoundedResponse : DownloadHandlerScript
        {
            private readonly byte[] received = new byte[MaximumPayloadBytes];
            private int count;
            public bool ExceededLimit { get; private set; }
            public BoundedResponse() : base(new byte[4096]) { }
            protected override void ReceiveContentLengthHeader(ulong contentLength)
            { if (contentLength > MaximumPayloadBytes) ExceededLimit = true; }
            protected override bool ReceiveData(byte[] data, int dataLength)
            {
                if (data == null || dataLength < 0 || dataLength > data.Length || ExceededLimit || dataLength > received.Length - count)
                { ExceededLimit = true; return false; }
                if (dataLength > 0) Buffer.BlockCopy(data, 0, received, count, dataLength);
                count += dataLength;
                return true;
            }
            public string ReadText() => StrictUtf8.GetString(received, 0, count);
        }
    }
}
