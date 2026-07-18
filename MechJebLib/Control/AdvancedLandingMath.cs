/*
 * Copyright Lamont Granquist, Sebastien Gaggini and the MechJeb contributors
 * SPDX-License-Identifier: LicenseRef-PD-hp OR Unlicense OR CC0-1.0 OR 0BSD OR MIT-0 OR MIT OR LGPL-2.1+
 */

using static System.Math;

namespace MechJebLib.Control
{
    /// <summary>
    /// Pure landing-control helpers. Keeping these independent of Unity/KSP makes the safety-critical
    /// calculations deterministic and unit-testable.
    /// </summary>
    public static class AdvancedLandingMath
    {
        public static double Clamp01(double value) => value < 0 ? 0 : value > 1 ? 1 : value;

        public static double StoppingDistance(double speed, double thrustAcceleration, double gravity, double responseTime,
            double safetyFactor)
        {
            double netAcceleration = thrustAcceleration - gravity;
            if (speed <= 0) return 0;
            if (netAcceleration <= 0) return double.PositiveInfinity;

            double responseDistance = speed * Max(0, responseTime);
            return safetyFactor * (speed * speed / (2 * netAcceleration) + responseDistance);
        }

        public static double RequiredLandingDeltaV(double verticalSpeed, double horizontalSpeed, double gravity,
            double thrustAcceleration, double responseTime)
        {
            if (thrustAcceleration <= gravity) return double.PositiveInfinity;

            double speed = Sqrt(verticalSpeed * verticalSpeed + horizontalSpeed * horizontalSpeed);
            double burnTime = speed / (thrustAcceleration - gravity);
            return speed + gravity * burnTime + gravity * Max(0, responseTime);
        }

        public static double VerticalThrottle(double altitude, double verticalSpeed, double targetTouchdownSpeed,
            double gravity, double minThrustAcceleration, double maxThrustAcceleration)
        {
            if (maxThrustAcceleration <= minThrustAcceleration) return maxThrustAcceleration > gravity ? 1 : 0;

            double safeAltitude = Max(altitude, 0.5);
            double desiredSpeed = -Sqrt(Max(targetTouchdownSpeed * targetTouchdownSpeed + 2 * gravity * safeAltitude * 0.35, 0));
            double velocityError = desiredSpeed - verticalSpeed;
            double desiredAcceleration = gravity + velocityError / Max(0.5, Sqrt(2 * safeAltitude / Max(gravity, 0.1)));
            return Clamp01((desiredAcceleration - minThrustAcceleration) / (maxThrustAcceleration - minThrustAcceleration));
        }

        public static double VerticalAccelerationCommand(double altitude, double verticalSpeed,
            double targetTouchdownSpeed, double gravity, double engineResponseTime, double maximumDescentSpeed)
        {
            double g = Max(0.1, gravity);
            double response = Max(0, engineResponseTime);
            double predictedAltitude = Max(0.05,
                altitude + verticalSpeed * response - 0.5 * g * response * response);
            double predictedVerticalSpeed = verticalSpeed - g * response;
            double touchdownSpeed = Max(0.05, Abs(targetTouchdownSpeed));
            double descentLimit = Max(touchdownSpeed, Abs(maximumDescentSpeed));

            // Follow a progressively shallower flare curve. At high altitude the
            // speed is capped; close to the surface it converges continuously on
            // the configured touchdown speed.
            double flareAcceleration = 0.35 * g;
            double desiredVerticalSpeed = -Min(descentLimit,
                Sqrt(touchdownSpeed * touchdownSpeed + 2 * flareAcceleration * predictedAltitude));
            double timeConstant = Min(2.0, Max(0.25,
                0.35 * Sqrt(2 * predictedAltitude / g)));
            return Max(0, g + (desiredVerticalSpeed - predictedVerticalSpeed) / timeConstant);
        }

        public static double TargetCaptureTime(double missDistance, double horizontalSpeed, double lateralAcceleration)
        {
            if (missDistance <= 0) return 0;
            if (lateralAcceleration <= 0) return double.PositiveInfinity;

            // Accelerate toward the target for half the maneuver and brake for the
            // second half, plus time to cancel the existing horizontal velocity.
            return 2 * Sqrt(missDistance / lateralAcceleration) +
                   Max(0, horizontalSpeed) / lateralAcceleration;
        }

