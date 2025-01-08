namespace VRCOSC.Modules.OSCLeash.Constants;

public static class LeashConstants
{
    public const int MAX_QUEUE_SIZE = 1000;
    public const float MOVEMENT_EPSILON = 0.0001f;
    public const int BATCH_SIZE = 10;
    public const int CACHE_CLEANUP_INTERVAL = 1000; // Cleanup every 1000 updates
    
    public static class MovementLimits
    {
        public const float SafetyMargin = 0.95f;
        public const float MinimumTimeStep = 0.016f; // ~60fps
    }
} 