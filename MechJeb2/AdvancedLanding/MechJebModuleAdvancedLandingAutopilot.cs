extern alias JetBrainsAnnotations;
using System;
using System.Collections.Generic;
using System.Reflection;
using JetBrainsAnnotations::JetBrains.Annotations;
using MechJebLib.Control;
using MechJebLib.FuelFlowSimulation;
using MechJebLibBindings;
using MuMech.AdvancedLanding;
using UnityEngine;
using static MechJebLib.Utils.Statics;
using static System.Math;

namespace MuMech
{
    [UsedImplicitly]
    public class MechJebModuleAdvancedLandingAutopilot : ComputerModule
    {
        [Persistent(pass = (int)(Pass.LOCAL | Pass.TYPE | Pass.GLOBAL))]
        public AdvancedLandingSafetyMode SafetyMode = AdvancedLandingSafetyMode.Balanced;

        [Persistent(pass = (int)(Pass.LOCAL | Pass.TYPE | Pass.GLOBAL))]
        public AdvancedLandingProfile LandingProfile = AdvancedLandingProfile.Automatic;

        [Persistent(pass = (int)(Pass.LOCAL | Pass.TYPE | Pass.GLOBAL))]
        public AdvancedLandingAirbrakeMode AirbrakeMode = AdvancedLandingAirbrakeMode.Automatic;

        [Persistent(pass = (int)(Pass.LOCAL | Pass.TYPE | Pass.GLOBAL))]
        public AdvancedLandingEngineMode EngineMode = AdvancedLandingEngineMode.Automatic;

        [Persistent(pass = (int)(Pass.LOCAL | Pass.TYPE | Pass.GLOBAL))]
        public readonly EditableDouble TargetRadius = 50;

        [Persistent(pass = (int)(Pass.LOCAL | Pass.TYPE | Pass.GLOBAL))]
        public readonly EditableDouble FuelReservePercent = 15;

        [Persistent(pass = (int)(Pass.LOCAL | Pass.TYPE | Pass.GLOBAL))]
        public readonly EditableDouble MaxGForce = 6;

        [Persistent(pass = (int)(Pass.LOCAL | Pass.TYPE | Pass.GLOBAL))]
        public readonly EditableDoubleMult MaxHeatRatio = new EditableDoubleMult(0.85, 0.01);

        [Persistent(pass = (int)(Pass.LOCAL | Pass.TYPE | Pass.GLOBAL))]
        public readonly EditableDouble TouchdownSpeed = 0.5;

        [Persistent(pass = (int)(Pass.LOCAL | Pass.TYPE | Pass.GLOBAL))]
        public readonly EditableDouble FinalDescentSpeedLimit = 12;

        [Persistent(pass = (int)(Pass.LOCAL | Pass.TYPE | Pass.GLOBAL))]
        public readonly EditableDouble HoverCaptureAltitude = 15;

        [Persistent(pass = (int)(Pass.LOCAL | Pass.TYPE | Pass.GLOBAL))]
        public readonly EditableDouble ThrottlePulseWidth = 0.12;

        [Persistent(pass = (int)(Pass.LOCAL | Pass.TYPE | Pass.GLOBAL))]
        public readonly EditableDouble LandingBurnLead = 1.5;

        [Persistent(pass = (int)(Pass.LOCAL | Pass.TYPE | Pass.GLOBAL))]
        public bool UseTrajectories = true;

        [Persistent(pass = (int)(Pass.LOCAL | Pass.TYPE | Pass.GLOBAL))]
        public bool UseAirbrakes = true;

        [Persistent(pass = (int)(Pass.LOCAL | Pass.TYPE | Pass.GLOBAL))]
        public bool UseRCS = true;

        [Persistent(pass = (int)(Pass.LOCAL | Pass.TYPE | Pass.GLOBAL))]
        public bool ForceEngineFirstAttitude = true;

        [Persistent(pass = (int)(Pass.LOCAL | Pass.TYPE | Pass.GLOBAL))]
        public bool UseSpinStabilization;

        [Persistent(pass = (int)(Pass.LOCAL | Pass.TYPE | Pass.GLOBAL))]
        public readonly EditableDouble SpinRateRpm = 3;

        [Persistent(pass = (int)(Pass.LOCAL | Pass.TYPE | Pass.GLOBAL))]
        public readonly EditableDouble MaxSpinDynamicPressure = 500;

        [Persistent(pass = (int)(Pass.LOCAL | Pass.TYPE | Pass.GLOBAL))]
        public bool UseUpperRcsStabilization = true;

        [Persistent(pass = (int)(Pass.LOCAL | Pass.TYPE | Pass.GLOBAL))]
        public bool SuppressAerodynamicRoll = true;

        [Persistent(pass = (int)(Pass.LOCAL | Pass.TYPE | Pass.GLOBAL))]
        public bool PoweredTargetCapture = true;

        [Persistent(pass = (int)(Pass.LOCAL | Pass.TYPE | Pass.GLOBAL))]
        public bool AtmosphericCaptureOnly = true;

        [Persistent(pass = (int)(Pass.LOCAL | Pass.TYPE | Pass.GLOBAL))]
        public readonly EditableDouble EntryOvershootDistance = 5000;

        [Persistent(pass = (int)(Pass.LOCAL | Pass.TYPE | Pass.GLOBAL))]
        public readonly EditableDouble MaximumTargetingTilt = 25;

        [Persistent(pass = (int)(Pass.LOCAL | Pass.TYPE | Pass.GLOBAL))]
        public bool FastHorizontalTransfer = true;

        [Persistent(pass = (int)(Pass.LOCAL | Pass.TYPE | Pass.GLOBAL))]
        public readonly EditableDouble MaximumHorizontalTransferSpeed = 120;

        [Persistent(pass = (int)(Pass.LOCAL | Pass.TYPE | Pass.GLOBAL))]
        public readonly EditableDouble HorizontalTransferGain = 1.8;

        [Persistent(pass = (int)(Pass.LOCAL | Pass.TYPE | Pass.GLOBAL))]
        public bool AutoWarp = true;

        [Persistent(pass = (int)(Pass.LOCAL | Pass.TYPE | Pass.GLOBAL))]
        public readonly EditableDouble MaxAutoWarpRate = 1000;

        [Persistent(pass = (int)(Pass.LOCAL | Pass.TYPE | Pass.GLOBAL))]
        public readonly EditableDouble MaxPhysicsWarpRate = 4;

        [Persistent(pass = (int)(Pass.LOCAL | Pass.TYPE | Pass.GLOBAL))]
        public readonly EditableDouble EntryWarpLead = 12;

        [Persistent(pass = (int)(Pass.LOCAL | Pass.TYPE | Pass.GLOBAL))]
        public bool IgnoreFuelLimits;

        [Persistent(pass = (int)(Pass.LOCAL | Pass.TYPE | Pass.GLOBAL))]
        public bool DeployLandingGear = true;

        [Persistent(pass = (int)(Pass.LOCAL | Pass.TYPE | Pass.GLOBAL))]
        public bool AllowLandingStaging = true;

        [Persistent(pass = (int)(Pass.LOCAL | Pass.TYPE | Pass.GLOBAL))]
        public bool BoosterRecoveryMode;

        [Persistent(pass = (int)(Pass.LOCAL | Pass.TYPE | Pass.GLOBAL))]
        public bool DebugLogging;

        [Persistent(pass = (int)(Pass.LOCAL | Pass.TYPE | Pass.GLOBAL))]
        public bool DebugOverlay = true;

        public readonly AdvancedLandingTelemetry Telemetry = new AdvancedLandingTelemetry();
        public BoosterRecoveryPlan RecoveryPlan { get; private set; }
        public bool Active => Telemetry.Phase != AdvancedLandingPhase.Off && Telemetry.Phase != AdvancedLandingPhase.Aborted &&
                              Telemetry.Phase != AdvancedLandingPhase.Touchdown;
        public string Status => Telemetry.Phase + (string.IsNullOrEmpty(Telemetry.Warning) ? "" : ": " + Telemetry.Warning);

        private readonly TrajectoriesAdapter _trajectories = new TrajectoriesAdapter();
        private MechJebModuleLandingPredictions _nativePredictor;
        private double _lastTargetSync;
        private double _lastTelemetryUpdate;
        private double _lastLog;
        private double _lastStage;
        private AdvancedLandingPhase _previousPhase;
        private bool _rcsWasEnabled;
        private bool _deorbitBurnCommitted;
        private bool _autoWarpActive;
        private readonly DeltaSigmaThrottleModulator _landingPwm =
            new DeltaSigmaThrottleModulator(0.02, 0.12) { PulseAtMinimum = true };
        private readonly Dictionary<ModuleRCS, RcsAxisState> _rcsAxisStates = new Dictionary<ModuleRCS, RcsAxisState>();
        private readonly Dictionary<ModuleControlSurface, bool> _surfaceRollStates = new Dictionary<ModuleControlSurface, bool>();

