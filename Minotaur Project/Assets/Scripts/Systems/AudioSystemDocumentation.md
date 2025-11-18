# Audio System Documentation

## Overview
A streamlined audio system for Unity with FMOD integration, featuring:
- **AudioManager**: Centralized sound playback and instance management
- **AudioOcclusion**: Raycasting-based occlusion calculation (directional + spatial)

## Architecture

### AudioManager (Singleton)
Core audio system manager that handles all FMOD operations.

**Key Features:**
- Singleton pattern with auto-creation (always persists across scenes)
- One-shot and persistent sound management
- Global and per-instance parameter control
- Automatic cleanup of finished sounds

### AudioOcclusion (Component)
Calculates sound occlusion using bouncing raycasts and integrates with AudioManager for playback.

**Occlusion Types:**
- **Directional**: Focused cone (e.g., voices, directional sounds)
- **Spatial**: 360° coverage (e.g., ambient sounds, footsteps)

**Returns:** 0 (clear/no occlusion) to 1 (fully blocked/maximum occlusion)

---

## Usage Examples

### Basic Setup

#### 1. AudioManager (Global Singleton)
```csharp
// Automatically created and persists across scenes
// Access via AudioManager.Instance anywhere
```

#### 2. AudioOcclusion (Per Sound Source)
```csharp
// Add AudioOcclusion component to GameObject that emits sound
// Configure in Inspector:
// - Occlusion Layer (what blocks sound)
// - Ray count, cone angles, max distance
// - Listener hit radius
```

---

## Code Examples

### Example 1: Simple One-Shot Sounds (No Occlusion)
```csharp
// Play sound attached to GameObject (always requires a GameObject)
AudioManager.Instance.PlayOneShot("event:/Monster/Screech", gameObject);

// With custom parameters
var parameters = new Dictionary<string, float>
{
    { "Intensity", 0.8f }
};
AudioManager.Instance.PlayOneShot("event:/Footsteps", gameObject, parameters);
```

### Example 2: One-Shot with Occlusion (AudioOcclusion)
```csharp
AudioOcclusion occlusion = GetComponent<AudioOcclusion>();

// Play with both directional and spatial occlusion (default)
occlusion.PlaySound("event:/Monster/Screech");

// Play with only directional occlusion
occlusion.PlaySound("event:/Monster/Screech", useDirectional: true, useSpatial: false);

// Play with only spatial occlusion
occlusion.PlaySound("event:/Monster/Screech", useDirectional: false, useSpatial: true);

// Play with no occlusion (same as using AudioManager directly)
occlusion.PlaySound("event:/Monster/Screech", useDirectional: false, useSpatial: false);
```

### Example 3: Directional Sounds (like WandererAI)
```csharp
AudioOcclusion occlusion = GetComponent<AudioOcclusion>();
Vector2 facingDirection = transform.right; // or ai.velocity.normalized

// Play with directional occlusion in a specific direction
occlusion.PlaySoundWithDirection("event:/Monster/Footsteps", facingDirection);

// Or manually calculate and use AudioManager
float dirOcclusion = occlusion.CalculateDirectionalOcclusion(facingDirection);
float spatOcclusion = occlusion.CalculateSpatialOcclusion();

var parameters = new Dictionary<string, float>
{
    { "Directional Occlusion", dirOcclusion },
    { "Spatial Occlusion", spatOcclusion }
};
AudioManager.Instance.PlayOneShot("event:/Monster/Footsteps", gameObject, parameters);
```

### Example 4: Persistent/Looping Sounds
```csharp
// Create persistent instance (like your test looping song)
AudioManager.Instance.CreatePersistentInstance("ambient_music", "event:/Ambient/Music", gameObject);
AudioManager.Instance.StartPersistentInstance("ambient_music");

// Update occlusion every frame for moving sources
AudioOcclusion occlusion = GetComponent<AudioOcclusion>();

void Update()
{
    // Update occlusion on the persistent sound
    occlusion.UpdateOcclusion("ambient_music", useDirectional: true, useSpatial: true);
    
    // Or update on a specific instance
    EventInstance instance = AudioManager.Instance.GetPersistentInstance("ambient_music");
    occlusion.UpdateOcclusion(instance, useDirectional: false, useSpatial: true);
}

// Stop when done
AudioManager.Instance.StopPersistentInstance("ambient_music");
```

### Example 5: Just Calculate Occlusion Values
```csharp
AudioOcclusion occlusion = GetComponent<AudioOcclusion>();

// Get directional occlusion towards listener
float dirOcclusion = occlusion.CalculateDirectionalOcclusionToListener();

// Get directional occlusion in a specific direction
Vector2 direction = transform.right;
float dirOcclusionCustom = occlusion.CalculateDirectionalOcclusion(direction);

// Get spatial occlusion
float spatOcclusion = occlusion.CalculateSpatialOcclusion();

// Get both at once (more efficient)
occlusion.CalculateBothOcclusions(out float dir, out float spat);
```

### Example 6: Global Controls
```csharp
// Set global parameters (affects all sounds)
AudioManager.Instance.SetGlobalParameter("MasterVolume", 0.7f);

// Pause/unpause all audio
AudioManager.Instance.PauseAll(true);  // pause
AudioManager.Instance.PauseAll(false); // unpause

// Stop all sounds
AudioManager.Instance.StopAllSounds();
```

---

## Porting from WandererAI

