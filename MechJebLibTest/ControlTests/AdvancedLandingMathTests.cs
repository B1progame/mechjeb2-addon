/*
 * Copyright Lamont Granquist, Sebastien Gaggini and the MechJeb contributors
 * SPDX-License-Identifier: LicenseRef-PD-hp OR Unlicense OR CC0-1.0 OR 0BSD OR MIT-0 OR MIT OR LGPL-2.1+
 */

using MechJebLib.Control;
using Xunit;

namespace MechJebLibTest.ControlTests
{
    public class AdvancedLandingMathTests
    {
        [Fact]
        public void StoppingDistanceIncludesEngineResponseAndSafetyMargin()
        {
            double distance = AdvancedLandingMath.StoppingDistance(100, 30, 10, 1, 1.1);
            Assert.Equal(385, distance, 8);
        }

        [Fact]
        public void StoppingDistanceRejectsInsufficientTwr()
        {
            Assert.True(double.IsPositiveInfinity(AdvancedLandingMath.StoppingDistance(50, 9, 10, 0, 1)));
        }

        [Fact]
        public void VerticalThrottleIncreasesNearUnsafeDescent()
        {
            double slow = AdvancedLandingMath.VerticalThrottle(100, -5, 0.5, 9.81, 0, 30);
            double fast = AdvancedLandingMath.VerticalThrottle(100, -50, 0.5, 9.81, 0, 30);
            Assert.True(fast > slow);
        }

        [Fact]
        public void VerticalAccelerationBrakesUnsafeDescent()
        {
            double settled = AdvancedLandingMath.VerticalAccelerationCommand(10, -3, 0.5, 9.81, 0, 25);
            double fast = AdvancedLandingMath.VerticalAccelerationCommand(10, -20, 0.5, 9.81, 0, 25);

            Assert.True(fast > settled);
            Assert.True(fast > 9.81);
        }

        [Fact]
        public void VerticalAccelerationAccountsForEngineResponse()
        {
            double instant = AdvancedLandingMath.VerticalAccelerationCommand(20, -10, 0.5, 9.81, 0, 25);
            double delayed = AdvancedLandingMath.VerticalAccelerationCommand(20, -10, 0.5, 9.81, 0.5, 25);

            Assert.True(delayed > instant);
        }

        [Fact]
        public void VerticalFlareConvergesNearTouchdownSpeed()
        {
            double command = AdvancedLandingMath.VerticalAccelerationCommand(0.05, -0.5, 0.5, 9.81, 0, 25);

            Assert.InRange(command, 8, 12);
        }

        [Fact]
        public void EarlyAscentGovernorCutsClimbAboveHoverZone()
        {
            Assert.Equal(0, AdvancedLandingMath.LimitEarlyAscentAcceleration(
                20, 100, 1, 15, 0.5, 9.81));
            Assert.InRange(AdvancedLandingMath.LimitEarlyAscentAcceleration(
                20, 100, -0.1, 15, 0.5, 9.81), 8.3, 8.4);
        }

        [Fact]
        public void HoverZoneRetainsVerticalAuthority()
        {
            Assert.Equal(20, AdvancedLandingMath.LimitEarlyAscentAcceleration(
                20, 10, 1, 15, 0.5, 9.81), 8);
            Assert.Equal(20, AdvancedLandingMath.LimitEarlyAscentAcceleration(
                20, 100, -20, 15, 0.5, 9.81), 8);
        }

        [Fact]
        public void ProbabilityRequiresPredictionAndRelight()
        {
            Assert.Equal(0, AdvancedLandingMath.LandingProbability(1000, 500, 2, 0, 50, 0.2, 1, 6, false, true));
            Assert.Equal(0, AdvancedLandingMath.LandingProbability(1000, 500, 2, 0, 50, 0.2, 1, 6, true, false));
        }

        [Fact]
        public void ProbabilityRewardsHealthyMargins()
        {
            double marginal = AdvancedLandingMath.LandingProbability(550, 500, 1.1, 300, 50, 0.8, 5, 6, true, true);
            double healthy = AdvancedLandingMath.LandingProbability(800, 500, 2.0, 20, 50, 0.4, 2, 6, true, true);
            Assert.True(healthy > marginal);
            Assert.InRange(healthy, 75, 100);
        }

