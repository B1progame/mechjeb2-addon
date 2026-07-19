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

        public static bool VerticalBrakingUrgent(double altitude, double stoppingDistance,
            double descentSpeed, double additionalLeadTime)
        {
            if (double.IsNaN(altitude) || double.IsNaN(stoppingDistance) ||
                double.IsInfinity(altitude)) return true;
            if (double.IsPositiveInfinity(stoppingDistance)) return true;

            double turnAndIgnitionReserve = Max(0, descentSpeed) * Max(0, additionalLeadTime);
            return altitude <= Max(0, stoppingDistance) + turnAndIgnitionReserve;
        }

        public static double VerticalPriorityAcceleration(double commandedAcceleration,
            double maximumThrustAcceleration, double thrustUpProjection)
        {
            if (commandedAcceleration <= 0 || maximumThrustAcceleration <= 0 ||
                thrustUpProjection <= 0.10) return 0;

            // Compensate for the vertical component actually available while the vehicle
            // finishes rotating upright. Unlike the old attitude-error cap, this never
            // suppresses useful upward thrust during an emergency descent.
            return Min(maximumThrustAcceleration,
                commandedAcceleration / Max(0.10, thrustUpProjection));
        }

        public static bool TouchdownSpeedIsSafe(double verticalSpeed, double configuredTouchdownSpeed)
        {
            double limit = Max(2.0, Abs(configuredTouchdownSpeed) + 1.0);
            return Max(0, verticalSpeed) <= limit;
        }

        public static double RequiredLandingDeltaV(double verticalSpeed, double horizontalSpeed, double gravity,
            double thrustAcceleration, double responseTime)
        {
            if (thrustAcceleration <= gravity) return double.PositiveInfinity;

            double speed = Sqrt(verticalSpeed * verticalSpeed + horizontalSpeed * horizontalSpeed);
            double burnTime = speed / (thrustAcceleration - gravity);
            return speed + gravity * burnTime + gravity * Max(0, responseTime);
        }

        public static double PlanningThrustAcceleration(double liveThrustAcceleration,
            double initialStageTwr, double gravity)
        {
            if (!double.IsNaN(liveThrustAcceleration) && !double.IsInfinity(liveThrustAcceleration) &&
                liveThrustAcceleration > 0)
                return liveThrustAcceleration;
            if (double.IsNaN(initialStageTwr) || double.IsInfinity(initialStageTwr) ||
                double.IsNaN(gravity) || double.IsInfinity(gravity))
                return 0;
            return Max(0, initialStageTwr) * Max(0, gravity);
        }

        public static double AtmosphericTouchdownReserve(double downwardSpeed, double horizontalSpeed,
            bool bodyHasAtmosphere, double altitude, double dynamicPressure, double gravity,
            double thrustAcceleration, double responseTime, double safetyFactor)
        {
            double expectedDownwardSpeed = Max(0, downwardSpeed);
            double expectedHorizontalSpeed = Max(0, horizontalSpeed);
            if (bodyHasAtmosphere && altitude > 500)
            {
                // At entry altitude the instantaneous vertical speed is often close to zero,
                // but a booster still needs fuel for the terminal fall after drag has removed
                // its orbital velocity. Dynamic pressure progressively lowers that forecast.
                double terminalForecast = dynamicPressure < 250 ? 180 :
                    dynamicPressure < 3000 ? 140 : 90;
                expectedDownwardSpeed = Max(expectedDownwardSpeed, terminalForecast);

                // Do not charge the full orbital velocity as propulsive delta-v, but retain
                // a terminal crossrange allowance because final guidance really does command
                // the engine and RCS to cancel residual horizontal motion.
                double terminalHorizontalCap = dynamicPressure < 250 ? 120 :
                    dynamicPressure < 3000 ? 80 : 50;
                expectedHorizontalSpeed = Min(expectedHorizontalSpeed, terminalHorizontalCap);
            }

            double required = RequiredLandingDeltaV(expectedDownwardSpeed, expectedHorizontalSpeed, gravity,
                thrustAcceleration, responseTime);
            if (double.IsInfinity(required)) return required;

            // Preserve relight, spool-up and a small flare/attitude reserve even when the
            // vehicle is momentarily descending slowly.
            double terminalMinimum = 40 + Max(0, gravity) * (2 + Max(0, responseTime));
            return Max(1, safetyFactor) * Max(required, terminalMinimum);
        }

        public static double PoweredDivertDeltaV(double missDistance, double targetRadius,
            double horizontalSpeed, double lateralAcceleration, double maximumHorizontalSpeed,
            double timeToImpact, double gravity)
        {
            double distance = Max(0, missDistance - Max(0, targetRadius));
            if (distance <= 0) return 0;
            if (lateralAcceleration <= 0 || maximumHorizontalSpeed <= 0 ||
                timeToImpact <= 0 || double.IsNaN(timeToImpact) || double.IsInfinity(timeToImpact))
                return double.PositiveInfinity;

            double captureTime = TargetCaptureTime(distance, horizontalSpeed, lateralAcceleration);
            if (captureTime > timeToImpact * 1.15) return double.PositiveInfinity;

            double translationDeltaV = 2 * Min(maximumHorizontalSpeed,
                Sqrt(lateralAcceleration * distance));
            double velocityCancellation = 0.25 * Max(0, horizontalSpeed);
            double gravityLoss = 0.75 * Max(0, gravity) * captureTime;
            return translationDeltaV + velocityCancellation + gravityLoss;
        }

        public static bool ShouldConserveLandingFuel(bool enabled, bool ignoreFuelLimits,
            double availableDeltaV, double protectedReserveDeltaV, double touchdownDeltaV,
            double poweredDivertDeltaV)
        {
            if (!enabled || ignoreFuelLimits) return false;
            if (availableDeltaV < 0 || protectedReserveDeltaV < 0 ||
                touchdownDeltaV < 0 || double.IsNaN(touchdownDeltaV)) return true;
            if (double.IsInfinity(touchdownDeltaV) || double.IsInfinity(poweredDivertDeltaV) ||
                double.IsNaN(poweredDivertDeltaV)) return true;
            return availableDeltaV < protectedReserveDeltaV + touchdownDeltaV + poweredDivertDeltaV;
        }

        public static bool AutomaticAirbrakesShouldDeploy(bool targetAheadOfImpact,
            double targetError, double targetRadius, double dynamicPressure,
            double heatRatio, double maximumHeatRatio)
        {
            bool heatRequiresDrag = heatRatio > maximumHeatRatio - 0.12;
            if (heatRequiresDrag) return true;
            if (targetAheadOfImpact) return false;
            return targetError > targetRadius || dynamicPressure > 3000;
        }

        public static bool NormalEntryBurnShouldStart(bool alreadyCompleted, bool conserveFuel,
            double speed, double startSpeed, double fuelMarginDeltaV, double burnBudgetDeltaV,
            bool ignoreFuelLimits)
        {
            return !alreadyCompleted && !conserveFuel && speed >= Max(0, startSpeed) &&
                   (ignoreFuelLimits || fuelMarginDeltaV > Max(0, burnBudgetDeltaV));
        }

        public static bool EntryBurnShouldContinue(bool safetyRequired, double speed,
            double targetSpeed, double spentDeltaV, double budgetDeltaV,
            double elapsedTime, double maximumDuration)
        {
            if (safetyRequired) return true;
            return speed > Max(0, targetSpeed) &&
                   spentDeltaV < Max(0, budgetDeltaV) &&
                   elapsedTime < Max(0, maximumDuration);
        }

        public static bool NormalEntryBurnUsefulForTarget(bool predictionReady,
            double targetError, double targetRadius)
        {
            if (!predictionReady || double.IsNaN(targetError) || double.IsInfinity(targetError))
                return true;

            // A retrograde entry burn is useful for energy management, but it must not
            // destroy an impact solution which the deorbit targeting already solved.
            return targetError > Max(2000, Max(0, targetRadius) * 20);
        }

        public static bool EntryBurnTargetProtectionAllows(bool safetyRequired,
            bool predictionReady, double targetError, double bestTargetError,
            double targetRadius)
        {
            if (safetyRequired || !predictionReady ||
                double.IsNaN(targetError) || double.IsInfinity(targetError) ||
                double.IsNaN(bestTargetError) || double.IsInfinity(bestTargetError))
                return true;

            // Prediction noise can be hundreds of metres during entry. A kilometre-scale
            // allowance avoids chatter while stopping a burn long before it creates the
            // hundred-kilometre downrange errors seen in flight logs.
            double allowedRegression = Max(1000, Max(0, targetRadius) * 20);
            return targetError <= bestTargetError + allowedRegression;
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

        public static double LimitEarlyAscentAcceleration(double commandedAcceleration, double altitude,
            double verticalSpeed, double hoverCaptureAltitude, double touchdownSpeed, double gravity)
        {
            double command = Max(0, commandedAcceleration);
            if (altitude <= Max(0, hoverCaptureAltitude)) return command;

            // Above the terminal hover-capture zone the vehicle must keep descending.
            // Cut thrust if it is already rising, and remain slightly below hover thrust
            // once descent has nearly stopped. Fast descents retain full braking authority.
            if (verticalSpeed >= 0) return 0;
            double settledDescentSpeed = Max(0.5, Abs(touchdownSpeed));
            if (verticalSpeed > -settledDescentSpeed)
                return Min(command, 0.85 * Max(0, gravity));
            return command;
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

        public static double BrakingLimitedHorizontalSpeed(double missDistance, double targetRadius,
            double timeToGo, double maximumSpeed, double lateralAcceleration, double aggressiveness)
        {
            double distance = Max(0, missDistance - targetRadius);
            if (distance <= 0 || maximumSpeed <= 0 || lateralAcceleration <= 0) return 0;

            double trackingSpeed = Max(0.25, aggressiveness) * distance / Max(1, timeToGo);
            double brakingSpeed = 0.90 * Sqrt(2 * lateralAcceleration * distance);
            return Min(maximumSpeed, Min(trackingSpeed, brakingSpeed));
        }

        public static double PrecisionHorizontalSpeed(double missDistance, double targetRadius,
            double timeToGo, double maximumSpeed, double lateralAcceleration, double aggressiveness)
        {
            double positionLimited = DesiredHorizontalSpeed(
                missDistance, targetRadius, timeToGo, maximumSpeed);
            double brakingLimited = BrakingLimitedHorizontalSpeed(
                missDistance, targetRadius, timeToGo, maximumSpeed,
                lateralAcceleration, aggressiveness);
            return Min(positionLimited, brakingLimited);
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

        public static double EntryPhysicsWarpRate(double secondsUntilWarpTarget, double entryWarpLead,
            double maximumPhysicsWarpRate)
        {
            double lead = Max(2, entryWarpLead);
            double remaining = Max(1, secondsUntilWarpTarget);
            return Min(Max(1, maximumPhysicsWarpRate), Max(1, remaining / lead));
        }

        public static bool ShouldStartBoostback(bool predictionReady, double targetError, double targetRadius,
            double timeToImpact, bool ignoreFuelLimits, double fuelMarginDeltaV)
        {
            return predictionReady &&
                   targetError > Max(targetRadius * 2, 100) &&
                   timeToImpact > 20 &&
                   (ignoreFuelLimits || fuelMarginDeltaV > 0);
        }

        public static double SurfaceOffsetAngleDegrees(double surfaceDistance, double bodyRadius)
        {
            if (surfaceDistance <= 0 || bodyRadius <= 0) return 0;
            return Min(180, surfaceDistance / bodyRadius * 180 / PI);
        }

        public static bool AtmosphericPoweredCaptureAllowed(bool atmosphericCaptureOnly, bool bodyHasAtmosphere,
            double altitudeAsl, double atmosphereTop, double dynamicPressure,
            double captureAltitudeRatio, double minimumDynamicPressure)
        {
            if (!atmosphericCaptureOnly || !bodyHasAtmosphere) return true;
            double captureCeiling = Max(0, atmosphereTop) * Min(Max(captureAltitudeRatio, 0.05), 0.95);
            return altitudeAsl < captureCeiling &&
                   dynamicPressure >= Max(0, minimumDynamicPressure);
        }

        public static bool UseBallisticAtmosphericDeorbit(bool atmosphericCaptureOnly, bool bodyHasAtmosphere) =>
            atmosphericCaptureOnly && bodyHasAtmosphere;

        public static double AtmosphericDeorbitPeriapsisAltitude(double atmosphereTop, double periapsisRatio)
        {
            if (atmosphereTop <= 0) return 0;
            return atmosphereTop * Min(Max(periapsisRatio, 0.05), 0.95);
        }

        public static double AtmosphericDeorbitGuidanceTime(bool ballisticAtmosphericDeorbit,
            double periapsisTime, double surfaceImpactTime)
        {
            double selected = ballisticAtmosphericDeorbit ? periapsisTime : surfaceImpactTime;
            return double.IsNaN(selected) || double.IsInfinity(selected) ? double.NaN : selected;
        }

        public static bool AtmosphericDeorbitWindowOpen(double groundTrackError,
            double alignmentTolerance)
        {
            return !double.IsNaN(groundTrackError) && !double.IsInfinity(groundTrackError) &&
                   groundTrackError <= Max(1000, alignmentTolerance);
        }

        public static bool DeorbitBurnReadyAfterWarp(bool windowOpen, double warpRate)
        {
            return windowOpen && !double.IsNaN(warpRate) &&
                   !double.IsInfinity(warpRate) && warpRate <= 1.01;
        }

        public static double OrbitalAlignmentWarpRate(double groundTrackError,
            double alignmentTolerance, double maximumRate)
        {
            if (double.IsNaN(groundTrackError) || double.IsInfinity(groundTrackError)) return 1;
            double tolerance = Max(1000, alignmentTolerance);
            if (groundTrackError <= 2 * tolerance) return 1;
            return Min(Max(1, maximumRate), Max(1, groundTrackError / (2 * tolerance)));
        }

        public static bool DeorbitPeriapsisEstablished(double periapsisAltitude,
            double targetPeriapsisAltitude, double tolerance)
        {
            return periapsisAltitude <= targetPeriapsisAltitude + Max(0, tolerance);
        }

        public static double DeorbitTrimThrottle(double aimError, double captureTolerance)
        {
            if (double.IsNaN(aimError) || double.IsInfinity(aimError)) return 1;
            double remaining = aimError - Max(0, captureTolerance);
            if (remaining <= 0) return 0;
            if (remaining <= 5000) return 0.05;
            if (remaining <= 25000) return 0.10;
            if (remaining <= 100000) return 0.20;
            return 0.35;
        }

        public static double DeorbitDeltaVThrottle(double remainingDeltaV)
        {
            if (double.IsNaN(remainingDeltaV) || double.IsInfinity(remainingDeltaV) || remainingDeltaV <= 0) return 0;
            if (remainingDeltaV <= 5) return 0.05;
            if (remainingDeltaV <= 15) return 0.10;
            if (remainingDeltaV <= 50) return 0.25;
            return 0.50;
        }

        public static bool DeorbitAimPassed(double bestAimError, double currentAimError, double captureTolerance)
        {
            if (double.IsNaN(bestAimError) || double.IsInfinity(bestAimError) ||
                double.IsNaN(currentAimError) || double.IsInfinity(currentAimError)) return false;
            double closeEnoughToTrack = Max(5000, 4 * Max(0, captureTolerance));
            return bestAimError <= closeEnoughToTrack &&
                   currentAimError > bestAimError + Max(750, 0.25 * bestAimError);
        }

        public static bool PoweredTargetCaptureWindowOpen(bool predictionReady, double targetError,
            double captureDeadband, double timeToImpact, double targetCaptureTime, double burnLead)
        {
            if (!predictionReady || targetError <= captureDeadband ||
                timeToImpact <= 0 || targetCaptureTime <= 0 ||
                double.IsNaN(targetCaptureTime) || double.IsInfinity(targetCaptureTime)) return false;

            // Start near the accelerate/brake boundary, but reject a target that is already
            // physically too far away. The old one-sided comparison started a continuous
            // burn whenever capture time exceeded remaining time, even by hundreds of seconds.
            bool physicallyReachable = targetCaptureTime <= timeToImpact * 1.15;
            bool timeToStart = timeToImpact <= targetCaptureTime + Max(0, burnLead);
            return physicallyReachable && timeToStart;
        }

        public static double LandingProbability(double availableDeltaV, double requiredDeltaV, double twr, double targetError,
            double targetRadius, double heatRatio, double gLoad, double maxG, bool engineRelightAvailable, bool predictionReady)
        {
            if (!engineRelightAvailable || !predictionReady || requiredDeltaV <= 0 ||
                double.IsNaN(requiredDeltaV) || double.IsInfinity(requiredDeltaV)) return 0;

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
