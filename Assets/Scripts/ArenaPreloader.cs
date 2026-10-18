using System;
using System.Collections;
using UnityEngine;

namespace OdysseyOracle
{
    /// <summary>
    /// Stages actual local preparation before the party barrier. Progress is the
    /// fraction of these preparation stages, never a claim about all GPU shaders.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ArenaPreloader : MonoBehaviour
    {
        public const string ShaderCollectionResource = "OracleRuntimeShaders";
        private const int VariantsPerFrame = 4, MaximumWarmupVariants = 128;
        private GameSession session;
        private Coroutine operation;
        private Func<bool> readyGate;
        private int generation;
        private bool requiresRoom, sawStartedSession;

        public bool IsLoading { get; private set; }
        public bool IsLocallyPrepared { get; private set; }
        public float Progress { get; private set; }
        public string Status { get; private set; } = "";

        public void Initialize(GameSession owner)
        {
            Cancel();
            session = owner;
            Status = "";
        }

        /// <param name="prepareArena">Builds the local world and resets its combat/player state.</param>
        /// <param name="completed">Runs after local render preparation; starts the defense/barrier.</param>
        public void Begin(Action prepareArena, Action completed)
        {
            if (IsLoading) return;
            if (session == null || !isActiveAndEnabled || prepareArena == null)
            { Status = "Arena preparation is unavailable."; return; }
            generation++;
            readyGate = null;
            IsLoading = true;
            IsLocallyPrepared = false;
            Progress = 0f;
            Status = "Preparing arena";
            requiresRoom = session.CoopMode || (session.Rooms != null && session.Rooms.Connected);
            sawStartedSession = session.HasStarted;
            HoldAtLoadingScreen();
            operation = StartCoroutine(Prepare(generation, prepareArena, completed));
        }

        /// <summary>Bind to actual host/snapshot barrier state, such as Defense.ArenaReady.</summary>
        public void FinishWhenReady(Func<bool> isReady)
        {
            readyGate = isReady;
        }

        public void SetWaitingForParty()
        {
            if (!IsLoading || !IsLocallyPrepared) return;
            Progress = .9f;
            Status = "Waiting for party";
        }

        /// <summary>Root may call this directly when its authoritative ready barrier is satisfied.</summary>
        public void Complete()
        {
            if (!IsLoading || !IsLocallyPrepared) return;
            IsLoading = false;
            Progress = 1f;
            Status = "Arena ready";
            readyGate = null;
            generation++;
            if (operation != null) StopCoroutine(operation);
            operation = null;
            // Loading released browser control. Resume belongs to a subsequent
            // visible user gesture; a coroutine never requests pointer lock.
        }

        /// <summary>Stops preparation; root owns leaving the room or returning to its menu.</summary>
        public void Cancel()
        {
            generation++;
            if (operation != null) StopCoroutine(operation);
            operation = null;
            readyGate = null;
            bool wasLoading = IsLoading;
            IsLoading = IsLocallyPrepared = false;
            Progress = 0f;
            if (wasLoading)
            {
                Status = "Arena preparation cancelled";
                HoldAtLoadingScreen();
            }
        }

        private IEnumerator Prepare(int ticket, Action prepareArena, Action completed)
        {
            // Give the Canvas two complete frames before any synchronous build
            // or driver work. No input component or time scale is disabled.
            yield return null;
            if (!Continue(ticket)) yield break;
            Progress = .03f;
            yield return null;
            if (!Continue(ticket)) yield break;

            ResourceRequest request = LoadOptionalShaderCollection();
            if (request != null)
            {
                while (!request.isDone)
                {
                    if (!Continue(ticket)) yield break;
                    Progress = .05f + Mathf.Clamp01(request.progress) * .1f;
                    yield return null;
                }
            }
            if (!Continue(ticket)) yield break;
            Progress = .15f;
            ShaderVariantCollection variants = request != null ? request.asset as ShaderVariantCollection : null;
            if (variants != null && variants.variantCount > 0 && !variants.isWarmedUp)
            {
                Status = "Warming available shader variants";
                yield return null; // Present this status before calling the driver.
                if (!Continue(ticket)) yield break;
                int initial = variants.warmedUpVariantCount;
                int target = Mathf.Min(MaximumWarmupVariants, Mathf.Max(0, variants.variantCount - initial));
                int batches = 0, stalled = 0, previous = initial;
                while (!variants.isWarmedUp && batches * VariantsPerFrame < target)
                {
                    if (!Continue(ticket)) yield break;
                    if (!TryWarmBatch(variants, out bool finished)) break;
                    batches++;
                    int actual = variants.warmedUpVariantCount;
                    Progress = .15f + .2f * Mathf.Clamp01((float)(actual - initial) / Mathf.Max(1, target));
                    stalled = actual > previous ? 0 : stalled + 1;
                    previous = actual;
                    yield return null;
                    if (finished || stalled >= 2) break;
                }
            }
            if (!Continue(ticket)) yield break;
            Progress = .35f;
            Status = "Preparing arena";
            yield return null;
            if (!Continue(ticket)) yield break;
            if (!TryInvoke(prepareArena, "Arena construction failed.")) yield break;
            if (!Continue(ticket)) yield break;
            Progress = .7f;
            if (session.Graphics != null && !TryInvoke(session.Graphics.RefreshWorldSettings, "Arena graphics preparation failed.")) yield break;

            // These are real render opportunities for the active arena camera.
            // They cannot guarantee preparation of invisible materials or every
            // pipeline state on every graphics driver.
            for (int frame = 0; frame < 3; frame++)
            {
                yield return null;
                if (!Continue(ticket)) yield break;
                Progress = .7f + (frame + 1) * (.18f / 3f);
            }
            IsLocallyPrepared = true;
            SetWaitingForParty();
            if (completed != null && !TryInvoke(completed, "The party could not start its arena.")) yield break;
            while (Continue(ticket))
            {
                if (readyGate != null)
                {
                    if (!TryReadReadyGate(out bool ready)) yield break;
                    if (ready) { Complete(); yield break; }
                }
                yield return null;
            }
        }

        private bool Continue(int ticket)
        {
            if (ticket != generation || !IsLoading) return false;
            if (session == null || (sawStartedSession && !session.HasStarted) ||
                (IsLocallyPrepared && (session.Victory || session.IsDead)))
            { Cancel(); return false; }
            sawStartedSession |= session.HasStarted;
            if (requiresRoom && (session.Rooms == null || !session.Rooms.Connected))
            {
                Cancel();
                Status = "The connection ended. Return to the menu to reconnect.";
                return false;
            }
            HoldAtLoadingScreen();
            return true;
        }

        private void HoldAtLoadingScreen()
        {
            if (session == null) return;
            session.Paused = true;
            session.ReleasePointer();
        }

        private ResourceRequest LoadOptionalShaderCollection()
        {
            try { return Resources.LoadAsync<ShaderVariantCollection>(ShaderCollectionResource); }
            catch (Exception error)
            {
                Debug.LogWarning("Optional arena shader collection could not be loaded: " + error.Message);
                return null;
            }
        }

        private static bool TryWarmBatch(ShaderVariantCollection variants, out bool finished)
        {
            try { finished = variants.WarmUpProgressively(VariantsPerFrame); return true; }
            catch (Exception error)
            {
                finished = false;
                Debug.LogWarning("Optional arena shader warmup was skipped: " + error.Message);
                return false;
            }
        }

        private bool TryInvoke(Action callback, string failure)
        {
            try { callback(); return true; }
            catch (Exception error) { Fail(failure, error); return false; }
        }

        private bool TryReadReadyGate(out bool ready)
        {
            try { ready = readyGate(); return true; }
            catch (Exception error)
            {
                ready = false;
                Fail("The party readiness check failed.", error);
                return false;
            }
        }

        private void Fail(string status, Exception error)
        {
            Cancel();
            Status = status + " Return to the menu and try again.";
            if (session != null) session.FailArenaPreparation(Status);
            Debug.LogException(error);
        }

        private void OnDisable() { Cancel(); }
    }
}

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: existing loading UI and main-thread cursor control.
// Array Formats: no new data containers.
// Dependencies/Pointers: GameSession exclusively releases capture intent, including during loading; no coroutine capture request.
