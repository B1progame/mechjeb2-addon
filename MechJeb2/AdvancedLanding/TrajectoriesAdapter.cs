using System;
using System.Reflection;
using UnityEngine;

namespace MuMech.AdvancedLanding
{
    /// <summary>
    /// Optional late-bound adapter for Trajectories 2.x. MechJeb keeps no hard assembly dependency,
    /// so stock installs and future Trajectories updates continue to load safely.
    /// </summary>
    internal sealed class TrajectoriesAdapter
    {
        private MethodInfo _correctedDirection;
        private MethodInfo _getImpactPosition;
        private MethodInfo _getTimeTillImpact;
        private MethodInfo _setTarget;
        private MethodInfo _updateTrajectory;
        private MethodInfo _resetDescentProfile;
        private PropertyInfo _alwaysUpdate;
        private PropertyInfo _retrogradeEntry;
        private bool _initialized;

        public bool Available { get; private set; }
        public string Version { get; private set; } = "";
        public string LastError { get; private set; } = "";

        public void Initialize()
        {
            if (_initialized) return;
            _initialized = true;

            try
            {
                Assembly trajectoriesAssembly = null;
                for (int i = 0; i < AssemblyLoader.loadedAssemblies.Count; i++)
                {
                    AssemblyLoader.LoadedAssembly loaded = AssemblyLoader.loadedAssemblies[i];
                    if (loaded.name == "Trajectories")
                    {
                        trajectoriesAssembly = loaded.assembly;
                        break;
                    }
                }

                if (trajectoriesAssembly == null) return;

                Type api = trajectoriesAssembly.GetType("Trajectories.API");
                if (api == null) return;

                _getImpactPosition = api.GetMethod("GetImpactPosition", BindingFlags.Public | BindingFlags.Static);
                _getTimeTillImpact = api.GetMethod("GetTimeTillImpact", BindingFlags.Public | BindingFlags.Static);
                _correctedDirection = api.GetMethod("CorrectedDirection", BindingFlags.Public | BindingFlags.Static);
                _setTarget = api.GetMethod("SetTarget", BindingFlags.Public | BindingFlags.Static);
                _updateTrajectory = api.GetMethod("UpdateTrajectory", BindingFlags.Public | BindingFlags.Static);
                _resetDescentProfile = api.GetMethod("ResetDescentProfile", BindingFlags.Public | BindingFlags.Static);
                _alwaysUpdate = api.GetProperty("AlwaysUpdate", BindingFlags.Public | BindingFlags.Static);
                _retrogradeEntry = api.GetProperty("RetrogradeEntry", BindingFlags.Public | BindingFlags.Static);
                PropertyInfo version = api.GetProperty("GetVersion", BindingFlags.Public | BindingFlags.Static);

                Available = _getImpactPosition != null && _getTimeTillImpact != null && _setTarget != null;
                Version = version == null ? trajectoriesAssembly.GetName().Version.ToString() : (string)version.GetValue(null, null);
            }
            catch (Exception e)
            {
                LastError = e.GetType().Name + ": " + e.Message;
                Available = false;
            }
        }

        public void SetTarget(double latitude, double longitude, double altitude)
        {
            if (!Available) return;
            try
            {
                _setTarget.Invoke(null, new object[] { latitude, longitude, (double?)altitude });
                if (_alwaysUpdate != null && _alwaysUpdate.CanWrite) _alwaysUpdate.SetValue(null, true, null);
                if (_updateTrajectory != null) _updateTrajectory.Invoke(null, null);
            }
            catch (Exception e)
            {
                LastError = e.GetType().Name + ": " + e.Message;
            }
        }

        public void SetRetrogradeEntry(bool enabled)
        {
            if (!Available) return;
            try
            {
                if (_resetDescentProfile != null)
                    _resetDescentProfile.Invoke(null, new object[] { enabled ? Math.PI : 0.0 });
                else if (_retrogradeEntry != null && _retrogradeEntry.CanWrite)
                    _retrogradeEntry.SetValue(null, (bool?)enabled, null);
                if (_updateTrajectory != null) _updateTrajectory.Invoke(null, null);
            }
            catch (Exception e)
            {
                LastError = e.GetType().Name + ": " + e.Message;
            }
        }

        public bool TryGetPrediction(out Vector3d impactPosition, out double timeToImpact, out Vector3d correctedDirection)
        {
            impactPosition = Vector3d.zero;
            correctedDirection = Vector3d.zero;
            timeToImpact = double.NaN;
            if (!Available) return false;

            try
            {
                object impact = _getImpactPosition.Invoke(null, null);
                object impactTime = _getTimeTillImpact.Invoke(null, null);
                if (impact == null || impactTime == null) return false;

                impactPosition = (Vector3)impact;
                timeToImpact = (double)impactTime;

                if (_correctedDirection != null)
                {
                    object corrected = _correctedDirection.Invoke(null, null);
                    if (corrected != null) correctedDirection = (Vector3)corrected;
                }

                return impactPosition.sqrMagnitude > 0 && timeToImpact >= 0;
            }
            catch (Exception e)
            {
                LastError = e.GetType().Name + ": " + e.Message;
                return false;
            }
        }
    }
}
