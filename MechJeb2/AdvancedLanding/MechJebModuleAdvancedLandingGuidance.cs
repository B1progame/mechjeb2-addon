extern alias JetBrainsAnnotations;
using System;
using System.Linq;
using JetBrainsAnnotations::JetBrains.Annotations;
using MuMech.AdvancedLanding;
using UnityEngine;
using static MechJebLib.Utils.Statics;

namespace MuMech
{
    [UsedImplicitly]
    public class MechJebModuleAdvancedLandingGuidance : DisplayModule
    {
        private int _landingSiteIndex;
        private bool _showSettings = true;

        public MechJebModuleAdvancedLandingGuidance(MechJebCore core) : base(core) { }

        protected override GUILayoutOption[] WindowOptions() => new[] { GuiUtils.LayoutWidth(320), GUILayout.Height(200) };

        protected override void WindowGUI(int windowID)
        {
            MechJebModuleAdvancedLandingAutopilot autopilot = Core.AdvancedLanding;
            AdvancedLandingTelemetry telemetry = autopilot.Telemetry;

            GUILayout.BeginVertical();
            DrawTargetSelector();

            GUILayout.BeginHorizontal();
            GUI.enabled = Core.Target.PositionTargetExists && !Vessel.LandedOrSplashed;
            if (!autopilot.Active)
            {
                if (GUILayout.Button("Analyze")) autopilot.UpdateFeasibility();
                if (GUILayout.Button("Start Advanced Landing")) autopilot.StartLanding(this);
            }
            else
            {
                GUI.enabled = true;
                if (GUILayout.Button("Abort Advanced Landing")) autopilot.StopLanding();
            }

            GUI.enabled = true;
            GUILayout.EndHorizontal();

            GUILayout.Label("Phase: " + telemetry.Phase);
            GUILayout.Label("Predictor: " + telemetry.Predictor);
            DrawReadout("Predicted miss", FormatDistance(telemetry.TargetError),
                IsFinite(telemetry.TargetError) && telemetry.TargetError <= autopilot.TargetRadius ? Color.green : Color.yellow);
            DrawReadout("Current target range", FormatDistance(telemetry.CurrentTargetRange), Color.white);
            DrawReadout("Predicted impact",
                IsFinite(telemetry.PredictedLatitude)
                    ? Coordinates.ToStringDMS(telemetry.PredictedLatitude, telemetry.PredictedLongitude)
                    : "N/A", Color.white);
            DrawReadout("Landing Δv", telemetry.RequiredDeltaV.ToString("F0") + " / " +
                                       telemetry.AvailableDeltaV.ToString("F0") + " m/s",
                autopilot.IgnoreFuelLimits ? Color.cyan : telemetry.FuelMarginDeltaV >= 0 ? Color.green : Color.red);
            DrawReadout("Targeted deorbit",
                telemetry.DeorbitDeltaV > 0
                    ? telemetry.DeorbitDeltaV.ToString("F0") + " m/s" +
                      (telemetry.AutoWarpActive ? " (warping)" : "")
                    : "N/A",
                telemetry.OrbitalReachable ? Color.green : Color.white);
            DrawReadout("Fuel margin", telemetry.FuelMarginDeltaV.ToString("F0") + " m/s",
                autopilot.IgnoreFuelLimits ? Color.cyan : telemetry.FuelMarginDeltaV >= 0 ? Color.green : Color.red);
            DrawReadout("Projected landing TWR", telemetry.Twr.ToString("F2"), telemetry.Twr > 1 ? Color.green : Color.red);
            DrawReadout("Landing probability", telemetry.Probability.ToString("F0") + "%",
                telemetry.Probability >= 70 ? Color.green : telemetry.Probability >= 40 ? Color.yellow : Color.red);
            DrawReadout("Entry / landing burn",
                FormatTime(telemetry.EntryBurnCountdown) + " / " + FormatTime(telemetry.LandingBurnCountdown), Color.white);
            DrawReadout("Atmosphere entry (game / real)",
                FormatTime(telemetry.AtmosphereEntryCountdown) + " / " +
                FormatTime(telemetry.AtmosphereEntryRealSeconds),
                telemetry.AutoWarpActive ? Color.cyan : Color.white);
            DrawReadout("Warp mode / rate",
                telemetry.WarpMode + " / " + telemetry.WarpRate.ToString("F1") + "x",
                telemetry.AutoWarpActive ? Color.cyan : Color.white);
            DrawReadout("Heat / G", (100 * telemetry.HeatRatio).ToString("F0") + "% / " + telemetry.GLoad.ToString("F1") + "g",
                telemetry.HeatRatio < autopilot.MaxHeatRatio && telemetry.GLoad < autopilot.MaxGForce ? Color.green : Color.red);
            DrawReadout("Attitude error", telemetry.AttitudeError.ToString("F1") + "°",
                telemetry.AttitudeError < 10 ? Color.green : telemetry.AttitudeError < 25 ? Color.yellow : Color.red);
            DrawReadout("Roll spin", telemetry.SpinRateRpm.ToString("F1") + " rpm",
                autopilot.UseSpinStabilization ? Color.cyan : Color.white);
            DrawReadout("Horizontal actual / command",
                telemetry.HorizontalSpeed.ToString("F1") + " / " +
                telemetry.DesiredHorizontalSpeed.ToString("F1") + " m/s; " +
                telemetry.CommandedLateralAcceleration.ToString("F1") + " m/s²", Color.white);
            DrawReadout("Vertical command",
                telemetry.CommandedVerticalAcceleration.ToString("F1") + " m/s² / " +
                (100 * telemetry.CommandedThrottle).ToString("F0") + "%", Color.white);
            DrawReadout("Dynamic pressure", telemetry.DynamicPressure.ToSI() + "Pa", Color.white);
            DrawReadout("Upper RCS / roll-disabled fins",
                telemetry.UpperRcsModules + " / " + telemetry.RollSuppressedSurfaces,
                telemetry.UpperRcsModules > 0 ? Color.green : Color.yellow);

            if (!string.IsNullOrEmpty(telemetry.Warning))
            {
                Color previous = GUI.color;
                GUI.color = Color.yellow;
                GUILayout.Label("Warning: " + telemetry.Warning);
                GUI.color = previous;
            }

            _showSettings = GUILayout.Toggle(_showSettings, "Advanced settings");
            if (_showSettings) DrawSettings(autopilot);

            GUILayout.EndVertical();
            base.WindowGUI(windowID);
        }

