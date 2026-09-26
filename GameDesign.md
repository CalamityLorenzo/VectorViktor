# Game design and feature ideas

Design notes, not plans. Nothing here is to be built yet, and most of it isn't fleshed out. Section 1 is the game as you described it; the rest turns it into feature areas. "Today" notes say what the code already does, so an idea's real size can be judged; they were checked against the code on 2026-09-26. Questions for you are in section 6.

## 1. The game

You operate a droid: a robot, an artificial thing. **It is damaged.** You see the world through it, and all it can broadcast is this low-polygon, low-resolution picture. **The world has no colour.**

Over the whole game you **slowly repair the droid** (each repair gives it more it can do, and so more options) and you **put the missing colour back into the world**. There are **several worlds in the same solar system** to travel between.

It is a **puzzle adventure**: find X to do Y. There is **action, and violence**, too. There is a **narrative**, which unfolds as the robot recovers.

It is **not a linear run** through the worlds: you are **free to roam**, and can go back to earlier places with new abilities.

Everything else in this document either follows from that or is a way of making it feel true.

### How the premise shapes what the player sees
- **The picture is the droid's broadcast.** The low-resolution render, hard pixels, letterbox and wireframe are the droid's limits, not the game's. Colourless, white edges on a plain background, is what the world looks like *to it*, and is the state everything starts in (the wireframe view the game is meant to be mainly played in).
- **The HUD is the droid's own console**, and its messages are how the narrative arrives: what it notices, what it can't do yet, what it remembers. Today the picture has no text at all (the window title shows how wet you are), so there is no HUD yet.
- **Being damaged is felt.** Early on it is slow and limited, the picture may glitch or drop out, and things it can't do yet are simply not there (no jump, no arms, no drone, a short link). Repairs remove those limits one at a time.
- **The broadcast could improve as it's repaired** (see question 2): colour first, and perhaps also resolution, how far it can see, or how much detail comes through. This is an idea, not a decision.
- **Controls with weight**: each way of moving (legs, wheels, tracks) accelerates, turns and stops differently, and the operator's inputs go through the machine. Heavy lag is less fun, so this wants restraint.
- **Waking and sleeping**: a session starting as the droid coming on, damaged, and ending as the link closing, costs almost nothing and sets the frame.

## 2. The loop, and what it needs

1. **Explore** a world, colourless.
2. **Find** things and places (X) that let the droid do something (Y): a part to repair, a key, a tool, a colour, a way through.
3. **Repair and restore**: the droid gets a new ability; and colour comes back to part of the world.
4. **Overcome** the things in the way: puzzles, and enemies (action, violence).
5. **Travel** to another world, when the droid can, and the story moves on. Nothing forces an order: the droid can go where it is able to, and come back to earlier worlds with abilities that open what was closed before.

Free roaming shapes several things. What stops the droid getting somewhere too early is what it can do (its repairs, its power), not a door that opens only in sequence, so the game is built from *abilities and state* rather than *levels*. That means puzzles that can be met in any order, a narrative whose beats are gated by what has been done and not by where the player has got to, and worlds that keep their state (what's been restored, moved or opened) while the droid is away.

What that needs that the game doesn't have today, in one place (see 4): a game state (what's repaired, what's restored, what's been found, where the story is), a way to interact with more than doors, enemies and a way to fight, several worlds and the way between them, and a way to say the narrative.

## 3. Feature areas

### 3.1 The droid: components and repairs
- **Replaceable components**: arms, legs and body, each changing height, speed and how it moves; they are the repairs, and what's missing is what limits it.
- **Ways of moving**: legs, wheels, tractor tracks, and stranger ones such as strapped on top of a toy. Different ones suit different worlds and puzzles.
- **Abilities gate options**: a repair opens what the droid can reach, carry, open, climb or fight with. Arms are what let it hold an implement, so a weapon needs them (see 3.6).
- Today: one walker (`CharacterController`, fixed height, radius and speeds from `WorldConstants`), drawn as a seven-box mesh (`PlayerMesh`) that darkens when wet. Its body sizes are constants, so a component changing its height or width means making them values of the droid.

