﻿using System.Reactive.Linq;
using Jabra.NET.Sdk.Core;
using Jabra.NET.Sdk.Core.Types;
using Jabra.NET.Sdk.Modules.EasyCallControl;
using Jabra.NET.Sdk.Modules.EasyCallControl.Types;

internal class Program
{
    class EccState
    {
        public MuteState IsMuted { get; set; }
        public HoldState IsOnHold { get; set; }
        public uint OngoingCalls { get; set; }
        public bool IsRinging { get; set; }
    }

    // Keeping track of the current state of Easy Call Control.
    private static EccState currentEccState = new EccState();

    // Keeping track of the current device that Easy Call Control is connected to.
    private static IDevice currentEccDevice;

    private static IManualApi jabraSdk;
    private static EasyCallControlFactory easyCallControlFactory;
    
    // Using MultiCallControl to handle multiple calls (i.e. support putting calls on hold). Even if you only need to handle single call use cases, you should use MultiCallControl.
    private static IMultiCallControl easyCallControl;

    public static async Task Main()
    {
        // Writing available demo commands to console 
        PrintMenu();

        // Start the key press listener in a separate task
        var keyPressTask = Task.Run(() => ListenForKeyPress(), CancellationToken.None);

        // Initialize the core SDK. Recommended to use Init.InitManualSdk(...) (not Init.InitSdk(...)) to allow setup of listeners before the SDK starts discovering devices. But don't forget the jabraSdk.Start(); call later.
        var config = new Config(
            partnerKey: "get-partner-key-at-developer.jabra.com",
            appId: "JabraEasyCallControlSample",
            appName: "Jabra .NET EasyCallControl Sample"
        );
        jabraSdk = Init.InitManualSdk(config);
        easyCallControlFactory = new EasyCallControlFactory(jabraSdk);

        // Subscribe to SDK log events.
        jabraSdk.LogEvents.Subscribe((log) =>
        {
            // Ignore info, warning, and debug log messages.
            if (log.Level == LogLevel.Error) OutPutToConsole(log.ToString());
        });

        // Setup listeners for Jabra devices being attached/detected.
        SetupDeviceListeners();

        // Enable the SDK's device discovery AFTER listeners and other necessary infrastructure is setup.
        await jabraSdk.Start();
        OutPutToConsole("Now listening for Jabra devices...\n");


        // Keep the sample app running until actively closed.
        await Task.Delay(Timeout.Infinite);
    }

    static void SetupDeviceListeners()
    {
        // Subscribe to Jabra devices being attached/detected by the SDK
        jabraSdk.DeviceAdded.Subscribe(static async (IDevice device) =>
        {
            OutPutToConsole($"> Device attached/detected: {device.Name} (Product ID: {device.ProductId}, Serial #: {device.SerialNumber})");

            // If the device supports Easy Call Control, enable it.
            if (easyCallControlFactory.SupportsEasyCallControl(device))
            {
                OutPutToConsole("Setting up Easy Call Control for device: " + device.Name);
                // Setting values of currentEccState to default values
                currentEccState = new EccState()
                {
                    IsMuted = MuteState.NoOngoingCalls,
                    IsOnHold = HoldState.NoOngoingCalls,
                    OngoingCalls = 0,
                    IsRinging = false
                };
                currentEccDevice = device;
                SetupEcc(device, null);

                // Listen for disconnect event for headset. Disconnect can happen when the device has multiple connections
                // such as a BT headset connected via the dongle and a USB cable at the same time. Depending on which active audio device
                // you have setup in your softphone, you might want to setup a new Easy Call Control instance when the other connection is removed.
                device.ConnectionRemoved.Subscribe(async (IConnection connection) => 
                {
                    if (!easyCallControl.Connection.IsConnected)
                    {
                        OutPutToConsole("Active connection for ECC was removed");
                        // Consider creating a new ECC with initial state if needed.
                    }
                    else
                    {
                        OutPutToConsole($"One of the ways the headset was connected was removed. \n" +
                            $"ECC was set up for another connection so is still active for: {device.Name}");
                    }
                });

                // Subscribe to connection list
                device.ConnectionList.Subscribe((List<IConnection> connections) =>
                {
                    OutPutToConsole($"Connections for device: {device.Name}");
                    foreach (var connection in connections)
                    {
                        OutPutToConsole($"Connection: {connection.Type} - {connection.IsConnected}");
                    }
                });
            }
            else
            {
                OutPutToConsole("Easy Call Control is not supported for device: " + device.Name);
            }
        });

        //Subscribe to Jabra devices being unplugged
        jabraSdk.DeviceRemoved.Subscribe((IDevice device) =>
        {
            OutPutToConsole($"< Device detached: {device.Name} (Product ID: {device.ProductId}, Serial #: {device.SerialNumber})");
            if (currentEccDevice == device)
            {
                OutPutToConsole("Removed current ECC headset: " + device.Name);
                // Here you might want to look at setting up ECC for another connected Jabra device if needed.
            }
        });
    }

