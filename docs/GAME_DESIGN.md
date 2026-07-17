# SubwayCarry Game Design

## 1. Overview

SubwayCarry is a 2D quarter-view delivery game set in the Seoul metropolitan subway system. The player protects a fragile cake while moving through stations, train cars, crowds, transfers, and train motion events.

The game combines physical avoidance, passenger behavior, route-based difficulty, and financial risk. NPC passengers are not enemies. They use the subway for their own purposes and create danger through ordinary movement, crowding, luggage, seat competition, boarding, and alighting.

## 2. Premise and Goal

The player is a Gachon University student trying to earn the next semester's tuition during vacation. The university provides a student job program through a delivery application.

The player accepts subway delivery jobs, earns delivery fees, improves character abilities, and unlocks harder destinations. Completing the final delivery designated by the program grants a full scholarship for the next semester.

The final destination and final delivery item are not fixed yet.

## 3. Design Pillars

1. Protect the cake through movement, positioning, facing direction, and safe locations.
2. Make subway passengers behave like people with individual goals instead of enemies targeting the player.
3. Use real subway characteristics while compressing stations and travel time for gameplay.
4. Create financial pressure through fares, compensation, insurance, upgrades, and bankruptcy.
5. Keep the full delivery loop complete before increasing the number of routes, stations, and NPC types.

## 4. Run Structure

A run starts with initial cash and no run upgrades. Standard deliveries start from Gachon University Station.

1. Open the university delivery application at Gachon University Station.
2. Select an available delivery.
3. Review the destination, cake value, expected transport cost, delivery fee, route, time, and difficulty.
4. Pay the outbound transport cost and begin the delivery.
5. Board trains, protect the cake, and complete required transfers.
6. Exit the destination station to complete the delivery.
7. Receive a settlement based on cake condition and expenses.
8. Return to Gachon University Station automatically.
9. Select another delivery, buy services, or continue the saved run later.

Normal exit saves the current run. Bankruptcy ends the run and resets all run progress.

## 5. Delivery Selection

The delivery application uses a metropolitan rail map as the main screen. A right sidebar lists deliveries from easier jobs at the bottom to harder jobs at the top.

- Each delivery displays a one-to-five-star difficulty rating.
- Available deliveries show their route clearly and can be started.
- Locked deliveries can be selected for route preview, but their route is dimmed and the start button is disabled.
- The confirmation screen shows destination, delivery item, item value, expected transport cost, and delivery fee.
- The confirmation screen does not calculate or display expected net profit.
- Difficulty formulas, star ratings, and unlock conditions are assigned during content design and balancing.

## 6. Playable Delivery Flow

### 6.1 Departure

The player moves through the required part of the departure station, waits on the platform, and boards the train directly.

### 6.2 Train Interior

The player moves inside the train, changes carry state, avoids passengers, finds safe positions, reacts to train motion, and monitors the cake box and cake durability.

Train cars can have different passenger counts. The player may move between connected cars to avoid dangerous crowd conditions.

### 6.3 Intermediate Stations

At ordinary intermediate stations, the player stays on the train while passengers board and alight. Trying to leave at an unrelated station is blocked by a short player monologue.

### 6.4 Transfers

At a required transfer station, the player must leave the train, follow the implemented transfer path, wait for the next train, and board it. Missing a required transfer or failing to leave at the destination causes delivery failure.

Character ability upgrades are available only at transfer stations and apply immediately after purchase.

### 6.5 Destination and Return

At the destination, the player leaves the train and reaches the station exit. Crossing the exit trigger ends the delivery and opens the settlement screen.

Outside-station movement is not playable. The return to Gachon University Station is automatic after settlement.

## 7. Station and Route Scope

The game uses the Seoul metropolitan rail network as its reference. The map does not reproduce every real station or corridor.

- Navigable interiors are built only for Gachon University Station, required departure areas, required transfer routes, and destination exits.
- Ordinary intermediate stations use reusable platform backgrounds and passenger spawn or exit points.
- Transfer and destination stations contain only the route needed for gameplay.
- Real station order can be compressed by removing less important intermediate stations.
- Line colors, signs, train layouts, station props, passenger patterns, and crowd behavior provide local identity.
- The initial content target is approximately six to eight deliveries, ranging from a short direct route to routes with multiple transfers.