### 3.2 The drone camera
- A separate camera machine, with its own control scheme in drone mode and a distance limit from the droid: at the limit the picture and the link degrade, and past it the link drops. It suits the premise (a second thing sending pictures) and can itself be something to repair or find.
- Today: it follows the player on a spring (3 m behind, 2 m up), keeps in sight, and stays clear of the ground and walls; you can't steer it and there is no range.

### 3.3 Restoring the colour: the central mechanic
The world's colour is missing; putting it back is the game's progress, and the most visible part of it. This is where the earlier ideas of *colouring in a mesh one colour at a time* and *painting the sides of objects* belong: the player is the one doing it. The way it would work is a design question (see question 1): by colour (find red, and reds return), by object or place, or by world.
- **Today it nearly already exists.** A mesh's faces are grouped by colour slot, every instance has its own palette, and the game has a colour switch (`C`) that shows either the palette or the wireframe (faces take the background colour). Making a slot restored or not, per mesh, is a small step from that; the world's own state is what would be new.
- **Painting a side of an object** needs that side to be a slot of its own in the mesh; today a box's slots are its side, dim and top shades.
- **What comes back can also be light**, the sun and moon, the lamps' pools: see 3.7.

### 3.4 Worlds, and travelling between them
- **Several worlds in one solar system**, each its own place with its own look and puzzles, reached by travelling.
- Today a world is a list of districts and a seed (`WorldBuilder.Build(districts, seed)`): a second world is a second list, with its own terrain, buildings and starts. Going elsewhere is done by portals to scenes off the map (the corridor and the hangar are two).
- **Leaving the planet.** Space is somewhere you fly to. A vehicle launches, climbs through the clouds, the sky darkens, and it's in space with the planet below in **bands of light around its edge**. The reference picture is a flat-shaded, dithered planet from orbit, at a shallow angle, the atmosphere a few soft-edged bands, a galaxy smudge in the stars; it fits the limited-colour look.
- **It can't stay.** The vehicle doesn't have the power to stay up, and is pulled back down, unless it docks at the **space station**, which flies overhead at times of the day and can be launched up to. This can also be what gates travel: to reach another world takes more power, or stepping stones, and so more repairs.
- Today: a space plane mesh exists (`SpacePlaneMesh`, a prop in Basic.Levels only, not in the world). Nothing flies: the walker's gravity and movement are for the ground, and the drone is a spring-follow camera. The sky is one solid colour (the background, also the fog's), the far plane is the fog's end (95 m) and terrain is built only within about 110 m of the camera, so nothing large or distant is drawn.
- What it would need: an altitude-dependent sky, clouds to fly through, the planet from orbit as a scene of its own (a curved rim of flat-shaded patches, the atmosphere as a small number of concentric flat bands with dither), a power budget with a gravity that pulls back, docking, and a movement model for flight.

### 3.5 Puzzles and interaction
- **Find X to do Y**: things to pick up, carry and use; places that open when the droid can do something; things to push, stack, knock over or reach.
- Today: you press E to open or shut a door (`BuildingGround.Interact`), and bodies (crates, lockers, boxes) can be pushed, stacked, toppled and floated, so physical puzzles are possible. There is nothing to pick up, carry or use, and no state that a puzzle can set and something else read (a door that stays locked until a switch is thrown, say).
- **Switches** are one such thing, and the natural way to do the room lights in 3.7.

### 3.6 Action and violence
- **Implements that are weapons**, in a progression that follows the droid's recovery and the worlds' technology:
  1. **Simple tools**: a bat, a shovel and the like, swung in melee.
  2. **Pistols.**
  3. **A rifle.**
  4. **A sci-fi laser gun**: it really damages enemies, and **can bore holes in certain items**, which makes it a puzzle tool as well as a weapon (a way through a wall or a door, something to cut open).