        private sealed class RcsAxisState
        {
            public bool Pitch;
            public bool Yaw;
            public bool Roll;
        }

        private sealed class DeorbitSolution
        {
            public Vector3d DeltaV;
            public double BaseDeorbitDeltaV;
            public double FreefallTime;
            public double TargetAheadAngle;
            public double PlaneChangeAngle;
            public double TargetAngleToOrbitNormal;
            public double AimOvershoot;
            public double AimLatitude;
            public double AimLongitude;
        }

        public MechJebModuleAdvancedLandingAutopilot(MechJebCore core) : base(core)
        {
            Priority = 710;
        }

        public override void OnStart(PartModule.StartState state)
        {
            _nativePredictor = Core.GetComputerModule<MechJebModuleLandingPredictions>();
            _trajectories.Initialize();
            Core.AddToPostDrawQueue(DrawDebugOverlay);
        }

        public void StartLanding(object controller)
        {
            if (!Core.Target.PositionTargetExists)
            {
                Telemetry.Warning = "Select a MechJeb position target first";
                return;
            }

            Users.Add(controller);
            Core.Attitude.Users.Add(this);
            Core.Thrust.Users.Add(this);
            _nativePredictor.Users.Add(this);
            Core.Hoverslam.Users.Add(this);
            Core.StageStats.Users.Add(this);

            _rcsWasEnabled = Vessel.ActionGroups[KSPActionGroup.RCS];
            if (UseRCS)
            {
                Core.RCS.Users.Add(this);
                Vessel.ActionGroups.SetGroup(KSPActionGroup.RCS, true);
            }

            Telemetry.Reset();
            _deorbitBurnCommitted = false;
            _autoWarpActive = false;
            _landingPwm.Reset();
            ConfigureStabilizationHardware();
            SetPhase(AdvancedLandingPhase.Preflight);
            if (ForceEngineFirstAttitude) _trajectories.SetRetrogradeEntry(true);
            SynchronizeTarget(true);
            UpdateTelemetry(true);

            if (!Telemetry.EngineRelightAvailable)
            {
                Abort("No relight-capable landing engine");
                return;
            }

            if (SafetyMode == AdvancedLandingSafetyMode.MaximumSafety && Telemetry.PredictionReady && !Telemetry.Feasible)
                Abort("Landing is outside configured safety margins");
        }

        public void StopLanding() => Abort("Manual abort");

        public void UpdateFeasibility()
        {
            SynchronizeTarget(true);
            UpdateTelemetry(true);
        }

        public void ArmRecoveryPlan(int separationStage)
        {
            UpdateTelemetry(true);
            RecoveryPlan = new BoosterRecoveryPlan
            {
                SourceVesselPersistentId = Vessel.persistentId,
                SeparationStage = separationStage,
                TargetLatitude = Core.Target.targetLatitude,
                TargetLongitude = Core.Target.targetLongitude,
                ReservedDeltaV = IgnoreFuelLimits
                    ? 0
                    : Telemetry.AvailableDeltaV * Clamp(FuelReservePercent / 100.0, 0, 0.9),
                EstimatedRequiredDeltaV = Telemetry.RequiredDeltaV,
                Viable = Telemetry.Feasible,
                Status = Telemetry.Feasible ? "Recovery reserve armed" : Telemetry.Warning
            };
        }

        protected override void OnModuleDisabled()
        {
            ReleaseControllers();
            if (Telemetry.Phase != AdvancedLandingPhase.Touchdown) Telemetry.Phase = AdvancedLandingPhase.Off;
        }

        public override void OnFixedUpdate()
        {
            if (!Active) return;
            if (Vessel.LandedOrSplashed)
            {
                SetPhase(AdvancedLandingPhase.Touchdown);
                Core.Thrust.ThrustOff();
                Vessel.ActionGroups.SetGroup(KSPActionGroup.Brakes, true);
                ReleaseControllers();
                return;
            }

            SynchronizeTarget(false);
            UpdateTelemetry(false);
            UpdatePhase();
            LogState();
        }

        public override void OnVesselWasModified(Vessel vessel)
        {
            if (!Active || vessel != Vessel) return;
            RestoreStabilizationHardware();
            ConfigureStabilizationHardware();
        }

        public override void Drive(FlightCtrlState s)
        {
            if (!Active) return;

            switch (Telemetry.Phase)
            {
                case AdvancedLandingPhase.Preflight:
                    Core.Thrust.RequestActiveThrottle(0);
                    HoldEntryAttitude();
                    break;

                case AdvancedLandingPhase.OrbitalCoast:
                    DriveOrbitalCoast();
                    break;

                case AdvancedLandingPhase.DeorbitBurn:
                    DriveDeorbitBurn();
                    break;

                case AdvancedLandingPhase.EntryCoast:
                    DriveEntryCoast();
                    break;

                case AdvancedLandingPhase.Boostback:
                    DriveBoostback();
                    break;

                case AdvancedLandingPhase.EntryBurn:
                    DriveEntryBurn();
                    break;

                case AdvancedLandingPhase.AerodynamicGuidance:
                    DriveAerodynamicGuidance();
                    break;

                case AdvancedLandingPhase.LandingBurn:
                    DriveLandingBurn(false);
                    break;

                case AdvancedLandingPhase.FinalDescent:
                    DriveLandingBurn(true);
                    break;
            }
        }

        private void UpdatePhase()
        {
            double altitude = Max(0, Min(VesselState.AltitudeBottom, VesselState.AltitudeTrue));
            double atmosphereTop = MainBody.RealMaxAtmosphereAltitude();
            double speed = VesselState.SurfaceVelocity.magnitude;

            if (_deorbitBurnCommitted)
            {
                SetPhase(AdvancedLandingPhase.DeorbitBurn);
                return;
            }

            if (NeedsDeorbitBurn())
            {
                if (Telemetry.OrbitalReachable && DeorbitWindowOpen())
                {
                    _deorbitBurnCommitted = true;
                    StopAutoWarp();
                    SetPhase(AdvancedLandingPhase.DeorbitBurn);
                }
                else
                {
                    SetPhase(AdvancedLandingPhase.OrbitalCoast);
                }

                return;
            }

            double projectedThrustAcceleration = Max(VesselState.LimitedMaxThrustAcceleration,
                Telemetry.Twr * VesselState.GravityForce.magnitude);
            double stoppingDistance = AdvancedLandingMath.StoppingDistance(Max(0, -VesselState.SpeedVertical),
                projectedThrustAcceleration, VesselState.GravityForce.magnitude, VesselState.MaxEngineResponseTime,
                SafetyFactor());
            bool landingBurnNow = altitude <= stoppingDistance || IsFinite(Telemetry.LandingBurnCountdown) &&
                                  Telemetry.LandingBurnCountdown <= LandingBurnLead;
            double availableLateralAcceleration = Max(0.5, projectedThrustAcceleration *
                Sin(Clamp(MaximumTargetingTilt, 0, 45) * PI / 180.0));
            double captureDeadband = AdvancedLandingMath.PrecisionAimDeadband(TargetRadius, false);
            double targetCaptureTime = AdvancedLandingMath.TargetCaptureTime(
                Max(0, Telemetry.TargetError - captureDeadband), VesselState.SpeedSurfaceHorizontal,
                availableLateralAcceleration) * SafetyFactor();
            bool poweredCaptureAllowed = AdvancedLandingMath.AtmosphericPoweredCaptureAllowed(
                AtmosphericCaptureOnly, MainBody.atmosphere, VesselState.AltitudeASL, atmosphereTop);
            bool targetCaptureNow = PoweredTargetCapture && Telemetry.PredictionReady &&
                                    poweredCaptureAllowed &&
                                    Telemetry.TargetError > captureDeadband &&
                                     Telemetry.TimeToImpact > 0 &&
                                     Telemetry.TimeToImpact <= targetCaptureTime + LandingBurnLead &&
                                     (IgnoreFuelLimits || Telemetry.FuelMarginDeltaV > 0);

            if (altitude < 150)
            {
                SetPhase(AdvancedLandingPhase.FinalDescent);
                return;
            }

            if (poweredCaptureAllowed && (landingBurnNow || targetCaptureNow))
            {
                SetPhase(AdvancedLandingPhase.LandingBurn);
                return;
            }

            if (MainBody.atmosphere && VesselState.AltitudeASL < atmosphereTop * 0.8 &&
                (speed > 800 || Telemetry.HeatRatio > MaxHeatRatio - 0.1 || Telemetry.GLoad > MaxGForce * 0.8))
            {
                SetPhase(AdvancedLandingPhase.EntryBurn);
                return;
            }

            if (MainBody.atmosphere && VesselState.AltitudeASL < atmosphereTop)
            {
                SetPhase(AdvancedLandingPhase.AerodynamicGuidance);
                return;
            }

            // In atmospheric-capture mode, establish the deliberately long ballistic arc
            // first. The 5 km overshoot is then removed by aerodynamic and powered guidance
            // after entry instead of turning the vehicle sideways in orbit.
            if (AtmosphericCaptureOnly && ShouldCoastToAtmosphere())
            {
                SetPhase(AdvancedLandingPhase.EntryCoast);
                return;
            }

            // Do not trap an out-of-fuel stage in Boostback with a permanent zero-throttle
            // command. If the atmospheric trajectory is already established, the next useful
            // action is to coast/warp to entry and use the remaining aerodynamic authority.
            if (poweredCaptureAllowed && AdvancedLandingMath.ShouldStartBoostback(
                    Telemetry.PredictionReady, Telemetry.TargetError, TargetRadius,
                    Telemetry.TimeToImpact, IgnoreFuelLimits, Telemetry.FuelMarginDeltaV))
            {
                SetPhase(AdvancedLandingPhase.Boostback);
                return;
            }

            if (ShouldCoastToAtmosphere())
            {
                SetPhase(AdvancedLandingPhase.EntryCoast);
                return;
            }

            SetPhase(AdvancedLandingPhase.Preflight);
        }