The final station list and delivery unlock order are decided during route and content production.

## 8. Controls and Carry States

### 8.1 Basic Controls

- `WASD`: move the player.
- Mouse position: set the player's facing direction.
- Interaction input: use seats, walls, doors, handles, poles, station objects, and UI points.
- The player does not have a direct push or attack action.
- Collision is avoided by movement, position, facing direction, and carry state.

### 8.2 Default Standing

The player carries the cake box with both hands in front of the body. Standing allows movement and turning but provides no balance protection.

### 8.3 Leaning

Leaning against a wall or a non-opening door provides a relatively safe position. Leaning against the door that opens at the next station causes the player to fall backward when the door opens.

### 8.4 Sitting

Sitting is the safest normal state. Seats are scarce at higher crowd levels. The player can observe passengers preparing to leave and wait near a likely empty seat. Other passengers may also compete for the seat after it becomes empty.

### 8.5 Holding a Handle or Pole

Holding support prevents normal balance events but locks player movement. Collision and compression damage remain possible.

### 8.6 Overhead Carry

The player can lift the cake overhead to avoid compression in severe crowds.

- Movement remains possible at reduced speed.
- Stamina decreases continuously.
- At zero stamina, the player lowers the cake automatically.
- Stamina recovery begins after a short delay.
- Balance events are harder and failure can create a larger impact.

### 8.7 State Transitions

Sitting, standing, leaning, leaving a wall, grabbing support, releasing support, lifting the cake, and lowering the cake have short action times. Movement is locked during the transition. Exact times are adjusted through playtesting.

## 9. Train Motion and Balance

Balance events occur when the train starts, stops, or changes speed during travel.

- Standing and overhead carry trigger the balance input sequence.
- Sitting, leaning, and holding support prevent the normal balance sequence.
- The UI displays direct inputs such as `W!`, `A!`, `S!`, or `D!`.
- Missing or pressing a wrong input causes an immediate fall.
- Damage is produced by the resulting physical collision with the floor or nearby objects.
- Balance input length, timing, and force are adjusted during playtesting.

Before arrival, one voice announcement indicates the opening side. The opening door also uses a visible yellow or green indicator light. A player leaning on the opening door falls when it opens.

## 10. Cake Box, Cake, and Damage

The initial delivery item is a cake inside a cake box. Other fragile items are expansion content after the core system is validated.

### 10.1 Collision Damage

- Damage is evaluated when the package collider contacts an NPC, player body, wall, door, seat, floor, or other object.
- Impact damage increases with relative collision speed.
- Carry states do not apply arbitrary damage by name.
- A fall causes damage through actual contact with the floor or environment.

### 10.2 Compression Damage

Compression occurs when the package is trapped between two colliders. Damage increases as the available gap becomes smaller than the package size. Compression sources include passengers, the player's body, walls, doors, seats, and station or train objects.

The calculation can be simplified if detailed physical simulation is unstable or difficult to understand during play.

### 10.3 Two Durability Layers

The cake box and cake use separate durability values.

- Light impact damages only the box.
- Impact above the box protection limit can transfer excess damage to the cake.
- After box durability reaches zero, later damage is applied directly to the cake.
- Box damage does not create cake-value compensation by itself.
- Box damage removes perfect-delivery status and free return transport.
- Cake damage creates compensation based on item value and damage amount.

The HUD shows a small box protection gauge and a larger cake durability percentage.

### 10.4 Visual Damage

Durability is stored and displayed from `100%` to `0%`. Visuals use a limited number of states: intact, slightly damaged, heavily damaged, and destroyed. Visual thresholds are adjusted after implementation.

## 11. Delivery Result

- `100%`: perfect delivery when both box and cake remain at full durability.
- `20%` to `100%`: delivery completes; cake damage produces compensation.
- Below `20%`: delivery fails; no delivery fee is paid.
- `0%`: immediate delivery failure and full item loss handling.

Net income is calculated as:

```text
delivery fee - item compensation - player-paid transport cost
```

The player pays the outbound fare. A perfect delivery receives free return transport. Any box or cake damage makes the player pay the return fare. Failure also requires compensation and return fare from the failure location.

The settlement screen shows delivery fee, compensation, return transport, insurance or university service coverage, and final profit or loss.

## 12. Passenger AI

