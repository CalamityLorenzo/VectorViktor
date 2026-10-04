// The game's side of the story: the external functions a script can call. In the game, World.Story's StoryRunner
// answers them (see StoryRunner.cs). Each also has a stand-in below, used only when there's no game to answer, so
// every script can still be played in Inky: there the stand-ins print what the game would have done, in [brackets].

EXTERNAL set(name, value)
EXTERNAL get(name)
EXTERNAL give(part)
EXTERNAL take(part)
EXTERNAL has(part)
EXTERNAL decided(id)
EXTERNAL text_quality()
EXTERNAL play(scene)
EXTERNAL start(scene)
EXTERNAL call(caller, video)
EXTERNAL hangup()

// A story flag, which the world reads too: set("route", "left")
=== function set(name, value) ===
[set {name} to {value}]

=== function get(name) ===
~ return 0

// The droid's parts: give("leg-left"), take("leg-left"), has("leg-left")
=== function give(part) ===
[given {part}]

=== function take(part) ===
[taken {part}]

=== function has(part) ===
~ return false

// Whether a choice with this id (its #id: tag) has been made, ever
=== function decided(id) ===
~ return false

// How well the droid can write: 0 (broken typewriter) to 4 (console). See NarrativePlan.md section 6.
=== function text_quality() ===
~ return 0

// A cut scene: play holds the conversation until it's over, start doesn't
=== function play(scene) ===
[cut scene: {scene}]

=== function start(scene) ===
[cut scene starts: {scene}]

// A video call to the operator, from outside the droid's broadcast: who, and which video
=== function call(caller, video) ===
[video call from {caller}: {video}]

=== function hangup() ===
[call ends]
