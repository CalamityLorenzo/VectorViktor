using Maps.Pass;
using Microsoft.Xna.Framework;
using System;
using World.Core.Characters;
using World.Core.Vehicles;
using World.Maps;
using Xunit;

namespace World.Core.Tests
{
    public class CarTests
    {
        private static Car CarOn(IGround ground, Vector2 at, float yaw) => new Car(new Vector3(at.X, 100f, at.Y), yaw, ground);

        private static void Drive(Car car, DriveInput input, float seconds, IGround ground, Action<Car> eachTick = null)
        {
            for (var t = 0; t < (int)MathF.Round(seconds / Grounds.Tick); t++)
            {
                car.Step(input, Grounds.Tick, ground);
                eachTick?.Invoke(car);
            }
        }

        private static readonly DriveInput Flat = new DriveInput(1f, 0f);

        // Braking hard till it stops (held on, it'd go on into reverse): how long that took
        private static float BrakeToAStop(Car car, IGround ground, Player driver = null)
        {
            var seconds = 0f;
            while (car.Speed > 0.2f && seconds < 30f)
            {
                if (driver != null)
                    driver.Drive(new DriveInput(-1f, 0f), Grounds.Tick, ground);
                else
                    car.Step(new DriveInput(-1f, 0f), Grounds.Tick, ground);
                seconds += Grounds.Tick;
            }
            return seconds;
        }

        // A long flat, to get up to speed on
        private static Terrain Airfield() => Terrain.FromFunction(1024, 64, 1f, (x, z) => 0f);

        [Fact]
        public void ItGetsUpToMotorwaySpeedAndBrakesToAStop()
        {
            var ground = Airfield();
            var car = CarOn(ground, new Vector2(-500f, 0f), Grounds.East);
            Drive(car, Flat, 10f, ground);
            Assert.True(car.Speed > 25f, $"only {car.Speed * 3.6f:F0} km/h after 10 s");
            Assert.True(car.Speed < Car.MaxSpeed);
            Assert.Equal(0f, car.Position.Z, 1);   // straight on

            Assert.True(BrakeToAStop(car, ground) < 3.5f, "took too long to stop");
        }

        [Fact]
        public void OnFullLockAtWalkingPaceItTurnsRoundInATightCircle()
        {
            var ground = Airfield();
            var car = CarOn(ground, new Vector2(0f, 0f), Grounds.East);
            Drive(car, new DriveInput(0.3f, 1f), 1f, ground);
            var start = new Vector2(car.Position.X, car.Position.Z);
            float furthest = 0f;
            var turned = 0f;
            var yaw = car.Yaw;
            Drive(car, new DriveInput(0.3f, 1f), 20f, ground, c =>
            {
                furthest = MathF.Max(furthest, Vector2.Distance(start, new Vector2(c.Position.X, c.Position.Z)));
                turned += MathHelper.WrapAngle(c.Yaw - yaw);
                yaw = c.Yaw;
            });
            Assert.True(turned > MathHelper.TwoPi, $"turned only {MathHelper.ToDegrees(turned):F0} degrees");
            Assert.True(furthest < 16f, $"a circle {furthest:F1} m across");   // a hairpin's worth
        }

        [Fact]
        public void TooFastIntoABendItSlidesWide()
        {
            var ground = Airfield();
            var car = CarOn(ground, new Vector2(-500f, 0f), Grounds.East);
            Drive(car, Flat, 8f, ground);
            Drive(car, new DriveInput(0f, 1f), 0.5f, ground);
            var sideways = Vector2.Dot(car.Velocity, car.Right2);
            Assert.True(MathF.Abs(sideways) > 1f, $"slid only {sideways:F2} m/s sideways");
        }

