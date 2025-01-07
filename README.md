<div align="center">
    <h1>OSCLeash</h1>
    <h3>
        A VRChat OSC module for VRCOSC that enables avatar movement control through physbone parameters.<br>
        Perfect for leashes, tails, or hand holding!
    </h3>
    <p>
        <a href="https://github.com/ZenithVal/OSCLeash/blob/main/LICENSE"><img alt="License" src="https://img.shields.io/github/license/ZenithVal/OSCLeash?label=License"></a>
    </p>
</div>

> ⚠️ **DISCLAIMER**: This project is currently a major work in progress (WIP). Features, documentation, and functionality may be incomplete or subject to significant changes.

# Features
- Smooth, responsive movement control through physbone parameters
- Configurable walking and running thresholds with deadzone control
- Optional turning control with adjustable sensitivity and momentum
- Up/down movement compensation with configurable limits
- SIMD-optimized calculations for efficient performance
- Thread-safe parameter handling with concurrent queue system
- Multiple leash support with direction-based control
- Seamless integration with VRCOSC's module system
- Movement curve customization with multiple curve types
- State interpolation for smooth transitions

# Requirements
- [VRCOSC](https://github.com/VolcanicArts/VRCOSC)
- .NET 8.0 Runtime
- Windows 10/11 (Build 22621 or later)
- VRChat with OSC enabled

# Installation

## Module Setup
1. Download the latest release from the releases page
2. Place the DLL in your VRCOSC packages folder (typically `%AppData%/VRCOSC/Packages`)
3. Enable the module in VRCOSC
4. Configure the module settings in VRCOSC's UI

## Avatar Setup
1. Import the prefab (`OSCLeash.prefab`) from releases into your Unity project
2. Place the prefab at the root of your model (NOT as a child of armature)
3. Configure the Physbone:
   - Select `Leash Physbone` and assign its Root Transform to your leash's first bone
   - Select `Compass` and assign the Position constraint source to the first bone
   - Select `Aim Needle` (child of Compass) and assign the Aim constraint source to the last bone

## Optional Setup
- For Quest/PC cross-platform support: Sync the Physbone's network ID
- For multiple leashes: Add additional sources to `Compass` and `Aim Needle`, animate weights based on grab state
- For remote users: Animate compass visibility using IsLocal

# Configuration
The module can be configured through VRCOSC's UI with the following settings:

| Setting | Description | Default |
|---------|-------------|---------|
| Walk Deadzone | Minimum stretch % to start walking | 0.15 |
| Run Deadzone | Minimum stretch % to trigger running | 0.70 |
| Strength Multiplier | Movement speed multiplier (capped at 1.0) | 1.2 |
| Turning Enabled | Enables turning functionality | false |
| Turning Multiplier | Adjusts turning speed | 0.80 |
| Turning Deadzone | Minimum stretch % to start turning | 0.15 |
| Turning Goal | Target turning angle range (0-144°) | 90 |
| Up/Down Compensation | Compensation % for vertical angles | 1.0 |
| Up/Down Deadzone | Vertical angle movement threshold | 0.5 |

# How It Works
The module processes physbone parameters in real-time to control avatar movement:

1. **Parameter Monitoring**
   - Tracks `_IsGrabbed` and `_Stretch` states for configured physbones
   - Processes directional parameters (X+/-, Y+/-, Z+/-) for movement calculation

2. **Movement Processing**
   - Calculates movement vectors using SIMD operations for efficiency
   - Applies configurable deadzones and multipliers
   - Handles smooth transitions and momentum for natural movement

3. **Direction Control**
   - Supports North, South, East, and West orientations
   - Adjusts turning behavior based on leash direction
   - Provides smooth turning with momentum and interpolation

# Troubleshooting

## Common Issues
- **No Movement Response**: Verify OSC is enabled in VRChat and VRCOSC is running
- **Incorrect Movement**: Check physbone constraints and contact setup
- **Quest Compatibility**: Ensure physbone network IDs are synced between platforms

## Getting Help
- Join the [Discord](https://discord.gg/vrcosc-1000862183963496519) for VRCOSC support
- Create an [Issue](https://github.com/CrookedToe/OSCLeash/issues) for bug reports
- Check VRCOSC logs for any error messages
```

# Parameter Naming
The module expects the following parameter naming convention for your avatar:

| Parameter | Format | Example |
|-----------|---------|---------|
| Base Parameter | `{name}` | `Leash` |
| Grabbed State | `{name}_IsGrabbed` | `Leash_IsGrabbed` |
| Stretch Value | `{name}_Stretch` | `Leash_Stretch` |
| Directional (Front) | `{name}_Z+` | `Leash_Z+` |
| Directional (Back) | `{name}_Z-` | `Leash_Z-` |
| Directional (Right) | `{name}_X+` | `Leash_X+` |
| Directional (Left) | `{name}_X-` | `Leash_X-` |
| Directional (Up) | `{name}_Y+` | `Leash_Y+` |
| Directional (Down) | `{name}_Y-` | `Leash_Y-` |

For direction-based leashes, append the direction to the base name:
- `Leash_North` - Front-facing leash
- `Leash_South` - Back-facing leash
- `Leash_East` - Right-facing leash
- `Leash_West` - Left-facing leash

# Advanced Configuration

## Movement Curves
The module supports different movement curve types to customize how stretch values map to movement speed:
- `Linear` - Direct mapping (default)
- `Quadratic` - Smooth acceleration curve
- `Cubic` - More aggressive acceleration
- `Exponential` - Customizable power curve

## State Transitions
Fine-tune how movement states blend together:
- `InterpolationStrength` - Controls smoothing between states
- `StateTransitionTime` - Duration of state transitions
- `CurveSmoothing` - Smoothing factor for movement curves

## Safety Limits
Enable safety limits to prevent excessive movement:
- `EnableSafetyLimits` - Master toggle for limits
- `MaxVelocity` - Maximum movement speed
- `MaxAcceleration` - Maximum speed change rate
- `MaxTurnRate` - Maximum turning speed

# Credits
- Original Python implementation by @ZenithVal and @ALeonic
- VRCOSC integration support by @VolcanicArts
- Community contributions and testing