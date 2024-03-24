using StardewModdingAPI;

namespace Always_On_Server.Framework;

class ModConfig
{
    public SButton serverHotKey { get; set; } = SButton.F9;

    public int profitmargin { get; set; } = 100;
    public int upgradeHouse { get; set; } = 0;
    public string petname { get; set; } = "Noodles";
    public bool farmcavechoicemushrooms { get; set; } = true;
    public bool communitycenterrun { get; set; } = true;
    public int timeOfDayToSleep { get; set; } = 2200;
    public bool festivalsOn { get; set; } = true;
    public int endofdayTimeOut { get; set; } = 300;

}