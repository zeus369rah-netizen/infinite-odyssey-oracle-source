using System;
using System.Reflection;
using UnityEngine;
using OdysseyOracle;

// ROOT RUNS ONLY after Stop/import. Uses actual selection and actual Light.enabled;
// no preferences or scene assets are changed. Requires a focused prepared session.
public static class PausedFireBudgetChecks
{
    // Focus-independent setter regression. Manual enabled flags intentionally emulate
    // a previously selected/frozen pool; this does not claim real nearest selection.
    public static string RunBudgetSetterOnly()
    {
        if (!Application.isPlaying) throw new InvalidOperationException("Run the native isolated setter fixture in Play mode.");
        GameObject root = null;
        TutorialFireLightPool pool = null;
        int assertions = 0;
        Action<bool, string> require = (ok, label) => {
            assertions++;
            if (!ok) throw new InvalidOperationException(label);
        };
        try
        {
            root = new GameObject("Transient focus-independent fire budget checks") {
                hideFlags = HideFlags.HideAndDontSave
            };
            // Keep synthetic native lights far outside the actual dungeon/arena.
            root.transform.position = new Vector3(2000f, 2000f, 2000f);
            pool = root.AddComponent<TutorialFireLightPool>();
            pool.Initialize(root.transform, null, 1700087000);
            Light[] lights = root.GetComponentsInChildren<Light>(true);
            require(lights.Length == 4 && pool.NativeStorageCreated, "Four actual isolated native Lights and pool storage exist");
            pool.SetLightBudget(4);
            foreach (Light light in lights) light.enabled = true;
            require(pool.ActiveLightCount == 4, "Manual native enabled flags emulate a frozen four-slot pool");
            pool.SetLightBudget(2);
            require(pool.LightBudget == 2 && pool.ActiveLightCount == 2 && Enabled(root) == 2,
                "Public setter enforces 4 to 2 immediately without Update, focus or a session");
            pool.SetLightBudget(1);
            require(pool.ActiveLightCount == 1, "Public setter enforces 2 to 1 immediately");
            pool.SetLightBudget(0);
            require(pool.ActiveLightCount == 0, "Public setter enforces 1 to 0 immediately");
            pool.SetLightBudget(4);
            require(pool.ActiveLightCount == 0, "Budget increase cannot enable unselected native Lights");
            foreach (Light light in lights) light.enabled = true;
            pool.SetLightBudget(2);
            require(pool.ActiveLightCount == 2, "A resumed/manual four-light state can be reduced again without selection");
            pool.SetLightBudget(-100);
            require(pool.LightBudget == 0 && pool.ActiveLightCount == 0, "Negative budget is clamped and enforced synchronously");
            pool.SetLightBudget(100);
            require(pool.LightBudget == 4 && pool.ActiveLightCount == 0 && root.GetComponentsInChildren<Light>(true).Length == 4,
                "Upper clamp retains the same bounded Light objects without enabling them");
            pool.PrepareForReload();
            require(!pool.NativeStorageCreated && Enabled(root) == 0, "Actual native ownership is disposed before fixture retirement");
            return "PASS: " + assertions + " actual focus-independent public-setter assertions. " +
                "Lights were manually enabled; nearest selection, rendering and visible contribution were not tested.";
        }
        finally
        {
            if (pool != null) pool.PrepareForReload();
            if (root != null) UnityEngine.Object.DestroyImmediate(root);
        }
    }

    public static string Run(GameSession owner)
    {
        if (!Application.isPlaying || !Application.isFocused)
            throw new InvalidOperationException("Run in focused Play mode after importing the candidate.");
        if (owner == null || owner.Player == null || owner.Player.ViewCamera == null)
            throw new ArgumentException("A prepared session camera is required.", nameof(owner));
        if (owner.Rooms != null && owner.Rooms.Connected)
            throw new InvalidOperationException("Use a disconnected local fixture, not a live shared session.");
        bool wasStarted = owner.HasStarted, wasPaused = owner.Paused;
        GameObject root = null;
        TutorialFireLightPool pool = null;
        int assertions = 0;
        Action<bool, string> require = (ok, label) => {
            assertions++;
            if (!ok) throw new InvalidOperationException(label);
        };
        try
        {
            root = new GameObject("Transient paused fire budget regression") {
                hideFlags = HideFlags.HideAndDontSave
            };
            // Place the isolated fixture at the actual camera so all four records
            // enter the real nearest-emitter selection; no synthetic enabled flags.
            root.transform.position = owner.Player.ViewCamera.transform.position;
            pool = root.AddComponent<TutorialFireLightPool>();
            pool.Initialize(root.transform, owner, 1700088000);
            for (int i = 0; i < 4; i++)
                require(pool.RegisterEmitter(new Vector3(i * .1f, 0f, 0f),
                    TutorialFireLightPool.Fixture.Torch, i), "Four actual emitter records accepted");
            owner.HasStarted = true;
            owner.Paused = false;
            pool.SetLightBudget(4);
            Update(pool);
            require(pool.ActiveLightCount == 4 && Enabled(root) == 4,
                "Real selection lights all four physical Light objects before reduction");
            owner.Paused = true;
            pool.SetLightBudget(2);
            require(pool.LightBudget == 2 && pool.ActiveLightCount == 2 && Enabled(root) == 2,
                "Paused 4 to 2 reduction disables excess Lights synchronously without Update");
            Update(pool);
            require(Enabled(root) == 2, "Paused early return cannot retain or re-enable excess Lights");
            pool.SetLightBudget(1);
            require(Enabled(root) == 1, "Paused 2 to 1 reduction is synchronous");
            pool.SetLightBudget(0);
            require(Enabled(root) == 0, "Paused 1 to 0 reduction is synchronous");
            pool.SetLightBudget(4);
            require(Enabled(root) == 0, "Increasing budget while paused does not invent a selection");
            owner.Paused = false;
            Update(pool);
            require(Enabled(root) == 4, "Resumed real selection can safely restore all four records");
            pool.SetLightBudget(2);
            require(Enabled(root) == 2, "Unpaused reduction is synchronous too");
            owner.Paused = true;
            pool.SetLightBudget(-1);
            require(pool.LightBudget == 0 && Enabled(root) == 0, "Negative budget clamps and disables immediately");
            return "PASS: " + assertions + " actual paused Light-budget assertions. " +
                "Focus-loss uses the same early-return path; real OS focus-loss was not simulated.";
        }
        finally
        {
            if (pool != null) pool.PrepareForReload();
            if (root != null) UnityEngine.Object.DestroyImmediate(root);
            owner.HasStarted = wasStarted;
            owner.Paused = wasPaused;
        }
    }

    private static void Update(TutorialFireLightPool pool)
    {
        MethodInfo method = typeof(TutorialFireLightPool).GetMethod("Update",
            BindingFlags.Instance | BindingFlags.NonPublic);
        if (method == null) throw new MissingMethodException("TutorialFireLightPool.Update");
        method.Invoke(pool, null);
    }

    private static int Enabled(GameObject root)
    {
        int count = 0;
        foreach (Light light in root.GetComponentsInChildren<Light>(true))
            if (light.enabled) count++;
        return count;
    }
}
