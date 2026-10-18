using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace OdysseyOracle.InterfaceArt.Authoring
{
    // # ARCHITECTURE SNAPSHOT
    // Original integer vector art. 64x64 top-left coordinates; ordered opaque polygon
    // paint over transparent pixels. Stable bitID = element*3+node, never unlock state.
    // No Unity, images, fonts, time, randomness, external content or gameplay calls.
    public static class PassiveNodeIconRecipes
    {
        public const int Version=1, Count=12, Grid=64, Cell=128, Columns=3, Rows=4;
        public const string Generator="ZeusRa.Oracle.OriginalPassiveNodeArt";
        public const uint Gold=0xB89445FF,GoldLight=0xF5DF99FF,GoldDark=0x5C4525FF,Panel=0x142226FF,Ink=0x0A151AFF;
        static readonly uint[] Main={0xFF781FFF,0x26B7F4FF,0x28C975FF,0xD5F1FFFF};
        static readonly uint[] Shade={0x862817FF,0x10518BFF,0x0E663EFF,0x4E8FA8FF};
        static readonly uint[] Light={0xFFE38AFF,0xD6F8FFFF,0xC2F18AFF,0xFFF5D3FF};
        static readonly string[] Elements={"Fire","Water","Earth","Air"};
        static readonly string[] Titles={"Kindle","Living Flame","Solar Focus","Flow","Returning Tide","Deep Current","Heavy Focus","Steady Channel","Stone Tempo","Light Step","Tailwind","Skystride"};
        static readonly string[] Meanings={
            "Damage: a single rising flame.","Damage: two interwoven flames.","Damage: an eight-ray solar flame.",
            "Longer Water slows: one sustained wave.","Longer Water slows: opposing tide bands.","Longer Water slows: three deep-current bands.",
            "Damage: one weighty faceted rock.","Earth interruption: a stone focus and broken impact bands.","Damage and longer Earth interruption: stepped stone impact cluster.",
            "Movement speed: a light boot.","Movement speed: three wind feathers.","Movement speed: a winged stride. No jump-height claim."};
        public sealed class Shape { public uint Rgba;public int[] XY;public Shape(uint color,int[] coordinates){Rgba=color;XY=coordinates;} }
        public sealed class Recipe
        {
            public int BitId,Element,Node;public string SpriteName,Title,ArtMeaning;
            public readonly List<Shape> Shapes=new List<Shape>(24);
            public int BitValue=>1<<BitId;
        }
        public static Recipe[] BuildAll(){var result=new Recipe[Count];for(int i=0;i<Count;i++)result[i]=Build(i);return result;}
        public static Recipe Build(int bitId)
        {
            if(bitId<0||bitId>=Count)throw new ArgumentOutOfRangeException(nameof(bitId));
            int e=bitId/3,n=bitId%3;var r=new Recipe{BitId=bitId,Element=e,Node=n,Title=Titles[bitId],SpriteName="Passive_"+Elements[e]+"_"+Titles[bitId].Replace(" ",""),ArtMeaning=Meanings[bitId]};
            P(r,GoldDark,12,4,52,4,60,12,60,52,52,60,12,60,4,52,4,12);
            P(r,Gold,12,6,50,6,58,14,58,50,50,58,14,58,6,50,6,14);
            P(r,GoldLight,12,4,52,4,58,10,54,12,50,8,14,8,8,14,4,12);
            P(r,GoldDark,56,14,60,12,60,52,52,60,50,56,56,50);
            P(r,Panel,14,10,50,10,54,14,54,50,50,54,14,54,10,50,10,14);
            P(r,Ink,16,14,48,14,50,16,50,48,48,50,16,50,14,48,14,16);
            // Identical broken bevel corners are frame decoration, never selected/locked badges.
            R(r,GoldLight,12,12,6,2);R(r,GoldLight,12,14,2,4);R(r,GoldDark,46,50,6,2);R(r,GoldDark,50,46,2,4);
            switch(bitId)
            {
                case 0:
                    G(r,e,30,12,36,20,36,28,42,22,44,34,48,40,44,48,34,52,22,48,16,40,20,30,24,36,26,26,24,22);
                    P(r,Light[e],30,30,36,38,38,44,32,48,26,44,26,38);break;
                case 1:
                    G(r,e,22,16,28,22,28,30,32,26,32,38,26,48,18,48,14,40,16,32,20,36,22,28);
                    G(r,e,40,14,46,24,44,30,48,28,50,38,46,46,38,50,32,44,36,34,38,38,40,28,36,22);
                    P(r,Light[e],22,30,26,38,24,44,20,42,20,38);P(r,Light[e],42,30,46,38,42,44,38,42,40,38);break;
                case 2:
                    for(int i=0;i<4;i++){int x=i==0?30:i==1?48:i==2?30:12,y=i==0?10:i==1?30:i==2?48:30;R(r,Light[e],x,y,i%2==0?4:6,i%2==0?6:4);}
                    G(r,e,26,18,38,18,46,26,46,38,38,46,26,46,18,38,18,26);
                    P(r,Light[e],32,22,38,30,38,38,32,42,26,36,28,30);R(r,GoldLight,16,16,4,4);R(r,GoldLight,44,44,4,4);R(r,GoldLight,44,16,4,4);R(r,GoldLight,16,44,4,4);break;
                case 3:
                    G(r,e,16,36,20,28,28,22,38,22,46,28,48,36,42,38,36,30,30,30,26,34,26,38,34,40,44,38,50,42,48,48,22,48,16,44);
                    P(r,Light[e],20,30,28,24,38,24,44,28,44,32,36,28,28,28,22,34);break;
                case 4:
                    G(r,e,14,24,22,18,32,18,42,22,48,22,48,28,40,28,30,24,24,24,18,28,14,28);
                    G(r,e,16,36,24,36,34,40,40,40,46,36,50,36,50,42,42,48,32,48,22,44,16,44);
                    R(r,Light[e],20,20,12,2);R(r,Light[e],34,42,10,2);break;
                case 5:
                    G(r,e,16,20,24,16,34,18,42,16,48,20,48,26,40,22,32,24,24,22,16,26);
                    G(r,e,16,32,24,28,34,30,42,28,48,32,48,38,40,34,32,36,24,34,16,38);
                    G(r,e,16,44,24,40,34,42,42,40,48,44,48,50,40,46,32,48,24,46,16,50);
                    R(r,Light[e],22,18,6,2);R(r,Light[e],22,30,6,2);R(r,Light[e],22,42,6,2);break;
                case 6:
                    G(r,e,26,16,40,18,48,30,44,46,28,50,16,40,18,26);
                    P(r,Light[e],26,16,30,30,24,42,16,40,18,26);P(r,Shade[e],30,30,48,30,44,46,28,50,24,42);
                    P(r,Main[e],32,32,42,32,40,40,30,44);break;
                case 7:
                    G(r,e,28,16,38,20,42,30,38,46,28,48,22,40,22,26);
                    P(r,Light[e],28,16,30,28,26,38,22,40,22,26);
                    P(r,Main[e],12,24,16,24,18,30,18,38,14,42,10,38,14,34,14,30);
                    P(r,Main[e],48,24,52,28,50,32,50,38,54,42,50,46,46,40,46,30);
                    P(r,Ink,24,30,30,30,34,24,38,30,42,30,40,34,36,34,32,40,28,34,24,34);break;
                case 8:
                    G(r,e,16,34,24,30,30,36,28,48,18,50,12,42);
                    G(r,e,30,20,38,16,46,24,44,44,34,48,28,38);
                    P(r,Light[e],30,20,34,28,32,38,28,38);P(r,Light[e],16,34,22,36,20,44,14,42);
                    P(r,Main[e],14,18,20,18,24,26,20,28);P(r,Main[e],46,14,50,16,48,24,44,24);R(r,GoldLight,48,34,6,4);break;
                case 9:
                    G(r,e,20,18,30,18,30,34,40,38,48,38,48,46,18,46,16,40,20,36);
                    R(r,Light[e],22,20,4,14);P(r,Shade[e],18,44,48,44,48,48,18,48);R(r,Light[e],36,40,10,2);break;
                case 10:
                    G(r,e,16,20,50,20,44,26,28,26,28,30,44,30,38,36,26,36,26,40,36,40,30,46,18,46,14,40);
                    R(r,Light[e],20,22,22,2);R(r,Light[e],20,32,14,2);R(r,Light[e],20,42,6,2);break;
                case 11:
                    G(r,e,14,38,24,38,24,32,32,32,32,26,42,26,42,20,50,20,50,30,42,34,36,38,28,44,22,48,14,48);
                    P(r,Light[e],18,38,24,38,24,34,32,34,32,30,40,30,40,34,32,38,26,42,18,44);
                    P(r,Main[e],14,20,24,20,30,24,26,28,20,26,14,26);break;
            }
            Validate(r);return r;
        }
        static void P(Recipe r,uint color,params int[] xy){r.Shapes.Add(new Shape(color,xy));}
        static void R(Recipe r,uint color,int x,int y,int width,int height){P(r,color,x,y,x+width,y,x+width,y+height,x,y+height);}
        static void G(Recipe r,int element,params int[] xy)
        {var shadow=new int[xy.Length];for(int i=0;i<xy.Length;i++)shadow[i]=xy[i]+2;P(r,Shade[element],shadow);P(r,Main[element],xy);}
        public static void Validate(Recipe r)
        {
            if(r==null||r.BitId<0||r.BitId>=Count||r.Element!=r.BitId/3||r.Node!=r.BitId%3||r.Shapes.Count<8||r.Shapes.Count>48)throw new ArgumentException("Invalid original icon identity/shape bound.");
            foreach(var shape in r.Shapes)
            {
                if(shape==null||shape.XY==null||shape.XY.Length<6||shape.XY.Length>128||shape.XY.Length%2!=0||(shape.Rgba&255)!=255)throw new ArgumentException("Invalid opaque vector polygon.");
                long area=0;int count=shape.XY.Length/2;
                for(int i=0;i<count;i++){int x=shape.XY[i*2],y=shape.XY[i*2+1],next=(i+1)%count;if(x<0||x>Grid||y<0||y>Grid)throw new ArgumentException("Polygon leaves64px master bounds.");area+=(long)x*shape.XY[next*2+1]-(long)y*shape.XY[next*2];}
                if(area==0)throw new ArgumentException("Degenerate polygon.");
            }
        }
        public static byte[] Rasterize(Recipe recipe,int size)
        {
            Validate(recipe);if(size!=32&&size!=64&&size!=128)throw new ArgumentOutOfRangeException(nameof(size));
            var rgba=new byte[size*size*4];
            for(int y=0;y<size;y++)for(int x=0;x<size;x++)
            {
                double px=(x+.5)*Grid/size,py=(y+.5)*Grid/size;uint color=0;
                foreach(var shape in recipe.Shapes)if(Contains(shape.XY,px,py))color=shape.Rgba;
                int index=(y*size+x)*4;rgba[index]=(byte)(color>>24);rgba[index+1]=(byte)(color>>16);rgba[index+2]=(byte)(color>>8);rgba[index+3]=(byte)color;
            }
            return rgba;
        }
        static bool Contains(int[] p,double x,double y)
        {
            bool inside=false;int j=p.Length-2;
            for(int i=0;i<p.Length;i+=2){double ax=p[i],ay=p[i+1],bx=p[j],by=p[j+1];if((ay>y)!=(by>y)&&x<(bx-ax)*(y-ay)/(by-ay)+ax)inside=!inside;j=i;}
            return inside;
        }
        public static string ToSvg(Recipe recipe)
        {
            Validate(recipe);var b=new StringBuilder("<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 64 64\" shape-rendering=\"crispEdges\">\n");
            b.Append("<title>Original passive icon art: ").Append(recipe.Title).Append("; not a game capture</title>\n");
            foreach(var s in recipe.Shapes){b.Append("<polygon fill=\"#").Append((s.Rgba>>8).ToString("X6",CultureInfo.InvariantCulture)).Append("\" points=\"");for(int i=0;i<s.XY.Length;i+=2)b.Append(s.XY[i]).Append(',').Append(s.XY[i+1]).Append(' ');b.Append("\"/>\n");}return b.Append("</svg>\n").ToString();
        }
        public static string Fingerprint(Recipe[] recipes)
        {
            if(recipes==null||recipes.Length!=Count)throw new ArgumentException("Exactly12 icon recipes required.");var b=new StringBuilder(Generator+"/"+Version+"\n");
            for(int i=0;i<recipes.Length;i++){Validate(recipes[i]);if(recipes[i].BitId!=i)throw new ArgumentException("Stable bit order required.");b.Append(i).Append(':').Append(recipes[i].SpriteName).Append(':').Append(ToSvg(recipes[i]));}
            using(var hash=SHA256.Create())return BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(b.ToString()))).Replace("-","").ToLowerInvariant();
        }
        public static string StableSpriteId(Recipe recipe)
        {Validate(recipe);using(var hash=SHA256.Create()){byte[] bytes=hash.ComputeHash(Encoding.UTF8.GetBytes(Generator+".bit."+recipe.BitId));var b=new StringBuilder(32);for(int i=0;i<16;i++)b.Append(bytes[i].ToString("x2",CultureInfo.InvariantCulture));return b.ToString();}}
    }
}
