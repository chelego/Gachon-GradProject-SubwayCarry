# SubwayCarry Development Breakdown

## 1. Purpose

This document divides the project into major development areas and detailed tasks. Team members select a primary area and a secondary area after reading this document and `GAME_DESIGN.md`.

Implementation details can change after prototypes and playtests. The dependency order and ownership boundaries should remain clear even when features are simplified.

## 2. Major Development Areas

| Area | Main Responsibility |
| --- | --- |
| A. Core Gameplay and Package Physics | Player control, carry states, collision, compression, durability, train motion, balance |
| B. Passenger AI and Crowd Simulation | Passenger decisions, movement, seats, boarding, alighting, crowd population |
| C. Transit World and Stage Flow | Stations, trains, doors, transfers, route data, scene transitions, map content |
| D. Delivery Systems and UI | Delivery selection, economy, settlement, upgrades, insurance, save data, HUD and menus |
| E. Integration, Content, and Quality | Shared data, testing, tools, performance, builds, content assembly, final integration |

Each area has one primary owner. Work can cross areas through reviewed interfaces and shared tasks.

## 3. Scene Plan

### 3.1 Required Scenes

| Scene | Purpose |
| --- | --- |
| `Bootstrap.unity` | Persistent initialization, service registration, save loading, first-scene routing |
| `MainMenu.unity` | New game, continue, settings, exit |
| `GachonHub.unity` | Delivery application, university services, run preparation |
| `StationGameplay.unity` | Reusable departure, transfer, and destination station shell loaded from station data or prefabs |
| `TrainGameplay.unity` | Reusable train interior shell loaded from line, car, crowd, and route data |
| `Prototype_Core.unity` | Isolated greybox scene for movement, package damage, NPC, and balance tests |

Settlement can be a UI state loaded over `GachonHub` or a dedicated scene if transition handling becomes simpler. Separate scenes for every station or line are not required; station and train variants should use prefabs and data where possible.

### 3.2 Scene Flow

```text
Bootstrap
  -> MainMenu
  -> GachonHub
  -> StationGameplay (departure)
  -> TrainGameplay
  -> StationGameplay (transfer, when required)
  -> TrainGameplay
  -> StationGameplay (destination)
  -> Settlement
  -> GachonHub
```

## 4. Shared Data and Prefabs

### 4.1 Data Definitions

Recommended ScriptableObject or serializable data definitions:

- `DeliveryDefinition`: item, value, fee, time, difficulty, route, unlock condition
- `RouteDefinition`: ordered ride and transfer segments
- `StationDefinition`: station identity, line links, boarding and alighting data, prefab set
- `LineDefinition`: line identity, colors, train variant, passenger weights
- `PassengerArchetype`: speed, seat preference, timing, behavior weights, prop set
- `CrowdProfile`: target occupancy and passenger composition by station and time
- `UpgradeDefinition`: type, level, price, gameplay value
- `UniversityServiceDefinition`: price, coverage, consumption rule
- `BalanceEventDefinition`: input pattern, timing, force, applicable train motion

### 4.2 Core Prefabs

- Player
- Cake package root
- Cake box
- Cake content
- Passenger base
- Train car
- Train door
- Seat
- Handle and pole interaction points
- Wall and leaning points
- Station platform module
- Transfer corridor module
- Exit trigger
- Passenger spawn and destination points

## 5. Area A: Core Gameplay and Package Physics

### A1. Player Movement

- WASD movement
- Mouse-facing rotation
- Collision with train and station geometry
- Movement speed data and agility modifier
- Movement lock during state transitions

### A2. Carry State Machine

- Default standing
- Leaning and leaving a wall or door
- Sitting and standing up
- Holding and releasing a handle or pole
- Raising and lowering overhead carry
- State transition timers
- Valid interaction and interruption rules

### A3. Stamina

- Overhead drain
- Automatic lower at zero
- Recovery delay and recovery rate
- Stamina upgrade modifier
- HUD events

### A4. Package Collision

- Separate box and cake colliders or damage targets
- Relative impact speed calculation
- Environment, NPC, player body, and floor contact
- Protection threshold and overflow from box to cake
- Damage event interface for UI, audio, visual state, and settlement

### A5. Compression

- Detect package trapped between valid colliders
- Measure available gap or compression depth
- Apply time-based or threshold-based damage
- Stabilize physics to avoid jitter damage
- Provide debug visualization and tuning values

### A6. Durability and Visual State

- Box protection value
- Cake durability percentage
- Damage stages
- Failure threshold below 20 percent
- Immediate destruction at zero
- Debug damage controls

### A7. Train Motion and Balance