        private double DeorbitPeriapsisThreshold() => MainBody.atmosphere ? MainBody.RealMaxAtmosphereAltitude() : 0;

        private bool NeedsDeorbitBurn() =>
            !Telemetry.PredictionReady && Orbit.PeA >= DeorbitPeriapsisThreshold();

        private bool SafeForOrbitalWarp() =>
            AdvancedLandingMath.SafeForOrbitalWarp(
                Orbit.PeA, VesselState.AltitudeASL, DeorbitPeriapsisThreshold(), 1000);

        private bool ShouldCoastToAtmosphere() =>
            MainBody.atmosphere && VesselState.AltitudeASL > MainBody.RealMaxAtmosphereAltitude() &&
            Orbit.PeA < MainBody.RealMaxAtmosphereAltitude() &&
            IsFinite(Telemetry.AtmosphereEntryCountdown) &&
            Telemetry.AtmosphereEntryCountdown > Max(2, EntryWarpLead);

        private bool DeorbitWindowOpen()
        {
            if (!TryCalculateDeorbitSolution(out DeorbitSolution solution)) return false;
            bool geometryReady = AdvancedLandingMath.TargetedDeorbitWindowOpen(
                solution.TargetAngleToOrbitNormal, solution.TargetAheadAngle, solution.PlaneChangeAngle);
            return geometryReady && CanAffordTargetedDeorbit(solution.DeltaV.magnitude);
        }

        private bool CanAffordTargetedDeorbit(double deorbitDeltaV)
        {
            double required = AdvancedLandingMath.ProvisionalOrbitalLandingDeltaV(
                deorbitDeltaV, VesselState.GravityForce.magnitude,
                VesselState.MaxEngineResponseTime, SafetyFactor());
            return Telemetry.EngineRelightAvailable && Telemetry.Twr > 1 &&
                   (IgnoreFuelLimits || AdvancedLandingMath.CanAffordManeuver(
                       Telemetry.AvailableDeltaV, FuelReservePercent, required));
        }

        private bool TryCalculateDeorbitSolution(out DeorbitSolution solution)
        {
            solution = null;
            try
            {
                Vector3d horizontalDeltaV = OrbitalManeuverCalculator.DeltaVToChangePeriapsis(
                    Orbit, VesselState.Time, 0.9 * MainBody.Radius);
                Orbit deorbitTrajectory = Orbit.PerturbedOrbit(VesselState.Time, horizontalDeltaV);
                double impactUT = deorbitTrajectory.NextTimeOfRadius(VesselState.Time, MainBody.Radius);
                double freefallTime = impactUT - VesselState.Time;
                if (!IsFinite(freefallTime) || freefallTime <= 0) return false;

                double rotationDegrees = MainBody.rotationPeriod > 0
                    ? 360 * freefallTime / MainBody.rotationPeriod
                    : 0;
                Vector3d targetNow = MainBody.GetWorldSurfacePosition(
                    Core.Target.targetLatitude, Core.Target.targetLongitude, 0) - MainBody.position;
                Quaternion rotation = Quaternion.AngleAxis((float)rotationDegrees, MainBody.angularVelocity);
                Vector3d targetAtImpact = rotation * targetNow;
                double aimOvershoot = AtmosphericCaptureOnly && MainBody.atmosphere
                    ? Max(0, EntryOvershootDistance)
                    : 0;
                double aimAngle = AdvancedLandingMath.SurfaceOffsetAngleDegrees(
                    aimOvershoot, MainBody.Radius);
                if (aimAngle > 0)
                {
                    Vector3d orbitNormal = Orbit.OrbitNormal().normalized;
                    targetAtImpact = Quaternion.AngleAxis((float)aimAngle, orbitNormal) * targetAtImpact;
                }

                Vector3d aimNow = Quaternion.Inverse(rotation) * targetAtImpact;
                Vector3d horizontalToTarget = Vector3d.Exclude(
                    VesselState.Up, MainBody.position + targetAtImpact - VesselState.CoM).normalized;
                Vector3d horizontalVelocity = Vector3d.Exclude(VesselState.Up, VesselState.OrbitalVelocity);
                if (horizontalToTarget.sqrMagnitude < 1e-8 || horizontalVelocity.sqrMagnitude < 1e-8) return false;

                double finalHorizontalSpeed = (horizontalVelocity + horizontalDeltaV).magnitude;
                Vector3d desiredHorizontalVelocity = finalHorizontalSpeed * horizontalToTarget;
                Vector3d currentRadial = VesselState.CoM - MainBody.position;
                double targetToNormal = Vector3d.Angle(Orbit.OrbitNormal(), targetAtImpact);
                targetToNormal = Min(targetToNormal, 180 - targetToNormal);
                bool ballisticAtmosphericDeorbit = AdvancedLandingMath.UseBallisticAtmosphericDeorbit(
                    AtmosphericCaptureOnly, MainBody.atmosphere);

                solution = new DeorbitSolution
                {
                    // Atmospheric capture uses the aim point to choose when to deorbit,
                    // but keeps the burn purely retrograde/periapsis-lowering. The lateral
                    // curve back to the real target is intentionally deferred until entry.
                    DeltaV = ballisticAtmosphericDeorbit
                        ? horizontalDeltaV
                        : desiredHorizontalVelocity - horizontalVelocity,
                    BaseDeorbitDeltaV = horizontalDeltaV.magnitude,
                    FreefallTime = freefallTime,
                    TargetAheadAngle = Vector3d.Angle(currentRadial, targetAtImpact),
                    PlaneChangeAngle = Vector3d.Angle(horizontalVelocity, horizontalToTarget),
                    TargetAngleToOrbitNormal = targetToNormal,
                    AimOvershoot = aimOvershoot,
                    AimLatitude = MainBody.GetLatitude(MainBody.position + aimNow),
                    AimLongitude = MuUtils.ClampDegrees180(MainBody.GetLongitude(MainBody.position + aimNow))
                };
                return IsFinite(solution.DeltaV.magnitude);
            }
            catch (Exception exception)
            {
                if (DebugLogging) Print("[AdvancedLanding] deorbit solution unavailable: " + exception.Message);
                return false;
            }
        }

        private void DriveOrbitalCoast()
        {
            Core.Thrust.RequestActiveThrottle(0);
            Core.Attitude.attitudeTo(Vector3d.back, AttitudeReference.ORBIT, this);
            Core.Attitude.SetOmegaTarget(roll: 0);
            Telemetry.AttitudeError = Core.Attitude.attitudeAngleFromTarget();

            if (AutoWarp && Telemetry.OrbitalReachable && SafeForOrbitalWarp() &&
                Vessel.angularVelocity.magnitude < 0.01)
            {
                float requestedRate = (float)Max(1, Min(MaxAutoWarpRate, Orbit.period / 10));
                RequestAvailableWarpRate(requestedRate);
                _autoWarpActive = true;
            }
            else if (_autoWarpActive)
            {
                StopAutoWarp();
            }

            Telemetry.AutoWarpActive = _autoWarpActive;
            SetAirbrakes(false);
        }

