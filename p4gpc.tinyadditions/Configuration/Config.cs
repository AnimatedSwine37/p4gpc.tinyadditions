using p4gpc.tinyadditions.Configuration.Implementation;
using System.ComponentModel;
using static p4gpc.tinyadditions.Utils;

namespace p4gpc.tinyadditions.Configuration
{
    public class Config : Configurable<Config>
    {
        /*
            User Properties:
                - Please put all of your configurable properties here.
                - Tip: Consider using the various available attributes https://stackoverflow.com/a/15051390/11106111
        
            By default, configuration saves as "Config.json" in mod folder.    
            Need more config files/classes? See Configuration.cs
        */

        [DisplayName("Sprint Button")]
        [Description("The button you press to toggle sprint. This does not overwrite the button's function but adds to it " +
            "(e.g. if you were to set the menu button it would toggle sprint and open the menu)")]
        public Input SprintButton { get; set; } = Input.Circle;

        [DisplayName("Sprint")]
        [Description("Enables you to sprint, slightly increasing your run speed. Pressing the sprint button toggles it.")]
        public bool SprintEnabled { get; set; } = true;

        [DisplayName("Sprint Speed")]
        [Description("How much to multiply your speed by when sprinting (default is 1.3)")]
        public float SprintSpeed { get; set; } = 1.3f;
        [DisplayName("Auto Advance Toggle Button")]
        [Description("The button you press to toggle auto advance text. This does not overwrite the button's function but adds to it" +
            "(e.g. if you were to set the next textbox button it would toggle auto advance and go to the next textbox.)")]
        public Input AdvanceButton { get; set; } = Input.Down;
        [DisplayName("Auto Advance Toggle")]
        [Description("Enables you to toggle auto advance text.")]
        public bool AdvanceEnabled { get; set; } = true;
        [DisplayName("Debug Mode")]
        [Description("Logs additional information to the console that is useful for debugging.")]
        public bool DebugEnabled { get; set; } = false;


    }
}