- Start, stop, and speed-change events
- Carry-state eligibility
- Input sequence UI events
- Success and failure handling
- Fall and resulting collision
- Balance upgrade modifier
- Opening-side door danger

### Area A Completion Check

The player can move inside a greybox train, change every carry state, react to one train motion event, collide with NPC placeholders, and produce understandable box and cake damage.

## 6. Area B: Passenger AI and Crowd Simulation

### B1. Passenger Runtime Data

- Archetype
- Personality values
- Destination station
- Current goal
- Current position target
- Behavior cooldowns
- Carried object or prop

### B2. Common Passenger State Flow

- Wait to board
- Board train
- Select seat, standing point, leaning point, or door area
- Move to target
- Remain or reconsider
- Prepare to alight
- Move to door
- Leave train

The team can choose the internal AI architecture after testing. Behavior output and data interfaces should remain independent of a specific architecture.

### B3. Navigation and Local Avoidance

- Train-car navigation representation
- Station boarding and alighting paths
- Dynamic obstacle handling
- Personal space and avoidance strength
- Narrow-gap and crowd movement
- Prevention of permanent deadlocks

### B4. Seat System

- Seat occupancy state
- Passenger leave cue
- Delayed empty-seat recognition
- Seat interest based on personality and context
- Multiple-passenger seat competition
- Player and NPC collision during competition

### B5. Behavior Modules

- Phone walking
- Hurry
- Board before alighting ends
- Sudden stop or turn
- Door blocking
- Late alighting movement
- Group following
- Strong or weak avoidance
- Seat competition

### B6. Passenger Archetypes

Implement common behavior first, then configure office commuters, students, general passengers, elderly passengers, and middle-aged passengers. Add the remaining archetypes only through reusable behavior and prop modules.

### B7. Crowd Manager

- Existing passengers when a train is entered
- Boarding and alighting counts per stop
- Occupancy calculation
- Four crowd levels
- Station, line, and time modifiers
- Spawn budget and performance limit
- Deterministic debug profiles for tests

### Area B Completion Check

Passengers complete boarding-to-alighting loops without targeting the player, seats change ownership, crowd density changes at stops, and passenger movement creates readable but avoidable package danger.

## 7. Area C: Transit World and Stage Flow

### C1. Station Framework

- Load a station definition and prefab set
- Departure platform state
- Transfer route state
- Destination exit state
- Passenger spawn and exit points
- Signs, line colors, station name, and opening-side data

### C2. Train Framework

- Reusable train car prefab
- Doors and opening-side control
- Seats, handles, poles, walls, and leaning points
- Connected-car transitions
- Line-specific visual and layout variants
- Train arrival, departure, and stop lifecycle

### C3. Route and Segment Controller

- Ordered station segments
- Required transfer detection
- Current stop tracking
- Block unrelated exits
- Failure for missed required transfer or destination
- Transfer handoff between station and train scenes

### C4. Rail Map Data

- Represented lines and stations
- Omitted intermediate stations
- Route highlighting
- Locked-route preview
- Available-route display
- Data validation for broken routes

### C5. Content Production

- Gachon University Station hub and platform
- One direct destination
- One transfer station and destination
- Additional routes up to the six-to-eight delivery target
- Reusable intermediate platform variants
- Line-specific train variants

### Area C Completion Check

One direct route and one transfer route can be completed through correct station and train transitions without creating a unique full scene for every real station.

## 8. Area D: Delivery Systems and UI

### D1. Run State

- Cash
- Delivery progress and unlock state
- Character upgrades
- Insurance and university services
- Current accepted delivery
- Current route segment
- Save and continue
- Full run reset on bankruptcy

### D2. Delivery Selection

- Metropolitan rail map screen
- Delivery sidebar ordered by difficulty
- One-to-five-star display
- Locked and available states
- Route preview and highlight
- Confirmation screen
- Start validation and fare payment

### D3. Delivery Runtime

- Delivery start
- Objective and destination tracking
- Box and cake condition tracking
- Missed-stop failure
- Destruction failure
- Destination exit completion

### D4. Economy and Settlement

- Delivery fee
- Cake-value compensation
- Outbound fare
- Free return on perfect delivery
- Player-paid return on damage or failure
- Insurance and support application
- Profit or loss receipt
- Bankruptcy check

### D5. Upgrades and Services

- Transfer-station upgrade UI
- Stamina, balance, and agility upgrades
- Immediate application
- Gachon University Station service UI
- One-use damage insurance
- One-use transport support

### D6. HUD and Feedback

- Box protection gauge
- Cake durability percentage
- Stamina bar
- Balance input prompt
- Interaction prompt
- Station and next-stop display
- Opening-side train indicator
- Damage, failure, and completion feedback