        private void DriveEntryCoast()
        {
            Core.Thrust.RequestActiveThrottle(0);
            Vector3d retrograde = VesselState.SurfaceVelocity.sqrMagnitude > 1
                ? -VesselState.SurfaceVelocity.normalized
                : VesselState.Up;
            double attitudeError = CommandAttitude(retrograde, ForceEngineFirstAttitude ? 70 : 180);
            SetAirbrakes(false);

            if (AutoWarp && IsFinite(Telemetry.AtmosphereEntryCountdown) &&
                Telemetry.AtmosphereEntryCountdown > Max(2, EntryWarpLead) &&
                attitudeError < 10 && Vessel.angularVelocity.magnitude < 0.01)
            {
                double targetUT = VesselState.Time + Telemetry.AtmosphereEntryCountdown - Max(2, EntryWarpLead);
                if (RegularWarpAvailable())
                {
                    Core.Warp.WarpToUT(targetUT, Max(1, MaxAutoWarpRate));
                }
                else
                {
                    double remaining = Max(1, targetUT - VesselState.Time);
                    float requestedRate = (float)AdvancedLandingMath.EntryPhysicsWarpRate(
                        remaining, EntryWarpLead, MaxPhysicsWarpRate);
                    Core.Warp.WarpPhysicsAtRate(requestedRate);
                }
                _autoWarpActive = true;
            }
            else if (_autoWarpActive)
            {
                StopAutoWarp();
            }

            Telemetry.AutoWarpActive = _autoWarpActive;
        }

        private bool RegularWarpAvailable() =>
            VesselState.AltitudeASL >= TimeWarp.fetch.GetAltitudeLimit(1, MainBody);

        private void RequestAvailableWarpRate(float requestedRate)
        {
            if (RegularWarpAvailable())
                Core.Warp.WarpRegularAtRate((float)Min(requestedRate, Max(1, MaxAutoWarpRate)));
            else
                Core.Warp.WarpPhysicsAtRate((float)Min(
                    requestedRate, Max(1, MaxPhysicsWarpRate)));
        }

        private void DriveDeorbitBurn()
        {
            StopAutoWarp();
            SetAirbrakes(false);
            if (!TryCalculateDeorbitSolution(out DeorbitSolution solution))
            {
                Core.Thrust.RequestActiveThrottle(0);
                return;
            }

            Telemetry.DeorbitDeltaV = solution.DeltaV.magnitude;
            bool ballisticAtmosphericDeorbit = AdvancedLandingMath.UseBallisticAtmosphericDeorbit(
                AtmosphericCaptureOnly, MainBody.atmosphere);
            bool periapsisEstablished = ballisticAtmosphericDeorbit
                ? AdvancedLandingMath.AtmosphericDeorbitPeriapsisEstablished(Orbit.PeA, MainBody.Radius)
                : Orbit.PeA < -0.05 * MainBody.Radius;
            if (solution.DeltaV.magnitude < 2 || periapsisEstablished)
            {
                Core.Thrust.RequestActiveThrottle(0);
                _deorbitBurnCommitted = false;
                return;
            }

            PrepareLandingEngines();
            Core.Attitude.attitudeTo(solution.DeltaV.normalized, AttitudeReference.INERTIAL, this);
            Core.Attitude.SetOmegaTarget(roll: 0);
            Telemetry.AttitudeError = Core.Attitude.attitudeAngleFromTarget();
            Core.Thrust.RequestActiveThrottle(Telemetry.AttitudeError < 5 ? 1 : 0, allowZero: true);
        }

        private void StopAutoWarp()
        {
            // MinimumWarp works for both HIGH (on-rails) and LOW (physics) warp.
            // PhysicsRunning() is true during physics warp, so guarding this call with
            // !PhysicsRunning() would leave a descending vessel at 2x-4x after guidance
            // asks auto-warp to stop.
            if (_autoWarpActive) Core.Warp.MinimumWarp();
            _autoWarpActive = false;
            Telemetry.AutoWarpActive = false;
        }

        private double AtmosphereEntryCountdown()
        {
            if (!MainBody.atmosphere) return double.NaN;
            double atmosphereTop = MainBody.RealMaxAtmosphereAltitude();
            if (VesselState.AltitudeASL <= atmosphereTop) return 0;
            if (Orbit.PeA >= atmosphereTop) return double.NaN;

            try
            {
                double entryUT = Orbit.NextTimeOfRadius(
                    VesselState.Time, MainBody.Radius + atmosphereTop);
                double countdown = entryUT - VesselState.Time;
                return IsFinite(countdown) && countdown >= 0 ? countdown : double.NaN;
            }
            catch (Exception)
            {
                return double.NaN;
            }
        }

        private void DriveBoostback()
        {
            Vector3d target = TargetRelativePosition();
            Vector3d impact = Telemetry.PredictedImpact;
            Vector3d lateral = Vector3d.Exclude(VesselState.Up, target - impact);
            Vector3d retrograde = -VesselState.SurfaceVelocity.normalized;
            Vector3d direction;
            double attitudeError;
            double aimDeadband = AdvancedLandingMath.PrecisionAimDeadband(TargetRadius, false);
            if (ForceEngineFirstAttitude)
            {
                // A full retrograde flip while the booster is still ascending leaves short,
                // low-authority stages falling nose-first at apoapsis. In strict engine-first
                // mode, perform the return correction by tilting toward the target while the
                // engine end remains below the center of mass.
                double tilt = Clamp((Telemetry.TargetError - aimDeadband) / 500, 0, 65) * PI / 180.0;
                Vector3d horizontal = lateral.sqrMagnitude > 1e-8 ? lateral.normalized : VesselState.North;
                direction = (Cos(tilt) * VesselState.Up + Sin(tilt) * horizontal).normalized;
                attitudeError = CommandAttitude(direction, 65);
            }
            else
            {
                direction = (retrograde + 0.75 * lateral.normalized).normalized;
                attitudeError = CommandAttitude(direction);
            }

            double throttle = Clamp((Telemetry.TargetError - aimDeadband) / 25000, 0.05, 0.75);
            if ((!IgnoreFuelLimits && Telemetry.FuelMarginDeltaV <= 0) ||
                Telemetry.TargetError <= aimDeadband) throttle = 0;
            if (attitudeError > 20) throttle = 0;
            Core.Thrust.RequestActiveThrottle((float)throttle, allowZero: true);
            SetAirbrakes(false);
        }

        private void DriveEntryBurn()
        {
            double maxTilt = VesselState.AltitudeASL < 15000 ? 45 : 80;
            double attitudeError = CommandAttitude(-VesselState.SurfaceVelocity.normalized, maxTilt);

            double heatDemand = Clamp01((Telemetry.HeatRatio - (MaxHeatRatio - 0.15)) / 0.15);
            double gDemand = MaxGForce > 0 ? Clamp01((Telemetry.GLoad - MaxGForce * 0.75) / (MaxGForce * 0.25)) : 0;
            double speedDemand = Clamp01((VesselState.SurfaceVelocity.magnitude - 700) / 800);
            double throttle = Max(0.15, Max(speedDemand, Max(heatDemand, gDemand)));
            if (attitudeError > 25) throttle = Min(throttle, 0.15);
            Core.Thrust.RequestActiveThrottle((float)throttle, allowZero: true);
            SetAirbrakes(true);
        }

        private void DriveAerodynamicGuidance()
        {
            Core.Thrust.RequestActiveThrottle(0);

            Vector3d retrograde = VesselState.SurfaceVelocity.sqrMagnitude > 1
                ? -VesselState.SurfaceVelocity.normalized
                : VesselState.Up;
            Vector3d corrected = Telemetry.CorrectedDirection;
            if (corrected.sqrMagnitude < 0.01)
            {
                Vector3d targetDelta = TargetRelativePosition() - Telemetry.PredictedImpact;
                Vector3d lateral = Vector3d.Exclude(VesselState.Up, targetDelta).normalized;
                double aimDeadband = AdvancedLandingMath.PrecisionAimDeadband(TargetRadius, false);
                double correctionAngle = Clamp((Telemetry.TargetError - aimDeadband) / 750, 0, 25) * PI / 180.0;
                corrected = (Cos(correctionAngle) * retrograde + Sin(correctionAngle) * lateral).normalized;
            }

            // Trajectories' marker follows its configured AoA and may be prograde. A powered
            // booster must keep its thrust axis on the retrograde hemisphere instead.
            if (ForceEngineFirstAttitude && Vector3d.Dot(corrected, retrograde) < 0) corrected = -corrected;
            double correctionWeight = Clamp(
                (Telemetry.TargetError - AdvancedLandingMath.PrecisionAimDeadband(TargetRadius, false)) /
                Max(TargetRadius * 12, 600), 0.05, 0.80);
            Vector3d attitude = ForceEngineFirstAttitude
                ? ((1 - correctionWeight) * retrograde + correctionWeight * corrected).normalized
                : corrected.normalized;
            bool highAerodynamicLoad = VesselState.DynamicPressure > 10000;
            double maxTilt = VesselState.AltitudeASL < 10000
                ? FastHorizontalTransfer && !highAerodynamicLoad ? 40 : 25
                : FastHorizontalTransfer ? 70 : 60;
            CommandAttitude(attitude, ForceEngineFirstAttitude ? maxTilt : 180);

            if (UseRCS && Telemetry.PredictionReady)
            {
                Vector3d predictedImpactError = Vector3d.Exclude(
                    VesselState.Up, TargetRelativePosition() - Telemetry.PredictedImpact);
                double correctionTime = IsFinite(Telemetry.TimeToImpact) && Telemetry.TimeToImpact > 0
                    ? Max(5, Telemetry.TimeToImpact)
                    : 10;
                Vector3d correctionDeltaV = 1.25 * predictedImpactError / correctionTime;
                const double maximumRcsCorrection = 8;
                if (correctionDeltaV.magnitude > maximumRcsCorrection)
                    correctionDeltaV = correctionDeltaV.normalized * maximumRcsCorrection;
                Core.RCS.Users.Add(this);
                Core.RCS.SetWorldVelocityError(correctionDeltaV);
                Telemetry.DesiredHorizontalSpeed = correctionDeltaV.magnitude;
            }

            bool deploy = AirbrakeMode == AdvancedLandingAirbrakeMode.Deployed ||
                          AirbrakeMode == AdvancedLandingAirbrakeMode.Automatic &&
                          (Telemetry.TargetError > TargetRadius || VesselState.DynamicPressure > 3000);
            SetAirbrakes(deploy);
        }

