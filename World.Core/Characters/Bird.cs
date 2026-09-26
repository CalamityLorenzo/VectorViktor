using Microsoft.Xna.Framework;
using System;

namespace World.Core.Characters
{
    // The bird from the first VectorViktor, where it went round and round over the grid: here it roams free over the
    // country round its home, CruiseHeight over whatever's under it. It wanders: every few seconds it takes to turning
    // a new way - mostly gently, so it flies long curving runs, now and then hard enough to circle - and to flying
    // faster or slower, easing into each. Once it's strayed most of RoamRadius from home it's drawn back round towards
    // it. Every so often it swoops down to skim what's under it before climbing back up to settle a little higher or
    // lower than it was. Its nose follows it up and down, so a dive reads as a dive rather than a float. Its wings
    // ripple as they flap (see BirdMesh), FlapPhase of the way through a flap.
    //
    // It keeps Clearance over what's under it: the ground, the water, a roof or a wall (see `skyline`, and
    // BuildingGround.SkylineAt). It sees what's coming, too, along the way it's turning: a hill or a roof ahead lifts
    // it in good time, no faster than ClimbRate, rather than all at once as it gets there, and once it's past, it
    // comes down as gently.
    //
    // Its height follows all that on a critically damped spring, so it never jerks: its vertical speed, and so the
    // tilt of its nose - and of the camera chasing it (see Chase) - change smoothly, even where a dive meets a roof.
    public sealed class Bird
    {
        public const float RoamRadius = 150f;           // how far from home it wanders
        public const float CruiseHeight = 10f;          // over whatever's under it (or ahead of it: see Floor)
        public const float HeightVariance = 0.10f;      // after a dive it settles within ±10% of that
        public const float Speed = 5.25f;               // metres per second, before its changes of speed: the first bird's round its circle
        public const float MaxTurn = 0.5f;              // radians per second: at its hardest, a circle 10 m round
        public const float FlapFrequency = 0.35f;       // flaps per second: slow
        public const float Clearance = 1.5f;            // over what's under it, at the bottom of a dive
        public const float WingReach = 2f;              // how far out from its middle its wings reach, and a little more
        public const float ClimbRate = 4f;              // metres per second, to clear what's ahead of it

        private const float DiveIntervalMin = 4f, DiveIntervalMax = 9f;
        private const float DiveDuration = 4f;   // long enough, for a drop of 10 m or more, to swoop rather than plummet
        private const float DivePitchSensitivity = 0.2f;   // tilts the nose with its vertical speed...
        private const float DiveMaxPitch = 0.85f;          // ...towards this steep (as a slope), and no steeper
        private const float SpeedChangeIntervalMin = 3f, SpeedChangeIntervalMax = 7f;
        private const float SpeedMultMin = 0.6f, SpeedMultMax = 1.8f;
        private const float SpeedEase = 1f;                // seconds: how long it takes to get most of the way to a new speed
        private const float TurnChangeIntervalMin = 2f, TurnChangeIntervalMax = 6f;
        private const float TurnEase = 1.5f;               // seconds, likewise for a new turn
        private const float HomeFrom = 0.7f;               // of RoamRadius from home, it starts to be drawn back

        // The spring its height follows what it's aiming for on, radians per second: it lags that by about 2 / this.
        // What it's aiming for steps a little as each of its look-ahead samples crosses a roof's edge (see Floor), so
        // that's eased first, over AimEase, or each step would jolt it.
        private const float HeightStiffness = 6f;
        private const float AimEase = 0.1f;   // seconds
        // And however suddenly what it's aiming for moves - its look-ahead swinging across a steep bank as it turns, say -
        // it's never pushed up or down harder than this, metres per second per second: about the first bird's hardest,
        // pulling out of its dives. Its nose (and the camera) can only tip so fast.
        private const float MaxLift = 25f;

        // How far ahead (and behind) along its way it looks for what's under it, and how often: 3 s, far enough
        // that ClimbRate over it lifts it over the tallest roof. It aims to be over a roof Lead before it gets there
        // (and until Lead after), so its spring's lag doesn't bring it in low.
        private const int LookSamples = 30;
        private const float LookStep = 0.1f;   // seconds
        private const float Lead = 0.5f;       // seconds

