<div align="Center">
    <h3 align="Center">
      A VRChat OSC module for VRCOSC that enables avatar movement control through physbone parameters. <br>
      Perfect for leashes, tails, or hand holding!
    </h3>
    <div align="Center">
      <p align="Center">
        <a href="https://discord.gg/7VAm3twDyy"><img alt="Discord Badge" src="https://img.shields.io/discord/955364088156921867?color=5865f2&label=Discord&logo=discord&logoColor=https%3A%2F%2Fshields.io%2Fcategory%2Fother"/></a>
        <a href="https://github.com/ZenithVal/OSCLeash/blob/main/LICENSE"><img alt="License" src="https://img.shields.io/github/license/ZenithVal/OSCLeash?label=License"></a>
      </p>
    </div>
</div>

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
- Join the [Discord](https://discord.gg/7VAm3twDyy) for support
- Create an [Issue](https://github.com/ZenithVal/OSCLeash/issues) for bug reports
- Check VRCOSC logs for any error messages

# Building from Source
```bash
dotnet build
```

# Credits
- Original Python implementation by @ZenithVal and @ALeonic
- VRCOSC integration support by @VolcanicArts
- Community contributions and testing

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

# Development Setup

## Prerequisites
- Visual Studio 2022 or later
- .NET 8.0 SDK
- VRCOSC SDK package

## Environment Setup
1. Clone the repository
```bash
git clone https://github.com/YourRepo/OSCLeash.git
cd OSCLeash
```

2. Install the VRCOSC SDK package
```bash
dotnet add package VolcanicArts.VRCOSC.SDK --version 2024.1223.0
```

3. Build the project
```bash
dotnet build
```

## Testing
1. Build the project in Debug configuration
2. Copy the DLL to your VRCOSC packages folder
3. Enable developer mode in VRCOSC for detailed logging
4. Use the VRCOSC test tools to simulate parameter changes