using UnityEngine;

public static class Utilities
{
    public static bool Test(this LayerMask mask, int other)
    {
        return (mask | (1 << other)) != 0;
    }
}