        public static double DesiredHorizontalSpeed(double missDistance, double targetRadius, double timeToGo,
            double maximumSpeed)
        {
            if (missDistance <= targetRadius || maximumSpeed <= 0) return 0;
            double usableTime = Max(timeToGo, 1);
            return Min(maximumSpeed, 1.25 * (missDistance - targetRadius) / usableTime);
        }

        public static double PrecisionAimDeadband(double targetRadius, bool finalDescent)
        {
            double radius = Max(0.1, targetRadius);
            return finalDescent
                ? Max(0.05, Min(1.0, radius * 0.10))
                : Max(0.25, Min(5.0, radius * 0.25));
        }

        public static double ProvisionalOrbitalLandingDeltaV(double deorbitDeltaV, double gravity,
            double engineResponseTime, double safetyFactor)
        {
            // Before a deorbit burn there is intentionally no ground-impact prediction. Use a
            // conservative terminal reserve without charging the full orbital velocity twice;
            // the deorbit burn and atmosphere remove that velocity before terminal guidance.
            double terminalReserve = 150 + Max(0, gravity) * Max(0, engineResponseTime);
            return Max(0, deorbitDeltaV) + Max(1, safetyFactor) * terminalReserve;
        }

        public static bool TargetedDeorbitWindowOpen(double targetAngleToOrbitNormal, double targetAheadAngle,
            double planeChangeAngle)
        {
            if (double.IsNaN(targetAngleToOrbitNormal) || double.IsNaN(targetAheadAngle) ||
                double.IsNaN(planeChangeAngle)) return false;
            return targetAngleToOrbitNormal < 10 ||
                   targetAheadAngle > 60 && targetAheadAngle < 90 && planeChangeAngle < 90;
        }

        public static bool CanAffordManeuver(double availableDeltaV, double reservePercent, double requiredDeltaV)
        {
            if (availableDeltaV < 0 || requiredDeltaV < 0 || double.IsInfinity(requiredDeltaV) ||
                double.IsNaN(requiredDeltaV)) return false;
            double reserve = availableDeltaV * Min(Max(reservePercent / 100.0, 0), 0.9);
            return availableDeltaV - reserve >= requiredDeltaV;
        }

        public static bool SafeForOrbitalWarp(double periapsisAltitude, double currentAltitude,
            double protectedAltitude, double margin)
        {
            double safeAltitude = protectedAltitude + Max(0, margin);
            return periapsisAltitude > safeAltitude && currentAltitude > safeAltitude;
        }

        public static double RealTimeCountdown(double gameSeconds, double warpRate)
        {
            if (double.IsNaN(gameSeconds) || double.IsInfinity(gameSeconds) || gameSeconds < 0)
                return double.NaN;
            return gameSeconds / Max(1, warpRate);
        }

        public static double LandingProbability(double availableDeltaV, double requiredDeltaV, double twr, double targetError,
            double targetRadius, double heatRatio, double gLoad, double maxG, bool engineRelightAvailable, bool predictionReady)
        {
            if (!engineRelightAvailable || !predictionReady || requiredDeltaV <= 0 || double.IsInfinity(requiredDeltaV)) return 0;

            double deltaVMargin = availableDeltaV / requiredDeltaV;
            double deltaVScore = Clamp01((deltaVMargin - 0.85) / 0.45);
            double twrScore = Clamp01((twr - 1.0) / 0.75);
            double errorScore = Clamp01(1.25 - targetError / Max(targetRadius * 8, 1));
            double heatScore = Clamp01((1.0 - heatRatio) / 0.25);
            double gScore = maxG <= 0 ? 1 : Clamp01((maxG - gLoad) / Max(maxG * 0.35, 0.1));

            return 100 * (0.35 * deltaVScore + 0.25 * twrScore + 0.20 * errorScore + 0.10 * heatScore + 0.10 * gScore);
        }
    }
}
