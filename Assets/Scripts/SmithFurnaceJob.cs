using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;

namespace OdysseyOracle
{
    [BurstCompile]
    public struct SmithFurnaceJob:IJob,ISmithFurnaceStorage
    {
        public NativeArray<byte> Materials;
        public NativeArray<ulong> Occupancy,Summaries;
        public NativeArray<int> FaceMask,Result;
        public NativeArray<SmithFurnaceQuad32> Quads;
        public int CellCapacity=>Materials.Length;public int WordCapacity=>Occupancy.Length;public int SummaryCapacity=>Summaries.Length;
        public int MaskCapacity=>FaceMask.Length;public int QuadCapacity=>Quads.Length;
        public byte Cell(int i)=>Materials[i];public void Cell(int i,byte v){Materials[i]=v;}
        public ulong Word(int i)=>Occupancy[i];public void Word(int i,ulong v){Occupancy[i]=v;}
        public ulong Summary(int i)=>Summaries[i];public void Summary(int i,ulong v){Summaries[i]=v;}
        public int Mask(int i)=>FaceMask[i];public void Mask(int i,int v){FaceMask[i]=v;}
        public void Quad(int i,SmithFurnaceQuad32 v){Quads[i]=v;}
        public void Execute(){var storage=this;Result[0]=SmithFurnaceMath.Generate(ref storage);}
    }
}
// # ARCHITECTURE SNAPSHOT
// Coordinate Space: Furnace-local20mm integer lattice; no scene/Transform queries.
// Array Formats: Fixed TempJob material/occupancy/summary/mask/Quad32/output-count arrays; no resize or managed data.
// Dependencies/Pointers: One scheduled cold producer; caller completes before reading and completes again before finally disposal. No Object APIs or gameplay authority.
