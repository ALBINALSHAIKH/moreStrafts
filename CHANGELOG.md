# Changelog

All notable changes to More Strafts Players will be documented in this file.

## [0.0.1] - 2025-10-05 (Alpha Release)

### Added
- Extended lobby capacity from 4 to 10 players
- Main menu dropdown expansion (2-10 player selection)
- Quick match filter updated to support 5-10 player lobbies
- Free-for-All mode: Each player gets unique team ID
- Teams mode: Dropdown expanded to support as many teams as max players
- Network transport configuration for 5+ player connections
- Array bounds safety patches for UI elements beyond 4 players
- Comprehensive crash prevention for:
  - PlayerListItem position updates
  - RoundManager score arrays
  - MatchPoitnsHUD team displays
  - Preview slot animations/cosmetics

### Fixed
- RemoveExtraPlayerItem no longer removes all players beyond index 1
- CreateClientPlayerItem safe array access with fallback positioning
- Player 5+ authorization issues (transport configuration on lobby entry)
- Team sharing bug (players 5+ sharing team 4 in FFA mode)
- MatchPoitnsHUD IndexOutOfRangeException crashes

### Known Issues
- Changing max players dropdown after lobby creation may cause issues
- Joining 5+ player lobbies may freeze screen for up to 2 minutes (normal)
- UI only displays first 4 players/teams on end-round screen and match points HUD
- Teams mode has limited UI feedback for teams 5+ (FFA recommended for 5+ players)

### Technical Details
- 18 Harmony patches applied across 8 game classes
- Complete method replacements for array-bound-sensitive methods
- Transpiler patches for hardcoded constant modifications
- BepInEx plugin ID: `com.nitrogenia.morestrafts`
