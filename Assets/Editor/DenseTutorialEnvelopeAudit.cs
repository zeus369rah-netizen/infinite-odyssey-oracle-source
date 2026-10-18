#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using Unity.Collections;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using OdysseyOracle.Diagnostics;

namespace OdysseyOracle.Editor
{
    // # ARCHITECTURE SNAPSHOT
    // Explicit root-run Editor diagnostic; no startup hook, scene/physics/game-state writes.
    // Cold public MeshData snapshots are disposed within their one callback. Subsequent hot
    // raster loops use only value triangles + one bounded managed grid at a time (world metres).
    // Work advances in Editor update slices; JSONL preserves exact nonuniform-grid coordinates
    // and every retained uncovered/uncertified run. No per-frame jobs/native scratch ownership.
    public static class DenseTutorialEnvelopeAudit
    {
        [Serializable] public sealed class Options
        {
            public float MaximumSpacingMetres=.02f;
            public bool EnableRectangleUnion=true;
            public int MillisecondsPerUpdate=6, MaximumSeconds=180, MaximumTriangles=1000000, MaximumCells=15000000, MaximumRuns=100000;
        }
        [Serializable] public sealed class SurfaceResult
        {
            public string Name; public int Axis,Sign,UCells,VCells; public double U0,U1,V0,V1,D0,D1;
            public long Planned,Scanned,Excluded,Certified,PointOnly,UncoveredCenters,UnknownMaterial,RectangleUnionCertified;
            public CoverageMask[] AuthoredOpenings;
        }
        [Serializable] public sealed class RunRecord
        {
            public string Record="uncovered_or_uncertified_run",Kind;
            public int Surface,VIndex,UStart,UEndInclusive;
            public CoveragePoint BoundsMin,BoundsMax,FirstInside,FirstOutside,LastInside,LastOutside;
        }
        [Serializable] public sealed class CoordinateRecord
        {
            public string Record="surface_coordinates",Name; public int Surface,Axis,Sign;
            public double D0,D1; public double[] U,V; public CoverageMask[] AuthoredOpenings;
        }
        [Serializable] public sealed class Report
        {
            public string Status="running",StartedUtc,FinishedUtc,WorldSourceSha256,SummaryPath,ExactRunsPath,Error="";
            public string Contract="Current tutorial: hall/alcove wall, roof and floor envelopes;51 deep backings plus shallow joint beds. No external end-door or portal exclusion.";
            public string Meaning="Known opaque axis quads/triangles certify complete <=2cm cells within declared normal-depth columns. Center-only or uncertain-material coverage remains UNKNOWN. Not a screenshot-color, lighting, global watertightness or every decorative block/prop acceptance claim.";
            public string Tolerance="Two micrometres geometry tolerance; opening edges are inserted into the grid, not rounded to centimetres.";
            public bool Complete,DetailsTruncated,RenderStateChanged,PhysicsUsed=false,SceneOrGameplayChanged=false;
            public int ContractSurfaces,CompletedSurfaces,UnvisitedSurfaces,MeshFilters,ReadMeshFilters,Triangles,RecoveredRectangles,UnknownBounds,SkippedTransparentSubmeshes,SkippedNonTriangleSubmeshes;
            public int ExcludedNonStructuralMeshes,OutsideEnvelopeBoundsMeshes,StructuralMaterialMismatches;
            public string ChangedRenderer="",RendererScope="Only direct immutable tutorial-root Stone/PaleStone/DarkStone/Moss/Floor/Iron batches or4m cells intersecting declared envelope slabs. Iron includes required window bars. Dynamic fire/portal/lore/decorative branches never certify structure.";
            public string[] ContractSurfaceNames;
            public long PlannedCells,ScannedCells,CertifiedCells,ExcludedCells,PointOnlyCells,UncoveredCenters,UnknownMaterialCells,CellTriangleTests,WrittenRuns,OmittedRuns;
            public long RectangleUnionCandidates,RectangleUnionCertified,RectangleUnionCapacitySkips;
            public int RectangleUnionCellCapacity=100000;
            public double ElapsedSeconds,MaximumObservedSliceMilliseconds;
            public Options Budget; public SurfaceResult[] Surfaces; public string[] Unknowns;
        }
        private struct MaterialState { public bool Skip,Known; public int Cull; }
        private struct RendererState { public MeshFilter Filter;public Mesh Mesh;public MeshRenderer Renderer;public Matrix4x4 Matrix;public Material[] Materials;public int[] Signatures; }
        private struct UnknownBound { public Bounds Bounds; }
        private enum Phase { ReadMeshes,NewSurface,Mask,Raster,Scan }
        private sealed class Work
        {
            public GameSession Session;public DungeonWorld World;public Transform StructuralRoot;public Camera Camera;public MeshFilter[] Filters;public string WorldSourcePath;
            public readonly List<RendererState> Renderers=new List<RendererState>(64);
            public readonly List<CoverageTriangle> Triangles=new List<CoverageTriangle>(80000);
            public readonly List<UnknownBound> UnknownBounds=new List<UnknownBound>();
            public readonly List<string> Unknowns=new List<string>(64);
            public readonly Dictionary<Material,MaterialState> Materials=new Dictionary<Material,MaterialState>();
            public readonly Dictionary<int,CoverageRectangleSet> RectangleUnions=new Dictionary<int,CoverageRectangleSet>();
            public CoveragePlane[] Planes;public SurfaceResult[] SurfaceReports;public Report Result;public Options Settings;
            public Stopwatch Clock=Stopwatch.StartNew();public Phase Phase;public int Filter,Surface=-1,Cell,Triangle,UnknownIndex;public Stopwatch ActiveSlice;
            public double[] U,V;public byte[] Flags;public int Width,Height;
            public bool RasterPending;public ProjectedCoverage Projected;public int X0,X1,Y0,Y1,X,Y;
            public int RunStart=-1,RunKind=-1,RunV=-1;public StreamWriter Writer;
        }
        private static Work current;
        private static Report last;
        private static readonly string[] StructuralSurfaces={"Stone","PaleStone","DarkStone","Moss","Floor","Iron"};
        private static readonly string[] StructuralMaterials={"Warm weathered limestone","Sun worn limestone edges","Green slate recesses","Living moss masonry","Worn warm flagstones","Black wrought iron"};
        public static bool IsRunning=>current!=null;

