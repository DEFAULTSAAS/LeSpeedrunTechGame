using UnityEngine;

public class GameUtils
{
    public static int CollisionLayerToRaycastMask(int inLayer)
    {
        int result = 0;
        for (int i = 0; i < 32; i++)
        {
            if (!Physics.GetIgnoreLayerCollision(inLayer, i))
                result |= 1 << i;  
        }
        
        return result;
    }
}