    async static void SetupEcc(IDevice device, MultiInitialState? initialState)
    {
        // Create Easy Call Control instance for the device
        if (initialState == null)
        {
            easyCallControl = await easyCallControlFactory.CreateMultiCallControl(device);
        }
        else
        {
            easyCallControl = await easyCallControlFactory.CreateMultiCallControl(device, initialState);
        }

        // Subscribe to Easy Call Control events. By listening to these events your application
        // can keep track of the call state of the device and update your application's UI accordingly.

        // OngoingCalls is the number of ongoing calls on the device. This includes active and held calls. 
        easyCallControl.OngoingCalls.Subscribe((ongoingCalls) =>
        {
            // Only output state if different from previous state.
            if (ongoingCalls != currentEccState.OngoingCalls)
            {
                OutPutToConsole($"Ongoing calls: {ongoingCalls} - new ECC state is:\n" +
                    "-----\n" +
                    $"Ongoing calls: {ongoingCalls} (was: {currentEccState.OngoingCalls})\n" +
                    $"IsMuted: {currentEccState.IsMuted}\n" +
                    $"IsOnHold: {currentEccState.IsOnHold}\n" +
                    $"IsRinging: {currentEccState.IsRinging}\n" +
                    "-----");
                currentEccState.OngoingCalls = (uint)ongoingCalls;
            }
        });

        // MuteState is the microphone mute state of the device. 
        easyCallControl.MuteState.Subscribe((isMuted) =>
        {
            // Only output state if different from previous state.
            if (isMuted!= currentEccState.IsMuted)
            {
                OutPutToConsole($"Mute state: {isMuted} - new ECC state is:\n" +
                "-----\n" +
                $"Ongoing calls: {currentEccState.OngoingCalls}\n" +
                $"IsMuted: {isMuted} (was: {currentEccState.IsMuted})\n" +
                $"IsOnHold: {currentEccState.IsOnHold}\n" +
                $"IsRinging: {currentEccState.IsRinging}\n" +
                "-----");
                currentEccState.IsMuted = isMuted;
            }  
        });

        // HoldState indicates if call is on hold or not. 
        easyCallControl.HoldState.Subscribe((isOnHold) =>
        {
            // Only output state if different from previous state.
            if (isOnHold != currentEccState.IsOnHold)
            {
                OutPutToConsole($"Hold state: {isOnHold} - new ECC state is:\n" +
                "-----\n" +
                $"Ongoing calls: {currentEccState.OngoingCalls}\n" +
                $"IsMuted: {currentEccState.IsMuted}\n" +
                $"IsOnHold: {isOnHold} (was: {currentEccState.IsOnHold})\n" +
                $"IsRinging: {currentEccState.IsRinging}\n" +
                "-----");
                currentEccState.IsOnHold = isOnHold;
            }
        });

        // RingState indicates if the device is ringing or not (incoming call).
        easyCallControl.RingState.Subscribe((isRinging) =>
        {
            // Only output state if different from previous state.
            if (isRinging != currentEccState.IsRinging)
            {
                OutPutToConsole($"Ring state: {isRinging} - new ECC state is:\n" +
                "-----\n" +
                $"Ongoing calls: {currentEccState.OngoingCalls}\n" +
                $"IsMuted: {currentEccState.IsMuted}\n" +
                $"IsOnHold: {currentEccState.IsOnHold}\n" +
                $"IsRinging: {isRinging} (was: {currentEccState.IsRinging})\n" +
                "-----");
                currentEccState.IsRinging = isRinging;
            }
        });

        // SwapRequest is called when the user wants to swap between two calls. 
        easyCallControl.SwapRequest.Subscribe((_) =>
        {
            OutPutToConsole("Swap Requested.");
            // (...) Implement your application's logic for swapping between two calls.
        });
    }