        private void DrawTargetSelector()
        {
            GUILayout.Label(Core.Target.PositionTargetExists
                ? "Target: " + Core.Target.GetPositionTargetString()
                : "Target: none");

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Pick on map")) Core.Target.PickPositionTargetOnMap();
            if (GUILayout.Button("Use coordinates"))
                Core.Target.SetPositionTarget(MainBody, Core.Target.targetLatitude, Core.Target.targetLongitude);
            GUILayout.EndHorizontal();

            if (MechJebModuleLandingGuidance.LandingSites == null) return;
            MechJebModuleLandingGuidance.LandingSite[] sites =
                MechJebModuleLandingGuidance.LandingSites.Where(site => site.Body == MainBody).ToArray();
            if (sites.Length == 0) return;

            GUILayout.BeginHorizontal();
            _landingSiteIndex = Math.Min(_landingSiteIndex, sites.Length - 1);
            _landingSiteIndex = GuiUtils.ComboBox.Box(_landingSiteIndex, sites.Select(site => site.Name).ToArray(), this);
            if (GUILayout.Button("Set site", GuiUtils.LayoutNoExpandWidth))
                Core.Target.SetPositionTarget(MainBody, sites[_landingSiteIndex].Latitude, sites[_landingSiteIndex].Longitude);
            GUILayout.EndHorizontal();
        }

