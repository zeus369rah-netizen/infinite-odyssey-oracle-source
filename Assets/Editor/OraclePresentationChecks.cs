#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace OdysseyOracle.Editor
{
    // # ARCHITECTURE SNAPSHOT
    // Editor-only, explicit root entry points. Canvas/reference units and world
    // metres are inspected through public APIs. No private reflection, synthetic
    // input, scene creation, native allocation, asset writes or polling loops.
    // The separately named preset probe mutates/saves ONE preset per call; root
    // must inspect it on a later rendered frame and explicitly restore it.
    public static class OraclePresentationChecks
    {
        [Serializable] public sealed class Check
        { public string name,status="running",details="",exception=""; public int assertions; }
        [Serializable] public sealed class Report
        {
            public string status,utc,execution="Native Editor fixture only; not browser, visual approval or FPS acceptance.";
            public int frame,passed,failed,deferred,assertions,fireEmitters,pooledFireLights,emissiveMaterials,uniqueShaders;
            public Check[] checks;
        }
        static readonly string[] Elements={"Fire","Water","Earth","Air"};
        static bool probeActive,probeChecked;
        static GameSession probeSession;
        static int originalPreset,appliedPreset,appliedFrame;
        static int lastMutationFrame=-1;

        /// <summary>Read-only game checks. A closed book is explicitly deferred, never counted as readable text.</summary>
        public static string RunReadOnly(int expectedFireEmitterCount=0)
        {
            var report=NewReport();var checks=new List<Check>(12);GameSession session=GameSession.Instance;
            Run(report,checks,"live_session_and_references",c=>Context(c,session));
            if(report.failed!=0)return Finish(report,checks);
            Run(report,checks,"normal_scene_and_domain_reload_policy",c=>{
                var disabled=EnterPlayModeOptions.DisableDomainReload|EnterPlayModeOptions.DisableSceneReload;
                Require(c,!EditorSettings.enterPlayModeOptionsEnabled||(EditorSettings.enterPlayModeOptions&disabled)==0,
                    "Both normal scene and domain reload must be enabled for this runtime-created scene.");
                c.details="Editor policy only: component teardown still owns native disposal and delegate detachment.";
            });
            Run(report,checks,"four_named_sprites_and_live_spell_bindings",c=>Sprites(c,session));
            Run(report,checks,"paper_book_structure_and_callbacks",c=>BookStructure(c,session));
            Run(report,checks,"safe_canvas_scale_and_minimap_geometry",c=>Interface(c,session));
            Run(report,checks,"four_real_preset_controls",c=>{
                Require(c,GraphicsPreferences.PresetCount==4&&GraphicsPresetCatalog.Count==4,"Exactly four runtime presets are expected.");
                for(int i=0;i<4;i++){
                    Require(c,GraphicsPreferences.PresetNames[i]==GraphicsPresetCatalog.Get(i).Name,"Preset label must use the real catalog.");
                    var button=Named<Button>(session.Hud.transform,"Preset"+i);
                    Require(c,button!=null&&button.targetGraphic!=null,"Actual preset button "+i+" must exist.");
                }
                Require(c,session.Graphics.Preset>=0&&session.Graphics.Preset<4,"Actual selected preset must be bounded.");
                c.details="Public GraphicsPreferences API read only; no preset or preference was changed.";
            });
            Run(report,checks,"bounded_four_point_fire_pool",c=>FirePool(c,session,report,expectedFireEmitterCount));
            Run(report,checks,"live_shrine_emission_and_shader_errors",c=>Materials(c,session,report));
            Run(report,checks,"retained_presentation_resources",c=>{
                var logo=Resources.Load<Sprite>("Interface/OracleReturnLogo");
                Require(c,logo!=null&&logo.texture!=null,"Original logo Sprite must load.");
                foreach(string path in new[]{"Audio/WorldCue/book_write","Audio/WorldCue/torch_crackle"}){
                    var clip=Resources.Load<AudioClip>(path);Require(c,clip!=null&&clip.samples>0&&clip.length>0,"Missing actual audio resource: "+path);
                }
                foreach(string path in new[]{"Shaders/PortalAnomaly","Shaders/OracleNightSky"}){
                    var shader=Resources.Load<Shader>(path);Require(c,shader!=null&&shader.isSupported&&!ShaderUtil.ShaderHasError(shader),"Retained shader is missing/unsupported/has an error: "+path);
                }
            });
            var ink=session.Hud.GetComponentInChildren<OracleInkRevealText>(true);
            if(ink!=null&&ink.isActiveAndEnabled&&ink.gameObject.activeInHierarchy)
                Run(report,checks,"actual_visible_book_text",c=>VisibleBook(c,session,ink));
            else{
                checks.Add(new Check {name="actual_visible_book_text",status="deferred",details="Root must open a real lore book, let one frame render, then call RunVisibleBookReadOnly(). No UI action was dispatched."});report.deferred++;
            }
            return Finish(report,checks);
        }

        /// <summary>Requires root to open a real book first; does not fill, finish, scroll or close it.</summary>
        public static string RunVisibleBookReadOnly()
        {
            var report=NewReport();var checks=new List<Check>(2);var session=GameSession.Instance;
            Run(report,checks,"live_session_and_references",c=>Context(c,session));
            if(report.failed==0)Run(report,checks,"actual_visible_book_text",c=>VisibleBook(c,session,session.Hud.GetComponentInChildren<OracleInkRevealText>(true)));
            return Finish(report,checks);
        }

        static void Context(Check c,GameSession session)
        {
            Require(c,Application.isPlaying,"Use a stable native Editor Play frame.");
            Require(c,session!=null&&session.isActiveAndEnabled,"An active GameSession is required.");
            int count=0;foreach(var item in UnityEngine.Object.FindObjectsByType<GameSession>(FindObjectsInactive.Exclude))if(item.isActiveAndEnabled)count++;
            Require(c,count==1,"Exactly one enabled session may own runtime bindings.");
            Require(c,session.Player!=null&&session.Player.ViewCamera!=null&&session.World!=null&&session.World.gameObject.activeInHierarchy,"Player camera and active procedural world must exist.");
            Require(c,session.Hud!=null&&session.Graphics!=null&&session.Audio!=null&&session.Progression!=null&&session.Books!=null,"Presentation and progression references must be populated.");
            Require(c,session.Combat!=null&&session.Combat.Shrine!=null&&session.Loading!=null,"Shrine and loader references must exist.");
            Require(c,!session.Loading.IsLoading,"Wait for loading to complete before these checks.");
            Require(c,Screen.width>0&&Screen.height>0,"A rendered viewport is required.");
        }
        static void Sprites(Check c,GameSession session)
        {
            var loaded=Resources.LoadAll<Sprite>("Interface/ElementEmblems");
            Require(c,loaded.Length==4,"The promoted emblem resource must contain exactly four Sprite slices.");
            for(int i=0;i<4;i++){
                Sprite sprite=OracleElementEmblems.Get(i);Require(c,sprite!=null&&sprite.name==Elements[i]&&sprite.texture!=null,"Exact Sprite name/index mismatch: "+Elements[i]);
                Require(c,sprite.rect.width>0&&sprite.rect.height>0,"Sprite rect must have actual pixels.");
                int matches=0;foreach(var item in loaded)if(item!=null&&item.name==Elements[i])matches++;
                Require(c,matches==1,"Each exact name must appear once.");
                var slot=Named<RectTransform>(session.Hud.transform,"Spell"+(i+1));Require(c,slot!=null,"Actual spell slot is missing.");
                var image=Named<Image>(slot,"AbilitySigil");
                Require(c,image!=null&&image.sprite==sprite&&image.preserveAspect&&!image.raycastTarget,"Real spell Image must bind its own Sprite with safe aspect/raycast settings.");
                for(int j=0;j<i;j++)Require(c,OracleElementEmblems.Get(j)!=sprite,"Elements must not alias a Sprite slice.");
            }
            Require(c,OracleElementEmblems.Get(-1)==null&&OracleElementEmblems.Get(4)==null,"Invalid indices must not alias Fire.");
        }
        static void BookStructure(Check c,GameSession session)
        {
            var all=session.Hud.GetComponentsInChildren<OracleInkRevealText>(true);Require(c,all.Length==1,"Exactly one reused book reveal component is expected.");
            var ink=all[0];Require(c,ink.font!=null&&ink.fontSize>=18&&!ink.resizeTextForBestFit,"Book needs a real font and readable fixed logical size.");
            Require(c,ink.color.a>=.95f&&ink.raycastTarget,"Opaque base ink and click-to-finish target must exist.");
            Require(c,ink.WritingStateChanged!=null&&ink.RevealStateChanged!=null,"Actual HUD callbacks must survive the current Play lifecycle.");
            var scroll=ink.GetComponentInParent<ScrollRect>(true);Require(c,scroll!=null&&scroll.viewport!=null&&scroll.content!=null,"Book must use a real viewport/content pair.");
            Require(c,scroll.vertical&&!scroll.horizontal&&scroll.movementType==ScrollRect.MovementType.Clamped,"Book scrolling must remain vertical and clamped.");
            Require(c,ink.transform.IsChildOf(scroll.content)&&scroll.content.IsChildOf(scroll.viewport),"Actual book text must be beneath its clipping viewport.");
            Require(c,scroll.viewport.GetComponent<Mask>()!=null||scroll.viewport.GetComponent<RectMask2D>()!=null,"An actual UI mask must clip the text.");
            var paper=Named<Image>(session.Hud.transform,"RefugeLoreBook");Require(c,paper!=null&&paper.color.a>=.99f,"An opaque ivory paper background must exist.");
            Require(c,Contrast(ink.color,paper.color)>=4.5,"Actual ink/paper color contrast is below4.5:1.");
            Require(c,Named<Button>(session.Hud.transform,"ShowFullBookText")!=null&&Named<Button>(session.Hud.transform,"ToggleInstantBookText")!=null,"Finish and instant-text controls must exist.");
            if(!ink.gameObject.activeInHierarchy)Require(c,!session.Hud.IsBookWriting,"Closed book must not report active pencil writing.");
        }
        static void VisibleBook(Check c,GameSession session,OracleInkRevealText ink)
        {
            Require(c,ink!=null&&ink.isActiveAndEnabled&&ink.gameObject.activeInHierarchy,"Open a real book first, then wait one rendered frame.");
            BookStructure(c,session);
            Require(c,!string.IsNullOrWhiteSpace(ink.text)&&ink.text.Trim().Length>=10,"Actual book content must be present, not an empty test placeholder.");
            Require(c,!ink.supportRichText,"Promoted ink reveal must render its full plain book string.");
            Require(c,ink.cachedTextGenerator.characterCountVisible>0&&ink.cachedTextGenerator.vertexCount>0,"The real book string must have generated glyph geometry.");
            var scroll=ink.GetComponentInParent<ScrollRect>(true);
            Require(c,ink.rectTransform.rect.width>0&&scroll.viewport.rect.height>100,"Book layout must have positive usable bounds.");
            Require(c,ink.rectTransform.rect.width<=scroll.viewport.rect.width+1f,"Book text must fit within the clipping viewport width.");
            if(!ink.IsRevealing)Require(c,!session.Hud.IsBookWriting,"A completed book must stop its writing state.");
            c.details="characters="+ink.text.Length+"; generated="+ink.cachedTextGenerator.characterCountVisible+"; revealing="+ink.IsRevealing+"; no text/alpha/scroll/selection change performed";
        }
        static void Interface(Check c,GameSession session)
        {
            var canvas=Named<Canvas>(session.Hud.transform,"OracleInterface");Require(c,canvas!=null&&canvas.isActiveAndEnabled&&canvas.renderMode==RenderMode.ScreenSpaceOverlay,"Actual sharp overlay Canvas must exist.");
            int canvasCount=0;foreach(var item in session.Hud.GetComponentsInChildren<Canvas>(true))if(item.name=="OracleInterface")canvasCount++;
            Require(c,canvasCount==1,"Repeated initialization must not create duplicate OracleInterface Canvas roots.");
            var preferences=canvas.GetComponent<OracleInterfacePreferences>();var scaler=canvas.GetComponent<CanvasScaler>();
            Require(c,preferences!=null&&scaler!=null,"Actual interface settings/scaler bindings must exist.");
            Require(c,Finite(preferences.UiScale)&&preferences.UiScale>=.75f&&preferences.UiScale<=1.35f,"UI scale must stay in its actual bounded range.");
            Require(c,scaler.uiScaleMode==CanvasScaler.ScaleMode.ScaleWithScreenSize&&
                (scaler.referenceResolution-OracleInterfacePreferences.BaseReferenceResolution/preferences.UiScale).sqrMagnitude<.1f,"UI scale must actually affect Canvas reference resolution.");
            Require(c,preferences.MinimapSize>=0&&preferences.MinimapSize<=2&&Finite(preferences.MinimapOpacity)&&preferences.MinimapOpacity>=.2f&&preferences.MinimapOpacity<=1f,"Actual minimap preferences must be bounded.");
            var map=Named<RectTransform>(session.Hud.transform,"CatacombMap");var footprint=Named<RectTransform>(session.Hud.transform,"ActualDungeonFootprint");
            Require(c,map!=null&&footprint!=null&&map.rect.width>0&&map.rect.height>0,"Actual minimap geometry must exist.");
            Require(c,footprint.GetComponent<RectMask2D>()!=null,"Moving map markers must have a real clip mask.");
            Require(c,Finite(map.localScale.x)&&map.localScale.x>=.44f&&map.localScale.x<=1.24f,"Fitted minimap scale must be finite and bounded.");
            var opacity=map.GetComponent<CanvasGroup>();Require(c,opacity!=null&&Mathf.Abs(opacity.alpha-preferences.MinimapOpacity)<.01f&&!opacity.blocksRaycasts,"Minimap opacity must affect actual UI without blocking controls.");
            var root=(RectTransform)canvas.transform;var corners=new Vector3[4];map.GetWorldCorners(corners);
            foreach(var corner in corners){var p=root.InverseTransformPoint(corner);Require(c,p.x>=root.rect.xMin-2&&p.x<=root.rect.xMax+2&&p.y>=root.rect.yMin-2&&p.y<=root.rect.yMax+2,"Minimap exceeds the CURRENT rendered Canvas bounds.");}
            c.details="viewport="+Screen.width+"x"+Screen.height+"; uiScale="+preferences.UiScale+"; mapScale="+map.localScale.x+"; this is not an untested resolution matrix";
        }
        static void FirePool(Check c,GameSession session,Report report,int expected)
        {
            Require(c,!session.World.IsCoopArena,"This finite fixture expects the promoted tutorial world, not an arena.");
            var pools=UnityEngine.Object.FindObjectsByType<TutorialFireLightPool>(FindObjectsInactive.Exclude);
            int count=0;TutorialFireLightPool pool=null;foreach(var item in pools)if(item.isActiveAndEnabled){count++;pool=item;}
            Require(c,count==1&&pool!=null,"Exactly one active tutorial fire pool must own the reusable points.");
            report.fireEmitters=pool.RegisteredEmitterCount;
            Require(c,report.fireEmitters>=4&&report.fireEmitters<=FireLightSelector.MaximumEmitters,"Fixture/candle emitter metadata must be present and within128 capacity.");
            if(expected>0)Require(c,report.fireEmitters==expected,"Emitter registration differs from root's explicit current geometry count.");
            var spec=GraphicsPresetCatalog.Get(session.Graphics.Preset);
            Require(c,pool.LightBudget==Mathf.Min(4,spec.AdditionalLights),"The live pool budget must follow the actual preset, capped at four.");
            int physical=0;foreach(var light in pool.GetComponentsInChildren<Light>(true))if(light.name.StartsWith("Reused tutorial fire spill ",StringComparison.Ordinal)){
                physical++;Require(c,light.type==LightType.Point&&light.shadows==LightShadows.None,"Pooled fire points must never add shadows.");
                if(light.enabled)Require(c,light.color.r>light.color.b&&light.color.r>=light.color.g,"An enabled actual spill must be warm orange.");
            }
            report.pooledFireLights=physical;Require(c,physical==4,"Exactly four reused point-light objects are expected, independently of emitter count.");
            Require(c,pool.ActiveLightCount<=pool.LightBudget,"Enabled fire lights must stay within the actual selected budget.");
            c.details="emitters="+report.fireEmitters+"; physicalPoints="+physical+"; budget="+pool.LightBudget+"; enabled="+pool.ActiveLightCount+"; no hardcoded35/chandelier flame count";
        }
        static void Materials(Check c,GameSession session,Report report)
        {
            var seen=new HashSet<Material>();var shaders=new HashSet<Shader>();int shrineGlow=0;
            foreach(var renderer in session.World.GetComponentsInChildren<Renderer>(false))foreach(var material in renderer.sharedMaterials)if(material!=null)seen.Add(material);
            foreach(var renderer in session.Combat.Shrine.GetComponentsInChildren<Renderer>(true))foreach(var material in renderer.sharedMaterials)if(material!=null)seen.Add(material);
            Require(c,seen.Count>0&&seen.Count<=2048,"Bounded live world/shrine materials must exist.");
            foreach(var material in seen){
                Require(c,material.shader!=null&&material.shader.isSupported,"A live material uses a missing/unsupported shader: "+material.name);shaders.Add(material.shader);
                if(!material.HasProperty("_EmissionColor")||material.GetColor("_EmissionColor").maxColorComponent<=.001f)continue;
                report.emissiveMaterials++;
                var keyword=material.shader.keywordSpace.FindKeyword("_EMISSION");
                if(material.IsKeywordEnabled("_EMISSION"))Require(c,keyword.isValid,"An enabled emission keyword must actually exist in the bound shader: "+material.name);
                if(keyword.isValid)Require(c,material.IsKeywordEnabled("_EMISSION"),"Positive emission requires the shader's real local keyword: "+material.name);
                if(material.name.StartsWith("Ward ",StringComparison.Ordinal))shrineGlow++;
            }
            foreach(var shader in shaders)Require(c,!ShaderUtil.ShaderHasError(shader),"Actual material shader has a compiler error: "+shader.name);
            report.uniqueShaders=shaders.Count;Require(c,shrineGlow>=3,"The actual sacred shrine needs multiple glowing materials.");
            var ward=Named<Light>(session.Combat.Shrine.transform,"Living ward green illumination");
            Require(c,ward!=null&&ward.type==LightType.Point&&ward.shadows==LightShadows.None&&ward.color.g>ward.color.r,"Actual green ward point light must remain present and nonshadowing.");
            c.details="uniqueShaders="+shaders.Count+"; glowingMaterials="+report.emissiveMaterials+"; shrineGlow="+shrineGlow+"; ShaderHasError checked on bound shaders, not a browser shader acceptance claim";
        }

        /// <summary>MUTATING: applies/saves exactly one real preset; never calls a button or waits.</summary>
        public static string ApplyOnePresetForNextFrame(int preset)
        {
            var report=NewReport();var checks=new List<Check>(1);
            Run(report,checks,"MUTATING_apply_one_preset",c=>{
                var session=GameSession.Instance;Context(c,session);Require(c,preset>=0&&preset<4,"Choose an actual preset0..3.");
                Require(c,Time.frameCount>lastMutationFrame,"Only one preset mutation is allowed per Play frame.");
                Require(c,!probeActive||probeChecked,"Inspect the previous applied preset on a later frame before another mutation.");
                if(!probeActive){Require(c,!session.Graphics.PresetIsCustomized,"Begin from a plain preset so one SetPreset call can restore it exactly; custom state was not changed.");originalPreset=session.Graphics.Preset;probeSession=session;probeActive=true;}
                Require(c,session==probeSession,"A new Play session cannot reuse the previous preset probe.");
                appliedPreset=preset;appliedFrame=lastMutationFrame=Time.frameCount;probeChecked=false;session.Graphics.SetPreset(preset);
                c.details="preset="+preset+"; original="+originalPreset+"; frame="+appliedFrame+"; next call must be CheckAppliedPresetReadOnly on a later frame; restore explicitly before stopping Play";
            });return Finish(report,checks);
        }
        public static string CheckAppliedPresetReadOnly()
        {
            var report=NewReport();var checks=new List<Check>(1);
            Run(report,checks,"applied_preset_actual_public_state",c=>{
                Require(c,probeActive&&probeSession!=null&&GameSession.Instance==probeSession,"No live preset probe exists.");
                Require(c,Time.frameCount>appliedFrame,"A later rendered Play frame is required; this method does not poll/wait.");
                var graphics=probeSession.Graphics;var spec=GraphicsPresetCatalog.Get(appliedPreset);
                Require(c,graphics.Preset==appliedPreset&&graphics.PresetName==spec.Name,"The real selected preset must match.");
                Require(c,!graphics.AutoResolutionEnabled&&graphics.UsingFixedResolution&&!graphics.PresetIsCustomized,"A preset must actually disable adaptive scaling and be uncustomized.");
                Require(c,Mathf.Abs(graphics.ResolutionScale-spec.SceneScale)<.001f&&Mathf.Abs(graphics.EffectiveResolutionScale-spec.SceneScale)<.001f,"Actual scene scale must match the preset.");
                Require(c,graphics.BloomEnabled==spec.Bloom&&graphics.ParticleQuality==spec.ParticleQuality,"Bloom/particle settings must actually change.");
                Require(c,Mathf.Abs(graphics.ShadowDistance-spec.ShadowDistance)<.01f&&graphics.ShadowMapResolution==spec.ShadowResolution&&graphics.AdditionalLightsPerObject==spec.AdditionalLights&&graphics.OpaqueTextureEnabled==spec.Opaque,"Actual runtime pipeline budgets must match the catalog.");
                FirePool(c,probeSession,report,0);probeChecked=true;
                c.details="checked actual preset="+spec.Name+" on frame="+Time.frameCount+"; original="+originalPreset+" still requires explicit restoration";
            });return Finish(report,checks);
        }
        /// <summary>MUTATING: one SetPreset call restores the captured plain preset and its saved settings.</summary>
        public static string RestorePresetProbe()
        {
            var report=NewReport();var checks=new List<Check>(1);
            Run(report,checks,"MUTATING_restore_original_plain_preset",c=>{
                Require(c,probeActive&&probeSession!=null&&GameSession.Instance==probeSession,"Original Play context is gone; no new session was mutated. Check saved preferences explicitly.");
                Require(c,Time.frameCount>lastMutationFrame,"Restore on a later frame; only one preset mutation is allowed per Play frame.");
                lastMutationFrame=Time.frameCount;probeSession.Graphics.SetPreset(originalPreset);c.details="Restored original plain preset="+originalPreset+"; verify it on a later frame if needed.";
                probeActive=probeChecked=false;probeSession=null;
            });return Finish(report,checks);
        }

        static T Named<T>(Transform root,string name) where T:Component
        {foreach(var item in root.GetComponentsInChildren<T>(true))if(item.name==name)return item;return null;}
        static float Linear(float value)=>value<=.04045f?value/12.92f:Mathf.Pow((value+.055f)/1.055f,2.4f);
        static float Luminance(Color value)=>.2126f*Linear(value.r)+.7152f*Linear(value.g)+.0722f*Linear(value.b);
        static float Contrast(Color a,Color b){float x=Luminance(a),y=Luminance(b);return(Mathf.Max(x,y)+.05f)/(Mathf.Min(x,y)+.05f);}
        static bool Finite(float value)=>!float.IsNaN(value)&&!float.IsInfinity(value);
        static Report NewReport()=>new Report{utc=DateTime.UtcNow.ToString("o"),frame=Time.frameCount};
        static void Require(Check check,bool condition,string message){check.assertions++;if(!condition)throw new InvalidOperationException(message);}
        static void Run(Report report,List<Check> checks,string name,Action<Check> action)
        {var check=new Check{name=name};checks.Add(check);try{action(check);check.status="passed";report.passed++;}catch(Exception error){check.status="failed";check.exception=error.GetType().Name+": "+error.Message;report.failed++;}report.assertions+=check.assertions;}
        static string Finish(Report report,List<Check> checks){report.status=report.failed>0?"failed":report.deferred>0?"passed_with_deferred_checks":"passed";report.checks=checks.ToArray();return JsonUtility.ToJson(report);}
    }
}
#endif