        [Fact]
        public void TargetCaptureTimeGrowsWithMissAndExistingSpeed()
        {
            double near = AdvancedLandingMath.TargetCaptureTime(100, 0, 4);
            double far = AdvancedLandingMath.TargetCaptureTime(400, 0, 4);
            double moving = AdvancedLandingMath.TargetCaptureTime(100, 20, 4);

            Assert.True(far > near);
            Assert.True(moving > near);
        }

        [Fact]
        public void DesiredHorizontalSpeedHonorsPrecisionRadiusAndLimit()
        {
            Assert.Equal(0, AdvancedLandingMath.DesiredHorizontalSpeed(40, 50, 10, 30));
            Assert.Equal(30, AdvancedLandingMath.DesiredHorizontalSpeed(1000, 50, 10, 30), 8);
            Assert.InRange(AdvancedLandingMath.DesiredHorizontalSpeed(150, 50, 10, 30), 12.4, 12.6);
        }

        [Fact]
        public void PrecisionAimDeadbandSeeksInsideSelectedRadius()
        {
            Assert.Equal(1, AdvancedLandingMath.PrecisionAimDeadband(50, true), 8);
            Assert.Equal(0.1, AdvancedLandingMath.PrecisionAimDeadband(1, true), 8);
            Assert.Equal(5, AdvancedLandingMath.PrecisionAimDeadband(50, false), 8);
        }

        [Fact]
        public void HorizontalTransferAcceleratesWhenFarAndBrakesNearCenter()
        {
            double far = AdvancedLandingMath.BrakingLimitedHorizontalSpeed(
                10000, 1, 100, 120, 12, 1.8);
            double near = AdvancedLandingMath.BrakingLimitedHorizontalSpeed(
                100, 1, 10, 120, 12, 1.8);

            Assert.Equal(120, far, 8);
            Assert.True(near < far);
            Assert.True(near > 0);
        }

        [Fact]
        public void HorizontalTransferNeverExceedsBrakingSpeed()
        {
            double distance = 100;
            double acceleration = 4;
            double speed = AdvancedLandingMath.BrakingLimitedHorizontalSpeed(
                distance, 0, 1, 1000, acceleration, 10);
            double brakingLimit = 0.90 * System.Math.Sqrt(2 * acceleration * distance);

            Assert.Equal(brakingLimit, speed, 8);
        }

        [Fact]
        public void EntryPhysicsWarpCanAdvanceBeyondTwoWhenTimeAllows()
        {
            Assert.Equal(4, AdvancedLandingMath.EntryPhysicsWarpRate(120, 12, 4), 8);
            Assert.Equal(3, AdvancedLandingMath.EntryPhysicsWarpRate(36, 12, 4), 8);
        }

        [Fact]
        public void EntryPhysicsWarpSlowsNearAtmosphereAndHonorsLimit()
        {
            Assert.Equal(1, AdvancedLandingMath.EntryPhysicsWarpRate(8, 12, 4), 8);
            Assert.Equal(2, AdvancedLandingMath.EntryPhysicsWarpRate(120, 12, 2), 8);
        }

        [Fact]
        public void BoostbackRequiresUsableFuelUnlessFuelLimitsAreIgnored()
        {
            Assert.False(AdvancedLandingMath.ShouldStartBoostback(
                true, 10000, 50, 120, false, -100));
            Assert.True(AdvancedLandingMath.ShouldStartBoostback(
                true, 10000, 50, 120, true, -100));
            Assert.True(AdvancedLandingMath.ShouldStartBoostback(
                true, 10000, 50, 120, false, 100));
        }

        [Fact]
        public void BoostbackRequiresPredictionRangeAndTime()
        {
            Assert.False(AdvancedLandingMath.ShouldStartBoostback(
                false, 10000, 50, 120, true, 100));
            Assert.False(AdvancedLandingMath.ShouldStartBoostback(
                true, 90, 50, 120, true, 100));
            Assert.False(AdvancedLandingMath.ShouldStartBoostback(
                true, 10000, 50, 20, true, 100));
        }

        [Fact]
        public void FiveKilometerOvershootProducesExpectedKerbinSurfaceAngle()
        {
            double angle = AdvancedLandingMath.SurfaceOffsetAngleDegrees(5000, 600000);

            Assert.InRange(angle, 0.4774, 0.4775);
            Assert.Equal(0, AdvancedLandingMath.SurfaceOffsetAngleDegrees(0, 600000), 8);
        }

