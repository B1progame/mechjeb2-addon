/*
 * Copyright Lamont Granquist, Sebastien Gaggini and the MechJeb contributors
 * SPDX-License-Identifier: LicenseRef-PD-hp OR Unlicense OR CC0-1.0 OR 0BSD OR MIT-0 OR MIT OR LGPL-2.1+
 */

using MechJebLib.Control;
using Xunit;

namespace MechJebLibTest.ControlTests
{
    public class DeltaSigmaThrottleModulatorTests
    {
        [Fact]
        public void DefaultPwmRetainsFullThrustPulse()
        {
            var pwm = new DeltaSigmaThrottleModulator(0.02, 0.02);

            float throttle = pwm.ThrottleCommand(2, 5, 20, 0.02);

            Assert.Equal(1, throttle);
        }

        [Fact]
        public void GentlePwmUsesMinimumStableThrottlePulse()
        {
            var pwm = new DeltaSigmaThrottleModulator(0.02, 0.02) { PulseAtMinimum = true };

            float throttle = pwm.ThrottleCommand(2, 5, 20, 0.02);

            Assert.InRange(throttle, 0.00009f, 0.00011f);
        }

        [Fact]
        public void GentlePwmStillUsesContinuousThrottleAboveMinimum()
        {
            var pwm = new DeltaSigmaThrottleModulator(0.02, 0.02) { PulseAtMinimum = true };

            float throttle = pwm.ThrottleCommand(10, 5, 20, 0.02);

            Assert.InRange(throttle, 0.333f, 0.334f);
        }
    }
}