        private void DriveLandingBurn(bool final)
        {
            double altitude = Max(0.5, Min(VesselState.AltitudeBottom, VesselState.AltitudeTrue));
            Vector3d targetDelta = TargetRelativePosition() - (VesselState.CoM - MainBody.position);
            Vector3d horizontalError = Vector3d.Exclude(VesselState.Up, targetDelta);
            Vector3d horizontalVelocity = Vector3d.Exclude(VesselState.Up, VesselState.SurfaceVelocity);
            double fallbackTimeToGo = altitude / Max(5, -VesselState.SpeedVertical);
            double timeToGo = IsFinite(Telemetry.TimeToImpact) && Telemetry.TimeToImpact > 0
                ? Clamp(Telemetry.TimeToImpact, 1, 30)
                : Clamp(fallbackTimeToGo, 1, 30);
            Vector3d effectiveHorizontalError = horizontalError;
            if (Telemetry.PredictionReady && Telemetry.PredictedImpact.sqrMagnitude > 1)
            {
                Vector3d predictedImpactError = Vector3d.Exclude(
                    VesselState.Up, TargetRelativePosition() - Telemetry.PredictedImpact);
                double predictionWeight = final
                    ? Clamp(altitude / 300, 0.05, 0.50)
                    : 0.80;
                effectiveHorizontalError += predictionWeight * predictedImpactError;
            }

            double effectiveRange = effectiveHorizontalError.magnitude;
            double aimDeadband = AdvancedLandingMath.PrecisionAimDeadband(TargetRadius, final);
            double configuredTilt = FastHorizontalTransfer && !final
                ? Max(MaximumTargetingTilt, 45)
                : (double)MaximumTargetingTilt;
            double tiltLimit = Clamp(configuredTilt, 5, final ? 12 : 55);
            double thrustLimitedLateral = VesselState.LimitedMaxThrustAcceleration * Sin(tiltLimit * PI / 180.0);
            double maxLateral = Min(final ? 4.0 : 15.0, Max(0.5, thrustLimitedLateral));
            double maximumHorizontalSpeed = final
                ? Min(5, Max(0.5, altitude / 15))
                : FastHorizontalTransfer
                    ? Min(Max(20, MaximumHorizontalTransferSpeed), Max(15, altitude / 12))
                    : Min(45, Max(8, altitude / 25));
            double desiredHorizontalSpeed = FastHorizontalTransfer && !final
                ? AdvancedLandingMath.BrakingLimitedHorizontalSpeed(
                    effectiveRange, aimDeadband, timeToGo, maximumHorizontalSpeed,
                    maxLateral, HorizontalTransferGain)
                : AdvancedLandingMath.DesiredHorizontalSpeed(
                    effectiveRange, aimDeadband, timeToGo, maximumHorizontalSpeed);
            Vector3d desiredHorizontalVelocity = effectiveRange > aimDeadband
                ? effectiveHorizontalError.normalized * desiredHorizontalSpeed
                : Vector3d.zero;
            Vector3d horizontalVelocityError = desiredHorizontalVelocity - horizontalVelocity;
            double responseTime = final ? 0.55 : 1.2;
            Vector3d desiredLateralAcceleration = horizontalVelocityError / responseTime;
            if (desiredLateralAcceleration.magnitude > maxLateral)
                desiredLateralAcceleration = desiredLateralAcceleration.normalized * maxLateral;

            Telemetry.DesiredHorizontalSpeed = desiredHorizontalSpeed;
            Telemetry.CommandedLateralAcceleration = desiredLateralAcceleration.magnitude;

            Vector3d desiredThrust = (VesselState.Up +
                                      desiredLateralAcceleration / Max(VesselState.GravityForce.magnitude, 2)).normalized;
            double attitudeError = CommandAttitude(desiredThrust, tiltLimit);

            double speedLimit = final ? Max(2, FinalDescentSpeedLimit) : Max(25, FinalDescentSpeedLimit);
            double commandedAcceleration = AdvancedLandingMath.VerticalAccelerationCommand(
                altitude, VesselState.SpeedVertical, TouchdownSpeed,
                VesselState.GravityForce.magnitude, VesselState.MaxEngineResponseTime, speedLimit);
            double minAcceleration = VesselState.MinThrustAcceleration;
            double maxAcceleration = VesselState.LimitedMaxThrustAcceleration;
            commandedAcceleration /= Max(Vector3d.Dot(desiredThrust, VesselState.Up), 0.5);
            commandedAcceleration = AdvancedLandingMath.LimitEarlyAscentAcceleration(
                commandedAcceleration, altitude, VesselState.SpeedVertical,
                HoverCaptureAltitude, TouchdownSpeed, VesselState.GravityForce.magnitude);
            if (attitudeError > 30)
                commandedAcceleration = Min(commandedAcceleration, maxAcceleration * (final ? 0.35 : 0.20));

            _landingPwm.MinOnTime = Clamp(ThrottlePulseWidth, 0.04, 1.0);
            _landingPwm.MinOffTime = TimeWarp.fixedDeltaTime;
            float throttle = _landingPwm.ThrottleCommand(
                commandedAcceleration, minAcceleration, maxAcceleration, TimeWarp.fixedDeltaTime);
            Telemetry.CommandedVerticalAcceleration = commandedAcceleration;
            Telemetry.CommandedThrottle = throttle;
            Core.Thrust.RequestActiveThrottle(throttle, allowZero: true);

            if (UseRCS)
            {
                Core.RCS.Users.Add(this);
                // SetWorldVelocityError expects the desired delta-v. The previous sign was
                // reversed and could command translation away from the landing target.
                Core.RCS.SetWorldVelocityError(desiredHorizontalVelocity - horizontalVelocity);
            }

            if (DeployLandingGear && altitude < 1000) Vessel.ActionGroups.SetGroup(KSPActionGroup.Gear, true);
            SetAirbrakes(final || AirbrakeMode == AdvancedLandingAirbrakeMode.Deployed);

            if (AllowLandingStaging && VesselState.ThrustAvailable <= 0 && Vessel.currentStage > 0 &&
                VesselState.Time - _lastStage > 2)
            {
                Core.Staging.ImmediateStage();
                _lastStage = VesselState.Time;
            }
        }

        private void HoldEntryAttitude()
        {
            Vector3d direction = ForceEngineFirstAttitude
                ? VesselState.Up
                : VesselState.SurfaceVelocity.sqrMagnitude > 1
                    ? -VesselState.SurfaceVelocity.normalized
                    : VesselState.Up;
            double maxTilt = MainBody.atmosphere && VesselState.SpeedVertical < 0 &&
                             VesselState.AltitudeASL < MainBody.RealMaxAtmosphereAltitude()
                ? 70
                : 180;
            CommandAttitude(direction, ForceEngineFirstAttitude ? 10 : maxTilt);
            SetAirbrakes(false);
        }

