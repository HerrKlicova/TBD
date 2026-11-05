// Shims MÍNIMOS de Unity para compilar y probar fuera de Unity la lógica pura de la simulación
// (Assets/TBD/Scripts/Sim/SimTypes.cs y SimCore.cs). No son los de Unity: solo cubren lo que esos dos archivos usan.
// Diferencias a tener en cuenta: math.hash aquí replica la fórmula de Unity.Mathematics, pero los tests no dependen de ella;
// y no se prueba el modo FloatMode.Deterministic de Burst (solo el determinismo lógico y la independencia del orden).
using System;
using System.Collections.Generic;

namespace Unity.Burst
{
    public enum FloatMode { Default, Strict, Deterministic, Fast }
    public enum FloatPrecision { Standard, High, Medium, Low }

    [AttributeUsage(AttributeTargets.All)]
    public sealed class BurstCompileAttribute : Attribute
    {
        public FloatMode FloatMode { get; set; }
        public FloatPrecision FloatPrecision { get; set; }
        public bool CompileSynchronously { get; set; }
        public BurstCompileAttribute() { }
    }
}

namespace Unity.Jobs
{
    public interface IJob { void Execute(); }
}

namespace Unity.Entities
{
    public interface IComponentData { }
    public interface IBufferElementData { }

    public struct DynamicBuffer<T> where T : struct
    {
        readonly List<T> _list;
        public DynamicBuffer(List<T> list) { _list = list; }
        public int Length => _list.Count;
        public T this[int i] { get => _list[i]; set => _list[i] = value; }
        public void Add(T v) => _list.Add(v);
        public void ResizeUninitialized(int n)
        {
            while (_list.Count < n) _list.Add(default);
            if (_list.Count > n) _list.RemoveRange(n, _list.Count - n);
        }
    }
}

namespace Unity.Collections
{
    public enum Allocator { Invalid, None, Temp, TempJob, Persistent }
    public enum NativeArrayOptions { UninitializedMemory, ClearMemory }

    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Parameter)]
    public sealed class ReadOnlyAttribute : Attribute { }

    public struct NativeArray<T> where T : struct
    {
        readonly T[] _a;
        public NativeArray(int length, Allocator allocator, NativeArrayOptions options = NativeArrayOptions.ClearMemory) { _a = new T[length]; }
        public NativeArray(T[] source) { _a = source; }
        public int Length => _a.Length;
        public T this[int i] { get => _a[i]; set => _a[i] = value; }
        public void CopyFrom(NativeArray<T> other) { Array.Copy(other._a, _a, Math.Min(_a.Length, other._a.Length)); }
        public void Dispose() { }
        public T[] Raw => _a;
    }
}

namespace Unity.Mathematics
{
    public struct int2
    {
        public int x, y;
        public int2(int x, int y) { this.x = x; this.y = y; }
        public static int2 operator +(int2 a, int2 b) => new int2(a.x + b.x, a.y + b.y);
        public static int2 operator -(int2 a, int2 b) => new int2(a.x - b.x, a.y - b.y);
        public static int2 operator +(int2 a, int b) => new int2(a.x + b, a.y + b);
        public static int2 operator -(int2 a, int b) => new int2(a.x - b, a.y - b);
    }

    public struct uint2
    {
        public uint x, y;
        public uint2(uint x, uint y) { this.x = x; this.y = y; }
        public static uint2 operator *(uint2 a, uint2 b) => new uint2(a.x * b.x, a.y * b.y);
    }

    public struct float2
    {
        public float x, y;
        public float2(float x, float y) { this.x = x; this.y = y; }
        public static float2 operator +(float2 a, float2 b) => new float2(a.x + b.x, a.y + b.y);
        public static float2 operator -(float2 a, float2 b) => new float2(a.x - b.x, a.y - b.y);
        public static float2 operator *(float2 a, float2 b) => new float2(a.x * b.x, a.y * b.y);
        public static float2 operator *(float2 a, float s) => new float2(a.x * s, a.y * s);
        public static float2 operator /(float2 a, float s) => new float2(a.x / s, a.y / s);
        public static float2 operator +(float2 a, float s) => new float2(a.x + s, a.y + s);
        public static float2 operator -(float2 a, float s) => new float2(a.x - s, a.y - s);
        public static explicit operator int2(float2 v) => new int2((int)v.x, (int)v.y);
        public static explicit operator float2(int2 v) => new float2(v.x, v.y);
    }

    public struct float3
    {
        public float x, y, z;
        public float3(float x, float y, float z) { this.x = x; this.y = y; this.z = z; }
        public static float3 zero => new float3(0f, 0f, 0f);
        public float2 xz => new float2(x, z);
        public static float3 operator +(float3 a, float3 b) => new float3(a.x + b.x, a.y + b.y, a.z + b.z);
        public static float3 operator *(float3 a, float s) => new float3(a.x * s, a.y * s, a.z * s);
    }

    public static class math
    {
        public static float min(float a, float b) => a < b ? a : b;
        public static float max(float a, float b) => a > b ? a : b;
        public static int min(int a, int b) => a < b ? a : b;
        public static int max(int a, int b) => a > b ? a : b;
        public static float2 min(float2 a, float2 b) => new float2(min(a.x, b.x), min(a.y, b.y));
        public static float2 max(float2 a, float2 b) => new float2(max(a.x, b.x), max(a.y, b.y));
        public static float clamp(float v, float lo, float hi) => v < lo ? lo : (v > hi ? hi : v);
        public static float2 clamp(float2 v, float2 lo, float2 hi) => new float2(clamp(v.x, lo.x, hi.x), clamp(v.y, lo.y, hi.y));
        public static int2 clamp(int2 v, int2 lo, int2 hi) => new int2(Math.Clamp(v.x, lo.x, hi.x), Math.Clamp(v.y, lo.y, hi.y));
        public static float saturate(float v) => clamp(v, 0f, 1f);
        public static float floor(float v) => (float)Math.Floor(v);
        public static float2 floor(float2 v) => new float2(floor(v.x), floor(v.y));
        public static float round(float v) => (float)Math.Round(v, MidpointRounding.ToEven);
        public static uint asuint(float v) => (uint)BitConverter.SingleToInt32Bits(v);
        public static float lerp(float a, float b, float t) => a + (b - a) * t;
        public static float2 lerp(float2 a, float2 b, float t) => new float2(lerp(a.x, b.x, t), lerp(a.y, b.y, t));
        public static float dot(float2 a, float2 b) => a.x * b.x + a.y * b.y;
        public static float lengthsq(float2 a) => a.x * a.x + a.y * a.y;
        public static float length(float2 a) => (float)Math.Sqrt(lengthsq(a));
        public static float2 normalize(float2 a) { float l = length(a); return new float2(a.x / l, a.y / l); }
        public static float atan2(float y, float x) => (float)Math.Atan2(y, x);
        // Fórmula de Unity.Mathematics: csum(v * uint2(0xFA3A3285, 0xAD55999D)) + 0xDCDD5341
        public static uint hash(uint2 v) { var m = v * new uint2(0xFA3A3285u, 0xAD55999Du); return unchecked(m.x + m.y + 0xDCDD5341u); }
    }
}
