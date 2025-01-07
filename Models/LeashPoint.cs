namespace VRCOSC.Modules.OSCLeash.Models;

public enum LeashDirection
{
    None,
    North,  // Front
    South,  // Back
    East,   // Right
    West    // Left
}

public struct LeashPoint
{
    public LeashDirection Direction;
    public string BaseName;
    public bool IsActive;
} 