        private double CommandAttitude(Vector3d direction, double maxTiltFromUp = 180)
        {
            if (!IsFinite(direction.x) || !IsFinite(direction.y) || !IsFinite(direction.z) || direction.sqrMagnitude < 1e-8)
                direction = VesselState.Up;

            direction.Normalize();
            // Once the vehicle is near or past apoapsis, never accept a command on the
            // downward hemisphere in engine-first mode. This is the final guard against
            // falling nose-first even if an external predictor returns a reversed vector.
            if (ForceEngineFirstAttitude && VesselState.SpeedVertical < 5)
                maxTiltFromUp = Min(maxTiltFromUp, 70);
            if (maxTiltFromUp < 179.9)
            {
                Vector3d up = VesselState.Up.normalized;
                double angle = Vector3d.Angle(up, direction);
                if (angle > maxTiltFromUp)
                {
                    Vector3d lateral = Vector3d.Exclude(up, direction);
                    if (lateral.sqrMagnitude < 1e-8) lateral = VesselState.North;
                    lateral.Normalize();
                    double radians = maxTiltFromUp * PI / 180.0;
                    direction = (Cos(radians) * up + Sin(radians) * lateral).normalized;
                }
            }

            // Roll remains under the attitude controller. Optional spin stabilization replaces
            // the zero roll-rate target only during non-powered coast/aerodynamic phases.
            Core.Attitude.attitudeTo(direction, AttitudeReference.INERTIAL_COT, this, true);
            Vector3d controlledAxis = VesselState.ThrustForward.sqrMagnitude > 1e-8
                ? VesselState.ThrustForward
                : VesselState.Forward;
            Telemetry.AttitudeError = Vector3d.Angle(controlledAxis, direction);
            double requestedRpm = Clamp(SpinRateRpm, -10, 10);
            bool spin = UseSpinStabilization && SpinAllowedInCurrentPhase() && Telemetry.AttitudeError < 15 &&
                        VesselState.DynamicPressure <= Max(0, MaxSpinDynamicPressure);
            Core.Attitude.SetOmegaTarget(roll: spin ? requestedRpm * 2.0 * PI / 60.0 : 0);
            Telemetry.SpinRateRpm = Vessel.angularVelocityD.y * 60.0 / (2.0 * PI);
            return Telemetry.AttitudeError;
        }

        private bool SpinAllowedInCurrentPhase() =>
            Telemetry.Phase == AdvancedLandingPhase.Preflight ||
            Telemetry.Phase == AdvancedLandingPhase.AerodynamicGuidance;

        private void SetAirbrakes(bool deploy)
        {
            if (!UseAirbrakes || AirbrakeMode == AdvancedLandingAirbrakeMode.Retracted) deploy = false;
            Vessel.ActionGroups.SetGroup(KSPActionGroup.Brakes, deploy);
        }

        private void SynchronizeTarget(bool force)
        {
            if (!UseTrajectories || !_trajectories.Available || !Core.Target.PositionTargetExists) return;
            if (!force && VesselState.Time - _lastTargetSync < 2) return;

            double altitude = MainBody.TerrainAltitude(Core.Target.targetLatitude, Core.Target.targetLongitude);
            _trajectories.SetTarget(Core.Target.targetLatitude, Core.Target.targetLongitude, altitude);
            _lastTargetSync = VesselState.Time;
        }

        private void UpdateTelemetry(bool force)
        {
            if (!force && VesselState.Time - _lastTelemetryUpdate < 0.2) return;
            _lastTelemetryUpdate = VesselState.Time;
            if (Telemetry.Phase != AdvancedLandingPhase.LandingBurn &&
                Telemetry.Phase != AdvancedLandingPhase.FinalDescent)
            {
                Telemetry.CommandedVerticalAcceleration = 0;
                Telemetry.CommandedThrottle = 0;
            }

            Vector3d impact = Vector3d.zero;
            Vector3d corrected = Vector3d.zero;
            double timeToImpact = double.NaN;
            bool trajectoriesReady = UseTrajectories && _trajectories.TryGetPrediction(out impact, out timeToImpact, out corrected);

            if (trajectoriesReady)
            {
                Telemetry.Predictor = "Trajectories " + _trajectories.Version +
                                      (ReflectionUtils.IsLoadedFAR ? " / FAR" : " / stock aero");
                Telemetry.PredictedImpact = impact;
                Telemetry.CorrectedDirection = corrected;
                Telemetry.TimeToImpact = timeToImpact;
                Telemetry.PredictionReady = true;
                Telemetry.PredictedLatitude = MainBody.GetLatitude(MainBody.position + impact);
                Telemetry.PredictedLongitude = MainBody.GetLongitude(MainBody.position + impact);
            }
            else
            {
                ReentrySimulation.Result result = _nativePredictor.Result;
                Telemetry.Predictor = ReflectionUtils.IsLoadedFAR ? "MechJeb stock fallback (FAR advisory)" : "MechJeb native";
                Telemetry.PredictionReady = result != null && result.Outcome == ReentrySimulation.Outcome.LANDED;
                if (Telemetry.PredictionReady)
                {
                    Telemetry.PredictedImpact = result.WorldEndPosition() - MainBody.position;
                    Telemetry.TimeToImpact = result.EndUT - VesselState.Time;
                    Telemetry.PredictedLatitude = result.EndPosition.Latitude;
                    Telemetry.PredictedLongitude = result.EndPosition.Longitude;
                }
            }

            if (Telemetry.PredictionReady)
                Telemetry.TargetError = SurfaceDistance(Telemetry.PredictedImpact, TargetRelativePosition());

            Core.StageStats.RequestUpdate();
            Telemetry.AvailableDeltaV = AvailableLandingDeltaV();
            Telemetry.Twr = ProjectedLandingTwr();
            Telemetry.EngineRelightAvailable = EngineRelightAvailable();
            Telemetry.HeatRatio = MaximumHeatRatio();
            Telemetry.GLoad = Vessel.geeForce_immediate;
            Telemetry.DynamicPressure = VesselState.DynamicPressure;
            Telemetry.WarpRate = TimeWarp.CurrentRate;
            Telemetry.WarpMode = TimeWarp.CurrentRate <= 1
                ? "1x"
                : TimeWarp.WarpMode == TimeWarp.Modes.HIGH
                    ? "on-rails"
                    : "physics";
            Telemetry.CurrentTargetRange = SurfaceDistance(VesselState.CoM - MainBody.position, TargetRelativePosition());
            Telemetry.HorizontalSpeed = VesselState.SpeedSurfaceHorizontal;
            if (Core.Attitude.Enabled) Telemetry.AttitudeError = Core.Attitude.attitudeAngleFromTarget();

            bool orbitalApproach = !Telemetry.PredictionReady &&
                                   Orbit.PeA >= DeorbitPeriapsisThreshold() || _deorbitBurnCommitted;
            DeorbitSolution deorbitSolution = null;
            bool deorbitSolutionReady = orbitalApproach && TryCalculateDeorbitSolution(out deorbitSolution);
            Telemetry.OrbitalReachable = false;
            if (deorbitSolutionReady)
            {
                Telemetry.DeorbitDeltaV = _deorbitBurnCommitted
                    ? deorbitSolution.DeltaV.magnitude
                    : deorbitSolution.BaseDeorbitDeltaV;
                Telemetry.DeorbitAimOvershoot = deorbitSolution.AimOvershoot;
                Telemetry.DeorbitAimLatitude = deorbitSolution.AimLatitude;
                Telemetry.DeorbitAimLongitude = deorbitSolution.AimLongitude;
                Telemetry.RequiredDeltaV = AdvancedLandingMath.ProvisionalOrbitalLandingDeltaV(
                    Telemetry.DeorbitDeltaV, VesselState.GravityForce.magnitude,
                    VesselState.MaxEngineResponseTime, SafetyFactor());
            }
            else
            {
                if (!orbitalApproach) Telemetry.DeorbitDeltaV = 0;
                double vertical = Max(0, -VesselState.SpeedVertical);
                double horizontal = VesselState.SpeedSurfaceHorizontal;
                double projectedThrustAcceleration = Max(VesselState.LimitedMaxThrustAcceleration,
                    Telemetry.Twr * VesselState.GravityForce.magnitude);
                Telemetry.RequiredDeltaV = AdvancedLandingMath.RequiredLandingDeltaV(vertical, horizontal,
                    VesselState.GravityForce.magnitude, projectedThrustAcceleration, VesselState.MaxEngineResponseTime);
                if (BoosterRecoveryMode && IsFinite(Telemetry.TargetError) && Telemetry.TimeToImpact > 0)
                    Telemetry.RequiredDeltaV += Min(1200, Telemetry.TargetError / Max(Telemetry.TimeToImpact, 1));
            }

            double reserve = Telemetry.AvailableDeltaV * Clamp(FuelReservePercent / 100.0, 0, 0.9);
            Telemetry.FuelMarginDeltaV = Telemetry.AvailableDeltaV - reserve - Telemetry.RequiredDeltaV;
            Telemetry.OrbitalReachable = deorbitSolutionReady && Telemetry.EngineRelightAvailable &&
                                         Telemetry.Twr > 1 &&
                                         (IgnoreFuelLimits || Telemetry.FuelMarginDeltaV >= 0);

            if (IsFinite(Core.Hoverslam.IgnitionCountdown))
                Telemetry.LandingBurnCountdown = Core.Hoverslam.IgnitionCountdown;
            else
                Telemetry.LandingBurnCountdown = EstimateLandingBurnCountdown();

            Telemetry.EntryBurnCountdown = EstimateEntryBurnCountdown();
            Telemetry.AtmosphereEntryCountdown = AtmosphereEntryCountdown();
            Telemetry.AtmosphereEntryRealSeconds = AdvancedLandingMath.RealTimeCountdown(
                Telemetry.AtmosphereEntryCountdown, TimeWarp.CurrentRate);
            double probabilityDeltaV = IgnoreFuelLimits
                ? Max(Telemetry.AvailableDeltaV - reserve, Telemetry.RequiredDeltaV * 1.5)
                : Telemetry.AvailableDeltaV - reserve;
            Telemetry.Probability = AdvancedLandingMath.LandingProbability(probabilityDeltaV,
                Telemetry.RequiredDeltaV, Telemetry.Twr,
                orbitalApproach ? 0 : Telemetry.TargetError, TargetRadius, Telemetry.HeatRatio,
                Telemetry.GLoad, MaxGForce, Telemetry.EngineRelightAvailable,
                Telemetry.PredictionReady || deorbitSolutionReady);
            bool commonSafety = Telemetry.EngineRelightAvailable && Telemetry.Twr > 1 &&
                                (IgnoreFuelLimits || Telemetry.FuelMarginDeltaV >= 0) &&
                                Telemetry.HeatRatio < MaxHeatRatio &&
                                (MaxGForce <= 0 || Telemetry.GLoad < MaxGForce);
            Telemetry.Feasible = commonSafety && (Telemetry.PredictionReady || Telemetry.OrbitalReachable);
            Telemetry.Warning = BuildWarning();
            Telemetry.LastUpdateUT = VesselState.Time;
        }