        public static string Begin(string absoluteSummaryPath,Options options=null)
        {
            if(current!=null)return "REFUSED: an audit is already running.";
            var session=GameSession.Instance;
            if(!Application.isPlaying||session==null||session.World==null||session.World.IsCoopArena)return "REFUSED: stable native tutorial world required; focus is not required.";
            if(session.World.transform.position.sqrMagnitude>1e-10f||Quaternion.Angle(session.World.transform.rotation,Quaternion.identity)>.0001f||
                (session.World.transform.lossyScale-Vector3.one).sqrMagnitude>1e-10f)return "REFUSED: current authored tutorial contract requires identity world transform.";
            if(string.IsNullOrWhiteSpace(absoluteSummaryPath)||!Path.IsPathRooted(absoluteSummaryPath))return "REFUSED: supply a new absolute .json summary path outside Assets.";
            string path=Path.GetFullPath(absoluteSummaryPath),assets=Path.GetFullPath(Application.dataPath)+Path.DirectorySeparatorChar;
            if(path.StartsWith(assets,StringComparison.OrdinalIgnoreCase)||!Directory.Exists(Path.GetDirectoryName(path))||
                !path.EndsWith(".json",StringComparison.OrdinalIgnoreCase)||File.Exists(path)||File.Exists(path+".gaps.jsonl"))return "REFUSED: existing directory/new filenames outside Assets required.";
            Options settings=Sanitize(options);
            var camera=session.Opening!=null?session.Opening.ActiveCamera:null;if(camera==null&&session.Player!=null)camera=session.Player.ViewCamera;
            if(camera==null)return "REFUSED: an actual session camera is required for renderer layer eligibility.";
            Transform structuralRoot=session.World.transform.Find("Tutorial catacomb environment");
            if(structuralRoot==null||!structuralRoot.gameObject.activeInHierarchy)return "REFUSED: actual immutable tutorial geometry root required.";
            var report=new Report {StartedUtc=DateTime.UtcNow.ToString("o"),SummaryPath=path,ExactRunsPath=path+".gaps.jsonl",Budget=settings};
            var work=new Work {Session=session,World=session.World,StructuralRoot=structuralRoot,Camera=camera,Filters=session.World.GetComponentsInChildren<MeshFilter>(false),Settings=settings,Result=report};
            work.Planes=BuildContract();work.SurfaceReports=new SurfaceResult[work.Planes.Length];report.MeshFilters=work.Filters.Length;
            report.ContractSurfaces=report.UnvisitedSurfaces=work.Planes.Length;report.ContractSurfaceNames=new string[work.Planes.Length];
            for(int i=0;i<work.Planes.Length;i++)report.ContractSurfaceNames[i]=work.Planes[i].Name;
            // Reviewed native tutorial: 618 filters after true spatial light partitions.
            // Triangle, cell, time and output budgets remain independently bounded.
            if(work.Filters.Length>2048)return "REFUSED: more than2048 mesh filters; explicitly review the world contract/budget.";
            work.WorldSourcePath=AssetDatabase.GetAssetPath(MonoScript.FromMonoBehaviour(work.World));report.WorldSourceSha256=SourceHash(work.WorldSourcePath);
            current=work;last=null;
            EditorApplication.update+=Tick;AssemblyReloadEvents.beforeAssemblyReload+=Reload;EditorApplication.playModeStateChanged+=PlayState;
            return "STARTED: rendered-envelope audit queued; poll GetStatus(). No geometry/physics/camera/gameplay state was changed.";
        }
        public static string GetStatus()
        {
            if(current==null)return last==null?"NOT STARTED":JsonUtility.ToJson(last,true);
            Work w=current;return "RUNNING phase="+w.Phase+" surface="+(w.Surface+1)+"/"+w.Planes.Length+" triangles="+w.Triangles.Count+
                " scanned="+w.Result.ScannedCells+"/"+w.Result.PlannedCells+" tests="+w.Result.CellTriangleTests+" seconds="+w.Clock.Elapsed.TotalSeconds.ToString("0.0");
        }
        public static void Cancel(){if(current!=null)Finish(false,"cancelled by caller");}
        private static void Reload(){if(current!=null)Finish(false,"assembly reload interrupted audit");}
        private static void PlayState(PlayModeStateChange state){if(current!=null&&(state==PlayModeStateChange.ExitingPlayMode||state==PlayModeStateChange.EnteredEditMode))Finish(false,"Play mode ended");}
        private static Options Sanitize(Options x)
        {
            x=x??new Options();return new Options {MaximumSpacingMetres=float.IsNaN(x.MaximumSpacingMetres)||float.IsInfinity(x.MaximumSpacingMetres)? .02f:Mathf.Clamp(x.MaximumSpacingMetres,.005f,.02f),
                EnableRectangleUnion=x.EnableRectangleUnion,MillisecondsPerUpdate=Mathf.Clamp(x.MillisecondsPerUpdate,1,12),MaximumSeconds=Mathf.Clamp(x.MaximumSeconds,10,600),MaximumTriangles=Mathf.Clamp(x.MaximumTriangles,1000,1000000),
                MaximumCells=Mathf.Clamp(x.MaximumCells,1000,30000000),MaximumRuns=Mathf.Clamp(x.MaximumRuns,100,500000)};
        }
        private static void Tick()
        {
            Work w=current;if(w==null)return;var slice=Stopwatch.StartNew();w.ActiveSlice=slice;
            try
            {
                if(!Application.isPlaying||w.World==null||w.Session==null||w.Session.World!=w.World||w.World.IsCoopArena){Finish(false,"world/Play context changed");return;}
                if(w.Clock.Elapsed.TotalSeconds>w.Settings.MaximumSeconds){Finish(false,"wall-clock budget exhausted; unvisited surfaces/cells remain unverified");return;}
                if(w.Writer==null){w.Writer=new StreamWriter(new FileStream(w.Result.ExactRunsPath,FileMode.CreateNew,FileAccess.Write,FileShare.Read));w.Writer.WriteLine("{\"record\":\"audit_scope\",\"physicsUsed\":false,\"colorRenderingVerified\":false}");}
                int operations=0;
                do
                {
                    if(w.Phase==Phase.ReadMeshes)
                    {
                        if(w.Filter<w.Filters.Length){ReadMesh(w,w.Filters[w.Filter++]);break;}
                        w.Result.Triangles=w.Triangles.Count;w.Result.UnknownBounds=w.UnknownBounds.Count;w.Phase=Phase.NewSurface;
                    }
                    else if(w.Phase==Phase.NewSurface)
                    {
                        if(++w.Surface>=w.Planes.Length){if(!Stable(w)){w.Result.RenderStateChanged=true;Finish(false,"included structural renderer changed after snapshot: "+w.Result.ChangedRenderer);}else Finish(true,"");return;}
                        if(!Stable(w)){w.Result.RenderStateChanged=true;Finish(false,"included structural renderer changed after snapshot: "+w.Result.ChangedRenderer);return;}
                        NewSurface(w);break;
                    }
                    else if(w.Phase==Phase.Mask)MaskCell(w);
                    else if(w.Phase==Phase.Raster)RasterCell(w);
                    else ScanCell(w);
                    operations++;
                }while((operations&255)!=0||slice.Elapsed.TotalMilliseconds<w.Settings.MillisecondsPerUpdate);
            }
            catch(Exception error){Finish(false,error.GetType().Name+": "+error.Message);}
            finally{if(w!=null){w.Result.MaximumObservedSliceMilliseconds=Math.Max(w.Result.MaximumObservedSliceMilliseconds,slice.Elapsed.TotalMilliseconds);w.ActiveSlice=null;}}
        }
        private static MaterialState InspectMaterial(Work w,Material material,bool negativeTransform)
        {
            if(material==null)return new MaterialState();
            if(w.Materials.TryGetValue(material,out var cached)){if(negativeTransform)cached.Known=false;return cached;}
            var result=new MaterialState {Cull=material.HasProperty("_Cull")?Mathf.RoundToInt(material.GetFloat("_Cull")):2};
            result.Skip=material.renderQueue>2500||material.HasProperty("_Surface")&&material.GetFloat("_Surface")>.5f;
            string shader=material.shader!=null?material.shader.name:"<missing>";
            bool standard=shader=="Universal Render Pipeline/Lit"||shader=="Universal Render Pipeline/Simple Lit"||shader=="Universal Render Pipeline/Unlit"||shader=="OdysseyOracle/VoxelDistanceLit";
            bool clip=material.IsKeywordEnabled("_ALPHATEST_ON")||material.HasProperty("_AlphaClip")&&material.GetFloat("_AlphaClip")>.5f;
            result.Known=!result.Skip&&standard&&!clip&&material.shader!=null&&material.shader.isSupported&&!ShaderUtil.ShaderHasError(material.shader)&&result.Cull>=0&&result.Cull<=2;
            w.Materials[material]=result;
            if(!result.Skip&&!result.Known)Unknown(w,"Uncertified material/shader: "+material.name+" / "+shader);
            if(negativeTransform)result.Known=false;return result;
        }
        private static void ReadMesh(Work w,MeshFilter filter)
        {
            if(filter==null)return;Mesh mesh=filter.sharedMesh;MeshRenderer renderer=filter.GetComponent<MeshRenderer>();
            int surface=StructuralSurface(filter.name);
            if(filter.transform.parent!=w.StructuralRoot||surface<0){w.Result.ExcludedNonStructuralMeshes++;return;}
            if(mesh==null||renderer==null||!renderer.enabled||renderer.forceRenderingOff||!renderer.gameObject.activeInHierarchy||
                (w.Camera.cullingMask&(1<<renderer.gameObject.layer))==0)return;
            if(!IntersectsEnvelope(w,renderer.bounds)){w.Result.OutsideEnvelopeBoundsMeshes++;return;}
            Matrix4x4 matrix=filter.transform.localToWorldMatrix;int source=w.Renderers.Count*1024;
            Material[] materials=renderer.sharedMaterials;
            bool materialMismatch=materials.Length!=1||materials[0]==null||materials[0].name!=StructuralMaterials[surface];
            if(materialMismatch){w.Result.StructuralMaterialMismatches++;Unknown(w,"Named structural batch has unexpected material; cannot certify: "+filter.name);}
            var signatures=new int[materials.Length];for(int i=0;i<materials.Length;i++)signatures[i]=MaterialSignature(materials[i]);
            w.Renderers.Add(new RendererState {Filter=filter,Mesh=mesh,Renderer=renderer,Matrix=matrix,Materials=materials,Signatures=signatures});
            try
            {
                using(Mesh.MeshDataArray snapshot=MeshUtility.AcquireReadOnlyMeshData(mesh))
                {
                    Mesh.MeshData data=snapshot[0];if(data.vertexCount>2000000)throw new InvalidOperationException("Per-mesh vertex budget exceeded.");
                    var vertices=new NativeArray<Vector3>(data.vertexCount,Allocator.Temp);
                    try
                    {
                        data.GetVertices(vertices);
                        for(int sub=0;sub<data.subMeshCount;sub++)
                        {
                            var description=data.GetSubMesh(sub);MaterialState state=InspectMaterial(w,sub<materials.Length?materials[sub]:null,matrix.determinant<0f);
                            if(materialMismatch)state.Known=false;
                            if(state.Skip){w.Result.SkippedTransparentSubmeshes++;continue;}
                            if(description.topology!=MeshTopology.Triangles){w.Result.SkippedNonTriangleSubmeshes++;Unknown(w,"Nontriangle submesh: "+filter.name);continue;}
                            if(description.indexCount/3+w.Triangles.Count>w.Settings.MaximumTriangles)throw new InvalidOperationException("Triangle budget exhausted; partial snapshot is not full coverage.");
                            var indices=new NativeArray<int>(description.indexCount,Allocator.Temp);
                            try
                            {
                                data.GetIndices(indices,sub,true);
                                for(int i=0;i+2<indices.Length;i+=3)w.Triangles.Add(new CoverageTriangle {
                                    A=Point(matrix.MultiplyPoint3x4(vertices[indices[i]])),B=Point(matrix.MultiplyPoint3x4(vertices[indices[i+1]])),
                                    C=Point(matrix.MultiplyPoint3x4(vertices[indices[i+2]])),KnownOpaque=state.Known,Cull=state.Cull,Source=source+sub});
                            }
                            finally{if(indices.IsCreated)indices.Dispose();}
                        }
                    }
                    finally{if(vertices.IsCreated)vertices.Dispose();}
                }
                w.Result.ReadMeshFilters++;
            }
            catch(Exception error)
            {
                w.UnknownBounds.Add(new UnknownBound {Bounds=renderer.bounds});Unknown(w,"Unread/partial renderer "+filter.name+": "+error.Message);
                if(error is InvalidOperationException&&error.Message.Contains("budget"))throw;
            }
        }
        private static bool Stable(Work w)
        {
            if(w.StructuralRoot==null||!w.StructuralRoot.gameObject.activeInHierarchy){w.Result.ChangedRenderer="tutorial structural root inactive/destroyed";return false;}
            foreach(var state in w.Renderers)
            {
                if(state.Filter==null||state.Renderer==null||state.Filter.sharedMesh!=state.Mesh||!state.Renderer.enabled||
                state.Renderer.forceRenderingOff||!state.Renderer.gameObject.activeInHierarchy||state.Filter.transform.localToWorldMatrix!=state.Matrix||
                (w.Camera.cullingMask&(1<<state.Renderer.gameObject.layer))==0){w.Result.ChangedRenderer=state.Filter!=null?state.Filter.name+" mesh/transform/visibility/layer":"destroyed included renderer";return false;}
                Material[] materials=state.Renderer.sharedMaterials;if(materials.Length!=state.Materials.Length){w.Result.ChangedRenderer=state.Filter.name+" material slot count";return false;}
                for(int i=0;i<materials.Length;i++)if(materials[i]!=state.Materials[i]||MaterialSignature(materials[i])!=state.Signatures[i]){w.Result.ChangedRenderer=state.Filter.name+" material identity/opaque settings";return false;}
            }
            return true;
        }
        internal static int StructuralSurface(string name)
        {
            const string suffix=" \u2022 combined voxel mesh";
            for(int i=0;i<StructuralSurfaces.Length;i++){
                string end=StructuralSurfaces[i]+suffix;
                if(name==end||name.StartsWith("Tutorial light cell ",StringComparison.Ordinal)&&name.EndsWith(" "+end,StringComparison.Ordinal))return i;
            }
            return -1;
        }
        private static bool IntersectsEnvelope(Work w,Bounds bounds)
        {
            CoveragePoint min=Point(bounds.min),max=Point(bounds.max);
            foreach(var plane in w.Planes)if(plane.U(max)>=plane.U0&&plane.U(min)<=plane.U1&&plane.V(max)>=plane.V0&&plane.V(min)<=plane.V1&&max.Axis(plane.Axis)>=plane.D0&&min.Axis(plane.Axis)<=plane.D1)return true;
            return false;
        }
        private static int MaterialSignature(Material material)
        {
            if(material==null)return 0;
            unchecked{return (material.shader==null?0:material.shader.GetEntityId().GetHashCode())*397^material.renderQueue*31^
                (material.HasProperty("_Cull")?Mathf.RoundToInt(material.GetFloat("_Cull")):2)*7^
                (material.HasProperty("_Surface")&&material.GetFloat("_Surface")>.5f?1:0)*11^
                (material.HasProperty("_AlphaClip")&&material.GetFloat("_AlphaClip")>.5f?1:0)*17^(material.IsKeywordEnabled("_ALPHATEST_ON")?1:0)*23;}
        }
        private static void NewSurface(Work w)
        {
            CoveragePlane p=w.Planes[w.Surface];w.U=DenseCoverageMath.Coordinates(p.U0,p.U1,w.Settings.MaximumSpacingMetres,p.Openings,true);
            w.V=DenseCoverageMath.Coordinates(p.V0,p.V1,w.Settings.MaximumSpacingMetres,p.Openings,false);w.Width=w.U.Length-1;w.Height=w.V.Length-1;
            long cells=(long)w.Width*w.Height;if(w.Result.PlannedCells+cells>w.Settings.MaximumCells)throw new InvalidOperationException("Cumulative cell budget exhausted.");
            w.Flags=new byte[(int)cells];w.Cell=w.Triangle=w.UnknownIndex=0;w.RasterPending=false;w.RunStart=-1;
            w.RectangleUnions.Clear();
            var row=new SurfaceResult {Name=p.Name,Axis=p.Axis,Sign=p.Sign,U0=p.U0,U1=p.U1,V0=p.V0,V1=p.V1,D0=p.D0,D1=p.D1,UCells=w.Width,VCells=w.Height,Planned=cells,AuthoredOpenings=p.Openings};
            w.SurfaceReports[w.Surface]=row;w.Result.PlannedCells+=cells;w.Phase=Phase.Mask;
            w.Writer.WriteLine(JsonUtility.ToJson(new CoordinateRecord {Surface=w.Surface,Name=p.Name,Axis=p.Axis,Sign=p.Sign,D0=p.D0,D1=p.D1,U=w.U,V=w.V,AuthoredOpenings=p.Openings}));
        }
        private static void MaskCell(Work w)
        {
            if(w.Cell>=w.Flags.Length){w.Cell=0;w.Phase=Phase.Raster;return;}
            int x=w.Cell%w.Width,y=w.Cell/w.Width;
            if(DenseCoverageMath.IsOpening(w.Planes[w.Surface],(w.U[x]+w.U[x+1])*.5,(w.V[y]+w.V[y+1])*.5))w.Flags[w.Cell]=DenseCoverageMath.Excluded;
            w.Cell++;
        }
        private static void RasterCell(Work w)
        {
            CoveragePlane plane=w.Planes[w.Surface];
            if(!w.RasterPending)
            {
                ProjectedCoverage p;
                if(w.UnknownIndex<w.UnknownBounds.Count)
                {
                    Bounds b=w.UnknownBounds[w.UnknownIndex++].Bounds;CoveragePoint low=Point(b.min),high=Point(b.max);
                    p=new ProjectedCoverage {Known=false,Rectangle=true,MinU=plane.U(low),MaxU=plane.U(high),MinV=plane.V(low),MaxV=plane.V(high),MinD=low.Axis(plane.Axis),MaxD=high.Axis(plane.Axis)};
                    if(p.MaxD<plane.D0||p.MinD>plane.D1)return;
                }
                else
                {
                    if(w.Triangle>=w.Triangles.Count){w.Cell=0;w.Phase=Phase.Scan;return;}
                    var triangle=w.Triangles[w.Triangle++];
                    if(w.Triangle<w.Triangles.Count&&DenseCoverageMath.TryRectangle(triangle,w.Triangles[w.Triangle],plane,out p))
                    {w.Triangle++;w.Result.RecoveredRectangles++;}
                    else if(!DenseCoverageMath.Project(triangle,plane,out p))return;
                }
                if(p.MaxU<plane.U0||p.MinU>plane.U1||p.MaxV<plane.V0||p.MinV>plane.V1)return;
                w.Projected=p;w.X0=DenseCoverageMath.CellAt(w.U,p.MinU);w.X1=DenseCoverageMath.CellAt(w.U,p.MaxU)+1;
                w.Y0=DenseCoverageMath.CellAt(w.V,p.MinV);w.Y1=DenseCoverageMath.CellAt(w.V,p.MaxV)+1;
                w.X1=Math.Min(w.Width-1,w.X1);w.Y1=Math.Min(w.Height-1,w.Y1);w.X=w.X0;w.Y=w.Y0;w.RasterPending=true;
            }
            int index=w.Y*w.Width+w.X;
            if((w.Flags[index]&(DenseCoverageMath.Excluded|DenseCoverageMath.Certified))==0)
            {
                w.Flags[index]|=DenseCoverageMath.TestCell(w.Projected,plane,w.U[w.X],w.U[w.X+1],w.V[w.Y],w.V[w.Y+1]);w.Result.CellTriangleTests++;
                if(w.Settings.EnableRectangleUnion && w.Projected.Known && w.Projected.Rectangle && (w.Flags[index]&DenseCoverageMath.Certified)==0)
                {
                    bool found=w.RectangleUnions.TryGetValue(index,out CoverageRectangleSet set);
                    if(found || w.RectangleUnions.Count<w.Result.RectangleUnionCellCapacity)
                    {
                        if(set.Add(w.Projected,plane)){w.RectangleUnions[index]=set;if(!found)w.Result.RectangleUnionCandidates++;}
                    }
                    else w.Result.RectangleUnionCapacitySkips++;
                }
            }
            if(++w.X>w.X1){w.X=w.X0;if(++w.Y>w.Y1)w.RasterPending=false;}
        }
        private static void ScanCell(Work w)
        {
            if(w.Cell>=w.Flags.Length){FlushRun(w);w.Writer.Flush();w.Result.CompletedSurfaces++;w.Result.UnvisitedSurfaces=w.Planes.Length-w.Result.CompletedSurfaces;w.Phase=Phase.NewSurface;return;}
            int x=w.Cell%w.Width,y=w.Cell/w.Width;byte flags=w.Flags[w.Cell];int kind=-1;SurfaceResult row=w.SurfaceReports[w.Surface];
            if(w.Settings.EnableRectangleUnion && (flags&(DenseCoverageMath.Excluded|DenseCoverageMath.Certified))==0 &&
                w.RectangleUnions.TryGetValue(w.Cell,out CoverageRectangleSet rectangles) &&
                DenseRectangleUnion.Covers(rectangles,w.U[x],w.U[x+1],w.V[y],w.V[y+1]))
            {flags|=DenseCoverageMath.Certified;row.RectangleUnionCertified++;w.Result.RectangleUnionCertified++;}
            row.Scanned++;w.Result.ScannedCells++;
            if((flags&DenseCoverageMath.Excluded)!=0){row.Excluded++;w.Result.ExcludedCells++;}
            else if((flags&DenseCoverageMath.Certified)!=0){row.Certified++;w.Result.CertifiedCells++;}
            else if((flags&DenseCoverageMath.Unknown)!=0){row.UnknownMaterial++;w.Result.UnknownMaterialCells++;kind=2;}
            else if((flags&DenseCoverageMath.CenterHit)!=0){row.PointOnly++;w.Result.PointOnlyCells++;kind=1;}
            else{row.UncoveredCenters++;w.Result.UncoveredCenters++;kind=0;}
            if(w.RunStart>=0&&(kind!=w.RunKind||y!=w.RunV))FlushRun(w);
            if(kind>=0&&w.RunStart<0){w.RunStart=x;w.RunKind=kind;w.RunV=y;}
            w.Cell++;
        }
        private static void FlushRun(Work w)
        {
            if(w.RunStart<0)return;int end=(w.Cell-1)%w.Width;
            if(w.Result.WrittenRuns<w.Settings.MaximumRuns)
            {
                CoveragePlane p=w.Planes[w.Surface];double v=(w.V[w.RunV]+w.V[w.RunV+1])*.5,u=(w.U[w.RunStart]+w.U[w.RunStart+1])*.5,lastU=(w.U[end]+w.U[end+1])*.5;
                CoveragePoint a=p.Point(w.U[w.RunStart],w.V[w.RunV],p.D0),b=p.Point(w.U[end+1],w.V[w.RunV+1],p.D1);
                var record=new RunRecord {Surface=w.Surface,Kind=w.RunKind==0?"uncovered-center-column":w.RunKind==1?"center-covered-cell-not-certified":"unknown-material-or-unread-mesh",
                    VIndex=w.RunV,UStart=w.RunStart,UEndInclusive=end,BoundsMin=Min(a,b),BoundsMax=Max(a,b),
                    FirstInside=p.Point(u,v,p.Sign>0?p.D0:p.D1),FirstOutside=p.Point(u,v,p.Sign>0?p.D1:p.D0),
                    LastInside=p.Point(lastU,v,p.Sign>0?p.D0:p.D1),LastOutside=p.Point(lastU,v,p.Sign>0?p.D1:p.D0)};
                w.Writer.WriteLine(JsonUtility.ToJson(record));w.Result.WrittenRuns++;
            }
            else{w.Result.DetailsTruncated=true;w.Result.OmittedRuns++;}
            w.RunStart=-1;
        }
        private static void Finish(bool completed,string error)
        {
            Work w=current;if(w==null)return;current=null;EditorApplication.update-=Tick;AssemblyReloadEvents.beforeAssemblyReload-=Reload;EditorApplication.playModeStateChanged-=PlayState;
            try
            {
                if(w.RunStart>=0)FlushRun(w);if(w.Writer!=null){w.Writer.Flush();w.Writer.Dispose();w.Writer=null;}
                Report r=w.Result;
                if(r.WorldSourceSha256!=SourceHash(w.WorldSourcePath)){r.RenderStateChanged=true;completed=false;error+=" World source changed during audit.";}
                if(w.ActiveSlice!=null)r.MaximumObservedSliceMilliseconds=Math.Max(r.MaximumObservedSliceMilliseconds,w.ActiveSlice.Elapsed.TotalMilliseconds);
                r.Complete=completed&&!r.DetailsTruncated;r.Error=error;r.FinishedUtc=DateTime.UtcNow.ToString("o");r.ElapsedSeconds=w.Clock.Elapsed.TotalSeconds;
                r.Surfaces=w.SurfaceReports;r.Unknowns=w.Unknowns.ToArray();
                r.Status=!r.Complete?"incomplete":r.UncoveredCenters+r.PointOnlyCells+r.UnknownMaterialCells>0?"completed-with-uncovered-or-uncertified-cells":"declared-normal-column-surfaces-certified";
                using(var stream=new FileStream(r.SummaryPath,FileMode.CreateNew,FileAccess.Write,FileShare.Read))using(var writer=new StreamWriter(stream))writer.Write(JsonUtility.ToJson(r,true));
                last=r;
            }
            catch(Exception failure){w.Result.Error+=" Final receipt: "+failure.GetType().Name+": "+failure.Message;w.Result.Complete=false;w.Result.Status="incomplete";last=w.Result;}
            finally{if(w.Writer!=null)w.Writer.Dispose();w.Flags=null;w.Triangles.Clear();}
        }
        private static void Unknown(Work w,string value){if(w.Unknowns.Count<256)w.Unknowns.Add(value);}
        private static string SourceHash(string source)
        {if(string.IsNullOrEmpty(source)||!File.Exists(source))return "unavailable";using(var stream=File.OpenRead(source))using(var hash=SHA256.Create())return BitConverter.ToString(hash.ComputeHash(stream)).Replace("-","").ToLowerInvariant();}
        private static CoveragePoint Point(Vector3 x)=>new CoveragePoint(x.x,x.y,x.z);
        private static CoveragePoint Min(CoveragePoint a,CoveragePoint b)=>new CoveragePoint(Math.Min(a.x,b.x),Math.Min(a.y,b.y),Math.Min(a.z,b.z));
        private static CoveragePoint Max(CoveragePoint a,CoveragePoint b)=>new CoveragePoint(Math.Max(a.x,b.x),Math.Max(a.y,b.y),Math.Max(a.z,b.z));

