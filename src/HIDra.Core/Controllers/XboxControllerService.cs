using SharpDX.XInput;
using HIDra.Models;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace HIDra.Core.Controllers
{
    public class XboxControllerService : IDisposable
    {
        private Controller? _controller;
        private ControllerInfo? _controllerInfo;
        private CancellationTokenSource? _cts;
        private Task? _readTask;
        private bool _isReading;

        public event EventHandler<ControllerState>? StateUpdated;
        public event EventHandler<ControllerInfo>? ConnectionChanged;

        public ControllerInfo? Controller => _controllerInfo;

        public async Task<ControllerInfo?> DetectControllerAsync()
        {
            // XInput supports up to 4 controllers (index 0-3)
            for (int i = 0; i < 4; i++)
            {
                var controller = new Controller((UserIndex)i);
                
                if (controller.IsConnected)
                {
                    _controller = controller;
                    
                    _controllerInfo = new ControllerInfo
                    {
                        DeviceId = $"XInput_{i}",
                        Type = ControllerType.Xbox360,
                        Name = "Xbox Controller",
                        VendorId = 0x045E, // Microsoft
                        ProductId = 0x028E, // Generic Xbox
                        Status = ConnectionStatus.Connected
                    };
                    
                    return _controllerInfo;
                }
            }

            return null;
        }

        public Task<bool> ConnectAsync(ControllerInfo controllerInfo)
        {
            // For XInput, detection and connection happen together
            // Just verify the controller is still connected
            if (_controller != null && _controller.IsConnected)
            {
                _controllerInfo = controllerInfo;
                return Task.FromResult(true);
            }
            
            return Task.FromResult(false);
        }

        public void StartPolling(int pollRateMs)
        {
            if (_controller == null || !_controller.IsConnected)
            {
                throw new InvalidOperationException("No controller connected");
            }

            _cts = new CancellationTokenSource();
            _isReading = true;

            _readTask = Task.Run(() => ReadLoop(pollRateMs, _cts.Token));
        }

        public void StopPolling()
        {
            _isReading = false;
            _cts?.Cancel();
            
            // Wait for the read loop to finish (with timeout)
            try
            {
                _readTask?.Wait(TimeSpan.FromSeconds(1));
            }
            catch (AggregateException)
            {
                // Task was cancelled, this is expected
            }
        }

        private async Task ReadLoop(int pollRateMs, CancellationToken cancellationToken)
        {
            while (_isReading && !cancellationToken.IsCancellationRequested)
            {
                try
                {
                    if (_controller == null || !_controller.IsConnected)
                    {
                        if (_controllerInfo != null)
                        {
                            _controllerInfo.Status = ConnectionStatus.Disconnected;
                            ConnectionChanged?.Invoke(this, _controllerInfo);
                        }
                        break;
                    }

                    var xinputState = _controller.GetState();
                    var state = ParseControllerState(xinputState);
                    StateUpdated?.Invoke(this, state);

                    await Task.Delay(pollRateMs, cancellationToken);
                }
                catch (Exception)
                {
                    // Silent error handling - controller may have disconnected
                }
            }
        }

        private ControllerState ParseControllerState(State xinputState)
        {
            var gamepad = xinputState.Gamepad;
            var state = new ControllerState();

            // Parse buttons
            state.ButtonA = (gamepad.Buttons & GamepadButtonFlags.A) != 0;
            state.ButtonB = (gamepad.Buttons & GamepadButtonFlags.B) != 0;
            state.ButtonX = (gamepad.Buttons & GamepadButtonFlags.X) != 0;
            state.ButtonY = (gamepad.Buttons & GamepadButtonFlags.Y) != 0;
            state.LeftBumper = (gamepad.Buttons & GamepadButtonFlags.LeftShoulder) != 0;
            state.RightBumper = (gamepad.Buttons & GamepadButtonFlags.RightShoulder) != 0;
            state.Start = (gamepad.Buttons & GamepadButtonFlags.Start) != 0;
            state.Back = (gamepad.Buttons & GamepadButtonFlags.Back) != 0;
            state.LeftStickClick = (gamepad.Buttons & GamepadButtonFlags.LeftThumb) != 0;
            state.RightStickClick = (gamepad.Buttons & GamepadButtonFlags.RightThumb) != 0;
            state.DpadUp = (gamepad.Buttons & GamepadButtonFlags.DPadUp) != 0;
            state.DpadDown = (gamepad.Buttons & GamepadButtonFlags.DPadDown) != 0;
            state.DpadLeft = (gamepad.Buttons & GamepadButtonFlags.DPadLeft) != 0;
            state.DpadRight = (gamepad.Buttons & GamepadButtonFlags.DPadRight) != 0;

            // Parse triggers (0-255) -> normalize to 0.0-1.0
            state.LeftTrigger = gamepad.LeftTrigger / 255f;
            state.RightTrigger = gamepad.RightTrigger / 255f;

            // Parse analog sticks (signed 16-bit values, -32768 to 32767) -> normalize to -1.0 to 1.0
            // Divide by 32767.0 (the actual max value) to get true -1.0 to 1.0 range
            state.LeftStickX = gamepad.LeftThumbX / 32767f;
            state.LeftStickY = gamepad.LeftThumbY / 32767f;
            state.RightStickX = gamepad.RightThumbX / 32767f;
            state.RightStickY = gamepad.RightThumbY / 32767f;

            // Clamp values to -1.0 to 1.0 range (handles the -32768 edge case)
            state.LeftStickX = Math.Clamp(state.LeftStickX, -1f, 1f);
            state.LeftStickY = Math.Clamp(state.LeftStickY, -1f, 1f);
            state.RightStickX = Math.Clamp(state.RightStickX, -1f, 1f);
            state.RightStickY = Math.Clamp(state.RightStickY, -1f, 1f);

            return state;
        }

        public void Dispose()
        {
            StopPolling();
            _cts?.Dispose();
            _controller = null;
            _controllerInfo = null;
        }
    }
}