        private double AvailableLandingDeltaV()
        {
            if (Core.StageStats.AtmoStats.Count == 0) return 0;

            double deltaV = 0;
            int selectedStage = -1;
            for (int i = Core.StageStats.AtmoStats.Count - 1; i >= 0; i--)
            {
                FuelStats stats = Core.StageStats.AtmoStats[i];
                if (stats.DeltaV <= 0) continue;
                if (selectedStage < 0) selectedStage = stats.KSPStage;
                if (EngineMode != AdvancedLandingEngineMode.AllActive && stats.KSPStage != selectedStage) break;
                deltaV += stats.DeltaV;
            }

            return deltaV;
        }

        private double ProjectedLandingTwr()
        {
            double liveTwr = VesselState.LimitedMaxThrustAcceleration /
                             Max(VesselState.GravityForce.magnitude, 0.01);
            if (Core.StageStats.AtmoStats.Count == 0) return liveTwr;

            int selectedStage = -1;
            double projectedTwr = liveTwr;
            for (int i = Core.StageStats.AtmoStats.Count - 1; i >= 0; i--)
            {
                FuelStats stats = Core.StageStats.AtmoStats[i];
                if (stats.DeltaV <= 0) continue;
                if (selectedStage < 0) selectedStage = stats.KSPStage;
                if (EngineMode != AdvancedLandingEngineMode.AllActive && stats.KSPStage != selectedStage) break;
                projectedTwr = Max(projectedTwr, stats.MaxTWR(MainBody.GeeASL));
            }

            return projectedTwr;
        }

        private bool EngineRelightAvailable()
        {
            bool foundEngine = false;
            for (int i = 0; i < Vessel.parts.Count; i++)
            {
                Part part = Vessel.parts[i];
                for (int j = 0; j < part.Modules.Count; j++)
                {
                    ModuleEngines engine = part.Modules[j] as ModuleEngines;
                    if (engine == null || !engine.isEnabled) continue;
                    foundEngine = true;
                    if (engine.EngineIgnited || !ReflectionUtils.IsLoadedRealFuels) return true;

                    FieldInfo ignitions = engine.GetType().GetField("ignitions");
                    if (ignitions == null) return true;
                    int remaining = (int)ignitions.GetValue(engine);
                    if (remaining != 0) return true;
                }
            }

            return !foundEngine ? false : !ReflectionUtils.IsLoadedRealFuels;
        }

        private double MaximumHeatRatio()
        {
            double ratio = 0;
            for (int i = 0; i < Vessel.parts.Count; i++)
            {
                Part part = Vessel.parts[i];
                if (part.maxTemp > 0) ratio = Max(ratio, part.temperature / part.maxTemp);
                if (part.skinMaxTemp > 0) ratio = Max(ratio, part.skinTemperature / part.skinMaxTemp);
            }

            return ratio;
        }

        private double EstimateLandingBurnCountdown()
        {
            double altitude = Max(0, Min(VesselState.AltitudeBottom, VesselState.AltitudeTrue));
            double verticalSpeed = Max(1, -VesselState.SpeedVertical);
            double projectedThrustAcceleration = Max(VesselState.LimitedMaxThrustAcceleration,
                Telemetry.Twr * VesselState.GravityForce.magnitude);
            double stoppingDistance = AdvancedLandingMath.StoppingDistance(verticalSpeed, projectedThrustAcceleration,
                VesselState.GravityForce.magnitude, VesselState.MaxEngineResponseTime, SafetyFactor());
            if (!IsFinite(stoppingDistance)) return double.NaN;
            return (altitude - stoppingDistance) / verticalSpeed;
        }

        private double EstimateEntryBurnCountdown()
        {
            if (!MainBody.atmosphere || VesselState.SpeedVertical >= 0) return double.NaN;
            double triggerAltitude = MainBody.RealMaxAtmosphereAltitude() * 0.8;
            return Max(0, (VesselState.AltitudeASL - triggerAltitude) / -VesselState.SpeedVertical);
        }

        private double SafetyFactor()
        {
            switch (SafetyMode)
            {
                case AdvancedLandingSafetyMode.Precision: return 1.05;
                case AdvancedLandingSafetyMode.MaximumSafety: return 1.35;
                default: return 1.18;
            }
        }

        private string BuildWarning()
        {
            bool orbitalApproach = !Telemetry.PredictionReady &&
                                   (Orbit.PeA >= DeorbitPeriapsisThreshold() || _deorbitBurnCommitted ||
                                    Telemetry.Phase == AdvancedLandingPhase.OrbitalCoast ||
                                    Telemetry.Phase == AdvancedLandingPhase.DeorbitBurn);
            if (orbitalApproach)
            {
                if (Telemetry.DeorbitDeltaV <= 0) return "Calculating targeted deorbit window";
                if (!Telemetry.EngineRelightAvailable) return "No engine relight available for deorbit";
                if (Telemetry.Twr <= 1) return "Landing TWR is below 1";
                if (!IgnoreFuelLimits && Telemetry.FuelMarginDeltaV < 0)
                    return "Insufficient delta-v for deorbit and landing reserve";
                return Telemetry.Phase == AdvancedLandingPhase.DeorbitBurn
                    ? "Executing targeted deorbit burn"
                    : "Orbit reachable; waiting for targeted deorbit window";
            }

            if (!Telemetry.PredictionReady)
                return _trajectories.Available && !string.IsNullOrEmpty(_trajectories.LastError)
                    ? "Waiting for trajectory: " + _trajectories.LastError
                    : "Waiting for landing prediction";
            if (!Telemetry.EngineRelightAvailable) return "No engine relight available";
            if (Telemetry.Twr <= 1) return "Landing TWR is below 1";
            if (!IgnoreFuelLimits && Telemetry.FuelMarginDeltaV < 0) return "Insufficient landing delta-v";
            if (Telemetry.HeatRatio >= MaxHeatRatio) return "Heat limit exceeded";
            if (MaxGForce > 0 && Telemetry.GLoad >= MaxGForce) return "G-force limit exceeded";
            if (Active && UseUpperRcsStabilization && Telemetry.UpperRcsModules == 0)
                return "No RCS stabilizer found above the center of mass";
            if (Telemetry.TargetError > TargetRadius) return "Predicted impact outside precision radius";
            return "";
        }

