# MechJeb2 Advanced Landing

Advanced Landing is an integrated MechJeb2 flight module for precision atmospheric and reusable-booster recovery. It is additive: the existing Landing Guidance and Landing Autopilot remain unchanged and available.

## Existing MechJeb architecture used

- `MechJebModuleTargetController` remains the single source of landing targets. Map-picked coordinates, configured `MechJeb2Landing` sites, KSC launch sites, custom coordinates, and landed/splashed vessel targets (for droneship-style markers) all flow through the existing position-target API.
- `MechJebModuleLandingPredictions` and `ReentrySimulation` provide the dependency-free stock-aero fallback and existing map trajectory. The simulator already models body rotation, terrain, drag cubes, lift, parachutes, and powered descent speed policies.
- The optional `TrajectoriesAdapter` late-binds to Trajectories 2.x. It synchronizes the MechJeb target and consumes impact position, impact time, and corrected descent direction. Trajectories selects stock or FAR aerodynamics itself. No Trajectories DLL reference is added, so MechJeb still loads when the mod is missing or changes incompatibly.
- `MechJebModuleHoverslamSimulation` supplies a high-quality landing-burn ignition estimate when available. The advanced controller falls back to its tested thrust/gravity/engine-response stopping-distance calculation.
- `VesselState`, `MechJebModuleStageStats`, and the fuel-flow simulator supply live mass, atmospheric delta-v, min/max thrust, TWR, engine response, RCS authority, gimbal/control-surface authority, ullage state, and FAR live-force information.
- `MechJebModuleAttitudeController`, `MechJebModuleThrustController`, `MechJebModuleRCSController`, action groups, and `MechJebModuleStagingController` remain the control owners. Advanced Landing joins their `Users` pools instead of bypassing MechJeb fly-by-wire arbitration.
- `ComputerModule`, `DisplayModule`, and MechJeb's reflection registry automatically load the new controller and window. Existing `Persistent` passes save global, vessel-type, and local settings through the normal MechJeb configuration files.

## Flight behavior

The controller continuously re-evaluates the trajectory and moves through:

1. **Preflight** — validates target, predictor, atmospheric delta-v, reserve, TWR, relight, heat, and G margins.
2. **Orbital coast / targeted deorbit** — a stable orbit is treated as a reachable planning state, not a failed impact prediction. The controller computes the retrograde/plane-change delta-v required to put the rotating target under the future impact path, coasts to the target window, and performs the deorbit burn.
3. **Boostback** — combines surface-retrograde braking with a target-tangent correction while respecting the configured reserve.
4. **Entry burn** — commands retrograde attitude and throttles against speed, heat, and G demand.
5. **Aerodynamic guidance** — configures Trajectories for retrograde entry and converts its corrected vector into an engine-first rocket command. Low-altitude tilt is constrained toward upright; automatic airbrakes react to dynamic pressure and target error.
6. **Landing burn** — uses Hoverslam ignition timing or a response-aware suicide-burn estimate. A closed-loop target-capture controller converts the selected precision radius and remaining flight time into a desired horizontal velocity, starts a powered divert early when necessary, and continuously removes both position and velocity error.
7. **Final descent** — upright soft-landing control, correctly signed RCS translation, gear deployment, airbrakes, and touchdown-speed tracking. Target correction remains active through touchdown rather than stopping at a fixed 500 m/5 km threshold.
8. **Touchdown/abort** — throttle is cut, controllers are released, and wheel brakes are applied after landing.

Final descent uses a response-aware flare curve: it predicts the altitude and vertical speed after engine response delay, then progressively converges on the configured touchdown speed. A delta-sigma throttle modulator supplies the requested average acceleration when an engine's minimum stable throttle is too high, pulsing between off and on with a configurable minimum pulse width. This prevents the old high-TWR behavior where the controller alternated between falling too fast and applying one excessively late continuous burn. The window reports commanded vertical acceleration and PWM throttle for tuning.

Before deorbit there is deliberately no atmospheric ground-impact prediction. Advanced Landing therefore reports **Orbit reachable** using the live targeted-deorbit solution, engine relight/TWR, available stage delta-v, fuel reserve, and a provisional terminal-landing reserve. It does not incorrectly charge the vehicle's entire orbital velocity as powered landing delta-v. Once an actual impact trajectory exists, the native/Trajectories simulation replaces that provisional estimate.