        // The steepest of the pass's road, from a standing start
        [Fact]
        public void ItClimbsTheSteepestGradeFromRest()
        {
            var ground = Terrain.FromFunction(256, 64, 1f, (x, z) => MathF.Max(0f, x) * PassRoute.MaxGrade);
            var car = CarOn(ground, new Vector2(2f, 0f), Grounds.East);
            Drive(car, Flat, 6f, ground);
            Assert.True(car.Position.X > 20f, $"only got to x = {car.Position.X:F1}, y = {car.Position.Y:F2}, speed {car.Speed:F1}, grounded {car.Grounded}");
            Assert.True(car.Speed > 5f, $"at {car.Speed:F1} m/s, x = {car.Position.X:F1}");
        }

        // Backed up a slope as steep as the jump's pit floor: reverse pulls as hard as going forward
        [Fact]
        public void ItBacksUpAHillItCouldDriveUp()
        {
            var ground = Terrain.FromFunction(256, 64, 1f, (x, z) => MathF.Max(0f, -x) * 0.4f);   // rising west at 40%
            var car = CarOn(ground, new Vector2(-2f, 0f), Grounds.East);
            Drive(car, new DriveInput(-1f, 0f), 6f, ground);
            Assert.True(car.Position.X < -10f, $"only backed up to x = {car.Position.X:F1}");
        }

        // Through half a metre of water it's slowed hard, but not flooded
        [Fact]
        public void WaterSlowsIt()
        {
            var dry = Airfield();
            var wet = Airfield().Flood(Pool.Rectangle(new Vector2(0f, 0f), new Vector2(10f, 10f), 0.5f));
            float Through(Terrain ground)
            {
                var car = CarOn(ground, new Vector2(-60f, 0f), Grounds.East);
                Drive(car, Flat, 3f, ground);
                Drive(car, DriveInput.None, 5f, ground);   // coasting through where the water is, or isn't
                Assert.False(car.Flooded);
                return car.Speed;
            }
            var (wetSpeed, drySpeed) = (Through(wet), Through(dry));
            Assert.True(wetSpeed < drySpeed * 0.8f, $"{wetSpeed:F1} m/s after the water, {drySpeed:F1} without it");
        }

        [Fact]
        public void LetGoOnAHillItRollsBackButTheHandbrakeHoldsIt()
        {
            var ground = Grounds.SlopeFrom(8f, cap: 100f);
            var car = CarOn(ground, new Vector2(15f, 0f), Grounds.East);
            Drive(car, DriveInput.Parked, 3f, ground);
            Assert.Equal(15f, car.Position.X, 1);
            Drive(car, DriveInput.None, 3f, ground);
            Assert.True(car.Position.X < 13f, $"didn't roll back: at x = {car.Position.X:F1}");
        }

        // Driven at a cliff's foot: stopped against it, on the ground, not up it or into it
        [Fact]
        public void AWallStopsIt()
        {
            var ground = Grounds.PlateauEdge(5f);
            var car = CarOn(ground, new Vector2(20f, 0f), -Grounds.East);
            Drive(car, Flat, 8f, ground);
            Assert.Equal(0f, car.Position.Y, 2);
            Assert.True(car.Position.X > Car.Length / 2f - 1f, $"into the cliff, to x = {car.Position.X:F1}");
            Assert.True(MathF.Abs(car.Speed) < 3f);
        }

        // Driven over a cliff's edge: off it, through the air, and down to the ground below
        [Fact]
        public void OverAnEdgeItFlies()
        {
            var ground = Grounds.PlateauEdge(5f);
            var car = CarOn(ground, new Vector2(-25f, 0f), Grounds.East);
            var flew = false;
            Drive(car, Flat, 5f, ground, c => flew |= !c.Grounded);
            Assert.True(flew);
            Assert.True(car.Grounded);
            Assert.Equal(0f, car.Position.Y, 2);
            Assert.True(car.Position.X > 5f);
        }