        private void ConfigureStabilizationHardware()
        {
            Telemetry.UpperRcsModules = 0;
            Telemetry.RollSuppressedSurfaces = 0;

            for (int i = 0; i < Vessel.parts.Count; i++)
            {
                Part part = Vessel.parts[i];
                double axialPosition = Vector3d.Dot(part.transform.position - VesselState.CoM, VesselState.Forward);
                for (int j = 0; j < part.Modules.Count; j++)
                {
                    if (part.Modules[j] is ModuleRCS rcs)
                    {
                        if (!_rcsAxisStates.ContainsKey(rcs))
                        {
                            _rcsAxisStates.Add(rcs, new RcsAxisState
                            {
                                Pitch = rcs.enablePitch,
                                Yaw = rcs.enableYaw,
                                Roll = rcs.enableRoll
                            });
                        }

                        if (UseUpperRcsStabilization && axialPosition > 0.25)
                        {
                            rcs.enablePitch = true;
                            rcs.enableYaw = true;
                            rcs.enableRoll = true;
                            Telemetry.UpperRcsModules++;
                        }
                    }
                    else if (part.Modules[j] is ModuleControlSurface surface)
                    {
                        if (!_surfaceRollStates.ContainsKey(surface)) _surfaceRollStates.Add(surface, surface.ignoreRoll);
                        if (SuppressAerodynamicRoll && !surface.ignoreRoll)
                        {
                            surface.ignoreRoll = true;
                            Telemetry.RollSuppressedSurfaces++;
                        }
                    }
                }
            }
        }

        private void RestoreStabilizationHardware()
        {
            foreach (KeyValuePair<ModuleRCS, RcsAxisState> pair in _rcsAxisStates)
            {
                if (pair.Key == null) continue;
                pair.Key.enablePitch = pair.Value.Pitch;
                pair.Key.enableYaw = pair.Value.Yaw;
                pair.Key.enableRoll = pair.Value.Roll;
            }

            foreach (KeyValuePair<ModuleControlSurface, bool> pair in _surfaceRollStates)
            {
                if (pair.Key != null) pair.Key.ignoreRoll = pair.Value;
            }

            _rcsAxisStates.Clear();
            _surfaceRollStates.Clear();
        }

        private Vector3d TargetRelativePosition()
        {
            double altitude = MainBody.TerrainAltitude(Core.Target.targetLatitude, Core.Target.targetLongitude);
            return MainBody.GetWorldSurfacePosition(Core.Target.targetLatitude, Core.Target.targetLongitude, altitude) - MainBody.position;
        }

        private static double SurfaceDistance(Vector3d a, Vector3d b)
        {
            if (a.sqrMagnitude <= 0 || b.sqrMagnitude <= 0) return double.NaN;
            double angle = SafeAcos(Clamp(Vector3d.Dot(a.normalized, b.normalized), -1, 1));
            return angle * (a.magnitude + b.magnitude) * 0.5;
        }

        private void SetPhase(AdvancedLandingPhase phase)
        {
            if (Telemetry.Phase == phase) return;
            _previousPhase = Telemetry.Phase;
            Telemetry.Phase = phase;
            if ((_previousPhase == AdvancedLandingPhase.OrbitalCoast ||
                 _previousPhase == AdvancedLandingPhase.EntryCoast) &&
                phase != AdvancedLandingPhase.OrbitalCoast &&
                phase != AdvancedLandingPhase.EntryCoast)
                StopAutoWarp();
            if (phase == AdvancedLandingPhase.LandingBurn &&
                _previousPhase != AdvancedLandingPhase.FinalDescent)
                _landingPwm.Reset();
            if (phase == AdvancedLandingPhase.DeorbitBurn || phase == AdvancedLandingPhase.Boostback || phase == AdvancedLandingPhase.EntryBurn ||
                phase == AdvancedLandingPhase.LandingBurn || phase == AdvancedLandingPhase.FinalDescent)
            {
                StopAutoWarp();
                Core.Attitude.SetOmegaTarget(roll: 0);
                PrepareLandingEngines();
            }
            if (DebugLogging) Print("[AdvancedLanding] " + _previousPhase + " -> " + phase);
        }

        private void PrepareLandingEngines()
        {
            bool activeEngineFound = false;
            for (int i = 0; i < Vessel.parts.Count; i++)
            {
                for (int j = 0; j < Vessel.parts[i].Modules.Count; j++)
                {
                    ModuleEngines engine = Vessel.parts[i].Modules[j] as ModuleEngines;
                    if (engine != null && engine.isEnabled && engine.EngineIgnited) activeEngineFound = true;
                }
            }

            if (EngineMode == AdvancedLandingEngineMode.Automatic && activeEngineFound) return;

            for (int i = 0; i < Vessel.parts.Count; i++)
            {
                Part part = Vessel.parts[i];
                bool selected = EngineMode == AdvancedLandingEngineMode.AllActive || part.inverseStage == Vessel.currentStage;
                if (!selected) continue;

                for (int j = 0; j < part.Modules.Count; j++)
                {
                    ModuleEngines engine = part.Modules[j] as ModuleEngines;
                    if (engine != null && engine.isEnabled && !engine.EngineIgnited) engine.Activate();
                }
            }
        }

        private void Abort(string reason)
        {
            Telemetry.Warning = reason;
            Telemetry.Phase = AdvancedLandingPhase.Aborted;
            Core.Thrust.ThrustOff();
            SetAirbrakes(false);
            ReleaseControllers();
            if (DebugLogging) Print("[AdvancedLanding] Abort: " + reason);
        }

        private void ReleaseControllers()
        {
            StopAutoWarp();
            _landingPwm.Reset();
            RestoreStabilizationHardware();
            Users.Clear();
            Core.Attitude.Users.Remove(this);
            Core.Attitude.SetOmegaTarget(roll: double.NaN);
            Core.Thrust.Users.Remove(this);
            Core.RCS.Users.Remove(this);
            if (UseRCS && !_rcsWasEnabled) Vessel.ActionGroups.SetGroup(KSPActionGroup.RCS, false);
            if (_nativePredictor != null) _nativePredictor.Users.Remove(this);
            if (Core.Hoverslam != null) Core.Hoverslam.Users.Remove(this);
            if (Core.StageStats != null) Core.StageStats.Users.Remove(this);
        }

        private void LogState()
        {
            if (!DebugLogging || VesselState.Time - _lastLog < 2) return;
            _lastLog = VesselState.Time;
            Print($"[AdvancedLanding] phase={Telemetry.Phase} predictor={Telemetry.Predictor} miss={Telemetry.TargetError:F1}m " +
                  $"range={Telemetry.CurrentTargetRange:F1}m h={Telemetry.HorizontalSpeed:F1}/{Telemetry.DesiredHorizontalSpeed:F1}m/s " +
                  $"aLat={Telemetry.CommandedLateralAcceleration:F2}m/s2 aVert={Telemetry.CommandedVerticalAcceleration:F2}m/s2 " +
                  $"throttle={Telemetry.CommandedThrottle:P0} q={Telemetry.DynamicPressure:F0}Pa " +
                  $"dv={Telemetry.AvailableDeltaV:F1}/{Telemetry.RequiredDeltaV:F1}m/s deorbit={Telemetry.DeorbitDeltaV:F1}m/s " +
                  $"aimPast={Telemetry.DeorbitAimOvershoot:F0}m " +
                  $"orbitReachable={Telemetry.OrbitalReachable} autoWarp={Telemetry.AutoWarpActive} " +
                  $"warp={Telemetry.WarpMode}/{Telemetry.WarpRate:F1}x " +
                  $"entryIn={Telemetry.AtmosphereEntryCountdown:F1}s real={Telemetry.AtmosphereEntryRealSeconds:F1}s " +
                  $"ignoreFuel={IgnoreFuelLimits} twr={Telemetry.Twr:F2} " +
                  $"p={Telemetry.Probability:F0}% upperRcs={Telemetry.UpperRcsModules} finsNoRoll={Telemetry.RollSuppressedSurfaces} " +
                  $"impact={Telemetry.PredictedLatitude:F5},{Telemetry.PredictedLongitude:F5}");
        }

        private void DrawDebugOverlay()
        {
            if (!DebugOverlay || !HighLogic.LoadedSceneIsFlight || !MapView.MapIsEnabled || !Vessel.isActiveVessel) return;

            if (Telemetry.PredictionReady)
            {
                Color color = Telemetry.Feasible ? Color.green : Color.yellow;
                GLUtils.DrawGroundMarker(MainBody, Telemetry.PredictedLatitude, Telemetry.PredictedLongitude, color, true);
            }

            if (IsFinite(Telemetry.DeorbitAimLatitude) && IsFinite(Telemetry.DeorbitAimLongitude))
                GLUtils.DrawGroundMarker(MainBody, Telemetry.DeorbitAimLatitude,
                    Telemetry.DeorbitAimLongitude, new Color(1.0f, 0.45f, 0.0f), true, 0, 80);
            GLUtils.DrawGroundMarker(MainBody, Core.Target.targetLatitude, Core.Target.targetLongitude, Color.cyan, true, (float)TargetRadius);
        }
    }
}
