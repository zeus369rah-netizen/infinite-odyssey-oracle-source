using System;

namespace OdysseyOracle
{
    // Fixed CPU state, independent of Unity objects and authority. Two visual slots only.
    public sealed class CosmicNovaPoolClock
    {
        public const int Capacity=2;
        private readonly double[] ages=new double[Capacity];
        private readonly float[] radii=new float[Capacity];
        private readonly int[] qualities=new int[Capacity];
        private readonly bool[] active=new bool[Capacity];
        public int ActiveCount { get { int count=0;for(int i=0;i<Capacity;i++)if(active[i])count++;return count; } }
        public bool IsActive(int slot)=>slot>=0&&slot<Capacity&&active[slot];
        public float Phase(int slot)=>(float)(ages[slot]/CosmicNovaGeometry.LifetimeSeconds);
        public int Quality(int slot)=>qualities[slot];
        public float Radius(int slot)=>radii[slot];
        public int Begin(float radius,int quality)
        {
            if(!CosmicNovaGeometry.ValidRadius(radius))return -1;
            int index=-1;
            for(int i=0;i<Capacity;i++)if(!active[i]){index=i;break;}
            if(index<0)index=ages[0]>=ages[1]?0:1;
            ages[index]=0d;radii[index]=radius;qualities[index]=CosmicNovaGeometry.ClampQuality(quality);active[index]=true;
            return index;
        }
        public void Advance(float delta,bool simulate)
        {
            if(!simulate||float.IsNaN(delta)||float.IsInfinity(delta)||delta<=0f)return;
            double step=Math.Min(delta,.1f);
            for(int i=0;i<Capacity;i++)if(active[i])
            {ages[i]+=step;if(ages[i]>=CosmicNovaGeometry.LifetimeSeconds)active[i]=false;}
        }
        public void Reset()
        {for(int i=0;i<Capacity;i++){active[i]=false;ages[i]=0d;radii[i]=0f;qualities[i]=0;}}
    }
}
