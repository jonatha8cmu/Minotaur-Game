# FMOD + Unity Audio Workflow Guide

## Overview
Most of your audio work happens in **FMOD Studio**, with minimal setup needed in Unity. The AudioManager and AudioOcclusion scripts handle the Unity side automatically.

---

## FMOD Studio Setup (Do This First)

### 1. Create Your Event in FMOD Studio
1. Open your FMOD Studio project
2. Create a new event (e.g., "Monster/Screech" or "Music/Combat")
3. Add audio files to the event
4. Configure the event:
   - Set looping (if needed) in the event timeline
   - Add parameters if you want control (volume, pitch, etc.)
   - **Important**: Add these parameters for occlusion support:
     - `Directional Occlusion` (0-1 range)
     - `Spatial Occlusion` (0-1 range)
   - Set up automation/modulation for these parameters in FMOD
5. Build your FMOD banks (File ? Build)

### 2. FMOD Parameter Setup for Occlusion
If you want occlusion to affect your sound:

**Option A: Simple Volume Reduction**
```
1. Add parameter: "Directional Occlusion"
2. Right-click event volume ? Add automation
3. Draw curve: 0 (parameter) = full volume, 1 (parameter) = -80dB
```

**Option B: Low-Pass Filter (More Realistic)**
```
1. Add parameter: "Spatial Occlusion"
2. Add effect: Low-pass filter
3. Automate cutoff frequency: 0 = 20kHz, 1 = 500Hz
```

### 3. Unity FMOD Integration
Unity automatically syncs your FMOD banks if FMOD Unity Integration is set up:
- Your events appear as paths like `"event:/Monster/Screech"`
- No additional setup needed in Unity Inspector
- Just use the event path in code!

---

## Playing Sounds in Unity

### Method 1: Script-Based (Recommended for Dynamic Sounds)
**Best for:** Sound effects, dynamic music, AI sounds, player actions

```csharp
// Simple one-shot (no occlusion)
AudioManager.Instance.PlayOneShot("event:/SFX/Footstep", gameObject);

// With occlusion
AudioOcclusion occlusion = GetComponent<AudioOcclusion>();
occlusion.PlaySound("event:/Monster/Screech");

// Looping sound
AudioManager.Instance.CreatePersistentInstance("bg_music", "event:/Music/Combat", gameObject);
AudioManager.Instance.StartPersistentInstance("bg_music");
```

### Method 2: Unity Inspector (NOT Recommended for This System)
While FMOD has a `StudioEventEmitter` component for Inspector-based setup, **our system uses script-based playback** because:
- ? More control over occlusion
- ? Dynamic parameter control
- ? Better integration with AudioManager
- ? Easier to manage complex audio logic

**You CAN mix both approaches**, but for consistency, stick with script-based.

---

## Common Workflows

### Workflow 1: Background Music (Looping)
**FMOD Setup:**
- Create event: `event:/Music/Ambient`
- Enable looping in the timeline
- No special parameters needed (or add "Intensity" for dynamic music)

**Unity Script:**
```csharp
void Start()
{
    // Create and start looping music
    AudioManager.Instance.CreatePersistentInstance("ambient_music", "event:/Music/Ambient", gameObject);
    AudioManager.Instance.StartPersistentInstance("ambient_music");
}

void Update()
{
    // Optional: Update occlusion if player moves
    AudioOcclusion occlusion = GetComponent<AudioOcclusion>();
    occlusion.UpdateOcclusion("ambient_music");
}

void OnDestroy()
{
    AudioManager.Instance.StopPersistentInstance("ambient_music");
}
```

### Workflow 2: One-Shot Sound Effect
**FMOD Setup:**
- Create event: `event:/SFX/DoorOpen`
- Add sound file
- Configure volume, pitch randomization, etc.

**Unity Script:**
```csharp
public void OpenDoor()
{
    // Play sound attached to door GameObject
    AudioManager.Instance.PlayOneShot("event:/SFX/DoorOpen", gameObject);
}
```

### Workflow 3: Occluded Voice/Monster Sound
**FMOD Setup:**
- Create event: `event:/Monster/Growl`
- Add parameters: `Directional Occlusion`, `Spatial Occlusion`
- Automate volume/filter based on these parameters

