INCLUDE game.ink

// The first scene, from "Story setup.docx", and a few short conversations that try out what scripts can do.
//
// How a line is written:
//   Droid: text              said by the droid (Droid, Drone, Mask, Operator); a line with no name carries on with
//                            whoever spoke last, so "Memory: 30%" in the diagnostics is still the droid's
//   ... #style:code          how this line looks, if not its speaker's usual
//   ... #event:zap           a one-off for the game, as the line appears (a flash, a sound)
//   ... #at:4.5              in a video call: when it's spoken in the video, for the subtitle
//   * [Option #id:name]      a choice; the id (inside the brackets) is what the decision log keeps
//   ~ play("scene")          the game's external functions: see game.ink
// A # or * at the start of text has to be written \# or \*, or ink takes it for a tag or a choice.
//
// The game starts a conversation by its name (opening, junction, mask.briefing...). Played in Inky, the script
// starts at the menu below instead; restart to pick another.

-> menu

=== menu ===
Which conversation?
+ [The opening scene] -> opening
+ [Kensington Mask's briefing (a video call)] -> mask.briefing
+ [The junction: left or right] -> junction
+ [The scrapyard: a new leg] -> scrapyard
+ [The vocal processor: the droid writes better] -> vocal_processor
+ [A remark on getting wet (try it a few times)] -> remarks.wet
+ [A remark on bumping into things] -> remarks.bump


// The drone finds the droid in the bedroom, plugs in, and wakes it.
=== opening ===
~ play("opening.arrive")
Drone: Physical connection: complete.
Drone: Broadcasting IFF..... No response.
Retry IFF..... No response.
Sharing charge. #event:zap
Droid: IFF response. Droid xClassifiedxNoneOfYourBeesWaxx
Drone: Sending security protocols.....
Droid security agent..... is a dick.
Reading droid security agent a bedtime story.
Acquiring write access to 6502 core.
Sending wake-up request.
SendWakeUp: LDX \#$00 #style:code
@next: LDA WakeMsg,X #style:code
BEQ @done #style:code
@wait: BIT STATUSPORT #style:code
BPL @wait #style:code
STA DATAPORT #style:code
INX #style:code
BNE @next #style:code
@done: RTS #style:code
WakeMsg: .byte "Wake Up", $00 #style:code
~ play("opening.wake")
Request: diagnostics.
Droid: Diagnostics report processing.
Memory: 30%
CPU: 85%
GPU: missing.
Legs: querying.
Arms: 40% (ish)
Sensors: 32%
Vocal processor: 0%
Details: my camera is missing, or all the light is missing.
Warning: so many, many things are going wrong. Recommend termination.
\*** ALERT: CHROMATIC ABERRATION *** OR MY CAMERA IS BROKEN ***
Body..... is missing.
Interrupting diagnostic: too distressing. Must process current situation. Is this message reaching you, commercial drone? Should I transmit slower?
Where is the light? Arm, legs, body, in that order. They have been present previously. I have some fantastically detailed diagnostic reports I can't share that prove it.
Drone: Your legs have been replaced by a milk urn.
Droid: That's suboptimal.
My body is 'undetected', but I have an arm. #event:click-sporks
Drone: Suboptimal! Assessment: it's probably a broom handle.
Droid: What do you have, drone?
A-HA! I'm reporting a slight increase in my locomotion abilities. Let's get moving!
~ play("opening.fall")
Drone: Switching to Ether broadcast. #style:code
Drone: Droid report! #style:ether
Droid: Droid reporting..... in shame. Apologies, and I appear to have squashed my self-destruct mechanism. Umbilicus snapped. Security protocols: re-binding.
Ether exchange rejected: operator required.
Drone: You are required to return yourself to an ambulatory state.
Droid: Ether exchange rejected: operator required..... and another arm.
-> mask.briefing


// Kensington Mask hires the operator: a video call, from outside the droid's broadcast. The #at: times are made up
// until there's a video. The choices, and Mask's answers to them, are stand-ins to show a choice in a call.
=== mask ===
= briefing
~ call("mask", "mask-briefing-01")
Mask: Operator! I'm Kensington Mask, In External InHouse Droid Recovery Services. #at:0.5
Have we got a job for you? The answer is yes. #at:5
You have been selected by our partners, as you have..... qualified..... to be a droid recovery operator. #at:8
Our completely lovely, but expensive, droid has sent a distress and recovery code, whilst on a wholly legitimate and utterly boring mission involving making weather for a balloon. #at:14
You must recover the droid, for environmental balloon reasons. And quickly too. #at:24
As you cannot be present physically, and the umbilicus is busted, the drone will initiate an Ether broadcast as you send your security protocols. That should convince the droid to start trusting you as an operative. #at:29
We believe it has been damaged, so some humouring of any idiosyncrasies may have to occur. #at:40
* [Accept the job #id:mask.accept]
    Operator: Understood. Sending security protocols.
* [What's in it for me? #id:mask.whats-in-it]
    Operator: What's in it for me?
    Mask: Continued employment. And the warm glow of environmental balloon reasons.
* [Why would a weather droid be expensive? #id:mask.why-expensive]
    Operator: Why would a weather droid be expensive?
    Mask: Weather is very expensive, Operator.
- Mask: Cease your noisy dawdling and get me a droid home.
~ hangup()
~ set("opening.done", true)
-> DONE


// A choice that changes what the world does: the world reads the "route" flag.
=== junction ===
Droid: Two ways. The left one smells of oil. Probably.
{ decided("junction.left") or decided("junction.right"):
    We have been here before. Last time we went {get("route")}.
}
+ [Left #id:junction.left]
    ~ set("route", "left")
+ [Right #id:junction.right]
    ~ set("route", "right")
- Droid: Rolling.
-> DONE


// A choice that gives the droid a part, and a cut scene the conversation waits for.
=== scrapyard ===
Droid: A leg. A real one. Hardly used.
{ has("leg-left"):
    I already have one of those. I am showing off now.
}
+ { not has("leg-left") } [Take it #id:scrapyard.take-leg]
    ~ give("leg-left")
    ~ play("fit-leg")
    Droid: Better. Considerably less suboptimal.
+ [Leave it #id:scrapyard.leave-leg]
    Droid: Your loss. Well. Mine.
- -> DONE


// The droid's text quality: fitting the vocal processor raises it, which changes how its lines look (the game's
// business) - and here, because the script chooses to, what it says too.
=== vocal_processor ===
Droid: {text_quality() < 2: Fnd smthng. Vcl? Prcssr?|I have found something: a vocal processor, by the look of it.}
+ { not has("vocal-processor") } [Fit it #id:vocal.fit]
    ~ give("vocal-processor")
    ~ play("fit-vocal-processor")
    Droid: {text_quality() < 2: Tstng. Tstng.|Testing, testing. Oh, that is much better. I can hear myself think.}
+ { has("vocal-processor") } [Take it out again (to try that) #id:vocal.remove]
    ~ take("vocal-processor")
    Droid: {text_quality() < 2: Wht hppnd.|What happened? Everything is still clear.}
+ [Leave it]
    Droid: {text_quality() < 2: Ok.|As you wish.}
- -> DONE


// Remarks while playing: the game starts these when something happens, and play goes on. Sequences keep them fresh.
=== remarks ===
= wet
Droid: {stopping: Water. Interesting.|Wet again.|Still wet.|I was not built for this.|...}
-> DONE

= bump
Droid: {shuffle: Ow.|Wall.|That was there before.|Recalibrating.|Who put that there?}
-> DONE
