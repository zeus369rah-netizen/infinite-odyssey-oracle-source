using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace OdysseyOracle
{
    /// <summary>
    /// Original voxel interpretation of the requested closed charcoal glove and
    /// framed green crystal, retaining Oracle's emerald cloth, brass and four-point
    /// sigil. Identity reference: oracle-source-v14 / oracle-class-paint-v055
    /// and the original four-point Oracle sigil in native-champion-sigils-v91.
    /// Everything is camera local, has no collider, and casts no world shadows.
    /// </summary>
    public sealed partial class OracleViewmodel : MonoBehaviour
    {
        const int Emerald = 0, Fold = 1, Brass = 2, BrassDark = 3, Glove = 4,
            GloveShade = 5, Wood = 6, Wrap = 7, Inlay = 8, Crystal = 9,
            CrystalShade = 10, SigilInk = 11, BrassEdge = 12;
        const int SparkCount = 12;
        const int ArcCount = 4, ArcSteps = 11;
        static readonly Vector3 RestPosition = new Vector3(.35f, -.36f, .72f);
        static readonly Quaternion RestRotation = Quaternion.Euler(-9f, -8f, 10f);
        static readonly Color Aether = new Color(.26f, 1f, .49f);
        static readonly float[] CastRecovery = {12f,11f,8f,17f};
        static readonly float[] CastStrength = {1f,.75f,1.35f,.55f};
        static readonly Color[] ElementAccents = {
            new Color(1f,.36f,.12f),new Color(.18f,.68f,1f),
            new Color(.62f,.78f,.29f),new Color(.66f,.91f,1f)
        };
        readonly List<Object> owned = new List<Object>();
        readonly RaycastHit[] obstructionHits = new RaycastHit[8];
        readonly Vector3[] sparkVertices = new Vector3[SparkCount * 6];
        readonly Vector3[] sparkNormals = new Vector3[SparkCount * 6];
        readonly Vector3[] arcVertices = new Vector3[ArcCount * (ArcSteps - 1) * 4];
        readonly GameObject[] elementGlyphs = new GameObject[4];
        OraclePlayer player;
        Camera viewCamera;
        Transform cameraRig, wandRoot;
        GameObject visualRoot;
        Mesh sparks, electricArcs;
        Material focusPaint, facetPaint, inlayPaint, sparkPaint, electricPaint;
        // # ARCHITECTURE SNAPSHOT
        // Camera-local cosmetic state only. Combat owns accepted bits/readiness; the optional Nova adapter
        // reads that state and the own accepted-cast counter. No cooldown, damage, visibility unlock or save mutation.
        // One cached MPB serves existing material slots; four arc submeshes borrow the same existing material.
        MeshRenderer bodyRenderer, sparkRenderer, arcRenderer;
        MaterialPropertyBlock appearanceBlock;
        Bounds presentationBounds;
        Vector3 outerHandProbe, gripProbe;
        ConfirmedWandAppearanceState confirmedAppearance;
        int ingredientMask => confirmedAppearance.AcceptedMask;
        int confirmedMask => confirmedAppearance.ConfirmedMask;
        bool finisherCue => confirmedAppearance.FinisherReady;
        float novaPulseAge = 10f;
        static readonly int BaseColorId=Shader.PropertyToID("_BaseColor"),ColorId=Shader.PropertyToID("_Color"),EmissionId=Shader.PropertyToID("_EmissionColor");
        static readonly Color PrismaticFocus=new Color(.72f,.58f,1f),NovaWhite=new Color(.90f,.95f,1f);
        public int DisplayedConfirmedMask => confirmedMask;
        public int DisplayedIngredientMask => ingredientMask;
        public bool FinisherCueVisible => finisherCue;
        public float CameraClearanceScale => clearance;
        public Bounds LocalPresentationBounds => presentationBounds;
        Color gemAccent = Aether;
        Vector3 lastPosition, previousAngles, sway, walkingOffset;
        float clock, movement, kickAge = 10f, clearance = 1f;
        int lastSpell, selectedElement = -1;

        public void Initialize(OraclePlayer owner)
        {
            DisposeVisuals();
            player = owner;
            appearanceBlock = new MaterialPropertyBlock();
            confirmedAppearance=default; novaPulseAge=10f;
            if (owner == null || owner.ViewCamera == null) return;
            viewCamera = owner.ViewCamera;
            visualRoot = new GameObject("Oracle first-person hands and wrapped wand");
            cameraRig = visualRoot.transform;
            cameraRig.SetParent(viewCamera.transform, false);
            CreateHandPivot015();
            wandRoot = new GameObject("Right hand and astral focus").transform;
            wandRoot.SetParent(nativeHandPivot015, false);
            wandRoot.localPosition = RestPosition;
            wandRoot.localRotation = RestRotation;

            var cloth = MakeTexture("Original Oracle pixel weave", 64, 0);
            var timber = MakeTexture("Original Oracle dark timber", 32, 1);
            var metal = MakeTexture("Original worn astral brass", 32, 2);
            var paints = new[] {
                Paint("Emerald vestment", new Color(.075f,.27f,.19f), .06f, .31f, cloth),
                Paint("Charcoal green folds", new Color(.025f,.075f,.058f), .01f, .22f, cloth),
                Paint("Worn astral brass", new Color(.81f,.60f,.20f), .62f, .39f, metal),
                Paint("Recessed astral brass", new Color(.29f,.20f,.075f), .60f, .32f, metal),
                Paint("Charcoal glove raised knuckles", new Color(.16f,.18f,.17f), .02f, .22f, cloth),
                Paint("Charcoal glove recessed fingers", new Color(.060f,.073f,.067f), 0f, .17f, cloth),
                Paint("Original dark wand shaft", new Color(.11f,.078f,.045f), .03f, .27f, timber),
                Paint("Wrapped wand grip", new Color(.12f,.18f,.14f), 0f, .15f, cloth),
                Paint("Green turquoise focus inlays", new Color(.075f,.62f,.40f), .30f, .46f, null),
                Paint("Living green light focus", new Color(.22f,.93f,.28f), .12f, .50f, null),
                Paint("Faceted emerald focus", new Color(.035f,.36f,.12f), .18f, .43f, null),
                Paint("Dark elemental focus engraving", new Color(.009f,.027f,.017f), 0f, .18f, null),
                Paint("Bevelled gold frame edges", new Color(.98f,.77f,.29f), .45f, .36f, metal)
            };
            focusPaint = paints[Crystal];
            facetPaint = paints[CrystalShade];
            inlayPaint = paints[Inlay];
            SetEmission(focusPaint, Aether * .75f);
            SetEmission(facetPaint, Aether * .13f);
            SetEmission(inlayPaint, Aether * .30f);
            var shape = new ShapeBuilder(paints.Length);
            BuildWand(shape);
            int armStart020 = shape.VertexCount;
            BuildArm(shape);
            Vector3 cameraRight020 = Quaternion.Inverse(RestRotation) * Vector3.right;
            if (!OracleViewmodelGrip020.TryRotation(new float3(0f, 1f, 0f),
                new float3(cameraRight020.x, cameraRight020.y, cameraRight020.z),
                new float3(0f, 1f, 0f), new float3(1f, 0f, 0f),
                OracleViewmodelGrip020.GripRadians, out quaternion grip020))
                throw new System.InvalidOperationException("Invalid authored right-hand grip frame.");
            shape.RotateFrom(armStart020, new Quaternion(grip020.value.x, grip020.value.y, grip020.value.z, grip020.value.w));
            var bodyMesh = shape.Finish("Oracle bevel-voxel hand, cuff and focus");
            owned.Add(bodyMesh);
            var body = new GameObject("Combined Oracle viewmodel");
            body.transform.SetParent(wandRoot, false);
            body.AddComponent<MeshFilter>().sharedMesh = bodyMesh;
            bodyRenderer=body.AddComponent<MeshRenderer>();
            ConfigureRenderer(bodyRenderer, paints);
            BuildElementGlyphs(paints[SigilInk]);
            BuildSparks();
            BuildElectricArcs();
            BuildStaffSpiral(bodyMesh, armStart020);
            CachePresentationBounds(bodyMesh);
            PrepareHandsCold015();
            lastPosition = owner.transform.position;
            previousAngles = viewCamera.transform.eulerAngles;
            kickAge = 10f;
            clearance = 1f;
            clock = movement = 0f;
            sway = Vector3.zero;
            walkingOffset = Vector3.zero;
            selectedElement = -1;
            RefreshElementAppearance();
            UpdateSparks();
            UpdateElectricArcs();
            visualRoot.SetActive(CanShowStaff020(GameSession.Instance));
        }

        public void PulseCast(int spellIndex) => PulseCast(spellIndex,1<<Mathf.Clamp(spellIndex,0,3));
        public void PulseCast(int spellIndex,int acceptedMask)
        {
            if (visualRoot == null || !StaffSpiralState.ValidMask(acceptedMask)) return;
            lastSpell = Mathf.Clamp(spellIndex, 0, 3);
            if((acceptedMask&(1<<lastSpell))==0)return;
            PulseNativeHands015();
            kickAge = 0f;
            staffAttackSway.Kick(CastStrength[lastSpell]*(acceptedMask==15?1.2f:1f));
            PulseAcceptedStaff(acceptedMask);
        }

        /// <summary>Legacy cosmetic input; confirmed casting uses SetConfirmedElementAppearance.</summary>
        public void SetElementChargeAppearance(int acceptedMask,bool finisherReady)
        {
            SetConfirmedElementAppearance(1<<Mathf.Clamp(selectedElement,0,3),acceptedMask,finisherReady);
        }

        /// <summary>Presentation only: confirmed one/two/four-bit choice and host/local accepted chain remain separate.</summary>
        public void SetConfirmedElementAppearance(int confirmedSelectionMask,int acceptedMask,bool authorityReady)
        {
            if(!confirmedAppearance.SetMasks(confirmedSelectionMask,acceptedMask,authorityReady))return;
            NotifyStaffSelection(confirmedMask);
            // Update at the same frozen pose even while a guide/menu pauses animation.
            UpdateFocusPaint(0f);UpdateSparks();UpdateElectricArcs();
        }

        /// <summary>Combat's own reset hook prevents replay across restart/trial/stage resets. Never changes visibility.</summary>
        public void ResetConfirmedAppearance(int acceptedNovaCount=0)
        {
            bool repaint=confirmedMask!=0||ingredientMask!=0||finisherCue||novaPulseAge<10f||staffSpiralState.PulseMask!=0;
            confirmedAppearance.Reset(acceptedNovaCount);novaPulseAge=10f;ResetStaffSpiral();ResetNativeHands015();
            if(!repaint)return;
            UpdateFocusPaint(0f);UpdateSparks();UpdateElectricArcs();
        }

        void RefreshConfirmedAppearance()
        {
            var session=GameSession.Instance;var combat=session!=null?session.Combat:null;
            if(combat==null){ResetConfirmedAppearance(-1);return;}
            if(!session.HasStarted||!session.CombatUnlocked||session.Victory||session.IsDowned)
            {ResetConfirmedAppearance(combat.OwnCosmicNovaCastCount);return;}
            // PreparedElement/SelectedSpell never enter the accepted ingredient accumulator.
            int choice=combat.HasConfirmedSelection?combat.ConfirmedMask:0;
            int accepted=ConfirmedWandAppearanceState.HasLiveChain(combat.NovaChainSeconds)?combat.NovaIngredientMask:0;
            SetConfirmedElementAppearance(choice,accepted,combat.CosmicNovaPrepared);
            // First observation and decreases are baselines. Only an accepted own launch increments this counter.
            if(confirmedAppearance.ObserveNovaCount(combat.OwnCosmicNovaCastCount))PulseCosmicNova();
        }

        public static bool IsFinisherCueReady(int acceptedMask,int selected,float chainSeconds)
        {
            if(selected<0||selected>3||float.IsNaN(chainSeconds)||float.IsInfinity(chainSeconds)||chainSeconds<=0f)return false;
            int mask=acceptedMask&15,bit=1<<selected;
            return (mask|bit)==15 && (mask&bit)==0;
        }

        public static float NearPlaneScale(float minimumCameraDepth,float nearClip)
        {
            if(float.IsNaN(minimumCameraDepth)||float.IsInfinity(minimumCameraDepth)||float.IsNaN(nearClip)||float.IsInfinity(nearClip)||nearClip<0f)return 1f;
            return System.Math.Min(1f,System.Math.Max(.28f,(nearClip+.016f)/System.Math.Max(.001f,minimumCameraDepth)));
        }

        /// <summary>Only an own accepted Cosmic Nova launch may call this; it cannot grant or launch an ability.</summary>
        public void PulseCosmicNova() { if(visualRoot!=null){novaPulseAge=0f;confirmedAppearance.SetMasks(confirmedMask,ingredientMask,false);UpdateFocusPaint(0f);UpdateSparks();UpdateElectricArcs();} }

        void ApplyPaint(MeshRenderer renderer,int slot,Color color,Color emission)
        {
            if(renderer==null||appearanceBlock==null)return;
            appearanceBlock.Clear();appearanceBlock.SetColor(BaseColorId,color);appearanceBlock.SetColor(ColorId,color);
            appearanceBlock.SetColor(EmissionId,emission);renderer.SetPropertyBlock(appearanceBlock,slot);
        }

        Color MaskAccent(int mask,Color fallback)
        {
            Color color=Color.black;int count=0;
            for(int i=0;i<4;i++)if((mask&(1<<i))!=0){color+=ElementAccents[i];count++;}
            color=count>0?color/count:fallback;color.a=1f;return color;
        }

        Color ConfirmedAccent() => MaskAccent(confirmedMask,gemAccent);
        Color ChargedAccent() => MaskAccent(ingredientMask,ConfirmedAccent());

        Color ConfirmedFacet(Color focus)
        {
            // Keep the second ingredient visibly saturated in the existing lens facets.
            // The matching two existing arc submeshes retain the exact original RGB colors.
            if((confirmedMask&(confirmedMask-1))!=0)
                for(int i=0;i<4;i++)if(i!=selectedElement&&(confirmedMask&(1<<i))!=0)return ElementAccents[i];
            return Color.Lerp(focus,new Color(.025f,.035f,.025f),.60f);
        }

        void UpdateFocusPaint(float recoil)
        {
            float nova=Mathf.Clamp01(1f-novaPulseAge/.65f);
            int focusMask=staffSpiralState.AcceptedActive?staffSpiralState.PulseMask:confirmedMask;
            Color selectedFocus=MaskAccent(focusMask,ConfirmedAccent());
            Color focus=Color.Lerp(finisherCue?PrismaticFocus:selectedFocus,NovaWhite,nova);
            Color inlay=Color.Lerp(finisherCue?PrismaticFocus:ChargedAccent(),NovaWhite,nova);
            float glow=.75f+Mathf.Sin(clock*2.7f)*.08f+recoil*1.6f+(finisherCue?.45f:0f)+nova*1.4f;
            ApplyPaint(bodyRenderer,Crystal,focus,focus*glow);
            Color facet=Color.Lerp(StaffFacetAccent(focusMask,focus),NovaWhite,nova);
            ApplyPaint(bodyRenderer,CrystalShade,facet,facet*(.13f+recoil*.32f+nova*.7f));
            ApplyPaint(bodyRenderer,Inlay,inlay,inlay*(.28f+recoil*1.4f+(finisherCue?.45f:0f)+nova));
        }

        void LateUpdate()
        {
            if (visualRoot == null || player == null || viewCamera == null) return;
            var session = GameSession.Instance;
            RefreshElementAppearance();
            RefreshConfirmedAppearance();
            bool visible = CanShowStaff020(session);
            ObserveStaffPickup020(session);
            if (visualRoot.activeSelf != visible) visualRoot.SetActive(visible);
            if (!visible || session.Paused) {
                lastPosition = player.transform.position;
                previousAngles = viewCamera.transform.eulerAngles;
                if (!visible) {
                    novaPulseAge=10f;ResetStaffSpiral();ResetNativeHands015();
                    walkingOffset = sway = Vector3.zero;
                    movement = 0f;
                    wandRoot.localPosition = RestPosition;
                    wandRoot.localRotation = RestRotation;
                }
                return;
            }
            float dt = Mathf.Min(Time.deltaTime, .05f);
            if (dt <= 0f) return;
            clock += dt;
            kickAge += dt;
            novaPulseAge += dt;
            AdvanceStaffSpiral(dt);
            float targetMovement = player.GroundedForBobbing ? player.StepBlend : 0f;
            movement = Mathf.Lerp(movement, targetMovement, 1f - Mathf.Exp(-dt * 9f));
            lastPosition = player.transform.position;
            Vector3 angles = viewCamera.transform.eulerAngles;
            Vector3 turn = new Vector3(Mathf.DeltaAngle(previousAngles.x, angles.x),
                Mathf.DeltaAngle(previousAngles.y, angles.y), 0f);
            previousAngles = angles;
            float3 turnTarget020 = OracleViewmodelGrip020.Turn(turn.y, turn.x, dt);
            Vector3 targetSway = new Vector3(turnTarget020.x, turnTarget020.y, 0f);
            sway = Vector3.Lerp(sway, targetSway, 1f - Mathf.Exp(-dt * 10f));
            float recoil = Mathf.Exp(-kickAge * CastRecovery[lastSpell]);
            float strength = player.BobbingIntensity * Mathf.Lerp(1f, .75f, player.SprintBobBlend);
            OracleViewMotion020 walk020 = OracleViewmodelGrip020.Walk(player.StepPhase, clock, movement, strength, player.GroundedForBobbing);
            Vector3 bob = new Vector3(walk020.Position.x, walk020.Position.y, walk020.Position.z);
            walkingOffset = Vector3.Lerp(walkingOffset, bob, 1f - Mathf.Exp(-dt * 16f));
            if (bob == Vector3.zero && walkingOffset.sqrMagnitude < .000000001f) walkingOffset = Vector3.zero;
            bool nativePose=EvaluateHands015(dt,strength);
            if(nativePose)walkingOffset=Vector3.zero;
            float3 attack020 = OracleViewmodelGrip020.Attack(staffAttackSway.PitchDegrees, staffAttackSway.YawDegrees);
            Vector3 walkAngles020 = nativePose ? Vector3.zero : new Vector3(walk020.Angles.x, walk020.Angles.y, walk020.Angles.z);
            // The parent is camera-local: convert the reticle axis into that parent's rigid frame.
            Vector3 slingAim020 = nativeHandPivot015 != null ? Quaternion.Inverse(nativeHandPivot015.localRotation) * Vector3.forward : Vector3.forward;
            wandRoot.localPosition = RestPosition + walkingOffset + new Vector3(attack020.x, attack020.y, attack020.z)
                + slingAim020 * StaffEngulfState020.Sling(staffAttackSway.PitchDegrees);
            wandRoot.localRotation = RestRotation * Quaternion.Euler(
                sway.y + staffAttackSway.PitchDegrees + walkAngles020.x,
                sway.x + staffAttackSway.YawDegrees + walkAngles020.y,
                walkAngles020.z + staffAttackSway.RollDegrees);
            ApplyStaffPickup020(dt);
            UpdateClearance(dt);
            UpdateFocusPaint(recoil);
            UpdateSparks();
            UpdateElectricArcs();
        }

        void UpdateClearance(float dt)
        {
            // Uniform camera-origin scaling keeps the framing while bringing the
            // complete hand closer at a wall. It does not alter the player's camera.
            int count = Physics.SphereCastNonAlloc(viewCamera.transform.position, .11f,
                viewCamera.transform.forward, obstructionHits, .86f, ~0,
                QueryTriggerInteraction.Ignore);
            float nearest = .86f;
            for (int i = 0; i < count; i++) {
                var hit = obstructionHits[i];
                if (hit.collider == null || hit.collider.transform.IsChildOf(player.transform)) continue;
                nearest = Mathf.Min(nearest, hit.distance);
            }
            float minimum = MinimumNearPlaneScale();
            float target = Mathf.Min(Mathf.Min((nearest + .075f) / .86f,ProbeScale(outerHandProbe)),ProbeScale(gripProbe));
            target=Mathf.Clamp(target,minimum,1f);
            // Enter a nearby wall immediately; ease out only when space is clear.
            clearance = target < clearance ? target : Mathf.Lerp(clearance, target, 1f - Mathf.Exp(-dt * 10f));
            cameraRig.localScale = Vector3.one * clearance;
        }

        void CachePresentationBounds(Mesh bodyMesh)
        {
            presentationBounds=bodyMesh.bounds;presentationBounds.Encapsulate(sparks.bounds);presentationBounds.Encapsulate(electricArcs.bounds);
            if(staffSpiralMesh!=null)presentationBounds.Encapsulate(staffSpiralMesh.bounds);
            // Cold only: cache real outer-hand and grip vertices, rather than hand-authored frame offsets.
            Vector3[] vertices=bodyMesh.vertices;float outer=float.NegativeInfinity,grip=float.NegativeInfinity;
            for(int i=0;i<vertices.Length;i++)
            {
                Vector3 p=vertices[i];float x=(RestPosition+RestRotation*p).x;
                if(x>outer){outer=x;outerHandProbe=p;}
                if(p.y>=-.10f&&p.y<=.10f&&x>grip){grip=x;gripProbe=p;}
            }
        }

        float MinimumNearPlaneScale()
        {
            Matrix4x4 handMatrix=HandLocalMatrix015();
            float minimumDepth=float.PositiveInfinity;Vector3 c=presentationBounds.center,e=presentationBounds.extents;
            for(int i=0;i<8;i++)
            {
                Vector3 corner=c+new Vector3((i&1)==0?-e.x:e.x,(i&2)==0?-e.y:e.y,(i&4)==0?-e.z:e.z);
                minimumDepth=Mathf.Min(minimumDepth,handMatrix.MultiplyPoint3x4(corner).z);
            }
            // Near-plane safety wins if a wall and the near plane cannot both contain the whole model.
            // Uniform camera-origin scale preserves projected framing; it never disables a renderer.
            return NearPlaneScale(minimumDepth,viewCamera.nearClipPlane);
        }

        float ProbeScale(Vector3 localPoint)
        {
            Vector3 end=HandLocalMatrix015().MultiplyPoint3x4(localPoint);float distance=end.magnitude;
            if(distance<.001f)return 1f;
            int count=Physics.SphereCastNonAlloc(viewCamera.transform.position,.055f,
                viewCamera.transform.TransformDirection(end/distance),obstructionHits,distance,~0,QueryTriggerInteraction.Ignore);
            float result=1f;
            for(int i=0;i<count;i++)
            {
                var hit=obstructionHits[i];if(hit.collider==null||hit.collider.transform.IsChildOf(player.transform))continue;
                result=Mathf.Min(result,Mathf.Max(0f,hit.distance-.015f)/distance);
            }
            return result;
        }

        void BuildWand(ShapeBuilder s)
        {
            s.Prism(new Vector3(0f,.010f,0f), .029f,.022f,.62f, 8, Wood);
            s.Box(new Vector3(0f,.135f,-.024f),new Vector3(.014f,.31f,.010f),.003f,BrassDark);
            // Broad individual binding turns and alternating facets retain a readable
            // dark wrapped shaft instead of dissolving into a thin smooth cylinder.
            for (int i=0;i<14;i++) {
                float y=-.235f+i*.037f;
                s.Band(new Vector3(0f,y,0f),Vector3.up,.036f,.026f,.026f,8,i%3==0?Fold:Wrap);
                s.Rod(new Vector3(-.027f,y-.013f,-.025f),new Vector3(.027f,y+.013f,-.025f),
                    .011f,i%3==0?Emerald:GloveShade);
                if(i%3==1)s.Box(new Vector3(.026f,y,-.027f),new Vector3(.012f,.017f,.010f),.002f,BrassDark);
            }
            foreach(float y in new[]{-.274f,-.145f,.110f,.264f,.303f})
                s.Band(new Vector3(0f,y,0f),Vector3.up,.038f,.025f,.014f,8,Brass);
            s.Prism(new Vector3(0f,-.296f,0f),.034f,.025f,.040f,8,BrassDark);
            s.Gem(new Vector3(0f,-.332f,0f),.027f,.067f,6,Inlay,CrystalShade);
            s.Box(new Vector3(0f,.299f,0f),new Vector3(.080f,.057f,.063f),.009f,BrassDark);
            s.Box(new Vector3(0f,.302f,-.036f),new Vector3(.047f,.041f,.014f),.004f,Inlay);

            Vector3 center=new Vector3(0f,.450f,0f);
            Vector3[] corners={Vector3.up*.171f,Vector3.right*.171f,Vector3.down*.171f,Vector3.left*.171f};
            for(int i=0;i<4;i++) {
                Vector3 a=center+corners[i],b=center+corners[(i+1)%4];
                s.Rod(a,b,.042f,BrassDark);
                s.Rod(a+Vector3.back*.021f,b+Vector3.back*.021f,.034f,Brass);
                s.Rod(a+Vector3.back*.041f,b+Vector3.back*.041f,.010f,BrassEdge);
                s.Box(a+Vector3.back*.021f,new Vector3(.038f,.038f,.040f),.006f,Brass,
                    Quaternion.Euler(0f,0f,45f));
                Vector3 middle=(a+b)*.5f+Vector3.back*.043f;
                s.Box(middle,new Vector3(.013f,.013f,.008f),.002f,BrassEdge);
            }
            // Original broad lens. The selected elemental glyph is a separate
            // mutually exclusive mesh; the four-point Oracle crest stays on cuff.
            s.Lens(center,.112f,.118f,.061f,12,Crystal,CrystalShade,Inlay);
            s.Box(center+new Vector3(-.037f,.047f,-.060f),new Vector3(.015f,.010f,.005f),.001f,Crystal);
            s.Box(center+new Vector3(-.020f,.061f,-.054f),new Vector3(.009f,.008f,.005f),.001f,Inlay);
            // Small inset gems at the frame's north and south corners.
            s.Gem(center+new Vector3(0f,.133f,-.043f),.016f,.038f,4,Inlay,CrystalShade);
            s.Gem(center+new Vector3(0f,-.133f,-.043f),.016f,.038f,4,Inlay,CrystalShade);
        }

        void BuildElementGlyphs(Material ink)
        {
            string[] names={"Fire flame","Water drop","Earth mountain","Air spiral and four rays"};
            Vector3 c=new Vector3(0f,.450f,-.073f);
            for(int element=0;element<4;element++) {
                var s=new ShapeBuilder(1);
                if(element==0) {
                    s.Box(c+new Vector3(0f,-.029f,0f),new Vector3(.043f,.017f,.007f),.001f,0);
                    s.Box(c+new Vector3(0f,-.012f,0f),new Vector3(.060f,.022f,.007f),.001f,0);
                    s.Box(c+new Vector3(-.006f,.007f,0f),new Vector3(.042f,.022f,.007f),.001f,0);
                    s.Box(c+new Vector3(-.012f,.026f,0f),new Vector3(.020f,.026f,.007f),.001f,0);
                    s.Box(c+new Vector3(-.015f,.043f,0f),new Vector3(.009f,.017f,.007f),.001f,0);
                    s.Box(c+new Vector3(.018f,.017f,0f),new Vector3(.013f,.027f,.007f),.001f,0,
                        Quaternion.Euler(0f,0f,-20f));
                }
                else if(element==1) {
                    // Stepped teardrop: pointed crown, rounded broad lower basin.
                    s.Box(c+new Vector3(0f,-.027f,0f),new Vector3(.036f,.013f,.007f),.001f,0);
                    s.Box(c+new Vector3(0f,-.011f,0f),new Vector3(.055f,.020f,.007f),.001f,0);
                    s.Box(c+new Vector3(0f,.007f,0f),new Vector3(.044f,.018f,.007f),.001f,0);
                    s.Box(c+new Vector3(0f,.023f,0f),new Vector3(.026f,.017f,.007f),.001f,0);
                    s.Box(c+new Vector3(0f,.038f,0f),new Vector3(.012f,.015f,.007f),.001f,0);
                }
                else if(element==2) {
                    Vector3[] frame={new Vector3(-.040f,-.039f,0f),new Vector3(.040f,-.039f,0f),
                        new Vector3(.040f,.039f,0f),new Vector3(-.040f,.039f,0f)};
                    for(int i=0;i<4;i++)s.Rod(c+frame[i],c+frame[(i+1)%4],.007f,0);
                    s.Rod(c+new Vector3(-.029f,-.023f,0f),c+new Vector3(-.006f,.022f,0f),.008f,0);
                    s.Rod(c+new Vector3(-.006f,.022f,0f),c+new Vector3(.020f,-.023f,0f),.008f,0);
                    s.Rod(c+new Vector3(.004f,-.002f,0f),c+new Vector3(.018f,.010f,0f),.007f,0);
                    s.Rod(c+new Vector3(.018f,.010f,0f),c+new Vector3(.031f,-.023f,0f),.007f,0);
                }
                else {
                    for(int i=0;i<12;i++) {
                        float a=i/12f*Mathf.PI*2.6f,b=(i+1)/12f*Mathf.PI*2.6f;
                        float ra=Mathf.Lerp(.006f,.034f,i/12f),rb=Mathf.Lerp(.006f,.034f,(i+1)/12f);
                        s.Rod(c+new Vector3(Mathf.Cos(a)*ra,Mathf.Sin(a)*ra,0f),
                            c+new Vector3(Mathf.Cos(b)*rb,Mathf.Sin(b)*rb,0f),.0055f,0);
                    }
                    for(int i=0;i<4;i++) {
                        float a=i*Mathf.PI*.5f;
                        Vector3 ray=new Vector3(Mathf.Cos(a),Mathf.Sin(a),0f);
                        s.Rod(c+ray*.043f,c+ray*.053f,.0055f,0);
                    }
                }
                Mesh mesh=s.Finish("Original voxel "+names[element]+" glyph");owned.Add(mesh);
                var glyph=new GameObject(names[element]+" focus symbol");
                glyph.transform.SetParent(wandRoot,false);
                glyph.AddComponent<MeshFilter>().sharedMesh=mesh;
                ConfigureRenderer(glyph.AddComponent<MeshRenderer>(),new[]{ink});
                glyph.SetActive(false);elementGlyphs[element]=glyph;
            }
        }

        void RefreshElementAppearance()
        {
            var session=GameSession.Instance;
            int preview=session!=null&&session.Tutorial!=null?session.Tutorial.GuideAbilityPreviewIndex:-1;
            int element=preview>=0?preview:session!=null && session.Combat!=null ? session.Combat.SelectedSpell :
                session!=null && session.Progression!=null ? session.Progression.SelectedElement : 0;
            element=Mathf.Clamp(element,0,3);
            if(element==selectedElement)return;
            selectedElement=element;
            Color[] colors=session!=null && session.Combat!=null ? session.Combat.SpellColors : ElementAccents;
            gemAccent=element<colors.Length?colors[element]:ElementAccents[element];
            UpdateFocusPaint(0f);
            for(int i=0;i<elementGlyphs.Length;i++)
                if(elementGlyphs[i]!=null)elementGlyphs[i].SetActive(i==element);
        }

        void BuildArm(ShapeBuilder s)
        {
            Vector3 wrist = new Vector3(.082f,-.126f,-.018f);
            Vector3 elbow = new Vector3(.337f,-.45f,-.142f);
            Vector3 axis = (wrist-elbow).normalized;
            Quaternion arm = Quaternion.FromToRotation(Vector3.up, axis);
            // A tapered octagonal sleeve forms the connected cuff silhouette.
            s.Prism((wrist+elbow)*.5f,.143f,.083f,Vector3.Distance(wrist,elbow),8,Emerald,arm);
            s.Band(wrist-axis*.022f,axis,.100f,.068f,.050f,8,Fold);
            s.Band(wrist-axis*.012f,axis,.105f,.068f,.030f,8,Brass);
            s.Band(wrist-axis*.043f,axis,.108f,.083f,.013f,8,BrassDark);
            s.Band(wrist+axis*.004f,axis,.104f,.072f,.009f,8,BrassEdge);
            s.Band(elbow+axis*.025f,axis,.144f,.128f,.013f,8,BrassDark);
            s.Prism(wrist+axis*.028f,.055f,.047f,.077f,8,GloveShade,arm);
            Vector3 front = new Vector3(0f,0f,-1f);
            for (int i = -1; i <= 1; i++) {
                Vector3 offset = new Vector3(i*.033f,0f,0f);
                s.Rod(wrist-axis*.053f + front*.071f + offset*.5f,
                    elbow+axis*.04f + front*.099f + offset, .0045f, i==0?Brass:BrassDark);
            }
            s.Box(new Vector3(.195f,-.278f,-.200f),new Vector3(.104f,.136f,.016f),.010f,Fold,
                Quaternion.Euler(0f,0f,35f));
            s.PlanarRing(new Vector3(.195f,-.278f,-.213f),.046f,.005f,16,Brass);
            s.Star(new Vector3(.195f,-.278f,-.220f),.034f,.005f,Brass,Quaternion.identity);
            s.Gem(new Vector3(.107f,-.149f,-.119f),.017f,.040f,4,Inlay,CrystalShade);
            // Small raised folds and stitched brass lozenges keep the sleeve voxel rich.
            for (int i = 0; i < 5; i++) {
                Vector3 c = Vector3.Lerp(wrist,elbow,.20f+i*.13f) + new Vector3(.043f,-.008f,-.067f);
                s.Box(c,new Vector3(.023f,.057f,.016f),.004f,i%2==0?Emerald:Fold,
                    Quaternion.Euler(0f,0f,35f));
                if (i%2==0) s.Box(c+front*.010f,new Vector3(.011f,.011f,.005f),.002f,BrassDark,
                    Quaternion.Euler(0f,0f,45f));
            }
            // A true closed grip: four complete curled fingers surround the shaft,
            // with a diagonal opposing thumb across the upper finger. Recesses
            // between phalanges remain dark rather than opening into the world.
            s.Box(new Vector3(.066f,-.024f,-.011f),new Vector3(.108f,.139f,.094f),.012f,GloveShade);
            s.Box(new Vector3(.087f,-.022f,-.054f),new Vector3(.067f,.116f,.025f),.006f,Glove);
            for (int i = 0; i < 4; i++) {
                float y=.040f-i*.031f;
                float taper=i==3?.88f:1f;
                s.Box(new Vector3(.055f,y,-.067f),new Vector3(.067f,.027f,.036f)*taper,.005f,Glove);
                s.Box(new Vector3(.002f,y,-.078f),new Vector3(.054f,.025f,.040f)*taper,.004f,GloveShade);
                s.Box(new Vector3(-.039f,y,-.041f),new Vector3(.033f,.026f,.056f)*taper,.004f,Glove);
                s.Box(new Vector3(-.032f,y,.001f),new Vector3(.029f,.024f,.030f)*taper,.004f,GloveShade);
                // Raised segmented knuckle pads give the dark glove a voxel edge.
                s.Box(new Vector3(.051f,y,-.089f),new Vector3(.037f,.019f,.009f)*taper,.002f,Glove);
            }
            s.Rod(new Vector3(.112f,.005f,-.065f),new Vector3(.043f,.062f,-.102f),.039f,GloveShade);
            s.Box(new Vector3(.022f,.061f,-.093f),new Vector3(.049f,.032f,.037f),.005f,Glove,
                Quaternion.Euler(0f,0f,-18f));
            s.Box(new Vector3(.067f,.040f,-.104f),new Vector3(.026f,.027f,.009f),.002f,Glove);
        }

        void BuildSparks()
        {
            var particle = new GameObject("Bounded living-light motes");
            particle.transform.SetParent(wandRoot,false);
            sparks = new Mesh {name="Twelve pooled Oracle motes"};
            sparks.MarkDynamic();
            owned.Add(sparks);
            int[] indices = new int[SparkCount*24];
            int[] octa = {0,4,2,0,3,4,0,5,3,0,2,5,1,2,4,1,4,3,1,3,5,1,5,2};
            for (int n=0;n<SparkCount;n++) for (int k=0;k<24;k++) indices[n*24+k]=n*6+octa[k];
            for (int n=0;n<SparkCount;n++) {
                int p=n*6;
                sparkNormals[p]=Vector3.up; sparkNormals[p+1]=Vector3.down;
                sparkNormals[p+2]=Vector3.right; sparkNormals[p+3]=Vector3.left;
                sparkNormals[p+4]=Vector3.forward; sparkNormals[p+5]=Vector3.back;
            }
            sparks.vertices=sparkVertices;
            sparks.normals=sparkNormals;
            sparks.triangles=indices;
            sparks.bounds=new Bounds(new Vector3(0f,.45f,0f),new Vector3(.70f,.70f,.60f));
            particle.AddComponent<MeshFilter>().sharedMesh=sparks;
            sparkPaint=UnlitPaint("Oracle living-light motes");
            sparkRenderer=particle.AddComponent<MeshRenderer>();
            ConfigureRenderer(sparkRenderer,new[]{sparkPaint});
        }

        void BuildElectricArcs()
        {
            var arcs=new GameObject("Bounded green focus electricity");
            arcs.transform.SetParent(wandRoot,false);
            electricArcs=new Mesh {name="Four pooled jagged aether ribbons"};
            electricArcs.MarkDynamic();owned.Add(electricArcs);
            int[] indices=new int[ArcCount*(ArcSteps-1)*12];
            for(int segment=0;segment<ArcCount*(ArcSteps-1);segment++) {
                int v=segment*4,t=segment*12;
                // Explicit back faces keep the fine ribbons visible from both sides
                // without transparent sorting or a bespoke shader variant.
                indices[t]=v;indices[t+1]=v+1;indices[t+2]=v+2;
                indices[t+3]=v;indices[t+4]=v+2;indices[t+5]=v+3;
                indices[t+6]=v+2;indices[t+7]=v+1;indices[t+8]=v;
                indices[t+9]=v+3;indices[t+10]=v+2;indices[t+11]=v;
            }
            electricArcs.vertices=arcVertices;electricArcs.subMeshCount=ArcCount;
            for(int arc=0;arc<ArcCount;arc++)electricArcs.SetTriangles(indices,arc*(ArcSteps-1)*12,(ArcSteps-1)*12,arc,false);
            electricArcs.bounds=new Bounds(new Vector3(0f,.25f,-.04f),new Vector3(.85f,1.10f,.60f));
            arcs.AddComponent<MeshFilter>().sharedMesh=electricArcs;
            electricPaint=UnlitPaint("Oracle green electric filaments");
            arcRenderer=arcs.AddComponent<MeshRenderer>();
            ConfigureRenderer(arcRenderer,new[]{electricPaint,electricPaint,electricPaint,electricPaint});
        }

        Vector3 ArcPoint(int arc,int step,float burst)
        {
            float u=(float)step/(ArcSteps-1);
            float tick=Mathf.Floor(clock*13f);
            float j=Mathf.Sin(step*37.13f+arc*9.17f+tick*17.19f);
            if(arc==0) {
                float a=u*Mathf.PI*3.4f+clock*.8f;
                float r=.046f+j*.009f;
                return new Vector3(Mathf.Cos(a)*r,-.105f+u*.432f+j*.006f,Mathf.Sin(a)*r);
            }
            if(arc==1) {
                float a=u*Mathf.PI*1.63f+clock*.37f;
                float r=.124f+j*.010f;
                return new Vector3(Mathf.Cos(a)*r,.450f+Mathf.Sin(a)*r,-.087f+j*.009f);
            }
            Vector3 direction=arc==2?new Vector3(.85f,.63f,-.10f):new Vector3(-.78f,.91f,-.12f);
            return new Vector3(0f,.45f,-.085f)+direction*(u*(.055f+burst*.16f))
                +new Vector3(j*.012f,Mathf.Sin(step*7.1f+tick)*.013f,0f)*(step==0?0f:1f);
        }

        void UpdateElectricArcs()
        {
            if(electricArcs==null)return;
            float nova=Mathf.Clamp01(1f-novaPulseAge/.65f);
            float burst=Mathf.Max(Mathf.Clamp01(1f-kickAge/.28f),nova);
            for(int arc=0;arc<ArcCount;arc++)
            {
                bool charged=(ingredientMask&(1<<arc))!=0;
                bool confirmed=(confirmedMask&(1<<arc))!=0;
                Color color=(finisherCue||nova>0f||charged||confirmed?ElementAccents[arc]:gemAccent)*
                    (charged||finisherCue?1.65f:confirmed?1.30f:1.05f);
                color=Color.Lerp(color,NovaWhite*2.2f,nova*.45f);color.a=1f;
                ApplyPaint(arcRenderer,arc,color,Color.black);
            }
            for(int arc=0;arc<ArcCount;arc++)for(int step=0;step<ArcSteps-1;step++) {
                Vector3 a=ArcPoint(arc,step,burst),b=ArcPoint(arc,step+1,burst);
                Vector3 side=Vector3.Cross(b-a,Vector3.back).normalized;
                if(side.sqrMagnitude<.1f)side=Vector3.right;
                bool charged=(ingredientMask&(1<<arc))!=0;
                bool confirmed=(confirmedMask&(1<<arc))!=0;
                float held=finisherCue?.65f:charged?.38f:confirmed?.25f:0f;
                float width=(.0015f+burst*.0022f)*(arc>1?Mathf.Max(burst,held):1f);
                side*=width;
                int at=(arc*(ArcSteps-1)+step)*4;
                arcVertices[at]=a-side;arcVertices[at+1]=a+side;
                arcVertices[at+2]=b+side;arcVertices[at+3]=b-side;
            }
            electricArcs.vertices=arcVertices;
        }

        void UpdateSparks()
        {
            if (sparks==null) return;
            float nova=Mathf.Clamp01(1f-novaPulseAge/.65f);
            float burst=Mathf.Max(Mathf.Clamp01(1f-kickAge/.36f),nova);
            var combat=GameSession.Instance!=null?GameSession.Instance.Combat:null;
            Color[] accents=combat!=null?combat.SpellColors:ElementAccents;
            Color castColor=lastSpell<accents.Length?accents[lastSpell]:ElementAccents[lastSpell];
            Color color=Color.Lerp(finisherCue?PrismaticFocus:ChargedAccent(),castColor,burst);
            color=Color.Lerp(color,NovaWhite,nova);ApplyPaint(sparkRenderer,0,color,Color.black);
            for (int i=0;i<SparkCount;i++) {
                float a=clock*(i%2==0?1.1f:-.8f)+i*2.399963f;
                float outward=i<3 ? .131f : .085f+(1f-burst)*.16f;
                float y=.450f+Mathf.Sin(clock*1.8f+i)*.084f+(i<3?0f:(1f-burst)*.12f);
                Vector3 c=new Vector3(Mathf.Cos(a)*outward,y,Mathf.Sin(a)*outward);
                float size=i<3 ? .0028f+Mathf.Sin(clock*3f+i)*.0006f : .0055f*burst;
                int k=i*6;
                sparkVertices[k]=c+Vector3.up*size*1.8f;
                sparkVertices[k+1]=c+Vector3.down*size*1.8f;
                sparkVertices[k+2]=c+Vector3.right*size;
                sparkVertices[k+3]=c+Vector3.left*size;
                sparkVertices[k+4]=c+Vector3.forward*size;
                sparkVertices[k+5]=c+Vector3.back*size;
            }
            sparks.vertices=sparkVertices;
        }

        Material Paint(string label,Color color,float metallic,float smoothness,Texture texture)
        {
            Shader shader=Shader.Find("Universal Render Pipeline/Lit");
            if (shader==null) shader=Shader.Find("Standard");
            var m=new Material(shader) {name="Oracle / "+label};
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor",color);
            if (m.HasProperty("_Color")) m.SetColor("_Color",color);
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic",metallic);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness",smoothness);
            if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness",smoothness);
            if (texture!=null) {
                if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap",texture);
                if (m.HasProperty("_MainTex")) m.SetTexture("_MainTex",texture);
            }
            owned.Add(m);
            return m;
        }

        Material UnlitPaint(string label)
        {
            Shader shader=Shader.Find("Universal Render Pipeline/Unlit");
            if(shader==null)shader=Shader.Find("Unlit/Color");
            var paint=new Material(shader) {name=label};
            owned.Add(paint);return paint;
        }

        static void SetUnlitColor(Material paint,Color color)
        {
            if(paint==null)return;
            if(paint.HasProperty("_BaseColor"))paint.SetColor("_BaseColor",color);
            if(paint.HasProperty("_Color"))paint.SetColor("_Color",color);
        }

        Texture2D MakeTexture(string label,int size,int style)
        {
            var t=new Texture2D(size,size,TextureFormat.RGBA32,false,true) {
                name=label, filterMode=FilterMode.Point,wrapMode=TextureWrapMode.Repeat,anisoLevel=0
            };
            Color[] pixels=new Color[size*size];
            for (int y=0;y<size;y++) for (int x=0;x<size;x++) {
                uint h=unchecked((uint)(x*73856093 ^ y*19349663 ^ style*83492791));
                h=unchecked((h^(h>>13))*1274126177u);
                float noise=(h&255)/255f;
                float value=.84f+noise*.15f;
                if (style==0) value-=((x+y)%4==0?.08f:0f)+(x%16==0?.05f:0f);
                else if (style==1) value-=((x+y/7)%9==0?.16f:0f);
                else value-=noise<.07f?.23f:0f;
                pixels[y*size+x]=new Color(value,value,value,1f);
            }
            t.SetPixels(pixels);t.Apply(false,true);owned.Add(t);return t;
        }

        static void SetEmission(Material paint,Color color)
        {
            if (paint==null || !paint.HasProperty("_EmissionColor")) return;
            paint.EnableKeyword("_EMISSION");paint.SetColor("_EmissionColor",color);
        }

        static void ConfigureRenderer(MeshRenderer renderer,Material[] materials)
        {
            renderer.sharedMaterials=materials;
            renderer.shadowCastingMode=ShadowCastingMode.Off;
            renderer.receiveShadows=false;
            renderer.lightProbeUsage=LightProbeUsage.BlendProbes;
            renderer.reflectionProbeUsage=ReflectionProbeUsage.Off;
            renderer.allowOcclusionWhenDynamic=false;
            renderer.motionVectorGenerationMode=MotionVectorGenerationMode.ForceNoMotion;
        }

        void OnDestroy() { DisposeVisuals(); }
        void DisposeVisuals()
        {
            DisposeHands015();
            ReleaseStaffSpiralReferences();
            if (visualRoot!=null) {visualRoot.SetActive(false);Destroy(visualRoot);}
            for (int i=owned.Count-1;i>=0;i--) if (owned[i]!=null) Destroy(owned[i]);
            owned.Clear();visualRoot=null;sparks=null;electricArcs=null;cameraRig=null;wandRoot=null;
            bodyRenderer=sparkRenderer=arcRenderer=null;appearanceBlock=null;
            for(int i=0;i<elementGlyphs.Length;i++)elementGlyphs[i]=null;
        }

        // All decorative pieces are packed into one mesh with thirteen material
        // submeshes. No primitive GameObjects, colliders or extra lights are created.
        sealed class ShapeBuilder
        {
            readonly List<Vector3> vertices=new List<Vector3>(14000);
            readonly List<Vector3> normals=new List<Vector3>(14000);
            readonly List<Vector2> uv=new List<Vector2>(14000);
            readonly List<int>[] triangles;
            public int VertexCount => vertices.Count;
            // Cold authoring only: preserve all original connectivity and UV/material records.
            public void RotateFrom(int first, Quaternion rotation)
            {
                if (first < 0 || first > vertices.Count)
                    throw new System.ArgumentOutOfRangeException(nameof(first));
                for (int i = first; i < vertices.Count; i++)
                {
                    vertices[i] = rotation * vertices[i];
                    normals[i] = rotation * normals[i];
                }
            }
            static readonly Vector3[] Axes={Vector3.right,Vector3.up,Vector3.forward};
            public ShapeBuilder(int materialCount) {
                triangles=new List<int>[materialCount];
                for(int i=0;i<materialCount;i++)triangles[i]=new List<int>(2000);
            }
            void Face(Vector3[] points,Vector3 normal,int material,Vector3 center,Quaternion rotation)
            {
                int at=vertices.Count;
                bool flip=Vector3.Dot(Vector3.Cross(points[1]-points[0],points[2]-points[0]),normal)<0f;
                for(int i=0;i<points.Length;i++) {
                    int j=flip?points.Length-1-i:i;
                    vertices.Add(center+rotation*points[j]);normals.Add(rotation*normal);
                    uv.Add(i==0?Vector2.zero:i==1?Vector2.right:i==2?Vector2.one:Vector2.up);
                }
                for(int i=1;i<points.Length-1;i++) {
                    triangles[material].Add(at);triangles[material].Add(at+i);triangles[material].Add(at+i+1);
                }
            }
            public void Box(Vector3 c,Vector3 size,float bevel,int material,Quaternion? rotation=null)
            {
                Quaternion q=rotation??Quaternion.identity;
                Vector3 h=size*.5f;
                float b=Mathf.Clamp(bevel,.0001f,Mathf.Min(h.x,Mathf.Min(h.y,h.z))*.8f);
                Vector3 inset=h-Vector3.one*b;
                for(int a=0;a<3;a++) for(int sign=-1;sign<=1;sign+=2) {
                    int u=(a+1)%3,v=(a+2)%3;
                    Vector3 middle=Axes[a]*(h[a]*sign),du=Axes[u]*inset[u],dv=Axes[v]*inset[v];
                    Face(new[]{middle-du-dv,middle+du-dv,middle+du+dv,middle-du+dv},Axes[a]*sign,material,c,q);
                }
                for(int a=0;a<3;a++) for(int d=a+1;d<3;d++) {
                    int v=3-a-d;
                    for(int sa=-1;sa<=1;sa+=2) for(int sd=-1;sd<=1;sd+=2) {
                        Vector3 p=Axes[a]*(h[a]*sa)+Axes[d]*(inset[d]*sd);
                        Vector3 r=Axes[a]*(inset[a]*sa)+Axes[d]*(h[d]*sd),e=Axes[v]*inset[v];
                        Face(new[]{p-e,r-e,r+e,p+e},(Axes[a]*sa+Axes[d]*sd).normalized,material,c,q);
                    }
                }
                for(int x=-1;x<=1;x+=2)for(int y=-1;y<=1;y+=2)for(int z=-1;z<=1;z+=2) {
                    Face(new[]{new Vector3(h.x*x,inset.y*y,inset.z*z),
                        new Vector3(inset.x*x,h.y*y,inset.z*z),new Vector3(inset.x*x,inset.y*y,h.z*z)},
                        new Vector3(x,y,z).normalized,material,c,q);
                }
            }
            public void Rod(Vector3 a,Vector3 b,float width,int material) {
                Vector3 delta=b-a;if(delta.sqrMagnitude<.0000001f)return;
                Box((a+b)*.5f,new Vector3(width,delta.magnitude,width),width*.18f,material,
                    Quaternion.FromToRotation(Vector3.up,delta));
            }
            public void Prism(Vector3 c,float bottomRadius,float topRadius,float length,int sides,int material,Quaternion? rotation=null)
            {
                Quaternion q=rotation??Quaternion.identity;
                for(int i=0;i<sides;i++) {
                    float a=i*Mathf.PI*2f/sides,b=(i+1)*Mathf.PI*2f/sides;
                    Vector3 r=new Vector3(Mathf.Cos(a),0f,Mathf.Sin(a)),t=new Vector3(Mathf.Cos(b),0f,Mathf.Sin(b));
                    Vector3 p=r*bottomRadius-Vector3.up*length*.5f,u=t*bottomRadius-Vector3.up*length*.5f;
                    Vector3 v=t*topRadius+Vector3.up*length*.5f,w=r*topRadius+Vector3.up*length*.5f;
                    Vector3 normal=Vector3.Cross(u-p,w-p).normalized;
                    if(Vector3.Dot(normal,r+t)<0f)normal=-normal;
                    Face(new[]{p,u,v,w},normal,material,c,q);
                    Face(new[]{Vector3.down*length*.5f,u,p},Vector3.down,material,c,q);
                    Face(new[]{Vector3.up*length*.5f,w,v},Vector3.up,material,c,q);
                }
            }
            public void Band(Vector3 c,Vector3 axis,float outer,float inner,float width,int sides,int material)
            {
                Quaternion q=Quaternion.FromToRotation(Vector3.up,axis);
                for(int i=0;i<sides;i++) {
                    float a=i*Mathf.PI*2f/sides,b=(i+1)*Mathf.PI*2f/sides;
                    Vector3 r=new Vector3(Mathf.Cos(a),0f,Mathf.Sin(a)),t=new Vector3(Mathf.Cos(b),0f,Mathf.Sin(b));
                    Vector3 up=Vector3.up*width*.5f;
                    Face(new[]{r*outer-up,t*outer-up,t*outer+up,r*outer+up},(r+t).normalized,material,c,q);
                    Face(new[]{r*inner-up,r*inner+up,t*inner+up,t*inner-up},-(r+t).normalized,material,c,q);
                    Face(new[]{r*inner+up,r*outer+up,t*outer+up,t*inner+up},Vector3.up,material,c,q);
                    Face(new[]{r*inner-up,t*inner-up,t*outer-up,r*outer-up},Vector3.down,material,c,q);
                }
            }
            public void Gem(Vector3 c,float radius,float height,int sides,int a,int b)
            {
                for(int i=0;i<sides;i++) {
                    float x=i*Mathf.PI*2f/sides,y=(i+1)*Mathf.PI*2f/sides;
                    Vector3 p=new Vector3(Mathf.Cos(x)*radius,0f,Mathf.Sin(x)*radius);
                    Vector3 r=new Vector3(Mathf.Cos(y)*radius,0f,Mathf.Sin(y)*radius);
                    Vector3 top=Vector3.up*height*.5f,bottom=-top;
                    Vector3 upper=Vector3.Cross(r-p,top-p).normalized;
                    Vector3 lower=Vector3.Cross(p-r,bottom-r).normalized;
                    if(Vector3.Dot(upper,p+r)<0f)upper=-upper;
                    if(Vector3.Dot(lower,p+r)<0f)lower=-lower;
                    Face(new[]{p,r,top},upper,i%3==0?b:a,c,Quaternion.identity);
                    Face(new[]{r,p,bottom},lower,i%2==0?a:b,c,Quaternion.identity);
                }
            }
            public void Lens(Vector3 c,float radiusX,float radiusY,float depth,int sides,int face,int shade,int rim)
            {
                for(int i=0;i<sides;i++) {
                    float a=i*Mathf.PI*2f/sides,b=(i+1)*Mathf.PI*2f/sides;
                    Vector3 p=new Vector3(Mathf.Cos(a)*radiusX,Mathf.Sin(a)*radiusY,0f);
                    Vector3 r=new Vector3(Mathf.Cos(b)*radiusX,Mathf.Sin(b)*radiusY,0f);
                    Vector3 p1=p*.91f+Vector3.back*depth*.38f,r1=r*.91f+Vector3.back*depth*.38f;
                    Vector3 p2=p*.62f+Vector3.back*depth*.82f,r2=r*.62f+Vector3.back*depth*.82f;
                    Vector3 n=(p+r).normalized;
                    Face(new[]{p,r,r1,p1},(n+Vector3.back*.7f).normalized,rim,c,Quaternion.identity);
                    Face(new[]{p1,r1,r2,p2},(n*.6f+Vector3.back).normalized,i%3==0?shade:face,c,Quaternion.identity);
                    Face(new[]{p2,r2,Vector3.back*depth},Vector3.back,i%4==0?shade:face,c,Quaternion.identity);
                    Face(new[]{p,Vector3.forward*depth*.35f,r},Vector3.forward,shade,c,Quaternion.identity);
                }
            }
            public void PlanarRing(Vector3 c,float radius,float width,int sides,int material)
            {
                for(int i=0;i<sides;i++) {
                    float a=i*Mathf.PI*2f/sides,b=(i+1)*Mathf.PI*2f/sides;
                    Rod(c+new Vector3(Mathf.Cos(a),Mathf.Sin(a),0f)*radius,
                        c+new Vector3(Mathf.Cos(b),Mathf.Sin(b),0f)*radius,width,material);
                }
            }
            public void Star(Vector3 c,float radius,float depth,int material,Quaternion q)
            {
                Vector2[] outline={new Vector2(0f,1f),new Vector2(.2f,.2f),new Vector2(.833333f,0f),
                    new Vector2(.2f,-.2f),new Vector2(0f,-1f),new Vector2(-.2f,-.2f),
                    new Vector2(-.833333f,0f),new Vector2(-.2f,.2f)};
                for(int i=0;i<8;i++) {
                    Vector3 p=(Vector3)(outline[i]*radius),r=(Vector3)(outline[(i+1)%8]*radius);
                    Face(new[]{Vector3.back*depth,p+Vector3.back*depth,r+Vector3.back*depth},Vector3.back,material,c,q);
                    Face(new[]{Vector3.zero,r,p},Vector3.forward,material,c,q);
                    Face(new[]{p,r,r+Vector3.back*depth,p+Vector3.back*depth},
                        new Vector3((p+r).x,(p+r).y,0f).normalized,material,c,q);
                }
            }
            public Mesh Finish(string label) {
                var mesh=new Mesh {name=label};
                if(vertices.Count>65535)mesh.indexFormat=IndexFormat.UInt32;
                mesh.SetVertices(vertices);mesh.SetNormals(normals);mesh.SetUVs(0,uv);
                mesh.subMeshCount=triangles.Length;
                for(int i=0;i<triangles.Length;i++)mesh.SetTriangles(triangles[i],i,false);
                mesh.RecalculateBounds();return mesh;
            }
        }
    }
}

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Original Oracle accepted world roots and camera/child-local poses only; no authority geometry changes.
// Array Formats: Fixed31shared body slots/2prewarmed player graphs and original palette/pose/feedback buffers; no wire ABI growth.
// Dependencies/Pointers: Player/Rooms/Combat remain sole owners; root integrates default-OFF original native player pilot and verifies real bindings/peer/clearance.

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Existing camera/wand-local metres; only cold authored right-arm vertices/normals rotate around the fixed staff axis; scales remain positive.
// Array Formats: Existing packed thirteen-material mesh and bounded presentation state plus one24B numeric gait value; no new hot resources/allocations or saved animation assets.
// Dependencies/Pointers: Original accepted attack spring, native hand graph, camera/aim, muzzle, movement, masks and full-mesh clearance retain ownership; root verifies geometry/native/browser presentation.

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: unchanged proper60degree hand frame; cold BuildWand vertex prefix alone feeds the existing wand-local effect renderer.
// Array Formats: original thirteen-material body geometry/palette and existing effect mesh owner; no new renderer, GameObject or active-frame allocation.
// Dependencies/Pointers: full-mesh clearance includes the extended staff effect bounds and accepted spring drives a bounded0.028m reticle-axis sling under the same rigid wrist/staff frame. Original models, pickup, glove/cuff and gameplay retain their owners.