        [Fact]
        public void AtmosphericCaptureDefersPoweredDivertUntilEntry()
        {
            Assert.False(AdvancedLandingMath.AtmosphericPoweredCaptureAllowed(
                true, true, 69999, 70000, 1000, 0.55, 250));
            Assert.False(AdvancedLandingMath.AtmosphericPoweredCaptureAllowed(
                true, true, 30000, 70000, 100, 0.55, 250));
            Assert.True(AdvancedLandingMath.AtmosphericPoweredCaptureAllowed(
                true, true, 30000, 70000, 1000, 0.55, 250));
            Assert.True(AdvancedLandingMath.AtmosphericPoweredCaptureAllowed(
                false, true, 100000, 70000, 0, 0.55, 250));
            Assert.True(AdvancedLandingMath.AtmosphericPoweredCaptureAllowed(
                true, false, 100000, 0, 0, 0.55, 250));
        }

        [Fact]
        public void AtmosphericCaptureUsesBallisticDeorbitOnlyOnAtmosphericBodies()
        {
            Assert.True(AdvancedLandingMath.UseBallisticAtmosphericDeorbit(true, true));
            Assert.False(AdvancedLandingMath.UseBallisticAtmosphericDeorbit(false, true));
            Assert.False(AdvancedLandingMath.UseBallisticAtmosphericDeorbit(true, false));
        }

        [Fact]
        public void AtmosphericDeorbitUsesShallowHalfAtmospherePeriapsis()
        {
            Assert.Equal(35000, AdvancedLandingMath.AtmosphericDeorbitPeriapsisAltitude(70000, 0.5), 8);
            Assert.False(AdvancedLandingMath.DeorbitPeriapsisEstablished(37000, 35000, 1000));
            Assert.True(AdvancedLandingMath.DeorbitPeriapsisEstablished(36000, 35000, 1000));
            Assert.True(AdvancedLandingMath.DeorbitPeriapsisEstablished(35000, 35000, 1000));
        }

        [Fact]
        public void PredictorGuidedDeorbitThrottlesDownNearAim()
        {
            Assert.Equal(0.35, AdvancedLandingMath.DeorbitTrimThrottle(200000, 500), 8);
            Assert.Equal(0.20, AdvancedLandingMath.DeorbitTrimThrottle(50000, 500), 8);
            Assert.Equal(0.10, AdvancedLandingMath.DeorbitTrimThrottle(10000, 500), 8);
            Assert.Equal(0.05, AdvancedLandingMath.DeorbitTrimThrottle(2000, 500), 8);
            Assert.Equal(0, AdvancedLandingMath.DeorbitTrimThrottle(500, 500), 8);
        }

        [Fact]
        public void DeorbitThrottleIsLimitedBeforePredictionIsReady()
        {
            Assert.Equal(0.50, AdvancedLandingMath.DeorbitDeltaVThrottle(100), 8);
            Assert.Equal(0.25, AdvancedLandingMath.DeorbitDeltaVThrottle(30), 8);
            Assert.Equal(0.10, AdvancedLandingMath.DeorbitDeltaVThrottle(10), 8);
            Assert.Equal(0.05, AdvancedLandingMath.DeorbitDeltaVThrottle(3), 8);
            Assert.Equal(0, AdvancedLandingMath.DeorbitDeltaVThrottle(0), 8);
        }

        [Fact]
        public void PredictorGuidedDeorbitStopsAfterPassingClosestAim()
        {
            Assert.False(AdvancedLandingMath.DeorbitAimPassed(4000, 4500, 500));
            Assert.True(AdvancedLandingMath.DeorbitAimPassed(2000, 3000, 500));
            Assert.False(AdvancedLandingMath.DeorbitAimPassed(10000, 20000, 500));
        }

        [Fact]
        public void PoweredCaptureRejectsAlreadyImpossibleTarget()
        {
            Assert.True(AdvancedLandingMath.PoweredTargetCaptureWindowOpen(
                true, 5000, 5, 100, 100, 1.5));
            Assert.False(AdvancedLandingMath.PoweredTargetCaptureWindowOpen(
                true, 150000, 5, 100, 200, 1.5));
            Assert.False(AdvancedLandingMath.PoweredTargetCaptureWindowOpen(
                true, 5000, 5, 200, 100, 1.5));
        }

