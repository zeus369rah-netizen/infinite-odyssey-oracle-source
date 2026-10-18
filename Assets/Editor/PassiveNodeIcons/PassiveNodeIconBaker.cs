#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;
using Object=UnityEngine.Object;
using Recipes=OdysseyOracle.InterfaceArt.Authoring.PassiveNodeIconRecipes;

namespace OdysseyOracle.InterfaceArt.Authoring.Editor
{
    // # ARCHITECTURE SNAPSHOT
    // Explicit stopped-Editor authoring only. Pure integer polygons -> original PNG,
    // SVG master, provenance JSON and twelve public data-provider sprite rectangles.
    // No automatic callbacks, Resources, packages, asset replacement or paid media.
    public static class PassiveNodeIconBaker
    {
        public const string OutputRoot="Assets/Art/Interface/PassiveNodeIcons_v1";
        const string Png=OutputRoot+"/PassiveNodes.png",Svg=OutputRoot+"/PassiveNodes_Master.svg",Meta=OutputRoot+"/PassiveNodes_Master.json",Library=OutputRoot+"/PassiveNodeIconLibrary.asset";
        [Serializable] public sealed class IconRecord {public int bitId,bitValue,element,node;public string name,title,meaning,spriteId;public int[] rect;}
        [Serializable] public sealed class Master
        {
            public string generator,recipeFingerprint,createdUtc,provenance,effectContract,pngSha256,svgSha256;public int version,width,height,cell;
            public IconRecord[] icons;
        }
        [Serializable] public sealed class Receipt {public string utc,status,exception="",fingerprint,outputRoot;public bool assetsWritten,idempotent,cleaned;public int sprites;}
        public static string BakeLibrary()
        {
            var result=new Receipt{utc=DateTime.UtcNow.ToString("o"),status="not run",outputRoot=OutputRoot};
            var created=new List<string>(4);var folders=new List<string>(3);Texture2D texture=null;PassiveNodeIconLibrary library=null;
            try
            {
                RequireStopped();var recipes=Recipes.BuildAll();string fingerprint=Recipes.Fingerprint(recipes);result.fingerprint=fingerprint;
                if(Directory.Exists(Absolute(OutputRoot))||AssetDatabase.IsValidFolder(OutputRoot))
                {ValidateExisting(recipes,fingerprint);result.idempotent=true;result.sprites=Recipes.Count;result.cleaned=true;result.status="Identical original icon library verified; no writes.";return JsonUtility.ToJson(result,true);}
                EnsureFolder(OutputRoot,folders);
                int width=Recipes.Columns*Recipes.Cell,height=Recipes.Rows*Recipes.Cell;var pixels=new Color32[width*height];
                foreach(var recipe in recipes)
                {
                    byte[] rgba=Recipes.Rasterize(recipe,Recipes.Cell);int startX=recipe.Node*Recipes.Cell,startY=(Recipes.Rows-1-recipe.Element)*Recipes.Cell;
                    for(int y=0;y<Recipes.Cell;y++)for(int x=0;x<Recipes.Cell;x++){int i=(y*Recipes.Cell+x)*4;pixels[(startY+Recipes.Cell-1-y)*width+startX+x]=new Color32(rgba[i],rgba[i+1],rgba[i+2],rgba[i+3]);}
                }
                texture=new Texture2D(width,height,TextureFormat.RGBA32,false,false){name="Original passive node atlas",filterMode=FilterMode.Point,wrapMode=TextureWrapMode.Clamp};
                texture.SetPixels32(pixels);texture.Apply(false,false);byte[] png=texture.EncodeToPNG();string svg=MasterSvg(recipes);
                var master=new Master{generator=Recipes.Generator,version=Recipes.Version,recipeFingerprint=fingerprint,createdUtc=DateTime.UtcNow.ToString("o"),width=width,height=height,cell=Recipes.Cell,
                    provenance="Twelve original integer-polygon recipes authored for ZeusRa Oracle's Return. No external image/model/font, paid generation or gameplay capture. Gold/element contrast harmonizes with the retained four-element emblem art; no source pixels were sampled or copied.",
                    effectContract="meaningful-passives-003: Fire damage; Water longer50%slow; Earth damage/interruption; Air movement. Every branch retains own damage training. All accepted ingredient recovery remains4s. Art does not encode learned/pending/confirmed state or grant an upgrade.",
                    pngSha256=Hash(png),svgSha256=Hash(Encoding.UTF8.GetBytes(svg)),icons=new IconRecord[Recipes.Count]};
                for(int i=0;i<recipes.Length;i++){var r=recipes[i];master.icons[i]=new IconRecord{bitId=r.BitId,bitValue=r.BitValue,element=r.Element,node=r.Node,name=r.SpriteName,title=r.Title,meaning=r.ArtMeaning,spriteId=Recipes.StableSpriteId(r),rect=new[]{r.Node*Recipes.Cell,(Recipes.Rows-1-r.Element)*Recipes.Cell,Recipes.Cell,Recipes.Cell}};}
                WriteNew(Png,png,created);WriteNew(Svg,Encoding.UTF8.GetBytes(svg),created);WriteNew(Meta,Encoding.UTF8.GetBytes(JsonUtility.ToJson(master,true)),created);
                AssetDatabase.ImportAsset(Png,ImportAssetOptions.ForceSynchronousImport);
                var importer=AssetImporter.GetAtPath(Png) as TextureImporter;if(importer==null)throw new InvalidOperationException("New PNG importer unavailable; no sprite modification attempted.");
                importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Multiple;importer.spritePixelsPerUnit=Recipes.Cell;
                var textureSettings=new TextureImporterSettings();importer.ReadTextureSettings(textureSettings);textureSettings.spriteMeshType=SpriteMeshType.FullRect;importer.SetTextureSettings(textureSettings);
                importer.alphaIsTransparency=true;importer.sRGBTexture=true;importer.mipmapEnabled=false;
                importer.npotScale=TextureImporterNPOTScale.None;importer.textureCompression=TextureImporterCompression.Uncompressed;importer.filterMode=FilterMode.Point;importer.wrapMode=TextureWrapMode.Clamp;importer.maxTextureSize=1024;importer.isReadable=false;
                importer.SaveAndReimport();
                var importedTexture=AssetDatabase.LoadAssetAtPath<Texture2D>(Png);
                if(importedTexture==null||importedTexture.width!=width||importedTexture.height!=height)throw new InvalidOperationException("Imported dimensions differ from original master; abort slicing instead of guessing scaled rectangles.");
                var provider=Provider(importer,true);var rects=new SpriteRect[Recipes.Count];var pairs=new List<SpriteNameFileIdPair>(Recipes.Count);
                for(int i=0;i<rects.Length;i++){var record=master.icons[i];var id=new GUID(record.spriteId);rects[i]=new SpriteRect{name=record.name,spriteID=id,rect=new Rect(record.rect[0],record.rect[1],Recipes.Cell,Recipes.Cell),pivot=new Vector2(.5f,.5f),alignment=SpriteAlignment.Center,border=Vector4.zero};pairs.Add(new SpriteNameFileIdPair(record.name,id));}
                var names=provider.GetDataProvider<ISpriteNameFileIdDataProvider>();if(names==null)throw new InvalidOperationException("Name/fileID provider absent; operation aborted, no metadata fallback.");
                provider.SetSpriteRects(rects);names.SetNameFileIdPairs(pairs);provider.Apply();importer.SaveAndReimport();
                library=ScriptableObject.CreateInstance<PassiveNodeIconLibrary>();library.name="Original twelve passive node icons";library.RecipeFingerprint=fingerprint;library.Icons=ReadSprites(recipes);
                if(File.Exists(Absolute(Library))||AssetDatabase.LoadMainAssetAtPath(Library)!=null)throw new InvalidOperationException("New library asset path required.");
                AssetDatabase.CreateAsset(library,Library);created.Add(Library);AssetDatabase.SaveAssetIfDirty(library);
                AssetDatabase.ImportAsset(Svg,ImportAssetOptions.ForceSynchronousImport);AssetDatabase.ImportAsset(Meta,ImportAssetOptions.ForceSynchronousImport);
                ValidateExisting(recipes,fingerprint);result.assetsWritten=true;result.sprites=Recipes.Count;result.status="Original PNG/master metadata and12sprites baked; native32/64px visual review still required.";
            }
            catch(Exception error)
            {
                result.status="Icon bake failed; no visual acceptance claimed.";result.exception=error.ToString();
                for(int i=created.Count-1;i>=0;i--)if(created[i].StartsWith(OutputRoot+"/",StringComparison.Ordinal)&&!AssetDatabase.DeleteAsset(created[i]))result.exception+=" Unremoved newly created output: "+created[i];
                for(int i=folders.Count-1;i>=0;i--)if(Directory.Exists(Absolute(folders[i]))&&Directory.GetFileSystemEntries(Absolute(folders[i])).Length==0)AssetDatabase.DeleteAsset(folders[i]);
                if(Directory.Exists(Absolute(OutputRoot))&&Directory.GetFileSystemEntries(Absolute(OutputRoot)).Length>0)result.exception+=" Output is retained for explicit review; no untracked file was deleted.";
            }
            finally
            {if(texture!=null)Object.DestroyImmediate(texture);if(library!=null&&!EditorUtility.IsPersistent(library))Object.DestroyImmediate(library);result.cleaned=texture==null&&(library==null||EditorUtility.IsPersistent(library));}
            return JsonUtility.ToJson(result,true);
        }
        public static string ValidateExistingLibrary()
        {RequireStopped();var recipes=Recipes.BuildAll();string hash=Recipes.Fingerprint(recipes);ValidateExisting(recipes,hash);return "Verified12 original node IDs, pixels, sprite rects and explicit library references: "+hash;}
        static ISpriteEditorDataProvider Provider(TextureImporter importer,bool modifying)
        {
            var factories=new SpriteDataProviderFactories();factories.Init();var provider=factories.GetSpriteEditorDataProviderFromObject(importer);
            if(provider==null)throw new InvalidOperationException("Sprite data provider unavailable; operation aborted.");provider.InitSpriteEditorDataProvider();
            if(modifying)
            {
                var edit=provider.GetDataProvider<ISpriteFrameEditCapability>();if(edit==null)throw new InvalidOperationException("Sprite edit capability unavailable; operation aborted.");
                var capability=edit.GetEditCapability();foreach(var required in new[]{EEditCapability.CreateAndDeleteSprite,EEditCapability.EditSpriteName,EEditCapability.EditSpriteRect,EEditCapability.EditPivot,EEditCapability.EditBorder})
                    if(!capability.HasCapability(required))throw new InvalidOperationException("Sprite operation unsupported: "+required+". Operation aborted without bypass.");
            }
            return provider;
        }
        static void ValidateExisting(Recipes.Recipe[] recipes,string fingerprint)
        {
            if(!File.Exists(Absolute(Meta)))throw new InvalidOperationException("Existing output has no original manifest; refusing overwrite.");
            var m=JsonUtility.FromJson<Master>(File.ReadAllText(Absolute(Meta),Encoding.UTF8));
            if(m==null||m.generator!=Recipes.Generator||m.version!=Recipes.Version||m.recipeFingerprint!=fingerprint||m.icons==null||m.icons.Length!=Recipes.Count||m.width!=Recipes.Cell*Recipes.Columns||m.height!=Recipes.Cell*Recipes.Rows||m.pngSha256!=Hash(File.ReadAllBytes(Absolute(Png)))||m.svgSha256!=Hash(File.ReadAllBytes(Absolute(Svg))))throw new InvalidOperationException("Existing original atlas/master changed; refusing overwrite.");
            if(File.ReadAllText(Absolute(Svg),Encoding.UTF8)!=MasterSvg(recipes))throw new InvalidOperationException("Saved vector master differs from original recipes.");
            ValidateOriginalPixels(recipes,m.width,m.height);
            var importer=AssetImporter.GetAtPath(Png) as TextureImporter;
            var imported=AssetDatabase.LoadAssetAtPath<Texture2D>(Png);
            if(importer==null||imported==null||imported.width!=m.width||imported.height!=m.height||importer.textureType!=TextureImporterType.Sprite||importer.spriteImportMode!=SpriteImportMode.Multiple||importer.mipmapEnabled||importer.filterMode!=FilterMode.Point||importer.textureCompression!=TextureImporterCompression.Uncompressed||!importer.sRGBTexture||!importer.alphaIsTransparency||importer.wrapMode!=TextureWrapMode.Clamp||importer.npotScale!=TextureImporterNPOTScale.None||Mathf.Abs(importer.spritePixelsPerUnit-Recipes.Cell)>.0001f)throw new InvalidOperationException("Existing sprite importer differs; refusing modification.");
            var settings=new TextureImporterSettings();importer.ReadTextureSettings(settings);if(settings.spriteMeshType!=SpriteMeshType.FullRect)throw new InvalidOperationException("Existing sprite mesh setting differs; refusing modification.");
            var rects=Provider(importer,false).GetSpriteRects();if(rects.Length!=Recipes.Count)throw new InvalidOperationException("Existing atlas has wrong sprite count.");
            var sprites=ReadSprites(recipes);var library=AssetDatabase.LoadAssetAtPath<PassiveNodeIconLibrary>(Library);
            if(!library||library.RecipeFingerprint!=fingerprint||library.Icons==null||library.Icons.Length!=Recipes.Count)throw new InvalidOperationException("Original explicit icon library differs.");
            for(int i=0;i<recipes.Length;i++)
            {
                var recipe=recipes[i];var record=m.icons[i];if(record.bitId!=i||record.bitValue!=(1<<i)||record.element!=recipe.Element||record.node!=recipe.Node||record.name!=recipe.SpriteName||record.title!=recipe.Title||record.meaning!=recipe.ArtMeaning||record.spriteId!=Recipes.StableSpriteId(recipe)||record.rect==null||record.rect.Length!=4||record.rect[0]!=recipe.Node*Recipes.Cell||record.rect[1]!=(Recipes.Rows-1-recipe.Element)*Recipes.Cell||record.rect[2]!=Recipes.Cell||record.rect[3]!=Recipes.Cell||library.Icons[i]!=sprites[i])throw new InvalidOperationException("Stable node identity/library changed.");
                bool found=false;foreach(var rect in rects)if(rect.name==recipe.SpriteName){if(found||rect.spriteID.ToString()!=record.spriteId||rect.rect!=new Rect(recipe.Node*Recipes.Cell,(Recipes.Rows-1-recipe.Element)*Recipes.Cell,Recipes.Cell,Recipes.Cell)||rect.pivot!=new Vector2(.5f,.5f)||rect.border!=Vector4.zero)throw new InvalidOperationException("Stable sprite name/rect/pivot changed.");found=true;}if(!found)throw new InvalidOperationException("Missing stable node sprite.");
            }
            var allowed=new HashSet<string>(new[]{Png,Svg,Meta,Library},StringComparer.Ordinal);int count=0;
            foreach(string file in Directory.EnumerateFiles(Absolute(OutputRoot),"*",SearchOption.AllDirectories)){if(++count>16)throw new InvalidOperationException("Unexpected output count.");if(file.EndsWith(".meta",StringComparison.OrdinalIgnoreCase))continue;string path=OutputRoot+file.Substring(Absolute(OutputRoot).Length).Replace('\\','/');if(!allowed.Contains(path))throw new InvalidOperationException("Foreign file in original atlas folder; no writes.");}
        }
        static Sprite[] ReadSprites(Recipes.Recipe[] recipes)
        {
            var result=new Sprite[Recipes.Count];int count=0;foreach(var asset in AssetDatabase.LoadAllAssetsAtPath(Png))if(asset is Sprite sprite){int bit=-1;for(int i=0;i<recipes.Length;i++)if(sprite.name==recipes[i].SpriteName){bit=i;break;}if(bit<0||result[bit]!=null)throw new InvalidOperationException("Unknown/duplicate imported passive sprite.");result[bit]=sprite;count++;}
            if(count!=Recipes.Count)throw new InvalidOperationException("Not all12sprites imported.");return result;
        }
        static void ValidateOriginalPixels(Recipes.Recipe[] recipes,int width,int height)
        {
            Texture2D decoded=null;
            try
            {
                decoded=new Texture2D(2,2,TextureFormat.RGBA32,false,false);
                if(!ImageConversion.LoadImage(decoded,File.ReadAllBytes(Absolute(Png)),false)||decoded.width!=width||decoded.height!=height)throw new InvalidOperationException("Original PNG cannot be decoded at authored dimensions.");
                var pixels=decoded.GetPixels32();
                foreach(var recipe in recipes)
                {
                    byte[] expected=Recipes.Rasterize(recipe,Recipes.Cell);int startX=recipe.Node*Recipes.Cell,startY=(Recipes.Rows-1-recipe.Element)*Recipes.Cell;
                    for(int y=0;y<Recipes.Cell;y++)for(int x=0;x<Recipes.Cell;x++){int index=(y*Recipes.Cell+x)*4;Color32 observed=pixels[(startY+Recipes.Cell-1-y)*width+startX+x];if(observed.r!=expected[index]||observed.g!=expected[index+1]||observed.b!=expected[index+2]||observed.a!=expected[index+3])throw new InvalidOperationException("Saved PNG pixels differ from actual original vector recipe.");}
                }
            }
            finally{if(decoded!=null)Object.DestroyImmediate(decoded);}
        }
        static string MasterSvg(Recipes.Recipe[] recipes)
        {
            var b=new StringBuilder("<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 384 512\"><title>Original passive-node vector art master; not a game capture</title>\n");
            foreach(var recipe in recipes)b.Append(Recipes.ToSvg(recipe).Replace("viewBox=\"0 0 64 64\"","x=\""+(recipe.Node*Recipes.Cell)+"\" y=\""+(recipe.Element*Recipes.Cell)+"\" width=\"128\" height=\"128\" viewBox=\"0 0 64 64\""));return b.Append("</svg>\n").ToString();
        }
        static void WriteNew(string path,byte[] bytes,List<string> tracked)
        {if(File.Exists(Absolute(path))||AssetDatabase.LoadMainAssetAtPath(path)!=null)throw new InvalidOperationException("Refusing existing output: "+path);using(var stream=new FileStream(Absolute(path),FileMode.CreateNew,FileAccess.Write,FileShare.Read))stream.Write(bytes,0,bytes.Length);tracked.Add(path);}
        static void EnsureFolder(string path,List<string> created)
        {if(AssetDatabase.IsValidFolder(path))return;string parent=Path.GetDirectoryName(path).Replace('\\','/');EnsureFolder(parent,created);if(string.IsNullOrEmpty(AssetDatabase.CreateFolder(parent,Path.GetFileName(path))))throw new InvalidOperationException("Could not create original atlas folder.");created.Add(path);}
        static void RequireStopped(){if(EditorApplication.isPlayingOrWillChangePlaymode||EditorApplication.isCompiling||EditorApplication.isUpdating)throw new InvalidOperationException("Explicit atlas bake requires stopped, settled Editor.");}
        static string Absolute(string assetPath)=>Path.GetFullPath(Path.Combine(Application.dataPath,"..",assetPath));
        static string Hash(byte[] bytes){using(var hash=SHA256.Create())return BitConverter.ToString(hash.ComputeHash(bytes)).Replace("-","").ToLowerInvariant();}
    }
}
#endif
