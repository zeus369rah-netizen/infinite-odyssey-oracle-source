using System;
using UnityEngine;

namespace OdysseyOracle
{
    [CreateAssetMenu(menuName="Infinite Odyssey/Verified soundtrack009 binding")]
    public sealed class SoundtrackBinding009:ScriptableObject
    {
        [SerializeField] int schema=1;
        [SerializeField] int verifiedMask;
        [SerializeField] string importReceiptSha256;
        public bool Valid=>schema==1&&ValidHash(importReceiptSha256);
        public uint VerifiedMask=>Valid?(uint)verifiedMask&MusicPlaylistCatalog009.All:0;
        public string ImportReceiptSha256=>importReceiptSha256;
        public void SetVerifiedTracks(uint mask,string receiptSha256)
        {if(!ValidHash(receiptSha256))throw new ArgumentException("Pin the root import/provenance receipt before binding recordings.");schema=1;verifiedMask=(int)(mask&MusicPlaylistCatalog009.All);importReceiptSha256=receiptSha256;}
        static bool ValidHash(string value)
        {if(value==null||value.Length!=64)return false;for(int i=0;i<value.Length;i++){char c=value[i];if(!(c>='0'&&c<='9'||c>='a'&&c<='f'||c>='A'&&c<='F'))return false;}return true;}
    }
}
// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Cold Resources binding only; no audio samples/spatial state or Unity-native field initializer.
// Array Formats: Schema1 plus18-bit verified-resource mask and SHA256 receipt reference; contains no AudioClip references and cannot preload/decompress the library.
// Dependencies/Pointers: Root verifies/imports each exact resource and authors this asset at Audio/MusicExpansion009/library009. A receipt reference is a root assertion, not independent licensing/listening proof; null/invalid mask is safe pending.