        [Fact]
        public void AtmosphericTouchdownReserveAnticipatesTerminalFall()
        {
            double reserve = AdvancedLandingMath.AtmosphericTouchdownReserve(
                5, true, 30000, 0, 9.81, 80, 0.5, 1.18);
            double vacuumLike = AdvancedLandingMath.AtmosphericTouchdownReserve(
                5, false, 30000, 0, 9.81, 80, 0.5, 1.18);

            Assert.True(reserve > 200);
            Assert.True(reserve > vacuumLike);
        }

        [Fact]
        public void PoweredDivertIncludesTranslationAndGravityLoss()
        {
            double near = AdvancedLandingMath.PoweredDivertDeltaV(
                500, 50, 20, 15, 120, 60, 9.81);
            double far = AdvancedLandingMath.PoweredDivertDeltaV(
                40000, 50, 20, 15, 120, 180, 9.81);

            Assert.True(near > 100);
            Assert.True(far > near + 500);
            Assert.True(double.IsInfinity(AdvancedLandingMath.PoweredDivertDeltaV(
                40000, 50, 20, 15, 120, 20, 9.81)));
        }

        [Fact]
        public void FuelConservationProtectsTouchdownBudget()
        {
            Assert.True(AdvancedLandingMath.ShouldConserveLandingFuel(
                true, false, 300, 100, 150, 100));
            Assert.False(AdvancedLandingMath.ShouldConserveLandingFuel(
                true, false, 400, 100, 150, 100));
            Assert.False(AdvancedLandingMath.ShouldConserveLandingFuel(
                true, true, 100, 100, 150, double.PositiveInfinity));
        }

        [Fact]
        public void OrbitalReserveDoesNotChargeFullOrbitalVelocity()
        {
            double required = AdvancedLandingMath.ProvisionalOrbitalLandingDeltaV(120, 9.81, 1, 1.18);

            Assert.InRange(required, 308, 309);
        }

        [Fact]
        public void OrbitalReserveIncludesDeorbitAndEngineResponse()
        {
            double fastEngine = AdvancedLandingMath.ProvisionalOrbitalLandingDeltaV(100, 9.81, 0, 1.1);
            double slowEngine = AdvancedLandingMath.ProvisionalOrbitalLandingDeltaV(200, 9.81, 2, 1.1);

            Assert.True(slowEngine > fastEngine + 100);
        }

        [Fact]
        public void TargetedDeorbitWaitsForCorrectGeometry()
        {
            Assert.False(AdvancedLandingMath.TargetedDeorbitWindowOpen(90, 120, 10));
            Assert.False(AdvancedLandingMath.TargetedDeorbitWindowOpen(90, 75, 100));
            Assert.True(AdvancedLandingMath.TargetedDeorbitWindowOpen(90, 75, 20));
            Assert.True(AdvancedLandingMath.TargetedDeorbitWindowOpen(5, 150, 120));
        }

        [Fact]
        public void ManeuverAffordabilityPreservesFuelReserve()
        {
            Assert.True(AdvancedLandingMath.CanAffordManeuver(1000, 15, 850));
            Assert.False(AdvancedLandingMath.CanAffordManeuver(1000, 15, 851));
            Assert.False(AdvancedLandingMath.CanAffordManeuver(1000, 15, double.PositiveInfinity));
        }

        [Fact]
        public void OrbitalWarpRequiresSafePeriapsisAndAltitude()
        {
            Assert.True(AdvancedLandingMath.SafeForOrbitalWarp(80000, 100000, 70000, 1000));
            Assert.False(AdvancedLandingMath.SafeForOrbitalWarp(70500, 100000, 70000, 1000));
            Assert.False(AdvancedLandingMath.SafeForOrbitalWarp(80000, 70500, 70000, 1000));
        }

        [Fact]
        public void RealTimeCountdownUsesCurrentWarpRate()
        {
            Assert.Equal(100, AdvancedLandingMath.RealTimeCountdown(1000, 10), 8);
            Assert.Equal(1000, AdvancedLandingMath.RealTimeCountdown(1000, 0), 8);
            Assert.True(double.IsNaN(AdvancedLandingMath.RealTimeCountdown(double.NaN, 100)));
        }
    }
}