        private static void DrawSettings(MechJebModuleAdvancedLandingAutopilot autopilot)
        {
            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label("Landing profile");
            autopilot.LandingProfile = (AdvancedLandingProfile)GUILayout.SelectionGrid((int)autopilot.LandingProfile,
                new[] { "Auto", "Booster", "Capsule", "Vacuum" }, 4);
            GUILayout.Label("Safety mode");
            autopilot.SafetyMode = (AdvancedLandingSafetyMode)GUILayout.SelectionGrid((int)autopilot.SafetyMode,
                new[] { "Precision", "Balanced", "Max safety" }, 3);
            GUILayout.Label("Airbrakes");
            autopilot.AirbrakeMode = (AdvancedLandingAirbrakeMode)GUILayout.SelectionGrid((int)autopilot.AirbrakeMode,
                new[] { "Auto", "Retracted", "Deployed" }, 3);
            GUILayout.Label("Engine selection");
            autopilot.EngineMode = (AdvancedLandingEngineMode)GUILayout.SelectionGrid((int)autopilot.EngineMode,
                new[] { "Auto", "Current stage", "All active" }, 3);

            GuiUtils.SimpleTextBox("Precision radius:", autopilot.TargetRadius, "m", 55);
            GuiUtils.SimpleTextBox("Fuel reserve:", autopilot.FuelReservePercent, "%", 55);
            Color fuelButtonColor = GUI.color;
            if (autopilot.IgnoreFuelLimits) GUI.color = Color.cyan;
            if (GUILayout.Button(autopilot.IgnoreFuelLimits
                    ? "Fuel limits forgotten (cheat) — restore"
                    : "Forget fuel limits (cheat)"))
                autopilot.IgnoreFuelLimits = !autopilot.IgnoreFuelLimits;
            GUI.color = fuelButtonColor;
            GuiUtils.SimpleTextBox("Maximum G-force:", autopilot.MaxGForce, "g", 55);
            GuiUtils.SimpleTextBox("Heat safety:", autopilot.MaxHeatRatio, "%", 55);
            GuiUtils.SimpleTextBox("Touchdown speed:", autopilot.TouchdownSpeed, "m/s", 55);
            GuiUtils.SimpleTextBox("Final descent limit:", autopilot.FinalDescentSpeedLimit, "m/s", 55);
            GuiUtils.SimpleTextBox("Allow hover/climb below:", autopilot.HoverCaptureAltitude, "m", 55);
            GuiUtils.SimpleTextBox("Minimum throttle pulse:", autopilot.ThrottlePulseWidth, "s", 55);
            GuiUtils.SimpleTextBox("Landing burn lead:", autopilot.LandingBurnLead, "s", 55);

            autopilot.UseTrajectories = GUILayout.Toggle(autopilot.UseTrajectories, "Use Trajectories when installed");
            autopilot.BoosterRecoveryMode = GUILayout.Toggle(autopilot.BoosterRecoveryMode, "Booster recovery / boostback");
            autopilot.UseAirbrakes = GUILayout.Toggle(autopilot.UseAirbrakes, "Control airbrakes");
            autopilot.UseRCS = GUILayout.Toggle(autopilot.UseRCS, "Use RCS for final correction");
            autopilot.ForceEngineFirstAttitude =
                GUILayout.Toggle(autopilot.ForceEngineFirstAttitude, "Keep rocket engine-first / upright");
            autopilot.UseSpinStabilization =
                GUILayout.Toggle(autopilot.UseSpinStabilization, "Gyroscopic roll stabilization during coast");
            GUI.enabled = autopilot.UseSpinStabilization;
            GuiUtils.SimpleTextBox("Target roll rate:", autopilot.SpinRateRpm, "rpm", 55);
            GuiUtils.SimpleTextBox("Stop spin above q:", autopilot.MaxSpinDynamicPressure, "Pa", 55);
            GUI.enabled = true;
            autopilot.UseUpperRcsStabilization =
                GUILayout.Toggle(autopilot.UseUpperRcsStabilization, "Use RCS above CoM for attitude stabilization");
            autopilot.SuppressAerodynamicRoll =
                GUILayout.Toggle(autopilot.SuppressAerodynamicRoll, "Disable roll input on fins during recovery");
            autopilot.PoweredTargetCapture =
                GUILayout.Toggle(autopilot.PoweredTargetCapture, "Start powered divert early to capture target");
            GuiUtils.SimpleTextBox("Maximum targeting tilt:", autopilot.MaximumTargetingTilt, "°", 55);
            autopilot.FastHorizontalTransfer =
                GUILayout.Toggle(autopilot.FastHorizontalTransfer, "Fast horizontal accelerate / brake transfer");
            GUI.enabled = autopilot.FastHorizontalTransfer;
            GuiUtils.SimpleTextBox("Maximum horizontal speed:", autopilot.MaximumHorizontalTransferSpeed, "m/s", 55);
            GuiUtils.SimpleTextBox("Horizontal aggressiveness:", autopilot.HorizontalTransferGain, "x", 55);
            GUI.enabled = true;
            autopilot.AutoWarp = GUILayout.Toggle(autopilot.AutoWarp, "Auto-warp to targeted deorbit window");
            GUI.enabled = autopilot.AutoWarp;
            GuiUtils.SimpleTextBox("Maximum auto-warp:", autopilot.MaxAutoWarpRate, "x", 55);
            GuiUtils.SimpleTextBox("Maximum physics warp:", autopilot.MaxPhysicsWarpRate, "x", 55);
            GuiUtils.SimpleTextBox("Stop before atmosphere:", autopilot.EntryWarpLead, "s", 55);
            GUI.enabled = true;
            autopilot.DeployLandingGear = GUILayout.Toggle(autopilot.DeployLandingGear, "Deploy landing legs / gear");
            autopilot.AllowLandingStaging = GUILayout.Toggle(autopilot.AllowLandingStaging, "Stage to a reserved landing engine if needed");
            autopilot.DebugOverlay = GUILayout.Toggle(autopilot.DebugOverlay, "Trajectory debug markers");
            autopilot.DebugLogging = GUILayout.Toggle(autopilot.DebugLogging, "Detailed phase logging");
            GUILayout.EndVertical();
        }

        private static void DrawReadout(string label, string value, Color color)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label + ":");
            Color previous = GUI.color;
            GUI.color = color;
            GUILayout.FlexibleSpace();
            GUILayout.Label(value);
            GUI.color = previous;
            GUILayout.EndHorizontal();
        }

        private static string FormatDistance(double value) => IsFinite(value) ? value.ToSI() + "m" : "N/A";
        private static string FormatTime(double value) => IsFinite(value) ? GuiUtils.TimeToDHMS(value, 1) : "N/A";
        private static bool IsFinite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);

        public override string GetName() => "Advanced Landing";
        public override string IconName() => "Landing Guidance";
        protected override bool IsSpaceCenterUpgradeUnlocked() => Vessel.patchedConicsUnlocked();
    }
}