Passengers follow personal subway goals instead of attacking the player.

Each passenger is assembled from:

```text
passenger archetype + personality + current goal + sudden behavior + carried object
```

### 12.1 Common Flow

1. Spawn with a destination station and personality values.
2. Board the train.
3. Select a seat, leaning position, standing position, or door area.
4. Maintain or reconsider the position during travel.
5. React to available seats, crowd changes, and the approaching destination.
6. Prepare early or move at the last moment.
7. Leave the train at the assigned destination.

### 12.2 Passenger Archetypes

- Office commuter
- Student
- General passenger with varied age and behavior
- Elderly passenger
- Middle-aged passenger
- Child with guardian
- Traveler or tourist with luggage
- Late-night passenger or intoxicated passenger
- Event or group passenger
- Passenger using mobility support
- Outer-area passenger with large equipment

The first implementation target is office commuters, students, general passengers, elderly passengers, and middle-aged passengers. Additional archetypes are added through shared behavior modules.

### 12.3 Behavior Modules

- Walk while looking at a phone
- Hurry or move through narrow gaps
- Board before alighting passengers finish
- Compete for an empty seat
- Stop or turn suddenly
- Remain near a door
- Stand up late for an approaching stop
- Follow or wait for a group member
- Use strong or weak avoidance of nearby passengers
- Rush after missing an intended movement

### 12.4 Seats

A seat becomes available only after its passenger stands up. The player can notice preparation cues. Other NPCs recognize the empty seat after a short delay, then decide whether to move based on personality and context.

## 13. Crowding

Crowding is based on actual passenger occupancy and available space.

- Passengers already exist when the player boards.
- Each station has different boarding and alighting amounts.
- Time, line, station role, and events change passenger composition and counts.
- Crowding changes after every stop.

Crowd levels:

1. Relaxed: seats, walls, and support are usually available.
2. Normal: safe locations may be available but are not guaranteed.
3. Crowded: seats are usually full and most support positions are occupied.
4. Severe: safe positions are rare and movement is restricted, but enough space remains for gameplay.

## 14. Economy and Growth

Cash is the only currency. It is used for fares, compensation, upgrades, insurance, university services, and later item purchases.

Delivery fees depend on distance, time, difficulty, and item value. Exact values and star ratings are balancing data, not fixed design rules.

### 14.1 Character Upgrades

Character upgrades are purchased only at transfer stations and apply immediately.

- Stamina: increases overhead carry duration.
- Balance: reduces balance minigame difficulty.
- Agility: increases movement speed.

Upgrade prices, levels, and values are adjusted during development.

### 14.2 University Services

Insurance and other university services are purchased only at Gachon University Station.

- Damage insurance: pays one eligible compensation charge and is consumed.
- Transport support: pays one eligible transport charge and is consumed.

The player chooses between keeping operating cash, buying protection, and investing in upgrades.

### 14.3 Bankruptcy

If the player cannot pay required compensation or transport costs and has no applicable protection, the run ends immediately.

Bankruptcy resets cash, delivery progress, unlocked run content, upgrades, insurance, and university services. A new game starts from the beginning.

## 15. Content Target

The graduation project targets a playable game rather than a feature-only demonstration.

- Approximately six to eight delivery jobs
- Direct and multi-transfer routes
- Multiple lines and station identities
- Reusable station and train modules
- Four crowd levels
- Time-based passenger composition
- As many passenger archetypes and behavior combinations as the common AI system allows
- Complete selection, delivery, settlement, growth, and bankruptcy loop

Content counts can be reduced or expanded without changing the core delivery loop.

## 16. First Prototype

The first prototype uses one greybox train car, a placeholder player, a cake box, separate box and cake durability, simple moving passengers, collision or compression damage, and one temporary balance event.

It does not include the rail map, complete stations, economy, upgrades, insurance, or final art. Its purpose is to test whether moving through a crowd while protecting the cake is readable and enjoyable.

## 17. Deferred Decisions

- Final delivery destination and item
- Delivery unlock order and conditions
- Final list of represented stations and omitted stations
- Target duration for one complete run
- Exact explanation and presentation of transfer-station upgrades
- Optional chained deliveries that begin from a previous destination
- Exact timings, prices, formulas, difficulty values, visual thresholds, and content counts
