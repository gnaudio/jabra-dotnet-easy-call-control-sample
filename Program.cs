using System.Reactive.Linq;
using Jabra.NET.Sdk.Core;
using Jabra.NET.Sdk.Core.Types;
using Jabra.NET.Sdk.Modules.EasyCallControl;
using Jabra.NET.Sdk.Modules.EasyCallControl.Types;

internal class Program
{
    private static IManualApi jabraSdk;
    private static EasyCallControlFactory easyCallControlFactory;
    private static IMultiCallControl multiCallControl;

    public static async Task Main()
    {
        Console.WriteLine("Jabra .NET SDK Easy Call Control Sample app starting. Press ctrl+c or close the window to end.\n");

        //Initialize the core SDK. Recommended to use Init.InitManualSdk(...) (not Init.Init(...)) to allow setup of listeners before the SDK starts discovering devices.
        var config = new Config(
            partnerKey: "get-partner-key-at-developer.jabra.com",
            appId: "JabraEasyCallControlSample",
            appName: "Jabra .NET EasyCallControl Sample"
        );
        jabraSdk = Init.InitManualSdk(config);
        easyCallControlFactory = new EasyCallControlFactory(jabraSdk);

        //Subscribe to SDK log events.
        jabraSdk.LogEvents.Subscribe((log) =>
        {
            if (log.Level == LogLevel.Error) Console.WriteLine(log.ToString());
            //Ignore info, warning, and debug log messages.
        });

        //Setup listeners for Jabra devices being attached/detected.
        SetupDeviceListeners();

        // Enable the SDK's device discovery AFTER listeners and other necessary infrastructure is setup.
        await jabraSdk.Start();
        Console.WriteLine("Now listening for Jabra devices...\n");

        //Keep the sample app running until actively closed.
        Task.Delay(-1).Wait();
    }

    static void SetupDeviceListeners()
    {
        //Subscribe to Jabra devices being attached/detected by the SDK
        jabraSdk.DeviceAdded.Subscribe(async (IDevice device) =>
        {
            Console.WriteLine($"> Device attached/detected: {device.Name} (Product ID: {device.ProductId}, Serial #: {device.SerialNumber})");

            // If the device supports Easy Call Control, enable it.
            if (easyCallControlFactory.SupportsEasyCallControl(device)) 
            {
                Console.WriteLine("Setting up Easy Call Control for device: " + device.Name);
                multiCallControl = await easyCallControlFactory.CreateMultiCallControl(device);
                await multiCallControl.StartCall();
                System.Threading.Thread.Sleep(5000);
                await multiCallControl.Hold();
                System.Threading.Thread.Sleep(5000);
                await multiCallControl.Resume();
                System.Threading.Thread.Sleep(5000);
                await multiCallControl.EndCall();
            }
            else
            {
                Console.WriteLine("Easy Call Control is not supported for device: " + device.Name);
            }
        });

        //Subscribe to Jabra devices being detached/rebooted
        jabraSdk.DeviceRemoved.Subscribe((IDevice device) =>
        {
            Console.WriteLine($"< Device detached/reboots: {device.Name} (Product ID: {device.ProductId}, Serial #: {device.SerialNumber})");
        });
    }
}