        [Fact]
        public void YouGetInGoWhereItGoesAndGetOutBesideIt()
        {
            var ground = Airfield();
            var car = CarOn(ground, new Vector2(0f, 0f), Grounds.East);
            var player = new Player(new Vector3(0f, 0f, 2.5f), 0f, ground);
            Assert.True(player.CanReach(car));
            player.GetIn(car);
            for (var t = 0; t < 120; t++)
                player.Drive(Flat, Grounds.Tick, ground);
            Assert.False(player.GetOut(ground), "got out of a moving car");
            BrakeToAStop(car, ground, player);
            Assert.True(car.Position.X > 5f);
            Assert.True(player.GetOut(ground));
            Assert.Null(player.Driving);
            var fromCar = new Vector2(player.Body.Position.X - car.Position.X, player.Body.Position.Z - car.Position.Z);
            Assert.True(fromCar.Length() > Car.Width / 2f && fromCar.Length() < 2.5f, $"got out {fromCar.Length():F1} m from its middle");
        }

        // Flat out off the pass's road, into the mountainside: wherever it ends up - wedged against a rock face, or
        // come down on one too steep to stand on - it can always be driven away again
        [Theory]
        [InlineData("firstpass", 0.2f)]
        [InlineData("firstpass", 0.5f)]
        public void OffTheRoadFastItNeverGetsStuck(string startName, float steer)
        {
            var built = WorldBuilder.Build(PassMap.Map);
            var ground = built.Physics;
            var start = built.Starts[startName];
            var car = CarOn(ground, start.At, start.Yaw);
            Drive(car, Flat, 1.5f, ground);
            Drive(car, new DriveInput(1f, steer), 4.5f, ground);
            Assert.False(car.Flooded);

            var from = new Vector2(car.Position.X, car.Position.Z);
            var moved = 0f;
            foreach (var input in new[] { new DriveInput(-1f, 1f), new DriveInput(-1f, -1f), new DriveInput(1f, 1f), new DriveInput(1f, -1f) })
                Drive(car, input, 3f, ground, c => moved = MathF.Max(moved, Vector2.Distance(from, new Vector2(c.Position.X, c.Position.Z))));
            Assert.True(moved > 3f, $"stuck at {from}: moved only {moved:F2} m");
        }

        // Driving the pass's road, on a simple autopilot: steering for a point ahead on the road, and as fast as the
        // bends ahead allow, slowing for them in time. On the road all the way - through the ford and over the jump
        // (see Crossings) - and down into the basin.
        [Fact]
        public void TheWholePassCanBeDrivenFromTheTrailheadToTheTown()
        {
            var built = PassWorld.Value;
            var ground = built.Physics;
            var start = built.Starts["trailhead"];
            var car = CarOn(ground, start.At, start.Yaw);
            var seconds = 0f;
            var fastest = 0f;
            bool forded = false, flew = false;
            while (PassTerrain.Basin.Outside(car.Position.X, car.Position.Z) > -10f)
            {
                var here = new Vector2(car.Position.X, car.Position.Z);
                var (s, distance, _) = PassRoute.Nearest(here);
                Assert.True(distance < PassRoute.HalfWidth, $"off the road {s:F0} m along, {distance:F1} m from its middle, after {seconds:F0} s");
                car.Step(Autopilot(car, s), Grounds.Tick, ground);
                fastest = MathF.Max(fastest, car.Speed);
                forded |= car.WaterDepth > 0f;
                flew |= !car.Grounded && Crossings.InThePit(s);
                seconds += Grounds.Tick;
                Assert.True(seconds < 600f, $"stuck {s:F0} m along");
            }
            Assert.True(fastest > 15f, $"never faster than {fastest * 3.6f:F0} km/h");
            Assert.True(forded, "never went through the ford");
            Assert.True(flew, "never flew over the jump's pit");
        }