Optional **Auto-warp to targeted deorbit window** uses `MechJebModuleWarpController`, caps warp at the configured maximum, requires the vessel to be settled and the deorbit-plus-landing budget to be feasible, and returns to 1x before attitude/throttle control begins. Aborting Advanced Landing also cancels only warp initiated by this module.

After the targeted burn, **Entry coast** holds an engine-first retrograde attitude and uses the same warp controller to coast to the atmospheric boundary. Warp stops at the configurable lead time before entry. The UI reports both remaining KSP/game time and estimated real-world time at the current warp rate; the real-time estimate updates continuously as warp changes.

The **Forget fuel limits (cheat)** button persistently bypasses available-delta-v, reserve, and fuel-margin gates. It does not bypass engine relight, landing TWR, heat, G-force, attitude, or trajectory safety checks. Press the button again to restore normal fuel accounting.

The Advanced Landing window reports predicted impact, surface target error, required/available delta-v, reserve margin, TWR, estimated landing probability, burn countdowns, heat/G state, predictor source, and warnings. Debug mode adds impact/target map markers and phase telemetry to `KSP.log`.

The **Keep rocket engine-first / upright** option is enabled by default. It actively damps roll, performs boostback as an upright targetward tilt instead of a full nose-down flip, rejects every below-horizon attitude once the booster approaches apoapsis, constrains final landing tilt, and limits burn throttle while attitude error is unsafe. Disable this option only for vehicles that intentionally perform a full flip. The window shows live attitude error for diagnosing insufficient reaction-wheel, RCS, control-surface, or gimbal authority.

Optional **Gyroscopic roll stabilization during coast** commands a bounded ±10 rpm roll-rate target through MechJeb's attitude controller after the thrust axis is within 15° of its target. It is available only during preflight/coast and unpowered aerodynamic guidance. Boostback, entry burn, landing burn, and final descent automatically command zero roll rate so spin cannot compromise gimbal steering or touchdown.

**Upper RCS stabilization** treats RCS modules more than 0.25 m above the current center of mass as top-mounted attitude thrusters and enables their pitch, yaw, and roll axes while Advanced Landing is engaged. **Disable fin roll** temporarily sets `ignoreRoll` on control surfaces, preventing top-mounted fins from amplifying a roll command; their original settings are restored when the controller disengages. The commanded coast spin is also stopped once dynamic pressure reaches the configured cutoff, because passive aerodynamic torque grows with dynamic pressure rather than at one universal speed.

For a controllable reusable booster:

- place at least one balanced RCS set above the expected descent center of mass, with propellant and the RCS action group enabled;
- use symmetric fins and keep their center of pressure behind the center of mass in the engine-first descent attitude;
- provide enough gimbal/reaction-wheel/RCS torque to overcome fin and body torque;
- provide landing-engine TWR above 1 at landing mass, sufficient relight capability, and useful throttle range;
- reserve enough delta-v for the powered divert and landing burn—the selected radius cannot compensate for a physically unreachable target.

The window distinguishes **Predicted miss** (where the current simulated trajectory lands) from **Current target range** (where the vessel is now). Horizontal command speed/acceleration, dynamic pressure, upper-RCS count, and roll-disabled fin count make it possible to diagnose whether the controller has enough authority to converge.

The selected precision radius is an acceptance radius, not the controller's aiming point. Powered and final guidance use a much smaller center deadband (10% of the selected radius during final descent, capped at 1 m), combine current position/velocity feedback with predicted-impact feed-forward, and increase terminal lateral response as altitude falls. This avoids merely touching the outside edge of a large radius and reduces predictor lag.

During atmospheric descent, predicted-impact error is also converted into a bounded RCS correction while the attitude controller uses pitch/yaw control surfaces and fins. The landing engine no longer has a forced 65% throttle floor merely because total surface speed is high; that floor could turn a lateral divert into an unwanted climb. Above the configurable **Allow hover/climb below** altitude, an ascent governor cuts thrust if the vehicle starts rising and keeps near-stationary flight slightly below hover thrust. Full braking remains available during a fast descent, and hover/climb authority is restored inside the terminal capture zone.

