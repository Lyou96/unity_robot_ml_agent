using System.Diagnostics;
using UnityEngine;
using UnityEngine.InputSystem;

public static class RobotArmInput
{
    public static float Joint1 => Axis(Key.Q, Key.A); // (어깨) Q = 위로 A = 아래로
    
    public static float Joint2 => Axis(Key.W, Key.S); // (팔꿈치) W = 위로  S = 아래로
    
    public static float Joint3 => Axis(Key.E, Key.D); // 
    
    public static bool Grip =>
    Keyboard.current != null && Keyboard.current.spaceKey.isPressed;
    
    public static bool ResetPressed =>
        Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame;
    
    
    static float Axis(Key positive, Key negative)
    {
        var kb = Keyboard.current;
        if (kb == null) return 0f;

        return (kb[positive].isPressed ? 1f : 0f) - (kb[negative].isPressed ? 1f : 0f);
    }


    public static float Joint(int i)
    {
        return i switch
        {
            0 => Joint1,
            1 => Joint2,
            2 => Joint3,
            _ => 0f
        };
        
        
    }
    
    
}