        // The pass's jump (see Crossings), coming at it at `kmh`: fast enough, over the pit and on down the road; too slow,
        // down into it, against the wall - and backed out again, up its floor and over the lip
        [Theory]
        [InlineData(90f, true)]   // as fast as it'll go, uphill: about 73 km/h
        [InlineData(35f, false)]
        public void FastEnoughItJumpsThePitAndTooSlowItBacksOutOfIt(float kmh, bool clears)
        {
            var built = PassWorld.Value;
            var ground = built.Physics;
            var from = Crossings.JumpAt - 120f;   // run-up enough, uphill, for the speed asked for
            var car = CarOn(ground, PassRoute.At(from).at, PassRoute.YawAt(from));
            float Along() => PassRoute.Nearest(new Vector2(car.Position.X, car.Position.Z)).s;
            for (var t = 0f; t < 20f; t += Grounds.Tick)
            {
                var s = Along();
                var input = Autopilot(car, s, kmh / 3.6f);
                car.Step(s < Crossings.JumpAt ? input : input with { Throttle = 0f }, Grounds.Tick, ground);
            }
            var landed = Along();
            if (clears)
            {
                Assert.True(landed > Crossings.JumpFar + 10f, $"got to {landed:F0} m along, the wall's at {Crossings.JumpFar}");
                Assert.Equal(Crossings.Surface(landed), car.Position.Y, 1);   // on the road
                return;
            }
            Assert.True(landed > Crossings.JumpAt && landed < Crossings.JumpFar, $"got to {landed:F0} m along, not in the pit");
            Drive(car, new DriveInput(-1f, 0f), 8f, ground);
            Assert.True(Along() < Crossings.JumpAt - 2f, $"backed up only to {Along():F0} m along");
        }

        // Through the pass's ford (see Crossings), at whatever the road allows: slowed hard by the water, but on through it
        [Fact]
        public void ItDrivesThroughTheFordSlowedButNotFlooded()
        {
            var built = PassWorld.Value;
            var ground = built.Physics;
            var start = built.Starts["ford"];
            var car = CarOn(ground, start.At, start.Yaw);
            float? into = null;
            var slowest = float.MaxValue;
            for (var t = 0f; t < 20f && PassRoute.Nearest(new Vector2(car.Position.X, car.Position.Z)).s < Crossings.FordAt + 40f; t += Grounds.Tick)
            {
                car.Step(Autopilot(car, PassRoute.Nearest(new Vector2(car.Position.X, car.Position.Z)).s), Grounds.Tick, ground);
                if (car.WaterDepth > 0f)
                {
                    into ??= car.Speed;
                    slowest = MathF.Min(slowest, car.Speed);
                }
            }
            Assert.True(into.HasValue, "never got into the water");
            Assert.False(car.Flooded);
            Assert.True(PassRoute.Nearest(new Vector2(car.Position.X, car.Position.Z)).s >= Crossings.FordAt + 40f, "didn't get through");
            Assert.True(slowest < into.Value * 0.8f, $"into it at {into * 3.6f:F0} km/h, and never slower than {slowest * 3.6f:F0}");
        }

        private static readonly Lazy<BuiltWorld> PassWorld = new Lazy<BuiltWorld>(() => WorldBuilder.Build(PassMap.Map));

        private static DriveInput Autopilot(Car car, float s, float most = 30f)
        {
            var speed = car.Speed;
            var here = new Vector2(car.Position.X, car.Position.Z);
            var to = PassRoute.At(s + 5f + MathF.Abs(speed) * 0.5f).at - here;
            var steer = MathHelper.Clamp(MathHelper.WrapAngle(MathF.Atan2(to.X, -to.Y) - car.Yaw) * 3f, -1f, 1f);

            // As fast as it can go round every bend in the next 80 m, and still slow for each in time
            var target = most;
            for (var d = 0f; d < 80f; d += 4f)
            {
                var a = PassRoute.At(s + d).heading;
                var b = PassRoute.At(s + d + 8f).heading;
                var bend = MathF.Acos(MathHelper.Clamp(Vector2.Dot(a, b), -1f, 1f)) / 8f;   // per metre
                var round = bend > 1e-4f ? MathF.Sqrt(0.6f * Car.Grip / bend) : 30f;
                target = MathF.Min(target, MathF.Sqrt(round * round + 2f * 0.5f * Car.Braking * d));
            }
            return new DriveInput(MathHelper.Clamp((target - speed) * 0.5f, -1f, 1f), steer);
        }
    }
}
