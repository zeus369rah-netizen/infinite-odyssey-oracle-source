using System;
using UnityEngine;

namespace OdysseyOracle
{
    /// <summary>Sound follows measured controller travel, never keys or camera bob.</summary>
    [DisallowMultipleComponent]
    public sealed class OracleFoley : MonoBehaviour
    {
        readonly RaycastHit[] floorHits=new RaycastHit[12];
        GameSession session;
        CharacterController controller;
        Vector3 previous;
        float distance,airTime,nextStep;
        bool baseline,wasGrounded,alternate,wasPlayable;
        string surface="stone";
        public void Initialize(GameSession owner){session=owner;controller=GetComponent<CharacterController>();baseline=false;}
        void LateUpdate()
        {
            if(session==null||controller==null||!controller.enabled)return;
            Vector3 position=transform.position;bool grounded=controller.isGrounded;
            bool playable=session.IsPlaying&&Application.isFocused;
            if(!baseline||!playable||!wasPlayable){ResetBaseline(position,grounded);wasPlayable=playable;return;}
            float dt=Mathf.Clamp(Time.deltaTime,0f,Time.maximumDeltaTime);
            Vector3 moved=position-previous;previous=position;
            if(dt<=.0001f||moved.sqrMagnitude>16f){ResetBaseline(position,grounded);return;}
            float planar=new Vector2(moved.x,moved.z).magnitude;
            if(!grounded)
            {
                airTime+=dt;
                if(wasGrounded&&moved.y/dt>1.5f)session.Audio?.PlayCue("jump",position);
            }
            else
            {
                if(!wasGrounded&&airTime>=.15f)session.Audio?.PlayCue("land_"+ProbeSurface(position),position);
                airTime=0f;
                if(planar>.0005f)
                {
                    distance+=planar;float stride=planar/dt>4.4f?1.75f:1.4f;
                    if(distance>=stride&&Time.unscaledTime>=nextStep)
                    {
                        distance%=stride;nextStep=Time.unscaledTime+.18f;alternate=!alternate;
                        session.Audio?.PlayCue("footstep_"+ProbeSurface(position)+(alternate?"_01":"_02"),position);
                    }
                }
            }
            wasGrounded=grounded;
        }
        void ResetBaseline(Vector3 position,bool grounded){previous=position;wasGrounded=grounded;baseline=true;distance=airTime=0f;}
        string ProbeSurface(Vector3 position)
        {
            int count=Physics.RaycastNonAlloc(position+Vector3.up*.3f,Vector3.down,floorHits,1f,~(1<<2),QueryTriggerInteraction.Ignore);
            Collider floor=null;float nearest=float.PositiveInfinity;
            for(int i=0;i<count;i++)
            {
                RaycastHit hit=floorHits[i];
                if(hit.collider==null||hit.collider==controller||hit.normal.y<.5f||hit.distance>=nearest||
                    hit.collider.GetComponentInParent<OracleEnemy>()!=null||hit.collider.GetComponentInParent<OraclePlayer>()!=null)continue;
                nearest=hit.distance;floor=hit.collider;
            }
            if(floor==null)return surface;
            Renderer renderer=floor.GetComponent<Renderer>();
            string material=renderer!=null&&renderer.sharedMaterial!=null?renderer.sharedMaterial.name:"";
            surface=Wood(floor.name)||Wood(material)?"wood":"stone";return surface;
        }
        static bool Wood(string value)=>value.IndexOf("wood",StringComparison.OrdinalIgnoreCase)>=0||
            value.IndexOf("timber",StringComparison.OrdinalIgnoreCase)>=0||value.IndexOf("plank",StringComparison.OrdinalIgnoreCase)>=0;
        void OnEnable(){baseline=wasPlayable=false;}
    }
}
