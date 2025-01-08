# Changelog

## [0.1.1] - 2024-01-08

### Changed
- Improved parameter registration with proper enum lookup and descriptions
- Added smooth movement transitions with state interpolation
- Enhanced run state handling with hysteresis and sequential application
- Simplified turning calculations and movement value clamping

### Fixed
- Fixed parameter registration order to match SDK requirements
- Fixed run state transitions to prevent flickering
- Fixed movement value handling with proper deadzones

### Removed
- Removed duplicate parameter registration method
- Reduced excessive debug logging

### Added
- Added comprehensive error handling and recovery
- Added proper cleanup on avatar change

## [0.1.0] - 2024-01-08

### Added
- Initial port of OSCLeash to VRCOSC
- Basic movement control with physbone parameters
- Configurable walk and run thresholds
- Optional turning functionality
- Safety limits for maximum velocity
- Basic parameter validation