        // Chasing it (see Chase): where the camera rides, behind and above it, and how far ahead of it it looks
        public const float ChaseDistance = 4f, ChaseHeight = 1f, ChaseLookAhead = 2f;

        private readonly Vector2 _home;
        private readonly Func<float, float, float> _skyline;
        private readonly Random _random;

        private Vector2 _at;           // where it is, seen from above
        private float _speedMult = 1f, _speedTarget = 1f, _speedTimer;
        private float _turn, _turnWander, _turnTimer;   // radians per second, + to the right; the way it'd turn if it weren't drawn home
        private float _cruise = CruiseHeight;           // how high over what's under it it's settled, between dives
        private float _diveTimer;
        private float _diveElapsed = -1f;   // < 0: not diving
        private float _diveStart, _diveEnd;
        private float _climb;          // its vertical speed, on the spring
        private float _aim;            // the height it's aiming for, eased

        public Vector3 Position { get; private set; }
        public Vector3 Velocity { get; private set; }
        // Which way it's flying, as a Player's: 0 faces -Z (north). Its nose is Pitch up from level.
        public float Yaw { get; private set; }
        public float Pitch { get; private set; }
        public float Time { get; private set; }

        public Vector3 Forward => new Vector3(MathF.Sin(Yaw) * MathF.Cos(Pitch), MathF.Sin(Pitch), -MathF.Cos(Yaw) * MathF.Cos(Pitch));

        // How far through a flap its wings are, from 0 up to 1.
        public float FlapPhase => Time * FlapFrequency % 1f;

        // It starts over `home`, facing `yaw`. `skyline` is how high whatever stands at (x, z), or within `margin`
        // of it, reaches.
        public Bird(Vector2 home, Func<float, float, float, float> skyline, float yaw = 0f, int seed = 1)
        {
            _home = home;
            _at = home;
            _skyline = (x, z) => skyline(x, z, WingReach);
            _random = new Random(seed);
            Yaw = yaw;
            _diveTimer = Between(DiveIntervalMin, DiveIntervalMax);
            _speedTimer = Between(SpeedChangeIntervalMin, SpeedChangeIntervalMax);
            _turnTimer = Between(TurnChangeIntervalMin, TurnChangeIntervalMax);
            _aim = Aim();
            Position = new Vector3(home.X, _aim, home.Y);
        }

        public void Step(float dt)
        {
            Time += dt;
            Wander(dt);
            var speed = Speed * _speedMult;
            Yaw = MathHelper.WrapAngle(Yaw + _turn * dt);
            _at += Heading(Yaw) * (speed * dt);

            if (_diveElapsed < 0f)
            {
                _diveTimer -= dt;
                if (_diveTimer <= 0f)
                {
                    _diveStart = _cruise;
                    _diveEnd = CruiseHeight * (1f + Between(-HeightVariance, HeightVariance));
                    _diveElapsed = 0f;
                }
            }
            else
            {
                _diveElapsed += dt;
                if (_diveElapsed >= DiveDuration)
                {
                    _diveElapsed = -1f;
                    _cruise = _diveEnd;
                    _diveTimer = Between(DiveIntervalMin, DiveIntervalMax);
                }
            }

            // Critically damped, integrated semi-implicitly (velocity first), as the drone's is
            _aim += (Aim() - _aim) * MathF.Min(1f, dt / AimEase);
            var lift = (_aim - Position.Y) * (HeightStiffness * HeightStiffness) - _climb * (2f * HeightStiffness);
            _climb += MathHelper.Clamp(lift, -MaxLift, MaxLift) * dt;
            var position = new Vector3(_at.X, Position.Y + _climb * dt, _at.Y);
            Velocity = (position - Position) / dt;
            Position = position;

            // tanh eases the tilt towards its steepest rather than hard-clamping it, so it doesn't snap level the
            // moment its vertical speed peaks
            Pitch = MathF.Atan(MathF.Tanh(_climb * DivePitchSensitivity) * DiveMaxPitch);
        }