### Before (WandererAI.cs pattern):
```csharp
void PlaySound(string path)
{
    var instance = RuntimeManager.CreateInstance(path);
    RuntimeManager.AttachInstanceToGameObject(instance, gameObject);
    
    RuntimeManager.StudioSystem.setParameterByName("Directional Occlusion", dirOcclusion);
    RuntimeManager.StudioSystem.setParameterByName("Spatial Occlusion", spatOcclusion);
    
    instance.start();
    instance.release();
}
```

### After (new system):
```csharp
// Option 1: Simple with AudioOcclusion component
AudioOcclusion occlusion = GetComponent<AudioOcclusion>();
occlusion.PlaySound("event:/Monster/Screech");

// Option 2: With specific direction (like AI facing)
Vector2 facingDir = ai.velocity.normalized;
occlusion.PlaySoundWithDirection("event:/Monster/Footsteps", facingDir);

// Option 3: Toggle occlusion types
occlusion.PlaySound("event:/Voice", useDirectional: true, useSpatial: false);
```

---

## Configuration Reference

### AudioManager Inspector
- **Log Sound Playback**: Debug logging for all sounds
- **Show Active Instances**: Log active sound count in console

### AudioOcclusion Inspector
- **Max Sound Distance**: How far rays travel (35m default)
- **Max Bounces**: Ray reflection count (3 default)
- **Occlusion Layer**: LayerMask for walls/obstacles
- **Directional Ray Count**: Rays in focused cone (7 default)
- **Directional Cone Angle**: Cone spread in degrees (80° default)
- **Spatial Ray Count**: Rays for 360° (5 default)
- **Listener Hit Radius**: Valid hit distance (4m default)
- **Show Debug Rays**: Visualize raycasts in Scene view
- **Show Listener Radius**: Visualize listener detection radius

---

## Current Workflow

For your test scene (walls, player, single looping test song):

### Setup:
1. AudioManager auto-creates itself (no manual setup needed)
2. Add AudioOcclusion to sound-emitting GameObjects
3. Configure Occlusion Layer to include walls

### Playing Sounds:
```csharp
// Without occlusion (simple, fast)
AudioManager.Instance.PlayOneShot("event:/Test", gameObject);

// With occlusion (more realistic)
AudioOcclusion occ = GetComponent<AudioOcclusion>();
occ.PlaySound("event:/Test");

// Toggle what occlusion you want
occ.PlaySound("event:/Test", useDirectional: true, useSpatial: false);
```

### Looping Test Song:
```csharp
// Create and start
AudioManager.Instance.CreatePersistentInstance("test_song", "event:/TestSong", gameObject);
AudioManager.Instance.StartPersistentInstance("test_song");

// Update occlusion if source or player moves
void Update()
{
    AudioOcclusion occ = GetComponent<AudioOcclusion>();
    occ.UpdateOcclusion("test_song");
}

// Stop
AudioManager.Instance.StopPersistentInstance("test_song");
```

---

## Performance Considerations

### Current System
- **Lightweight**: Matches WandererAI implementation
- **On-demand**: Occlusion calculated only when needed
- **Minimal overhead**: Simple raycasting

### Future Optimizations (when needed)
- Cache occlusion values for X frames
- Fewer rays based on distance
- Spatial partitioning for ray culling
- Async occlusion calculation

---

## Future Enhancements

### Planned Features (when ready)
- **Reverb zones**: Room size detection
- **Frequency filtering**: Low-pass for walls
- **Doppler effect**: Fast-moving sources
- **Distance attenuation**: Custom falloff curves
- **Audio zones**: Area-based mixing

### Easy to Add Later
- System is modular and extensible
- AudioManager can be extended with new methods
- AudioOcclusion can be inherited for custom logic
- New components (AudioZone, AudioReverb) integrate easily

---

## Debugging Tips

### Enable Debug Visualization
1. Select GameObject with AudioOcclusion
2. Check "Show Debug Rays" and "Show Listener Radius"
3. Run game - see raycasts in Scene view
4. Gizmos show detection range and directional cone when selected

### Common Issues
- **No sound**: Check FMOD event path is correct
- **Wrong occlusion**: Verify Occlusion Layer includes walls
- **Rays not hitting**: Increase Listener Hit Radius or Max Bounces
- **Performance issues**: Reduce ray counts or max distance

### Inspector Debugging
- AudioManager "Show Active Instances" logs sound counts
- AudioOcclusion "Show Debug Rays" visualizes calculations

---

## Quick Reference

| Task | Code |
|------|------|
| Play sound (no occlusion) | `AudioManager.Instance.PlayOneShot(path, gameObject)` |
| Play with occlusion | `occlusion.PlaySound(path)` |
| Play with direction | `occlusion.PlaySoundWithDirection(path, direction)` |
| Toggle occlusion types | `occlusion.PlaySound(path, useDirectional, useSpatial)` |
| Create looping sound | `AudioManager.Instance.CreatePersistentInstance(id, path, gameObject)` |
| Start looping sound | `AudioManager.Instance.StartPersistentInstance(id)` |
| Update loop occlusion | `occlusion.UpdateOcclusion(id)` |
| Stop looping sound | `AudioManager.Instance.StopPersistentInstance(id)` |
| Calculate occlusion | `occlusion.CalculateDirectionalOcclusionToListener()` |
| Check line of sight | `occlusion.HasLineOfSightToListener()` |
| Pause all | `AudioManager.Instance.PauseAll(true)` |
| Stop all | `AudioManager.Instance.StopAllSounds()` |
