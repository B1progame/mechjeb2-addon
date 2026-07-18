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