**Unity Script:**
```csharp
// Add AudioOcclusion component to monster GameObject
AudioOcclusion occlusion = GetComponent<AudioOcclusion>();

public void MakeSound()
{
    // Automatically calculates and applies occlusion
    occlusion.PlaySound("event:/Monster/Growl");
}
```

### Workflow 4: Footsteps with Direction
**FMOD Setup:**
- Create event: `event:/Player/Footstep`
- Add occlusion parameters

**Unity Script:**
```csharp
AudioOcclusion occlusion = GetComponent<AudioOcclusion>();

void PlayFootstep()
{
    Vector2 movementDirection = rb.velocity.normalized;
    occlusion.PlaySoundWithDirection("event:/Player/Footstep", movementDirection);
}
```

---

## Your Test Scene Setup

### Current Setup:
- Walls (Tilemap with colliders)
- Player
- Single looping test song

### To Test Occlusion:

**1. Create Empty GameObject "SoundEmitter"**
- Position it somewhere in the scene
- Add `AudioOcclusion` component
- Configure:
  - Occlusion Layer: Set to layer your walls are on
  - Show Debug Rays: ? (to see raycasts)
  - Show Listener Radius: ?

**2. Add AudioTest Script**
```csharp
// Already created! Just attach AudioTest.cs to SoundEmitter
```

**3. Set Up Your FMOD Event**
- In FMOD Studio: Create `event:/TestSong`
- Make it loop
- Add `Directional Occlusion` and `Spatial Occlusion` parameters
- Automate volume to decrease when parameters increase
- Build banks

**4. Test in Unity**
- Play scene
- Move player around walls
- Watch debug rays (Scene view)
- Toggle "Start Looping Sound" in Inspector
- Hear occlusion effect as player goes behind walls

---

## Debugging Checklist

### Rays Not Bouncing?
- ? Check Occlusion Layer in AudioOcclusion Inspector
- ? Verify wall GameObjects are on that layer
- ? Ensure Tilemap has Collider2D component
- ? Enable "Show Debug Rays" in AudioOcclusion
- ? Run game and watch Scene view (not Game view)

### Sound Not Playing?
- ? Check FMOD event path is correct (copy from FMOD Studio)
- ? Verify FMOD banks are built (File ? Build in FMOD)
- ? Check Unity Console for "[AudioManager] Failed to create instance" errors
- ? Ensure GameObject passed to PlayOneShot is not null

### Occlusion Not Working?
- ? FMOD event has "Directional Occlusion" and "Spatial Occlusion" parameters
- ? Parameters are automated/modulating something (volume, filter, etc.)
- ? AudioOcclusion component is on the sound source GameObject
- ? Using `occlusion.PlaySound()` not `AudioManager.PlayOneShot()` directly

### Debug Ray Colors:
- ?? **Green**: Ray successfully reached listener
- ?? **Yellow**: Ray bounced off wall
- ?? **Cyan**: Initial ray segment
- ?? **Red**: Ray missed listener completely
- ?? **Magenta**: Bounce point marker / reflection direction

---

## Best Practices

### Do:
? Use script-based playback for dynamic sounds  
? Set up looping in FMOD, not code  
? Use persistent instances for looping sounds  
? Add occlusion parameters to events that need them  
? Test in FMOD Studio first before Unity  

### Don't:
? Mix FMOD StudioEventEmitter with AudioManager (pick one approach)  
? Loop sounds by calling PlayOneShot repeatedly (use persistent instances)  
? Set volume directly in Unity (use FMOD parameters)  
? Forget to build FMOD banks after changes  

---

## Quick Reference

| Task | Where | How |
|------|-------|-----|
| Create sound | FMOD Studio | New event, add audio file |
| Make it loop | FMOD Studio | Enable loop region in timeline |
| Set up occlusion | FMOD Studio | Add parameters, automate volume/filter |
| Play one-shot | Unity Script | `AudioManager.Instance.PlayOneShot(path, gameObject)` |
| Play with occlusion | Unity Script | `occlusion.PlaySound(path)` |
| Start looping | Unity Script | `CreatePersistentInstance()` + `StartPersistentInstance()` |
| Stop looping | Unity Script | `StopPersistentInstance(id)` |
| Update occlusion | Unity Script | `occlusion.UpdateOcclusion(id)` in Update() |
| Debug raycasts | Unity Inspector | AudioOcclusion ? Show Debug Rays ? |
