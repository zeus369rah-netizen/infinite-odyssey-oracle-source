using UnityEngine;

namespace OdysseyOracle
{
    public sealed partial class RefugeActivityDirector
    {
        AudioClip[] npcBanter015;
        int[] npcVisits015;
        float[] npcLast015;
        static readonly string[] NpcBanterCaptions015={
            "Good steel takes patience. A good tower takes a plan.",
            "Bring your next idea to the anvil. We'll give it some weight.",
            "The runes are listening. Try a different element and see what answers.",
            "Even a small spark can change a battle. Practice makes it yours.",
            "A quiet gate is a good gate. Let's keep it that way.",
            "I'll watch the portal. You watch each other's backs.",
            "The cauldron has opinions. Most of them are purple.",
            "Bring me a little patience, Oracle. The sparks are the easy part."
        };
        void InitializeNpcBanter015()
        {
            npcBanter015=new AudioClip[8];npcVisits015=new int[4];npcLast015=new float[4];
            string[] roles={"smith","ilyra","bram","raykon"};
            for(int role=0;role<4;role++)
            {
                npcLast015[role]=-100f;
                npcBanter015[role*2]=Resources.Load<AudioClip>(role==0?"Tutorial/Smith015/banter_a015":"Tutorial/npc_"+roles[role]+"_banter_a015");
                npcBanter015[role*2+1]=Resources.Load<AudioClip>(role==0?"Tutorial/Smith015/banter_b015":"Tutorial/npc_"+roles[role]+"_banter_b015");
                if(npcBanter015[role*2]!=null)npcBanter015[role*2].LoadAudioData();
                if(npcBanter015[role*2+1]!=null)npcBanter015[role*2+1].LoadAudioData();
            }
        }
        bool TryChooseNpcLine015(int role,out AudioClip clip,out string caption)
        {
            clip=null;caption=null;int index=role-1;
            if(index<0||index>=4||npcVisits015==null||Time.unscaledTime-npcLast015[index]<8f)return false;
            int visit=npcVisits015[index];
            if(visit==0)
            {
                clip=role==4?raykonVoice:role==3?npcBram009:role==2?npcIlyra009:npcSmith009;
                caption=NpcGreetingCaption009(role);
            }
            else
            {
                int take=index*2+(visit-1)%2;clip=npcBanter015[take];
                caption=NpcBanterCaptions015[take];
            }
            // Count accepted explicit interactions, including subtitle-only muted visits.
            // Saturated three-state cycle cannot overflow during a long session.
            npcVisits015[index]=visit==0?1:visit==1?2:1;
            npcLast015[index]=Time.unscaledTime;
            return true;
        }
    }
}

// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Four existing accepted NPC roles and conversation UI; no movement, quest or economy authority.
// Array Formats: Eight cold clip references/exact caption literals, four bounded visit/cooldown slots; one shared source.
// Dependencies/Pointers: Existing voice owner/mute/focus/dialogue fences remain; explicit interaction only and no new Update, sample arrays or runtime resource loads.
