using System;
using UnityEngine;

namespace OdysseyOracle
{
    public sealed class RigidPosePilot015:ScriptableObject
    {
        public const string ResourcePath="Animation015/Biped/Pilot015";
        [SerializeField] RuntimeAnimatorController controller;
        [SerializeField] Avatar avatar;
        [SerializeField] bool bindingVerified,pilotEnabled;
        [SerializeField] string verification;
        public RuntimeAnimatorController Controller=>controller;
        public Avatar Avatar=>avatar;
        public bool BindingVerified=>bindingVerified;
        public bool PilotEnabled=>pilotEnabled;
        public bool Ready=>bindingVerified&&controller!=null&&avatar!=null&&avatar.isValid;
#if UNITY_EDITOR
        public void ConfigureDisabled(RuntimeAnimatorController value,Avatar rig,string proof,bool verified)
        {controller=value;avatar=rig;verification=proof;bindingVerified=verified;pilotEnabled=false;}
        public void EnableVerifiedPilot(bool enabled)
        {if(enabled&&!Ready)throw new InvalidOperationException("Native binding verification is required.");pilotEnabled=enabled;}
#endif
    }
}
// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Shared original rigid-pivot controller assets, no gameplay coordinates/state.
// Array Formats: One borrowed flyweight profile/controller/avatar; default booleans OFF, no runtime clip generation.
// Dependencies/Pointers: Root-native editor factory alone verifies/enables pilot. Actor graphs never destroy these shared assets or authorize gameplay.