    /************************************
    ** Methods for setting up demo app **
    ************************************/
    static void PrintMenu()
    {
        Console.WriteLine("Jabra .NET SDK Easy Call Control Sample app starting. Press ctrl+c or close the window to end.");
        Console.WriteLine("----------");
        Console.WriteLine("Available commands when a Jabra device is detected:");
        Console.WriteLine("1: New incoming call");
        Console.WriteLine("2: New outgoing call");
        Console.WriteLine("3: Answer call");
        Console.WriteLine("4: Reject call");
        Console.WriteLine("5: End call");
        Console.WriteLine("6: Hold call");
        Console.WriteLine("7: Resume call");
        Console.WriteLine("8: Mute call");
        Console.WriteLine("9: Unmute call");
        Console.WriteLine("----------");
    }
    async static void HandleKeyPress(char keyChar)
    {
        switch (keyChar)
        {
            case '1':
                // New incoming call - note there is an optional parameter for timeout in milliseconds. 
                // After timeout the SDK considers the call Rejected. 
                OutPutToConsole("SignalIncomingCall()");
                bool callAcceptedResult = await easyCallControl.SignalIncomingCall();
                if (callAcceptedResult)
                {
                    OutPutToConsole("SignalIncomingCall() - Call accepted");
                }
                else
                {
                    OutPutToConsole("SignalIncomingCall() - Call rejected");
                }
                break;
            case '2':
                // New outgoing call
                await easyCallControl.StartCall();
                OutPutToConsole("StartCall()");
                break;
            case '3':
                // Answer call. Note there is an optional parameter in case of multiple calls to specify whether to 
                // accept and hang up current call, or accept and put current call on hold.
                await easyCallControl.AcceptIncomingCall();
                OutPutToConsole("AcceptIncomingCall()");
                break;
            case '4':
                // Reject incoming call. 
                await easyCallControl.RejectIncomingCall();
                OutPutToConsole("RejectIncomingCall()");
                break;
            case '5':
                // End call
                await easyCallControl.EndCall();
                OutPutToConsole("EndCall()");
                break;
            case '6':
                // Hold call
                await easyCallControl.Hold();
                OutPutToConsole("Hold()");
                break;
            case '7':
                // Resume call
                await easyCallControl.Resume();
                OutPutToConsole("Resume()");
                break;
            case '8':
                // Mute call
                await easyCallControl.Mute();
                OutPutToConsole("Mute()");
                break;
            case '9':
                // Unmute call
                await easyCallControl.Unmute();
                OutPutToConsole("Unmute()");
                break;
            default:
                // Ignore other keys
                break;
        }
    }

    static void ListenForKeyPress()
    {
        while (true)
        {
            if (Console.KeyAvailable) // Check if a key press is available
            {
                var keyInfo = Console.ReadKey(intercept: true);
                HandleKeyPress(keyInfo.KeyChar);
            }
            else
            {
                // Sleep briefly to avoid busy waiting
                Thread.Sleep(50);
            }
        }
    }

    static void OutPutToConsole(string message)
    {
        // Create a new DateTime value for today's date and time
        DateTime now = DateTime.Now;
        // create string with current time
        string time = now.ToString("HH:mm:ss.fff");
        Console.WriteLine(time + ": " + message);
    }
}
