using System;
using UnityEngine;
namespace OdysseyOracle
{
    /// <summary>Optional root-run Play-mode fixture. Never installed/invoked by the staging helper.</summary>
    public static class ConfirmedWandNativeChecks
    {
        static readonly Color[] Palette={new Color(1f,.36f,.12f),new Color(.18f,.68f,1f),new Color(.62f,.78f,.29f),new Color(.66f,.91f,1f)};
        public static int RunCurrentContext()
        {
            var session=GameSession.Instance;
            if(!Application.isPlaying||session==null||session.Viewmodel==null||session.Player==null)throw new InvalidOperationException("Requires an initialized isolated Play-mode review context.");
            var model=session.Viewmodel;var root=session.Player.ViewCamera.transform.Find("Oracle first-person hands and wrapped wand");
            if(root==null)throw new Exception("Original camera-local root missing.");
            var body=root.Find("Right hand and astral focus/Combined Oracle viewmodel").GetComponent<MeshRenderer>();
            var arcs=root.Find("Right hand and astral focus/Bounded green focus electricity").GetComponent<MeshRenderer>();
            int choice=model.DisplayedConfirmedMask,chain=model.DisplayedIngredientMask;bool ready=model.FinisherCueVisible;
            Vector3 scale=root.localScale;int checks=0;var block=new MaterialPropertyBlock();
            var bodyMesh=body.GetComponent<MeshFilter>().sharedMesh;var arcMesh=arcs.GetComponent<MeshFilter>().sharedMesh;
            int bodyVertices=bodyMesh.vertexCount,arcVertices=arcMesh.vertexCount;var materials=arcs.sharedMaterials;
            Action<bool,string> require=(value,message)=>{checks++;if(!value)throw new Exception(message);};
            try
            {
                require(materials.Length==4&&arcMesh.subMeshCount==4,"Four original arc slots required.");
                for(int i=1;i<4;i++)require(materials[i]==materials[0],"Existing one shared filament material is reused.");
                for(int mask=1;mask<16;mask++)if(ConfirmedWandAppearanceState.ValidConfirmedMask(mask))
                {
                    model.ResetConfirmedAppearance(session.Combat!=null?session.Combat.OwnCosmicNovaCastCount:0);
                    model.SetConfirmedElementAppearance(mask,0,false);
                    require(model.DisplayedConfirmedMask==mask&&model.DisplayedIngredientMask==0,"Confirmed choice is separate from accepted charge.");
                    Color mixed=Color.black;int bits=0;
                    for(int i=0;i<4;i++)if((mask&(1<<i))!=0){mixed+=Palette[i];bits++;}
                    mixed/=bits;body.GetPropertyBlock(block,9);Color lens=block.GetColor(Shader.PropertyToID("_BaseColor"));
                    require(Mathf.Abs(lens.r-mixed.r)<.00001f&&Mathf.Abs(lens.g-mixed.g)<.00001f&&Mathf.Abs(lens.b-mixed.b)<.00001f,"Existing crystal lens contains both confirmed ingredients before an accepted shot.");
                    for(int arc=0;arc<4;arc++)if((mask&(1<<arc))!=0)
                    {
                        arcs.GetPropertyBlock(block,arc);Color actual=block.GetColor(Shader.PropertyToID("_BaseColor"));
                        Color expected=Palette[arc]*1.30f;expected.a=1f;
                        require(Mathf.Abs(actual.r-expected.r)<.00001f&&Mathf.Abs(actual.g-expected.g)<.00001f&&Mathf.Abs(actual.b-expected.b)<.00001f,"Each confirmed ingredient retains its original RGB in its own MPB arc slot.");
                    }
                    for(int accepted=0;accepted<15;accepted++)
                    {
                        model.SetConfirmedElementAppearance(mask,accepted,true);
                        require(model.DisplayedIngredientMask==accepted,"Accepted partial chain is retained exactly.");
                        require(model.FinisherCueVisible==(accepted!=0&&(mask|accepted)==15&&(mask&~accepted)!=0),"Ready requires actual complementary masks.");
                        if(!model.FinisherCueVisible)for(int arc=0;arc<4;arc++)if((accepted&(1<<arc))!=0)
                        {
                            arcs.GetPropertyBlock(block,arc);Color actual=block.GetColor(Shader.PropertyToID("_BaseColor"));Color expected=Palette[arc]*1.65f;
                            require(Mathf.Abs(actual.r-expected.r)<.00001f&&Mathf.Abs(actual.g-expected.g)<.00001f&&Mathf.Abs(actual.b-expected.b)<.00001f,"Each accepted ingredient gets the stronger exact-RGB arc paint.");
                        }
                        require(root.localScale==scale&&body.enabled&&arcs.enabled,"Cosmetic changes cannot hide, resize or move the wand.");
                    }
                }
                model.PulseCosmicNova();
                require(!model.FinisherCueVisible&&body.enabled&&arcs.enabled,"Accepted Nova pulse leaves the complete held model enabled.");
                model.ResetConfirmedAppearance();
                require(model.DisplayedConfirmedMask==0&&model.DisplayedIngredientMask==0&&!model.FinisherCueVisible,"Reset clears paint state only.");
                require(bodyMesh.vertexCount==bodyVertices&&arcMesh.vertexCount==arcVertices,"No meshes or geometry are added.");
                bool expectedVisible=session.HasStarted&&(session.CombatUnlocked||session.Tutorial!=null&&session.Tutorial.WandPreviewVisible)&&!session.Victory&&!session.IsDowned;
                require(root.gameObject.activeSelf==expectedVisible,"Visibility remains the verified combat/paused-guide policy.");
                require(Mathf.Approximately(root.localScale.x,root.localScale.y)&&Mathf.Approximately(root.localScale.y,root.localScale.z)&&model.CameraClearanceScale>=.28f,"Camera clearance is still bounded uniform scale.");
                return checks;
            }
            finally
            {
                model.ResetConfirmedAppearance(session.Combat!=null?session.Combat.OwnCosmicNovaCastCount:0);
                model.SetConfirmedElementAppearance(choice,chain,ready);
            }
        }
    }
}