Optional **Fast horizontal accelerate / brake transfer** raises the allowable tilt and horizontal speed while the target is distant. The powered transfer itself does not intentionally overshoot: a braking-limited planner chooses the fastest velocity that can still be removed with the available lateral acceleration before reaching the center, then continuously lowers the commanded speed as stopping distance shrinks. High dynamic pressure still reduces aerodynamic tilt for stability.

The default **Atmospheric capture (no orbital boostback)** profile deliberately shifts the targeted-deorbit aim point forward along the orbital ground track—5 km past the selected landing site by default. The offset controls the deorbit-window timing, while the orbital maneuver itself remains a pure periapsis-lowering burn rather than adding a sideways target-capture plane change. Above the atmosphere, only that deorbit burn and coast are allowed; target-capture and boostback burns are gated off. After atmospheric entry, aerodynamic guidance and the powered accelerate/brake controller bend the predicted impact back toward the real target and remove horizontal velocity over its center. The overshoot distance is configurable, persists through MechJeb's normal settings system, appears as an orange map marker, and is reported separately from the cyan landing target. Disable the option to restore conventional orbital boostback behavior.

KSP forbids on-rails warp below body-specific altitude limits. In that region MechJeb normally caps its fallback physics warp at 2x. Advanced Landing instead uses the configurable **Maximum physics warp** (4x by default, or modded rates when available) during orbital/entry coast, then switches to on-rails warp automatically when altitude permits. The UI identifies `physics` versus `on-rails` warp and shows the live rate so an altitude-imposed limit is visible rather than appearing as a failed auto-warp.

## Booster recovery staging

MechJeb's Autostaging Settings now include **Reserve fuel for Advanced Landing**. A configured recovery stage can separate early when its remaining atmospheric delta-v reaches the landing reserve and the live landing TWR is acceptable. The resulting `BoosterRecoveryPlan` records the source vessel, separation stage, target, reserve, requirement, and viability for later vessel/FMRS handoff.

KSP only applies full flight physics and Trajectories predictions to the active vessel. The current implementation therefore flies any command-equipped separated booster when it is active/focused; it does not pretend to physically land an unloaded vessel. Both the booster and the main command vessel receive the integrated module and persistent settings, so either can be landed after focus or timeline handoff. The recovery-plan boundary is intentionally independent of FMRS so a later adapter can replay the booster without changing guidance or control code.

## Compatibility and safety

- Target runtime: KSP 1.12.x and the current MechJeb2 `dev`/release architecture.
- Stock aero: native MechJeb prediction, or Trajectories when selected.
- FAR: Trajectories/FAR prediction when Trajectories is installed; otherwise the UI clearly labels the native stock-model fallback as advisory while live FAR forces still feed `VesselState`.
- Existing Landing Guidance is not modified or replaced.
- Trajectories calls are optional, reflection guarded, and restricted to its documented active-vessel API.
- The default precision radius is 50 m. This is a guidance target, not a guarantee for arbitrary craft; control authority, terrain, engine throttling, frame rate, and vessel construction remain physical constraints.

## Build and verification

Build against a KSP 1.12.x installation:

```powershell
dotnet build MechJeb2.sln -c Release `
  -p:KspDir="C:\Program Files (x86)\Steam\steamapps\common\Kerbal Space Program" `
  -p:KspData="C:\Program Files (x86)\Steam\steamapps\common\Kerbal Space Program\KSP_x64_Data"
```

Run the deterministic landing-control tests:

```powershell
dotnet test MechJebLibTest\MechJebLibTest.csproj -c Release `
  --filter FullyQualifiedName~AdvancedLandingMathTests
```

Recommended in-game validation matrix:

- Kerbin vertical booster to KSC Pad: stock aero, Trajectories off/on, target error ≤50 m.
- Kerbin booster to a landed vessel/droneship marker: boostback and airbrakes enabled.
- FAR + Trajectories: confirm the window reports `Trajectories / FAR`, then repeat entry and landing.
- Vacuum body: verify the Hoverslam/fallback landing-burn path with aerodynamic controls inactive.
- Low-throttle and high-min-throttle engines, limited relights/RealFuels, insufficient TWR, insufficient reserve, overheated craft, and manual abort.
- Autostaging reserve: confirm early separation occurs near configured remaining delta-v and both resulting command vessels expose Advanced Landing after focus switching.
