using M3Logic.Settings;
using System;

namespace SettingManagerTest
{
    //Just for testing purposes...
    public class Artifact
    {
        public string Name { get; set; }
        public decimal Value { get; set; }
    }

    class Program
    {
        static void Main(string[] args)
        {
            SettingManager settings = new SettingManager("M3Logic", "Test App", "Settings.db");

            //Should create a new hive in the common app settings location
            //based information passed to the constructor (see above) i.e. c:\ProgramData\M3Logic\Test App\Settings.db
            //and because the @ap (application data) prefix is used.
            //See documentation for further information on prefixes - https://github.com/YourRobotOverlord/SettingManager/blob/master/README.md
            settings.SaveSetting<string>("@apTestStringSetting", "Test String");

            settings.SaveSetting<decimal>("@apTestDecimalSetting", 123.456M);

            string nulltest = null;
            settings.SaveSetting<string>("@apTestNullSetting", nulltest);

            Artifact pricelessArtifact = new Artifact { Name = "Stone Idol", Value = 500.00M };
            settings.SaveSetting<Artifact>("@apTestArtifactSetting", pricelessArtifact);

            //Will throw an exception as anonymous types are not serializable.
            //object testObj = new { name = "Stone Idol", value = 500000.00M };
            //settings.SaveSetting<object>("@apComplexTypeSetting", testObj);

            //Get settings
            Console.WriteLine("string setting:");
            Console.WriteLine(settings.GetSetting<string>("@apTestStringSetting"));

            Console.WriteLine("decimal setting:");
            Console.WriteLine(settings.GetSetting<decimal>("@apTestDecimalSetting"));

            Console.WriteLine("empty string:");
            Console.WriteLine(settings.GetSetting<string>("@apTestNullSetting"));

            Console.WriteLine("Object as a setting:");
            Console.WriteLine(settings.GetSetting<Artifact>("@apTestArtifactSetting").Name);

            Console.WriteLine("default value used:");
            Console.WriteLine(settings.GetSetting<string>("@apNoSuchSetting", "empty string"));

            Console.WriteLine("Hit any key to exit...");
            Console.ReadKey();

        }
    }
}
