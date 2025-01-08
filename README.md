# OSCLeash

<div align="center">
    <h3>
        A VRChat OSC module for VRCOSC that enables avatar movement control through physbone parameters.<br>
        Perfect for leashes, tails, or hand holding!
    </h3>
    <p>
        <a href="https://github.com/CrookedToe/OSCLeash/blob/main/LICENSE"><img alt="License" src="https://img.shields.io/github/license/ZenithVal/OSCLeash?label=License"></a>
    </p>
</div>

> ⚠️ **DISCLAIMER**: This project is currently a major work in progress (WIP). Features, documentation, and functionality may be incomplete or subject to significant changes.

# Quick Start Guide

## Requirements
- [VRCOSC](https://github.com/VolcanicArts/VRCOSC)
- .NET 8.0 Runtime
- Windows 10/11 (Build 22621 or later)
- VRChat with OSC enabled

## Known Issues
- Parameter names containing '+' are not received by the module. In the Unity prefab, rename:
  - `X+` to `XPositive`
  - `Y+` to `YPositive`
  - `Z+` to `ZPositive`
  This is a temporary workaround until the parameter handling is fixed.

## Installation Steps

### 1. Module Setup
1. Download the latest release from the releases page
2. Place the DLL in your VRCOSC packages folder (typically `%AppData%/VRCOSC/Packages`)
3. Enable the module in VRCOSC
4. Configure the module settings in VRCOSC's UI

### 2. Avatar Setup
1. Import the prefab (`OSCLeash.prefab`) from releases into your Unity project
2. Place the prefab at the root of your model (NOT as a child of armature)
3. Configure the Physbone:
   - Select `Leash Physbone` and assign its Root Transform to your leash's first bone
   - Select `Compass` and assign the Position constraint source to the first bone
   - Select `Aim Needle` (child of Compass) and assign the Aim constraint source to the last bone

### 3. Parameter Setup
The module uses the following parameter naming convention:

| Parameter | Format | Example |
|-----------|---------|---------|
| Base Parameter | `{name}` | `Leash` |
| Grabbed State | `{name}_IsGrabbed` | `Leash_IsGrabbed` |
| Stretch Value | `{name}_Stretch` | `Leash_Stretch` |
| Directional (Front) | `{name}_ZPositive` | `Leash_ZPositive` |
| Directional (Back) | `{name}_Z-` | `Leash_Z-` |
| Directional (Right) | `{name}_XPositive` | `Leash_XPositive` |
| Directional (Left) | `{name}_X-` | `Leash_X-` |
| Directional (Up) | `{name}_YPositive` | `Leash_YPositive` |
| Directional (Down) | `{name}_Y-` | `Leash_Y-` |

For direction-based leashes, append the direction to the base name:
- `Leash_North` - Front-facing leash
- `Leash_South` - Back-facing leash
- `Leash_East` - Right-facing leash
- `Leash_West` - Left-facing leash

# Features
- Smooth, responsive movement control through physbone parameters
- Configurable walking and running thresholds with deadzone control
- Optional turning control with adjustable sensitivity and momentum
- Up/down movement compensation with configurable limits
- Safety limits for maximum velocity, acceleration, and turn rate
- SIMD-optimized calculations for efficient performance
- Thread-safe parameter handling with concurrent queue system
- Multiple leash support with direction-based control
- Movement curve customization with multiple curve types
- State interpolation for smooth transitions
- Toggleable debug logging for troubleshooting

# Configuration

## Basic Settings
| Setting | Description | Default |
|---------|-------------|---------|
| Walk Deadzone | Minimum stretch % to start walking | 0.15 |
| Run Deadzone | Minimum stretch % to trigger running | 0.70 |
| Strength Multiplier | Movement speed multiplier (capped at 1.0) | 1.2 |
| Enable Debug Logging | Toggle detailed state change logging | false |

## Safety Settings
| Setting | Description | Default |
|---------|-------------|---------|
| Enable Safety Limits | Enable movement speed and acceleration limits | true |
| Max Velocity | Maximum movement speed in any direction | 1.0 |
| Max Acceleration | Maximum speed change per second | 2.0 |
| Max Turn Rate | Maximum turning speed in degrees per second | 180 |

## Turning Settings
| Setting | Description | Default |
|---------|-------------|---------|
| Turning Enabled | Enables turning functionality | false |
| Turning Multiplier | Adjusts turning speed | 0.80 |
| Turning Deadzone | Minimum stretch % to start turning | 0.15 |
| Turning Goal | Target turning angle range (0-144°) | 90 |
| Turning Momentum | Momentum factor for smooth turning | 0.5 |
| Smooth Turning Speed | Speed of turn angle interpolation | 1.0 |

## Vertical Movement Settings
| Setting | Description | Default |
|---------|-------------|---------|
| Up/Down Compensation | Compensation % for vertical angles | 1.0 |
| Up/Down Deadzone | Vertical angle movement threshold | 0.5 |

## Movement Curve Settings
| Setting | Description | Default |
|---------|-------------|---------|
| Movement Curve Type | Type of movement response curve | Linear |
| Curve Exponent | Power factor for exponential curves | 2.0 |
| Curve Smoothing | Smoothing factor for curve transitions | 0.5 |
| Interpolation Strength | Strength of state interpolation | 0.8 |
| State Transition Time | Time for state transitions in seconds | 0.2 |

# Advanced Features

## Optional Setup
- For Quest/PC cross-platform support: Sync the Physbone's network ID
- For multiple leashes: Add additional sources to `Compass` and `Aim Needle`, animate weights based on grab state
- For remote users: Animate compass visibility using IsLocal

## Movement Curves
The module supports different movement curve types:
- `Linear` - Direct mapping (default)
- `Quadratic` - Smooth acceleration curve
- `Cubic` - More aggressive acceleration
- `Exponential` - Customizable power curve with exponent control
- `Smooth` - Smoothed curve with adjustable transition factor

The curve behavior can be further customized using:
- `Curve Exponent`: Controls the power factor for exponential curves
- `Curve Smoothing`: Adjusts the smoothness of curve transitions
- `Interpolation Strength`: Controls how quickly values interpolate
- `State Transition Time`: Sets the duration of state changes

# Troubleshooting

## Common Issues
- **No Movement Response**: Verify OSC is enabled in VRChat and VRCOSC is running
- **Incorrect Movement**: Check physbone constraints and contact setup
- **Quest Compatibility**: Ensure physbone network IDs are synced between platforms
- **Jerky Movement**: Try adjusting safety limits or increasing state transition time

## Understanding Log Messages

### State Changes (When Debug Logging Enabled)
- `Leash grab state changed: [true/false]` - Indicates when the leash is grabbed or released
- `Movement state changed: [Started/Stopped]` - Indicates when movement begins or ends
- `Running state changed: [Started/Stopped]` - Indicates when running state changes
- `Turning state changed: [Started/Stopped]` - Indicates when turning begins or ends
- `Parameter cache cleanup: [count] values removed` - Indicates cache maintenance

### Warnings
- `Walk deadzone value [X] is outside valid range [0,1]` - Walk deadzone setting needs adjustment
- `Run deadzone value [X] is invalid` - Run deadzone must be greater than walk deadzone
- `Strength multiplier [X] is outside valid range (0,2]` - Movement strength needs adjustment
- `Invalid leash direction '[X]'` - Direction setting is not valid (use North/South/East/West)
- `Parameter queue overflow` - Too many parameters being processed, some will be dropped
- `Invalid movement curve type '[X]'` - Curve type setting is not valid

### Errors
- `Error updating parameter value: [message]` - Issue processing a parameter value
- `Error during cache cleanup: [message]` - Issue cleaning up parameter cache
- `Error sending movement values: [message]` - Issue sending movement to VRChat

## Performance Optimization
The module includes several features to maintain performance:
- Force value deadzone (FORCE_EPSILON = 0.001) to prevent micro-movements
- Parameter value caching with periodic cleanup (every 1000 updates)
- Batch processing of parameters (10 at a time)
- Movement value clamping and validation
- Safety limits to prevent excessive calculations

## Getting Help
- Join the [Discord](https://discord.gg/vrcosc-1000862183963496519) for VRCOSC support
- Create an [Issue](https://github.com/CrookedToe/OSCLeash/issues) for bug reports
- Check VRCOSC logs for any error messages
- Enable debug logging for detailed state information