- **Enemies**, and things that hurt the droid (it is already damaged, and can be damaged further). What they are is open (question 5).
- **Effects**: particles for water splashes, backfire and explosions, and for hits and the laser.
- How it would fit what exists:
  - **Arms and repairs.** Wielding anything needs the droid to have arms, so the first implement is itself a reward for a repair (3.1).
  - **Melee** can use the physics already there: crates and lockers take forces and topple, so a swing can knock things over.
  - **Ranged weapons** need a line from the droid to what it hits. Today `ClearLine` finds where a line meets the terrain, walls and ceilings (it is how the drone keeps you in sight), but not bodies or enemies; that is the missing piece for pistols, the rifle and the laser.
  - **Boring holes** is changing the world's shape while the game runs: a wall or an object gets a hole, and things can go through it. Today a building's walls are solid walls to collide with (kept in a grid for speed, built once) and every mesh is built once. The pattern the game already uses for something that changes is two meshes swapped by state (the cottages' windows: a shut one and an open one, shown by where you stand). A boreable item could be the same: a whole one and a bored one, swapped when hit, with its collision changed to match. It would also want to be in the game state (3.5, section 4) so a hole stays a hole.
  - **The droid's own weapons and ammunition**: pistols, the rifle and the laser could run on the droid's power, on ammunition to be found, or both (question 5).
- Today: nothing to fight with or against. The world's other bodies are crates and the like, moved by pushing; only the player and the drone are walkers, so enemies need a general list of walkers (the review plan's 5.5). There is no dynamic vertex data for particles, nothing to pick up or hold, and no damage.

### 3.7 The narrative
- Told as the droid recovers: in what it says and remembers, in what the worlds hold, and in what each repair or restored colour unlocks. It should be tied to the game state (3.5) so that story beats are gated by what's been done.
- Needs text on screen (the console, in 1) and a way of scripting what is said and when.