        // The camera chasing it: behind and a little above it, looking a little ahead of it - tipping with it as it
        // dives and climbs - but never in the ground or through a wall, where `ground` would have it stop short.
        public (Vector3 eye, Vector3 lookAt) Chase(IGround ground)
        {
            var forward = Forward;
            var eye = Position - forward * ChaseDistance + Vector3.Up * ChaseHeight;
            var floor = _skyline(eye.X, eye.Z) + 0.5f;
            if (eye.Y < floor)
                eye.Y = floor;
            return (ground.ClearLine(Position, eye), Position + forward * ChaseLookAhead);
        }

        // Its speed and its turn, drifting to new ones every few seconds; and, far enough from home, its turn drawn
        // round to head back there
        private void Wander(float dt)
        {
            _speedTimer -= dt;
            if (_speedTimer <= 0f)
            {
                _speedTarget = Between(SpeedMultMin, SpeedMultMax);
                _speedTimer = Between(SpeedChangeIntervalMin, SpeedChangeIntervalMax);
            }
            _speedMult += (_speedTarget - _speedMult) * MathF.Min(1f, dt / SpeedEase);

            _turnTimer -= dt;
            if (_turnTimer <= 0f)
            {
                var u = Between(-1f, 1f);
                _turnWander = MaxTurn * u * u * u;   // cubed: mostly gentle, only now and then hard
                _turnTimer = Between(TurnChangeIntervalMin, TurnChangeIntervalMax);
            }

            var toHome = _home - _at;
            var away = MathHelper.Clamp((toHome.Length() / RoamRadius - HomeFrom) / (1f - HomeFrom), 0f, 1f);
            var turn = _turnWander;
            if (away > 0f)
            {
                var homeward = MathHelper.WrapAngle(MathF.Atan2(toHome.X, -toHome.Y) - Yaw);
                turn = MathHelper.Lerp(turn, MathHelper.Clamp(homeward, -MaxTurn, MaxTurn), away * away * (3f - 2f * away));
            }
            _turn += (turn - _turn) * MathF.Min(1f, dt / TurnEase);
        }

        // The height it's aiming for now: its cruising height over what's under and ahead of it, or on its way down a
        // dive to skim that and back up - but never under Clearance over it
        private float Aim()
        {
            var over = _cruise;
            if (_diveElapsed >= 0f)
            {
                var phase = _diveElapsed / DiveDuration;
                over = phase < 0.5f
                    ? MathHelper.Lerp(_diveStart, Clearance, Smooth(phase / 0.5f))
                    : MathHelper.Lerp(Clearance, _diveEnd, Smooth((phase - 0.5f) / 0.5f));
            }
            return Floor() + MathF.Max(over, Clearance);
        }

        // The lowest it may come here, less Clearance: over what's under it, and over what's ahead of it or just behind
        // it along its way - from Lead before it's over it until Lead after, to make up for the lag - less how far it
        // can climb (or come down) in the time it'll take to get there
        private float Floor()
        {
            var floor = float.MinValue;
            for (var k = -LookSamples; k <= LookSamples; k++)
            {
                var ahead = k * LookStep;
                var p = Along(ahead);
                floor = MathF.Max(floor, _skyline(p.X, p.Y) - ClimbRate * MathF.Max(0f, MathF.Abs(ahead) - Lead));
            }
            return floor;
        }

        // Where it'll be (or was) `seconds` from now, if it keeps on turning and flying as it is: round an arc
        private Vector2 Along(float seconds)
        {
            var speed = Speed * _speedMult;
            if (MathF.Abs(_turn) < 1e-4f)
                return _at + Heading(Yaw) * (speed * seconds);
            var turned = Yaw + _turn * seconds;
            var r = speed / _turn;
            return _at + new Vector2(MathF.Cos(Yaw) - MathF.Cos(turned), MathF.Sin(Yaw) - MathF.Sin(turned)) * r;
        }

        // Which way (in X, Z) a yaw faces
        private static Vector2 Heading(float yaw) => new Vector2(MathF.Sin(yaw), -MathF.Cos(yaw));

        private float Between(float min, float max) => min + (float)_random.NextDouble() * (max - min);

        private static float Smooth(float t)
        {
            t = MathHelper.Clamp(t, 0f, 1f);
            return t * t * (3f - 2f * t);
        }
    }
}