        private static CoveragePlane[] BuildContract()
        {
            var planes=new List<CoveragePlane>(36);float[] centers={-3f,8f,18f};
            foreach(int side in new[]{-1,1})
            {
                // Include the actual inward arch-foot face:5.42 - .88/2 =4.98m.
                // Starting at5m placed the ray column INSIDE this opaque foot; its outward face is correctly culled.
                // This changes only column extent; aperture masks,2-micrometre tolerance and facing checks stay exact.
                const double alcoveFootInnerFace=5.42-.88*.5-DenseCoverageMath.GeometryTolerance;
                var apertures=new List<CoverageMask>();foreach(float c in centers){apertures.Add(new CoverageMask("Alcove base clear "+c,c-1.86,c+1.86,-.03,.4));
                    apertures.Add(new CoverageMask("Alcove jamb clear "+c,c-1.955,c+1.955,.4,3.82));apertures.Add(new CoverageMask("Alcove arch first clear "+c,c-1.445,c+1.445,3.82,4.1));
                    apertures.Add(new CoverageMask("Alcove arch second clear "+c,c-.845,c+.845,4.1,4.24));}
                Add(planes,"Hall wall "+side,0,side,-14.1,28.1,-.03,7.4,side<0?-6.25:alcoveFootInnerFace,side<0?-alcoveFootInnerFace:6.25,apertures.ToArray());
            }
            Add(planes,"Hall floor",1,-1,-5.8,5.8,-14.1,28.1,-.20,.10);
            Add(planes,"Hall roof",1,1,-5.8,5.8,-14.1,28.1,6.8,7.6);
            Add(planes,"Solid arrival end; no door exemption",2,-1,-5.8,5.8,-.03,7.4,-14.8,-13.85);
            Add(planes,"Solid rear end; no portal exemption",2,1,-5.8,5.8,-.03,7.4,27.85,28.6);
            foreach(float c in centers)foreach(int side in new[]{-1,1})
            {
                double x0=side<0?-9.85:5.3,x1=side<0?-5.3:9.85;
                Add(planes,"Alcove floor "+side+"/"+c,1,-1,x0,x1,c-2.2,c+2.2,-.20,.10);
                Add(planes,"Alcove roof "+side+"/"+c,1,1,side<0?-10.0:5.4,side<0?-5.4:10.0,c-2.2,c+2.2,4.85,5.5);
                Add(planes,"Alcove side negative "+side+"/"+c,2,-1,x0,x1,-.03,5.15,c-2.7,c-2.10);
                Add(planes,"Alcove side positive "+side+"/"+c,2,1,x0,x1,-.03,5.15,c+2.10,c+2.7);
                var windows=new List<CoverageMask>();
                if(side>0)
                {
                    // Only clear spaces between authored iron bars are exempt.
                    double[] z0={-1.28,-.9425,-.4525,.0375,.5275,1.0175},z1={-1.0175,-.5275,-.0375,.4525,.9425,1.28};
                    double[] y0={1.79,2.3875,3.5375},y1={2.3125,3.4625,4.39};
                    for(int z=0;z<z0.Length;z++)for(int y=0;y<y0.Length;y++)windows.Add(new CoverageMask("Moon window clear between bars "+c+"/"+z+"/"+y,c+z0[z],c+z1[z],y0[y],y1[y]));
                }
                Add(planes,"Alcove outer "+side+"/"+c,0,side,c-2.2,c+2.2,-.03,5.15,side<0?-10.3:9.0,side<0?-9.0:10.3,windows.ToArray());
            }
            return planes.ToArray();
        }
        private static void Add(List<CoveragePlane> output,string name,int axis,int sign,double u0,double u1,double v0,double v1,double d0,double d1,CoverageMask[] openings=null)
        {output.Add(new CoveragePlane {Name=name,Axis=axis,Sign=sign,U0=u0,U1=u1,V0=v0,V1=v1,D0=d0,D1=d1,Openings=openings??Array.Empty<CoverageMask>()});}
    }
}
#endif
