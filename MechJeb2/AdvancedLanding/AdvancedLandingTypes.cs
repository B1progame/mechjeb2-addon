using UnityEngine;

namespace MuMech.AdvancedLanding
{
    public enum AdvancedLandingPhase
    {
        Off,
        Preflight,
        OrbitalCoast,
        DeorbitBurn,
        EntryCoast,
        Boostback,
        EntryBurn,
        AerodynamicGuidance,
        LandingBurn,
        FinalDescent,
        Touchdown,
        Aborted
    }

    public enum AdvancedLandingProfile
    {
        Automatic,
        Booster,
        Capsule,
        Vacuum
    }

    public enum AdvancedLandingSafetyMode
    {
        Precision,
        Balanced,
        MaximumSafety
    }

    public enum AdvancedLandingAirbrakeMode
    {
        Automatic,
        Retracted,
        Deployed
    }

    public enum AdvancedLandingEngineMode
    {
        Automatic,
        CurrentStage,
        AllActive
    }

    public sealed class AdvancedLandingTelemetry
    {
        public AdvancedLandingPhase Phase;
        public string Predictor = "Native";
        public string Warning = "";
        public bool PredictionReady;
        public bool Feasible;
        public bool EngineRelightAvailable;
        public bool OrbitalReachable;
        public bool AutoWarpActive;
        public bool FuelConservationActive;
        public bool EmergencyDiversionActive;
        public bool EmergencyDiversionToWater;
        public bool TargetAheadOfImpact;
        public bool AirbrakesDeployed;
        public string WarpMode = "1x";
        public double WarpRate = 1;
        public double TargetError = double.NaN;
        public double AvailableDeltaV;
        public double RequiredDeltaV;
        public double FuelMarginDeltaV;
        public double TouchdownReserveDeltaV;
        public double PoweredDivertDeltaV;
        public double ProtectedReserveDeltaV;
        public double Twr;
        public double Probability;
        public double EntryBurnCountdown = double.NaN;
        public double EntryBurnDeltaVSpent;
        public double AtmosphereEntryCountdown = double.NaN;
        public double AtmosphereEntryRealSeconds = double.NaN;
        public double LandingBurnCountdown = double.NaN;
        public double DeorbitDeltaV;
        public double DeorbitAimOvershoot;
        public double DeorbitAimLatitude = double.NaN;
        public double DeorbitAimLongitude = double.NaN;
        public double DeorbitAimError = double.NaN;
        public double DeorbitGroundTrackError = double.NaN;
        public double DeorbitPeriapsisTarget = double.NaN;
        public double PeriapsisAltitude = double.NaN;
        public double TimeToImpact = double.NaN;
        public double AltitudeAsl;
        public double RadarAltitude;
        public double VerticalSpeed;
        public double HeatRatio;
        public double GLoad;
        public double AttitudeError;
        public double SpinRateRpm;
        public double CurrentTargetRange = double.NaN;
        public double HorizontalSpeed;
        public double DesiredHorizontalSpeed;
        public double CommandedLateralAcceleration;
        public double CommandedVerticalAcceleration;
        public double CommandedThrottle;
        public double ActualThrottle;
        public double ThrustUpProjection;
        public double TouchdownVerticalSpeed;
        public bool TouchdownSpeedSafe;
        public double DynamicPressure;
        public int UpperRcsModules;
        public int RollSuppressedSurfaces;
        public double PredictedLatitude = double.NaN;
        public double PredictedLongitude = double.NaN;
        public Vector3d PredictedImpact = Vector3d.zero;
        public Vector3d CorrectedDirection = Vector3d.zero;
        public double LastUpdateUT;

        public void Reset()
        {
            Phase = AdvancedLandingPhase.Off;
            Predictor = "Native";
            Warning = "";
            PredictionReady = false;
            Feasible = false;
            OrbitalReachable = false;
            AutoWarpActive = false;
            FuelConservationActive = false;
            EmergencyDiversionActive = false;
            EmergencyDiversionToWater = false;
            TargetAheadOfImpact = false;
            AirbrakesDeployed = false;
            WarpMode = "1x";
            WarpRate = 1;
            TargetError = double.NaN;
            AvailableDeltaV = 0;
            RequiredDeltaV = 0;
            FuelMarginDeltaV = 0;
            TouchdownReserveDeltaV = 0;
            PoweredDivertDeltaV = 0;
            ProtectedReserveDeltaV = 0;
            Twr = 0;
            Probability = 0;
            EntryBurnCountdown = double.NaN;
            EntryBurnDeltaVSpent = 0;
            AtmosphereEntryCountdown = double.NaN;
            AtmosphereEntryRealSeconds = double.NaN;
            LandingBurnCountdown = double.NaN;
            DeorbitDeltaV = 0;
            DeorbitAimOvershoot = 0;
            DeorbitAimLatitude = double.NaN;
            DeorbitAimLongitude = double.NaN;
            DeorbitAimError = double.NaN;
            DeorbitGroundTrackError = double.NaN;
            DeorbitPeriapsisTarget = double.NaN;
            PeriapsisAltitude = double.NaN;
            TimeToImpact = double.NaN;
            AltitudeAsl = 0;
            RadarAltitude = 0;
            VerticalSpeed = 0;
            HeatRatio = 0;
            GLoad = 0;
            AttitudeError = 0;
            SpinRateRpm = 0;
            CurrentTargetRange = double.NaN;
            HorizontalSpeed = 0;
            DesiredHorizontalSpeed = 0;
            CommandedLateralAcceleration = 0;
            CommandedVerticalAcceleration = 0;
            CommandedThrottle = 0;
            ActualThrottle = 0;
            ThrustUpProjection = 0;
            TouchdownVerticalSpeed = double.NaN;
            TouchdownSpeedSafe = false;
            DynamicPressure = 0;
            UpperRcsModules = 0;
            RollSuppressedSurfaces = 0;
            PredictedLatitude = double.NaN;
            PredictedLongitude = double.NaN;
            PredictedImpact = Vector3d.zero;
            CorrectedDirection = Vector3d.zero;
            LastUpdateUT = 0;
        }
    }

    public sealed class BoosterRecoveryPlan
    {
        public uint SourceVesselPersistentId;
        public int SeparationStage;
        public double TargetLatitude;
        public double TargetLongitude;
        public double ReservedDeltaV;
        public double EstimatedRequiredDeltaV;
        public bool Viable;
        public string Status;
    }
}
