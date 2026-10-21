using System;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
#if UNITY_EDITOR
using XpTransportReload015 = UnityEditor.AssemblyReloadEvents.AssemblyReloadCallback;
#else
using XpTransportReload015 = System.Action;
#endif

namespace OdysseyOracle
{
    public sealed partial class RoomConnection
    {
        const string XpChannel015 = "odyssey.xp.binary.015";
        FastBufferWriter xpWriter015;
        bool xpWriterReady015, xpWriterSubscribed015, xpSending015, xpReleasePending015;
        XpTransportReload015 xpReload015;
        double xpSendWindow015, xpReceiveWindow015;
        int xpSent015, xpReceived015;
        internal event Action<ulong, XpFrame015> XpMessage015;
        void EnsureXpWriter015()
        {
            if (xpWriterReady015) return;
            if (!BitConverter.IsLittleEndian || Unity.Collections.LowLevel.Unsafe.UnsafeUtility.SizeOf<XpFrame015>() != XpFrameRules015.Bytes)
                throw new InvalidOperationException("XP015 requires its exact128byte little-endian ABI.");
            xpWriter015 = new FastBufferWriter(XpFrameRules015.Bytes, Allocator.Persistent, XpFrameRules015.Bytes);
            xpWriterReady015 = true;
            xpWriter015.WriteValueSafe(default(XpFrame015)); xpWriter015.Seek(0); xpWriter015.Truncate(0);
            if (xpReload015 == null) xpReload015 = ReleaseXpTransport015;
            if (!xpWriterSubscribed015) { LightPoolLifecycle.SubscribeBeforeReload(xpReload015); xpWriterSubscribed015 = true; }
        }
        void RegisterXpHandler015()
        {
            EnsureXpWriter015();
            try { network.CustomMessagingManager.RegisterNamedMessageHandler(XpChannel015, ReceiveXp015); }
            catch { ReleaseXpTransport015(); throw; }
        }
        internal bool SendXp015(in XpFrame015 frame, ulong recipient)
        {
            if (!isActiveAndEnabled || !Connected || network == null || network.CustomMessagingManager == null ||
                !xpWriterReady015 || xpSending015 || recipient == LocalClientId || !XpFrameRules015.Shape(frame) ||
                (IsHost ? !network.IsServer || (frame.Kind != XpFrameRules015.Source&&frame.Kind!=XpFrameRules015.Entitlement) || !IsConnectedPeer(recipient) :
                    (frame.Kind != XpFrameRules015.Claim && frame.Kind != XpFrameRules015.Ack && frame.Kind!=XpFrameRules015.EntitlementAck) || recipient != NetworkManager.ServerClientId)) return false;
            double now = Time.realtimeSinceStartupAsDouble;
            if (now - xpSendWindow015 >= 1d) { xpSendWindow015 = now; xpSent015 = 0; }
            // Reserve four client slots for pickup requests during an ACK backlog.
            // Source/ACK state remains dirty; nearby rejected pickup requests are re-evaluated.
            if (xpSent015 >= 16 || (frame.Kind == XpFrameRules015.Ack||frame.Kind==XpFrameRules015.EntitlementAck) && xpSent015 >= 12) return false;
            xpSending015 = true;
            try
            {
                xpWriter015.Seek(0); xpWriter015.Truncate(0); xpWriter015.WriteValueSafe(frame);
                if (xpWriter015.Length != XpFrameRules015.Bytes) return false;
                network.CustomMessagingManager.SendNamedMessage(XpChannel015, recipient, xpWriter015, NetworkDelivery.ReliableFragmentedSequenced);
                xpSent015++; return true;
            }
            finally { xpSending015 = false; if (xpReleasePending015) ReleaseXpTransport015(); }
        }
        void ReceiveXp015(ulong sender, FastBufferReader reader)
        {
            if (!isActiveAndEnabled || !Connected || network == null || reader.Length != XpFrameRules015.Bytes ||
                !reader.TryBeginRead(XpFrameRules015.Bytes) ||
                (IsHost ? sender == LocalClientId || !IsConnectedPeer(sender) : sender != NetworkManager.ServerClientId)) return;
            double now = Time.realtimeSinceStartupAsDouble;
            if (now - xpReceiveWindow015 >= 1d) { xpReceiveWindow015 = now; xpReceived015 = 0; }
            if (xpReceived015 >= 16) return;
            reader.ReadValueSafe(out XpFrame015 frame);
            if (!XpFrameRules015.Shape(frame) || (IsHost ? frame.Kind != XpFrameRules015.Claim && frame.Kind != XpFrameRules015.Ack && frame.Kind!=XpFrameRules015.EntitlementAck : frame.Kind != XpFrameRules015.Source && frame.Kind!=XpFrameRules015.Entitlement)) return;
            xpReceived015++; XpMessage015?.Invoke(sender, frame);
        }
        void ReleaseXpTransport015()
        {
            if (xpSending015) { xpReleasePending015 = true; return; }
            xpReleasePending015 = false;
            if (xpWriterReady015) { xpWriter015.Dispose(); xpWriterReady015 = false; }
            if (xpWriterSubscribed015 && xpReload015 != null) LightPoolLifecycle.UnsubscribeBeforeReload(xpReload015);
            xpWriterSubscribed015 = false; xpSent015 = xpReceived015 = 0; xpSendWindow015 = xpReceiveWindow015 = 0d;
        }
    }
}

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Existing authenticated NGO server/client room; no new socket/session, simulation clock or claim permission.
// Array Formats: One persistent128B unmanaged writer primed cold, reused; bounded16frames/sec/direction with retained-source retries.
// Dependencies/Pointers: Room registers/unregisters handler and releases on disable/detach/reload; main-thread callbacks validate sender/type/length before source owner. No new hot JSON/byte-array creation.

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Accepted parent/wave/owner/run identities and original source positions; literal claim-time catch-up children.
// Array Formats: Existing4096source64 ledger,905entitlement32 sidecars/dirty words,4096readonly projection, exact128BVersion2 frames.
// Dependencies/Pointers: Root composes structural clearance and these guarded owner hooks; base-onlyForge, no doublebonus, native/peer/carry/visual/performance acceptance pending.