### 3.8 Content of the worlds
- **Paths**: roads, railways, tunnels (general enough for roads, railways and walkers), road crossings (a crossover with road meshes).
- **Vehicles and people**: cars and traffic lights, trains going to places forward and backward on timetables, vehicles and people moving on circuits over the day.
- **Time**: a day and night cycle, with a sun that becomes a moon.
- **Lighting**: outside, the time of day; inside, within the 1980s 16-bit look, **coloured pools from lamps** (a lamp lights a patch of floor and nearby walls in its colour, as a few flat bands, not a smooth glow) and **a room's main light**, on a switch, for a whole room or a section of it, that changes the room between its light and dark views, the lamps' pools being what you see by in the dark. With the world colourless (see 1), lights might be what colour first comes back as (question 3).
- **Trees**: more types, more experimentation.
- Today:
  - Roads are pieces laid end to end (`RoadNetwork`: straight, T-junction, bend; the meshes also have a roundabout and an island), each levelling its own ground. There are car meshes (`CarMesh`, `TrabantMesh`) but nothing drives; there are no tracks, tunnels or lights. Bodies and the walker only know the terrain, buildings and free walls, so a tunnel means ground that isn't a heightfield (a room-like shell you drive into is how the game already does interiors).
  - There is a clock (seconds since the start, for what moves by itself, such as the hangar's turntables), and a moving thing is a function from time to a position (`ScenePart`): fine for things on rails and fixed loops. Nothing keeps a time of day, and nothing simulates something with a destination.
  - No lighting. Three shades per box (side, dim, top) are baked into the palette, terrain is shaded in three steps against one fixed light, and fog and background are one constant colour (`RetroStyle.Background`, which faces also take in wireframe).
  - A room is one shell mesh with its own palette plus its props, each an instance with a palette, so a room's light or dark view is a palette change across those instances, which is cheap. A lamp's pool would be flat-coloured shapes laid on the floor, the way the billboard's art is laid on its board. What doesn't exist is a room with a state its meshes follow, or a "section" of a room: rooms are whole outlines.
  - Trees: an oak (one large rounded canopy on a forked trunk, per your notes), a tree, a spiky bush and a fern. Canopies use view-dependent outlines that are recomputed every frame: cheap for a few, and the first cost to deal with before a forest (the review plan, 5.3).

## 4. What several of these share

Worth doing once, because more than one idea needs it:

- **A game state.** What's repaired, what colour is restored (and where), what's been found and used, which switches are thrown, where the story is; kept between sessions (there is no saving today), and for every world at once, since any of them can be visited at any time. Everything in 3.1, 3.3, 3.5 and 3.7 reads or writes it. This is the largest thing the game doesn't have and the design turns on.
- **Interaction.** More than doors: pick up, use, switch, talk. Doors already work by facing one and pressing E.
- **A world clock and a time of day**, read by the day and night cycle, timetables, the station's passes and lamps.
- **A sky that's a value, not a constant**: time of day and altitude both change the sky, and the fog, and what faces take in wireframe. Done once as "the sky colour at this time and height", it serves the sun and moon, the climb into space and the planet's banding alike.
- **A route**: roads, railways, tunnels and walked paths are all "a line through the world that things follow", the same idea as `RoadNetwork` pieces, with a length and a place at a distance along it. Trains, cars and people on circuits are things that move along routes on the clock.
- **Scenes and transitions**: rooms off the map with portals already work; interiors, tunnels, other worlds and space are the same trick.
- **Energy**: the droid (and its vehicles) having limited power, which flight, docking, the link and the HUD's readouts lean on.
- **Dynamic vertex data**, for particles: today every mesh is built once and never changes.
- **Palettes as the lever for the look**: time of day, damage, lighting, restored colour and painting can mostly be done by changing an instance's palette, which the design already supports and keeps cheap.

## 5. A rough order, by what depends on what (a suggestion only)

1. **The game state and interaction**, with the smallest loop of them: something found, something repaired, a slot of colour restored, kept between sessions. The rest hangs on this.
2. **The droid's components**, as values that the repairs change; then the drone's own controls and range.
3. **Colour restoration** properly: the mechanism, and its look in the picture.
4. **The console and the narrative's first beats** (text on screen).
5. **A second world**, to prove that worlds are lists of districts, then the way between them.
6. **Enemies and fighting**, with the walker registry, then particles and effects.
7. **The clock, sun and moon, and lights** (with the sky as a value), then **paths and vehicles**, then **space** (altitude sky and clouds first, then flight and power, then the planet from orbit and the station).

Trees, tunnels and crossings can be done any time; trees are cheap to try one at a time.

## 6. Questions

1. **How is colour restored?** By colour (find red, and every red in the world comes back), by object or place (repair or "paint" each), or by world (finish a world, it's coloured)? Your earlier "one colour at a time" suggests the first: is that right?
2. **Does the broadcast improve?** Beyond colour, do repairs also raise the picture's resolution, how far it can see, or the detail that comes through, or is colour the only thing that changes?
3. **Lights while colourless:** the world starts as wireframe, where a room can't get darker by changing colours. Do lamps' pools and room lights show at all before colour returns? If so, how: edges dimmed or dashed in an unlit room, faces tinted by a pool, or is light one of the things that gets restored?
4. **The flying vehicle and travel:** is the vehicle the droid itself (flight as a way of moving, fitted like legs or wheels), or a separate craft it controls? When its power runs out does it fall (and crash, or land), glide, or get hauled back? Is power what limits how far apart the worlds can be?
5. **Enemies and ammunition:** the weapons run from a bat or shovel up to a laser that bores holes. What does the droid fight (other machines, creatures, the worlds' defences), do the enemies differ between worlds, and roughly how much of the game is action against puzzle? Do the guns use ammunition to be found, the droid's power, or both, and can the droid carry more than one implement at a time?
6. **What stops the player getting somewhere too early?** With free roaming, is it the droid's abilities and power alone (it simply can't reach or survive it yet), or also the world itself (danger, distance, a locked way)? And does the droid have to be able to travel between worlds before it can leave the first, or is there more than one world within reach from the start?