### D7. Menus

- Main menu
- New game
- Continue
- Settings
- Pause
- Exit

### Area D Completion Check

A player can start a run, select and complete a delivery, receive a correct settlement, purchase an upgrade or service, save and continue, and reach a full reset through bankruptcy.

## 9. Area E: Integration, Content, and Quality

### E1. Project Foundation

- Bootstrap and service lifetime
- Scene transition service
- Input configuration
- Shared event interfaces
- Assembly definitions when needed
- Debug settings and test data

### E2. Data Validation and Tools

- Missing-reference checks
- Route continuity checks
- Passenger profile validation
- Delivery economy validation
- Editor shortcuts for test scenes and delivery states

### E3. Tests

- Box-to-cake damage transfer tests
- Delivery result threshold tests
- Settlement calculation tests
- Bankruptcy reset tests
- Route transition tests
- Passenger state-flow tests
- Play Mode smoke test for one full delivery

### E4. Performance

- NPC update budget
- Object pooling for passengers and props
- Physics layer matrix
- Collider count review
- Profiling in severe crowd conditions

### E5. Build and Release

- Windows build profile
- Build scene list
- Save-data version handling
- Error-free clean checkout build
- Final input, resolution, and audio checks

### E6. Content Integration

- Station variant assembly
- Train variant assembly
- Passenger appearance and prop combinations
- Damage visuals
- Animation, audio, signs, and UI asset integration
- Final route and balance data

### Area E Completion Check

The project builds from a clean checkout, shared data is validated, severe crowd conditions remain playable, and one complete delivery passes a repeatable smoke test.

## 10. Milestones

### Milestone 1: Core Prototype

- `Prototype_Core.unity`
- Player movement and facing
- Default carry and overhead carry
- Box and cake damage
- Simple moving passenger placeholders
- One balance event

Goal: confirm that protecting the cake in a crowded car is readable and enjoyable.

### Milestone 2: Complete Direct Delivery

- Main menu and hub entry
- Delivery selection for one job
- Departure station
- Train ride
- Destination exit
- Settlement and return

Goal: complete the full loop with one direct route.

### Milestone 3: Passenger and Crowd System

- Common passenger flow
- Seats and position selection
- Boarding and alighting
- Five initial archetypes
- Four crowd levels

Goal: create repeatable variation without scripted enemy behavior.

### Milestone 4: Transfer and Run Progression

- Transfer station flow
- Second train segment
- Character upgrades
- University services
- Save, continue, and bankruptcy

Goal: complete one meaningful multi-transfer run sequence.

### Milestone 5: Content Expansion and Polish

- Six to eight deliveries when schedule allows
- Additional stations, lines, passengers, props, and events
- Final visuals, audio, UI, tuning, optimization, and build QA

Goal: expand content without changing the completed core loop.

## 11. Recommended Team Ownership

Team members select one primary area and one secondary area.

### Role 1: Gameplay and Physics

Primary: Area A

Secondary options: train interactions, gameplay HUD, damage tests, integration

### Role 2: Passenger AI and Simulation

Primary: Area B

Secondary options: crowd data, station spawn points, AI debug tools, performance

### Role 3: World and Transit

Primary: Area C

Secondary options: train interactions, content integration, route data, scene transitions

### Role 4: Game Systems and UI

Primary: Area D

Secondary options: save data, economy tests, delivery data, integration

Area E is shared. One person coordinates integration, branch reviews, milestone builds, and task dependencies. Roles are ownership guides rather than exclusive boundaries.

## 12. First Task Split

Before large content production, split the first milestone into four parallel tasks:

1. Player movement, facing, and carry-state base
2. Box and cake durability with collision debug tools
3. Passenger placeholder movement and crowd spawn test
4. Prototype scene, train geometry, HUD, and integration

Merge the four tasks into `Prototype_Core.unity`, playtest, and revise the physics and NPC assumptions before building route content.

## 13. Known Risks

| Risk | Response |
| --- | --- |
| Package compression physics becomes unstable | Use simplified overlap and gap calculations with debug visualization |
| Many NPCs block each other or reduce performance | Use local avoidance limits, pooling, update budgets, and density caps |
| Scene and prefab merge conflicts | Assign temporary ownership and use prefabs instead of editing one large shared scene |
| Too many unique stations and passengers | Complete reusable systems first and cut content counts without cutting the loop |
| Balance or collision feels unfair | Add readable cues, tune thresholds through playtests, and keep deterministic test profiles |
| Economy creates unwinnable runs | Validate required fares, compensation limits, services, and delivery payouts with test data |
