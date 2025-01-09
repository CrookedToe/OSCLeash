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

This is a VRCOSC module port of [ZenithVal's OSCLeash](https://github.com/ZenithVal/OSCLeash), rewritten in C# to work within VRCOSC's module system. While the core functionality remains the same*, this version integrates directly with VRCOSC for a more streamlined experience. For detailed information about the original implementation, advanced features, and troubleshooting, please visit the original repository.

> ⚠️ **WARNING**: This project is currently a Work In Progress. Features may be incomplete, unstable, or subject to significant changes. Use at your own risk and please report any issues you encounter.

# Quick Start Guide

## Requirements
- [VRCOSC](https://github.com/VolcanicArts/VRCOSC)
- .NET 8.0 Runtime
- Windows 10/11
- VRChat with OSC enabled

## Known Issues
- Parameter names containing '+' are not received by the module due to an upstream SDK limitation. In your Unity parameters, rename:
  - `X+` to `XPositive`
  - `Y+` to `YPositive`
  - `Z+` to `ZPositive`
  This is required until the VRCOSC SDK parameter handling is updated.

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
| Directional (Back) | `{name}_ZNegative` | `Leash_ZNegative` |
| Directional (Right) | `{name}_XPositive` | `Leash_XPositive` |
| Directional (Left) | `{name}_XNegative` | `Leash_XNegative` |
| Directional (Up) | `{name}_YPositive` | `Leash_YPositive` |
| Directional (Down) | `{name}_YNegative` | `Leash_YNegative` |

The leash direction is set in the module settings:
- `North` - Front-facing leash (default)
- `South` - Back-facing leash
- `East` - Right-facing leash
- `West` - Left-facing leash

# Configuration

## Basic Settings
| Setting | Description | Default |
|---------|-------------|---------|
| Leash Name | Base name for your leash parameters | "Leash" |
| Leash Direction | Direction the leash faces | North |
| Walk Deadzone | Minimum stretch for walking | 0.15 |
| Run Deadzone | Minimum stretch for running | 0.70 |
| Strength Multiplier | Movement speed multiplier | 1.2 |

## Up/Down Control Settings
| Setting | Description | Default |
|---------|-------------|---------|
| Up/Down Compensation | Compensation for vertical movement | 1.0 |
| Up/Down Deadzone | Vertical angle deadzone | 0.5 |

## Turning Settings
| Setting | Description | Default |
|---------|-------------|---------|
| Turning Enabled | Enable turning control | false |
| Turning Multiplier | Turning speed multiplier | 0.80 |
| Turning Deadzone | Minimum stretch for turning | 0.15 |
| Turning Goal | Maximum turning angle in degrees | 90° |

# Troubleshooting

## Common Issues
- **No Movement Response**: 
  - Verify OSC is enabled in VRChat
  - Check that VRCOSC is running
  - Verify parameter names match exactly (including case)
  - Check that the leash name in settings matches your parameter prefix
- **Incorrect Movement**: 
  - Check physbone constraints and contact setup
  - Verify the leash direction setting matches your setup
- **No Turning**: 
  - Check that turning is enabled in settings
  - Verify the leash direction is set correctly

## Getting Help
- Join the [Discord](https://discord.com/invite/vj4brHyvT5) for VRCOSC support
- Create an [Issue](https://github.com/CrookedToe/OSCLeash/issues) for bug reports
- Check VRCOSC logs for any error messages- Enable debug logging for detailed state information
