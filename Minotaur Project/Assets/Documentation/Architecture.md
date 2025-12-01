\# Core Architecture Overview



This project uses a \*\*manager-based, event-driven architecture\*\* with a persistent bootstrap layer.



All core systems operate independently and communicate exclusively through events rather than direct references. This prevents dependency loops and enforces predictable startup behavior.



---



\## Startup Sequence



The game boots through a fixed initialization pipeline controlled by a bootstrap system.



\### Initialization Flow



```

BOOT

&nbsp;↓

GameStateManager enters Loading

&nbsp;↓

SceneManager loads level scene

&nbsp;↓

SceneActivated event

&nbsp;↓

PlayerManager spawns player

&nbsp;↓

PlayerSpawned event

&nbsp;↓

SaveManager restores data

&nbsp;↓

LoadCompleted event

&nbsp;↓

CameraManager attaches

&nbsp;↓

CameraTargetChanged event

&nbsp;↓

GameStateManager enters Playing

&nbsp;↓

GameEvents emits GameplayReady

```



Each step only proceeds once the previous one is confirmed via event.



---



\## Manager Responsibilities



Each manager has a \*\*single responsibility\*\* and must not perform another manager’s job.



---



\### GameStateManager



Controls high-level game state:



• Booting

• Loading

• Playing

• Paused

• GameOver



All state changes are published as events for other systems to react to.



---



\### SceneManager



The only system allowed to load or unload scenes.



\*\*Responsibilities:\*\*

• Load and unload gameplay scenes

• Publish when a scene finishes loading



---



\### PlayerManager



Handles the lifecycle of the player object.



\*\*Handles:\*\*

• Player spawning

• Player destruction

• Player identity



\*\*Does NOT:\*\*

• Control health

• Control camera

• Save or load data



---



\### CameraManager



Controls visual behavior only.



\*\*Controls:\*\*

• Camera follow behavior

• Camera mode (gameplay, paused, future cutscenes)



\*\*Never:\*\*

• Controls game logic

• Accesses save data



---



\### SaveManager



The only system that handles persistence.



\*\*Responsibilities:\*\*

• Saving

• Loading

• Serialization

• Auto-save policy



\*\*Never:\*\*

• Moves the player

• Changes game state

• Spawns objects



---



\### GameEvents



A facade layer for UI and gameplay logic.



Converts low-level events such as:



• PlayerSpawned

• GameStateChanged



into higher-level gameplay events such as:



• GameplayReady

• PlayerContextChanged



This prevents UI or game systems from depending on low-level manager events.



---



\### BootstrapSequence



A controller for ordering only.



Not responsible for:

• Game logic

• State

• Data



Responsibilities:

• Enforces startup order

• Coordinates system readiness



Acts as the glue between systems during boot.



---



\## Architecture Rules



1\. Managers do not reference each other directly

2\. All communication occurs through events

3\. Managers persist across scenes

4\. Gameplay scenes contain no managers

5\. The Core Scene hosts all core services

