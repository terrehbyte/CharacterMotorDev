using UnityEngine;

public static class Utilities
{
    public static bool Test(this LayerMask mask, int other)
    {
        return (mask | (1 << other)) != 0;
    }
}

public static class Vector3Extensions
{
    public static Vector3 GetXZ(this Vector3 vec)
    {
        return new Vector3(vec.x, 0.0f, vec.z);
    }
    
    public static Vector3 GetY(this Vector3 vec)
    {
        return new Vector3(0.0f, vec.y, 0.0f);
    }
}