﻿using p4gpc.tinyadditions.Configuration.Implementation;
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

        [DisplayName("Sprint Only In Dungeons")]
        [Description("If enabled you will only be able to sprint when in dungeons.")]
        public bool SprintDungeonsOnly { get; set; } = false;

        [DisplayName("Sprint Speed")]
        [Description("How much to multiply your speed by when sprinting (default is 1.3)")]
        public float SprintSpeed { get; set; } = 1.3f;

        [DisplayName("Toggle Sprint")]
        [Description("If enabled pressing the sprint button will toggle sprint, otherwise you must hold it.")]
        public bool SprintToggle { get; set; } = false;

        [DisplayName("Auto Advance Toggle Button")]
        [Description("The button you press to toggle auto advance text. This does not overwrite the button's function but adds to it" +
            "(e.g. if you were to set the next textbox button it would toggle auto advance and go to the next textbox.)")]
        public Input AdvanceButton { get; set; } = Input.Down;
        
        [DisplayName("Auto Advance Toggle")]
        [Description("Enables you to toggle auto advance text.")]
        public bool AdvanceEnabled { get; set; } = true;

        [DisplayName("Easy Bug Catching")]
        [Description("Makes it so you will always get a perfect catch when catching bugs at the shrine.")]
        public bool EasyBugCatchingEnabled { get; set; } = true;

        [DisplayName("Social Link Affinity Boost")]
        [Description("Makes it so you also get the boosted social link affinity that you normal get from having a " +
            "Persona of the matching Arcana if you have the link's max rank item from a previous playthrough. (This does not stack with the matching Arcana bonus, only one will ever be applied)")]
        public bool AffinityBoostEnabled { get; set; } = true;

        [DisplayName("Social Link Always Boosted Affinity")]
        [Description("Makes it so you always get the matching Arcana boosted social link affinity for every link. " +
            "(The matching arcana message may not display in game, however, the bonus is being applied in the background)")]
        public bool AlwaysBoostedAffinity { get; set; } = false;


        [DisplayName("Debug Mode")]
        [Description("Logs additional information to the console that is useful for debugging.")]
        public bool DebugEnabled { get; set; } = false;
    }
}
