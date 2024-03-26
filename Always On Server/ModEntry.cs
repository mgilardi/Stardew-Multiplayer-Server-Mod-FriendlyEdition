using Always_On_Server.Framework;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewModdingAPI.Utilities;
using StardewValley;
using StardewValley.Locations;
using StardewValley.Menus;
using SObject = StardewValley.Object;

namespace Always_On_Server;

public class ModEntry : Mod
{
    private static ModEntry? modInstance;

    /// <summary>The mod configuration from the player.</summary>
    private ModConfig Config;

    private int numPlayers; //stores number of players
    private bool IsEnabled; //stores if the the server mod is enabled 
    private bool forcePaused = false;
    public int bedX;
    public int bedY;
    public bool clientPaused;

    //debug tools
    private bool debug;
    private bool shippingMenuActive;

    private readonly Dictionary<string, int>
        PreviousFriendships = new Dictionary<string, int>(); //stores friendship values

    private bool eventCommandUsed;

    private bool eggHuntAvailable; //is egg festival ready start timer for triggering eggHunt Event
    private bool flowerDanceAvailable;
    private bool luauSoupAvailable;
    private bool jellyDanceAvailable;
    private bool grangeDisplayAvailable;
    private bool goldenPumpkinAvailable;
    private bool iceFishingAvailable;
    private bool winterFeastAvailable;

    //variables for current time and date
    int currentTime = Game1.timeOfDay;
    SDate currentDate = SDate.Now();
    SDate eggFestival = new SDate(13, "spring");
    SDate dayAfterEggFestival = new SDate(14, "spring");
    SDate flowerDance = new SDate(24, "spring");
    SDate luau = new SDate(11, "summer");
    SDate danceOfJellies = new SDate(28, "summer");
    SDate stardewValleyFair = new SDate(16, "fall");
    SDate spiritsEve = new SDate(27, "fall");
    SDate festivalOfIce = new SDate(8, "winter");
    SDate feastOfWinterStar = new SDate(25, "winter");

    SDate grampasGhost = new SDate(1, "spring", 3);
    ///////////////////////////////////////////////////////


    //variables for timeout reset code

    private int timeOutTicksForReset;
    private int shippingMenuTimeoutTicks;


    SDate currentDateForReset = SDate.Now();
    SDate danceOfJelliesForReset = new SDate(28, "summer");

    SDate spiritsEveForReset = new SDate(27, "fall");
    //////////////////////////

    private List<string> queuedCommands = new();

    public ModEntry()
    {
        modInstance = this;
        Config = new();
    }


    public override void Entry(IModHelper helper)
    {
        IsEnabled = false;
        Config = Helper.ReadConfig<ModConfig>();

        var harmony = new Harmony(ModManifest.UniqueID);
        // chat commands patch
        harmony.Patch(
            original: AccessTools.Method(typeof(ChatBox), nameof(ChatBox.receiveChatMessage)),
            prefix: new HarmonyMethod(typeof(ModEntry), nameof(ChatBox_receiveChatMessage_Prefix))
        );
        // level up patch
        harmony.Patch(
            original: AccessTools.Method(typeof(Farmer), nameof(Farmer.gainExperience)),
            prefix: new HarmonyMethod(typeof(ModEntry), nameof(Farmer_gainExperience_Prefix))
        );

        helper.ConsoleCommands.Add("server", "Toggles headless server on/off", ServerToggle);
        helper.ConsoleCommands.Add("debug_server",
            "Turns debug mode on/off, lets server run when no players are connected", DebugToggle);

        helper.Events.GameLoop.Saving += OnSaving!; // Shipping Menu handler
        helper.Events.GameLoop.OneSecondUpdateTicked += OnOneSecondUpdateTicked!; //game tick event handler
        helper.Events.GameLoop.TimeChanged += OnTimeChanged!; // Time of day change handler
        helper.Events.GameLoop.UpdateTicked +=
            OnUpdateTicked!; //handles various events that should occur as soon as they are available
        helper.Events.Input.ButtonPressed += OnButtonPressed!;
        helper.Events.Display.Rendered += OnRendered!;
        helper.Events.Specialized.UnvalidatedUpdateTicked +=
            OnUnvalidatedUpdateTick!; //used bc only thing that gets throug save window
    }

    static bool ChatBox_receiveChatMessage_Prefix(ChatBox __instance,
        long sourceFarmer,
        int chatKind,
        LocalizedContentManager.LanguageCode language,
        string message)
    {
        if (modInstance!.IsEnabled && message.StartsWith("!"))
        {
            modInstance.HandleCommandLater(message);
        }

        return true;
    }

    static bool Farmer_gainExperience_Prefix(Farmer __instance, int which, int howMuch)
    {
        if (modInstance!.IsEnabled && __instance.IsLocalPlayer)
        {
            return false; // do not gain experience when in server mode
        }

        return true;
    }

    private void HandleCommandLater(string message)
    {
        queuedCommands.Add(message);
    }


    private void HandleCommand(string message)
    {
        if (message == null)
        {
            return;
        }

        switch (message)
        {
            case "!sleep" when currentTime >= Config.timeOfDayToSleep:
                GoToBed();
                SendChatMessage("Trying to go to bed.");
                break;
            case "!sleep":
                SendChatMessage("It's too early.");
                SendChatMessage($"Try after {Config.timeOfDayToSleep}.");
                break;
            case "!festival":
            {
                SendChatMessage("Trying to go to Festival.");

                if (currentDate == eggFestival)
                {
                    EggFestival();
                }
                else if (currentDate == flowerDance)
                {
                    FlowerDance();
                }
                else if (currentDate == luau)
                {
                    Luau();
                }
                else if (currentDate == danceOfJellies)
                {
                    DanceOfTheMoonlightJellies();
                }
                else if (currentDate == stardewValleyFair)
                {
                    StardewValleyFair();
                }
                else if (currentDate == spiritsEve)
                {
                    SpiritsEve();
                }
                else if (currentDate == festivalOfIce)
                {
                    FestivalOfIce();
                }
                else if (currentDate == feastOfWinterStar)
                {
                    FeastOfWinterStar();
                }
                else
                {
                    SendChatMessage("Festival Not Ready.");
                }

                break;
            }
            case "!event":
            case "!continue":
            {
                if (Game1.CurrentEvent != null && Game1.CurrentEvent.isFestival)
                {
                    if (currentDate == eggFestival)
                    {
                        eventCommandUsed = true;
                        eggHuntAvailable = true;
                    }
                    else if (currentDate == flowerDance)
                    {
                        eventCommandUsed = true;
                        flowerDanceAvailable = true;
                    }
                    else if (currentDate == luau)
                    {
                        eventCommandUsed = true;
                        luauSoupAvailable = true;
                    }
                    else if (currentDate == danceOfJellies)
                    {
                        eventCommandUsed = true;
                        jellyDanceAvailable = true;
                    }
                    else if (currentDate == stardewValleyFair)
                    {
                        eventCommandUsed = true;
                        grangeDisplayAvailable = true;
                    }
                    else if (currentDate == spiritsEve)
                    {
                        eventCommandUsed = true;
                        goldenPumpkinAvailable = true;
                    }
                    else if (currentDate == festivalOfIce)
                    {
                        eventCommandUsed = true;
                        iceFishingAvailable = true;
                    }
                    else if (currentDate == feastOfWinterStar)
                    {
                        eventCommandUsed = true;
                        winterFeastAvailable = true;
                    }
                }
                else
                {
                    SendChatMessage("I'm not at a Festival.");
                }

                break;
            }
            case "!leave" when Game1.CurrentEvent != null && Game1.CurrentEvent.isFestival:
                SendChatMessage("Trying to leave Festival");
                LeaveFestival();
                break;
            case "!leave":
                SendChatMessage("I'm not at a Festival.");
                break;
            case "!unstick" when Game1.player.currentLocation is FarmHouse:
                SendChatMessage("Warping to Farm.");
                WarpToFarm();
                break;
            case "!unstick":
                SendChatMessage("Warping inside house.");
                GetBedCoordinates();
                Game1.warpFarmer("Farmhouse", bedX, bedY, false);
                break;
        }
    }

    //debug for running with no one online
    private void DebugToggle(string command, string[] args)
    {
        if (Context.IsWorldReady)
        {
            debug = !debug;
            Monitor.Log($"Server Debug {(debug ? "On" : "Off")}", LogLevel.Info);
        }
    }

    //draw textbox rules
    public static void DrawTextBox(int x, int y, SpriteFont font, string message, int align = 0,
        float colorIntensity = 1f)
    {
        SpriteBatch spriteBatch = Game1.spriteBatch;
        int width = (int)font.MeasureString(message).X + 32;
        int num = (int)font.MeasureString(message).Y + 21;
        switch (align)
        {
            case 0:
                IClickableMenu.drawTextureBox(spriteBatch, Game1.menuTexture, new Rectangle(0, 256, 60, 60), x, y,
                    width, num + 4, Color.White * colorIntensity);
                Utility.drawTextWithShadow(spriteBatch, message, font, new Vector2(x + 16, y + 16), Game1.textColor);
                break;
            case 1:
                IClickableMenu.drawTextureBox(spriteBatch, Game1.menuTexture, new Rectangle(0, 256, 60, 60),
                    x - width / 2, y, width, num + 4, Color.White * colorIntensity);
                Utility.drawTextWithShadow(spriteBatch, message, font, new Vector2(x + 16 - width / 2, y + 16),
                    Game1.textColor);
                break;
            case 2:
                IClickableMenu.drawTextureBox(spriteBatch, Game1.menuTexture, new Rectangle(0, 256, 60, 60), x - width,
                    y, width, num + 4, Color.White * colorIntensity);
                Utility.drawTextWithShadow(spriteBatch, message, font, new Vector2(x + 16 - width, y + 16),
                    Game1.textColor);
                break;
        }
    }

    /// <summary>Raised after the game draws to the sprite patch in a draw tick, just before the final sprite batch is rendered to the screen.</summary>
    /// <param name="sender">The event sender.</param>
    /// <param name="e">The event data.</param>
    private void OnRendered(object sender, RenderedEventArgs e)
    {
        //draw a textbox in the top left corner saying Server On
        if (Game1.options.enableServer && IsEnabled && Game1.server != null)
        {
            int connectionsCount = Game1.server.connectionsCount;
            DrawTextBox(5, 100, Game1.dialogueFont, "Server Mode On");
            DrawTextBox(5, 180, Game1.dialogueFont, $"Press {Config.serverHotKey} On/Off");
            int profitMargin = Config.profitmargin;
            DrawTextBox(5, 260, Game1.dialogueFont, $"Profit Margin: {profitMargin}%");
            DrawTextBox(5, 340, Game1.dialogueFont, $"{connectionsCount} Players Online");
        }
    }


    // toggles server on/off with console command "server"
    private void ServerToggle(string command, string[] args)
    {
        if (Context.IsWorldReady)
        {
            if (!IsEnabled)
            {
                Helper.ReadConfig<ModConfig>();
                IsEnabled = true;


                Monitor.Log("Server Mode On!", LogLevel.Info);
                SendChatMessage("The Host is in Server Mode!");

                Game1.displayHUD = true;
                Game1.addHUDMessage(new HUDMessage("Server Mode On!"));

                Game1.options.pauseWhenOutOfFocus = false;
            }
            else
            {
                IsEnabled = false;
                Monitor.Log("The server off!", LogLevel.Info);

                SendChatMessage("The Host has returned!");

                Game1.displayHUD = true;
                Game1.addHUDMessage(new HUDMessage("Server Mode Off!"));
            }
        }
    }

    /// <summary>Raised after the player presses a button on the keyboard, controller, or mouse.</summary>
    /// <param name="sender">The event sender.</param>
    /// <param name="e">The event data.</param>
    private void OnButtonPressed(object sender, ButtonPressedEventArgs e)
    {
        //toggles server on/off with configurable hotkey
        if (Context.IsWorldReady)
        {
            if (e.Button == Config.serverHotKey)
            {
                ServerToggle("server", new string[0]);
            }

            if (IsEnabled)
            {
                Helper.Input.Suppress(e.Button);
            }
        }
    }


    private int skipCooldown;

    /// <summary>Raised once per second after the game state is updated.</summary>
    /// <param name="sender">The event sender.</param>
    /// <param name="e">The event data.</param>
    private void OnOneSecondUpdateTicked(object sender, OneSecondUpdateTickedEventArgs e)
    {
        if (!IsEnabled) // server toggle
        {
            if (forcePaused)
            {
                Game1.isTimePaused = false;
                forcePaused = false;
            }

            if (clientPaused)
            {
                Game1.netWorldState.Value.IsTimePaused = false;
                clientPaused = false;
            }

            return;
        }


        NoClientsPause();

        //left click menu spammer and event skipper to get through random events happening
        //also moves player around, this seems to free host from random bugs sometimes
        if (Game1.activeClickableMenu is DialogueBox)
        {
            Game1.activeClickableMenu.receiveLeftClick(10, 10);
        }

        if (skipCooldown != 0)
        {
            skipCooldown--;
        }

        if (Game1.CurrentEvent != null && Game1.CurrentEvent.skippable)
        {
            if (skipCooldown == 0)
            {
                Game1.CurrentEvent.skipEvent();
                skipCooldown = 10;
            }
        }
        /*if (!playerMovedRight && Game1.player.canMove)
        {
            Game1.player.tryToMoveInDirection(1, true, 0, false);
            playerMovedRight = true;
        }
        else if (playerMovedRight && Game1.player.canMove)
        {
            Game1.player.tryToMoveInDirection(3, true, 0, false);
            playerMovedRight = false;
        }*/


        //disable friendship decay
        if (PreviousFriendships.Any())
        {
            foreach (string key in Game1.player.friendshipData.Keys)
            {
                Friendship friendship = Game1.player.friendshipData[key];
                if (PreviousFriendships.TryGetValue(key, out int oldPoints) && oldPoints > friendship.Points)
                    friendship.Points = oldPoints;
            }
        }

        PreviousFriendships.Clear();
        foreach (var pair in Game1.player.friendshipData.FieldDict)
            PreviousFriendships[pair.Key] = pair.Value.Value.Points;


        //eggHunt event
        if (eggHuntAvailable && IsInFestival())
        {
            if (eventCommandUsed)
            {
                Game1.CurrentEvent!.answerDialogueQuestion(Game1.getCharacterFromName("Lewis"), "yes");
                eventCommandUsed = false;
            }
        }


        //flowerDance event
        if (flowerDanceAvailable && IsInFestival())
        {
            if (eventCommandUsed)
            {
                Game1.CurrentEvent!.answerDialogueQuestion(Game1.getCharacterFromName("Lewis"), "yes");
                eventCommandUsed = false;
            }
        }

        //luauSoup event
        if (luauSoupAvailable && IsInFestival())
        {
            if (eventCommandUsed)
            {
                //add iridium starfruit to soup
                var item = ItemRegistry.Create("(O)268", 1, 3);
                new Event().addItemToLuauSoup(item, Game1.player);
                Game1.CurrentEvent!.answerDialogueQuestion(Game1.getCharacterFromName("Lewis"), "yes");
                eventCommandUsed = false;
            }
        }

        //Dance of the Moonlight Jellies event
        if (jellyDanceAvailable && IsInFestival())
        {
            if (eventCommandUsed)
            {
                Game1.CurrentEvent!.answerDialogueQuestion(Game1.getCharacterFromName("Lewis"), "yes");
                eventCommandUsed = false;
            }
        }

        //Grange Display event
        if (grangeDisplayAvailable && IsInFestival())
        {
            if (eventCommandUsed)
            {
                Game1.CurrentEvent!.answerDialogueQuestion(Game1.getCharacterFromName("Lewis"), "yes");
                eventCommandUsed = false;
            }
        }

        //golden pumpkin maze event
        if (goldenPumpkinAvailable && IsInFestival())
        {
        }

        //ice fishing event
        if (iceFishingAvailable && IsInFestival())
        {
            if (eventCommandUsed)
            {
                Game1.CurrentEvent!.answerDialogueQuestion(Game1.getCharacterFromName("Lewis"), "yes");
                eventCommandUsed = false;
            }
        }

        //Feast of the Winter event
        if (winterFeastAvailable && IsInFestival())
        {
        }
    }


    //Pause game if no clients Code
    private void NoClientsPause()
    {
        numPlayers = Game1.otherFarmers.Count;

        if (numPlayers >= 1 || debug)
        {
            if (clientPaused)
            {
                forcePaused = true;
                Game1.netWorldState.Value.IsTimePaused = true;
            }
            else
            {
                Game1.isTimePaused = false;
            }
        }
        else if (numPlayers <= 0)
        {
            forcePaused = true;
            Game1.isTimePaused = true;
        }
    }


    /// <summary>Raised after the game state is updated (≈60 times per second).</summary>
    /// <param name="sender">The event sender.</param>
    /// <param name="e">The event data.</param>
    private void OnUpdateTicked(object sender, UpdateTickedEventArgs e)
    {
        if (!IsEnabled)
        {
            return;
        }

        if (queuedCommands.Count > 0)
        {
            foreach (var s in queuedCommands)
            {
                HandleCommand(s);
            }

            queuedCommands.Clear();
        }

        //petchoice
        if (!Game1.player.hasPet())
        {
            Helper.Reflection.GetMethod(new Event(), "namePet").Invoke(Config.petname.Substring(0));
        }

        //cave choice unlock 
        if (!Game1.player.eventsSeen.Contains("65"))
        {
            Game1.player.eventsSeen.Add("65");


            if (Config.farmcavechoicemushrooms)
            {
                Game1.MasterPlayer.caveChoice.Value = 2;
                (Game1.getLocationFromName("FarmCave") as FarmCave)!.setUpMushroomHouse();
            }
            else
            {
                Game1.MasterPlayer.caveChoice.Value = 1;
            }
        }

        //community center unlock
        if (!Game1.player.eventsSeen.Contains("611439"))
        {
            Game1.player.eventsSeen.Add("611439");
            Game1.MasterPlayer.mailReceived.Add("ccDoorUnlock");
        }

        if (Config.upgradeHouse != 0 && Game1.player.HouseUpgradeLevel != Config.upgradeHouse)
        {
            Game1.player.HouseUpgradeLevel = Config.upgradeHouse;
        }

        // just turns off server mod if the game gets exited back to title screen
        if (Game1.activeClickableMenu is TitleMenu)
        {
            IsEnabled = false;
        }
    }


    /// <summary>Raised after the in-game clock time changes.</summary>
    /// <param name="sender">The event sender.</param>
    /// <param name="e">The event data.</param>
    public void OnTimeChanged(object sender, TimeChangedEventArgs e)
    {
        if (!IsEnabled)
        {
            return;
        }

        // auto-sleep and Holiday code
        currentTime = Game1.timeOfDay;
        currentDate = SDate.Now();
        eggFestival = new SDate(13, "spring");
        dayAfterEggFestival = new SDate(14, "spring");
        flowerDance = new SDate(24, "spring");
        luau = new SDate(11, "summer");
        danceOfJellies = new SDate(28, "summer");
        stardewValleyFair = new SDate(16, "fall");
        spiritsEve = new SDate(27, "fall");
        festivalOfIce = new SDate(8, "winter");
        feastOfWinterStar = new SDate(25, "winter");
        grampasGhost = new SDate(1, "spring", 3);
        if (Config.festivalsOn)
        {
            if (currentDate == eggFestival &&
                (numPlayers >= 1 ||
                 debug)) //set back to 1 after testing~!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!
            {
                if (currentTime == 630)
                {
                    SendChatMessage("Egg Festival Today!");
                    SendChatMessage("I will not be in bed until after 2:00 P.M.");
                }

                EggFestival();
            }

            //flower dance message changed to disabled bc it causes crashes
            else if (currentDate == flowerDance && numPlayers >= 1)
            {
                if (currentTime == 630)
                {
                    SendChatMessage("Flower Dance Today.");
                    SendChatMessage("I will not be in bed until after 2:00 P.M.");
                }

                FlowerDance();
            }

            else if (currentDate == luau && numPlayers >= 1)
            {
                if (currentTime == 630)
                {
                    SendChatMessage("Luau Today!");
                    SendChatMessage("I will not be in bed until after 2:00 P.M.");
                }

                Luau();
            }

            else if (currentDate == danceOfJellies && numPlayers >= 1)
            {
                if (currentTime == 630)
                {
                    SendChatMessage("Dance of the Moonlight Jellies Tonight!");
                    SendChatMessage("I will not be in bed until after 12:00 A.M.");
                }

                DanceOfTheMoonlightJellies();
            }

            else if (currentDate == stardewValleyFair && numPlayers >= 1)
            {
                if (currentTime == 630)
                {
                    SendChatMessage("Stardew Valley Fair Today!");
                    SendChatMessage("I will not be in bed until after 3:00 P.M.");
                }

                StardewValleyFair();
            }

            else if (currentDate == spiritsEve && numPlayers >= 1)
            {
                if (currentTime == 630)
                {
                    SendChatMessage("Spirit's Eve Tonight!");
                    SendChatMessage("I will not be in bed until after 12:00 A.M.");
                }

                SpiritsEve();
            }

            else if (currentDate == festivalOfIce && numPlayers >= 1)
            {
                if (currentTime == 630)
                {
                    SendChatMessage("Festival of Ice Today!");
                    SendChatMessage("I will not be in bed until after 2:00 P.M.");
                }

                FestivalOfIce();
            }

            else if (currentDate == feastOfWinterStar && numPlayers >= 1)
            {
                if (currentTime == 630)
                {
                    SendChatMessage("Feast of the Winter Star Today!");
                    SendChatMessage("I will not be in bed until after 2:00 P.M.");
                }

                FeastOfWinterStar();
            }
        }

        //handles various events that the host normally has to click through

        if (currentDate != eggFestival && currentDate != flowerDance &&
            currentDate != luau && currentDate != danceOfJellies && currentDate != stardewValleyFair &&
            currentDate != spiritsEve && currentDate != festivalOfIce && currentDate != feastOfWinterStar)
        {
            if (currentTime == 620)
            {
                for (int i = 0; i < 10; i++)
                {
                    //check mail 10 a day
                    Game1.currentLocation.mailbox();
                }
            }

            if (currentTime == 630)
            {
                //rustkey-sewers unlock
                if (!Game1.player.hasRustyKey)
                {
                }


                //community center complete
                if (Config.communitycenterrun)
                {
                }

                //Joja run 
                if (!Config.communitycenterrun)
                {
                }
            }

            //go outside
            if (currentTime == 640)
            {
                WarpToFarm();
            }

            //get fishing rod (standard spam clicker will get through cutscene)
            if (currentTime == 900 && !Game1.player.eventsSeen.Contains("739330"))
            {
                // Game1.player.increaseBackpackSize(1);
                Game1.warpFarmer("Beach", 1, 20, 1);
            }

            if (currentTime >= Config.timeOfDayToSleep && numPlayers >= 1)
            {
                GoToBed();
            }
        }
    }

    public void EggFestival()
    {
        if (currentTime >= 900 && currentTime <= 1400)
        {
            //teleports to egg festival
            Game1.netReady.SetLocalReady("festivalStart", true);
            Game1.activeClickableMenu = new ReadyCheckDialog("festivalStart", true, who =>
            {
                Game1.exitActiveMenu();
                Game1.warpFarmer("Town", 1, 20, 1);
            });

            eggHuntAvailable = true;
        }
        else if (currentTime >= 1410)
        {
            eggHuntAvailable = false;
            Game1.options.setServerMode("online");
            GoToBed();
        }
    }


    public void FlowerDance()
    {
        if (currentTime >= 900 && currentTime <= 1400)
        {
            Game1.netReady.SetLocalReady("festivalStart", true);
            Game1.activeClickableMenu = new ReadyCheckDialog("festivalStart", true, who =>
            {
                Game1.exitActiveMenu();
                Game1.warpFarmer("Forest", 1, 20, 1);
            });

            flowerDanceAvailable = true;
        }
        else if (currentTime >= 1410 && currentTime >= Config.timeOfDayToSleep)
        {
            flowerDanceAvailable = false;
            Game1.options.setServerMode("online");
            GoToBed();
        }
    }

    public void Luau()
    {
        if (currentTime >= 900 && currentTime <= 1400)
        {
            Game1.netReady.SetLocalReady("festivalStart", true);
            Game1.activeClickableMenu = new ReadyCheckDialog("festivalStart", true, who =>
            {
                Game1.exitActiveMenu();
                Game1.warpFarmer("Beach", 1, 20, 1);
            });

            luauSoupAvailable = true;
        }
        else if (currentTime >= 1410)
        {
            luauSoupAvailable = false;
            Game1.options.setServerMode("online");
            GoToBed();
        }
    }

    public void DanceOfTheMoonlightJellies()
    {
        if (currentTime >= 2200 && currentTime <= 2400)
        {
            Game1.netReady.SetLocalReady("festivalStart", true);
            Game1.activeClickableMenu = new ReadyCheckDialog("festivalStart", true, who =>
            {
                Game1.exitActiveMenu();
                Game1.warpFarmer("Beach", 1, 20, 1);
            });

            jellyDanceAvailable = true;
        }
        else if (currentTime >= 2410)
        {
            jellyDanceAvailable = false;
            Game1.options.setServerMode("online");
            GoToBed();
        }
    }

    public void StardewValleyFair()
    {
        if (currentTime >= 900 && currentTime <= 1500)
        {
            Game1.netReady.SetLocalReady("festivalStart", true);
            Game1.activeClickableMenu = new ReadyCheckDialog("festivalStart", true, who =>
            {
                Game1.exitActiveMenu();
                Game1.warpFarmer("Town", 1, 20, 1);
            });

            grangeDisplayAvailable = true;
        }
        else if (currentTime >= 1510)
        {
            Game1.displayHUD = true;
            grangeDisplayAvailable = false;
            Game1.options.setServerMode("online");

            GoToBed();
        }
    }

    public void SpiritsEve()
    {
        if (currentTime >= 2200 && currentTime <= 2350)
        {
            Game1.netReady.SetLocalReady("festivalStart", true);
            Game1.activeClickableMenu = new ReadyCheckDialog("festivalStart", true, who =>
            {
                Game1.exitActiveMenu();
                Game1.warpFarmer("Town", 1, 20, 1);
            });

            goldenPumpkinAvailable = true;
        }
        else if (currentTime >= 2400)
        {
            Game1.displayHUD = true;
            goldenPumpkinAvailable = false;
            Game1.options.setServerMode("online");
            GoToBed();
        }
    }

    public void FestivalOfIce()
    {
        if (currentTime >= 900 && currentTime <= 1400)
        {
            Game1.netReady.SetLocalReady("festivalStart", true);
            Game1.activeClickableMenu = new ReadyCheckDialog("festivalStart", true, who =>
            {
                Game1.exitActiveMenu();
                Game1.warpFarmer("Forest", 1, 20, 1);
            });

            iceFishingAvailable = true;
        }
        else if (currentTime >= 1410)
        {
            iceFishingAvailable = false;
            Game1.options.setServerMode("online");
            GoToBed();
        }
    }

    public void FeastOfWinterStar()
    {
        if (currentTime >= 900 && currentTime <= 1400)
        {
            Game1.netReady.SetLocalReady("festivalStart", true);
            Game1.activeClickableMenu = new ReadyCheckDialog("festivalStart", true, who =>
            {
                Game1.exitActiveMenu();
                Game1.warpFarmer("Town", 1, 20, 1);
            });

            winterFeastAvailable = true;
        }
        else if (currentTime >= 1410)
        {
            winterFeastAvailable = false;
            Game1.options.setServerMode("online");
            GoToBed();
        }
    }


    private void GetBedCoordinates()
    {
        Point bedSpot = (Game1.getLocationFromName("FarmHouse") as FarmHouse)!.GetPlayerBedSpot();
        bedX = bedSpot.X;
        bedY = bedSpot.Y;
    }

    private void GoToBed()
    {
        GetBedCoordinates();

        Game1.warpFarmer("Farmhouse", bedX, bedY, false);

        Helper.Reflection.GetMethod(Game1.currentLocation, "startSleep").Invoke();
        Game1.displayHUD = true;
    }

    /// <summary>Raised before the game begins writes data to the save file (except the initial save creation).</summary>
    /// <param name="sender">The event sender.</param>
    /// <param name="e">The event data.</param>
    private void OnSaving(object sender, SavingEventArgs e)
    {
        if (!IsEnabled) // server toggle
            return;

        // shipping menu "OK" click through code
        Monitor.Log("This is the Shipping Menu");
        shippingMenuActive = true;
        if (Game1.activeClickableMenu is ShippingMenu)
        {
            try
            {
                Helper.Reflection.GetMethod(Game1.activeClickableMenu, "okClicked").Invoke();
            }
            catch (Exception)
            {
                if (Game1.activeClickableMenu is ShippingMenu menu)
                {
                    Rectangle bounds = menu.okButton.bounds;
                    menu.receiveLeftClick(bounds.X + 3, bounds.Y + 3);
                }
            }
        }
    }

    /// <summary>Raised after the game state is updated (≈60 times per second), regardless of normal SMAPI validation. This event is not thread-safe and may be invoked while game logic is running asynchronously. Changes to game state in this method may crash the game or corrupt an in-progress save. Do not use this event unless you're fully aware of the context in which your code will be run. Mods using this event will trigger a stability warning in the SMAPI console.</summary>
    /// <param name="sender">The event sender.</param>
    /// <param name="e">The event data.</param>
    private void OnUnvalidatedUpdateTick(object sender, UnvalidatedUpdateTickedEventArgs e)
    {
        if (!IsEnabled)
        {
            return;
        }

        //resets server connection after certain amount of time end of day
        if (Game1.timeOfDay >= Config.timeOfDayToSleep || Game1.timeOfDay == 600 &&
            currentDateForReset != danceOfJelliesForReset && currentDateForReset != spiritsEveForReset &&
            Config.endofdayTimeOut != 0)
        {
            timeOutTicksForReset += 1;
            var countdowntoreset = (2600 - Config.timeOfDayToSleep) * .01 * 6 * 7 * 60;
            if (timeOutTicksForReset >= (countdowntoreset + (Config.endofdayTimeOut * 60)))
            {
                Game1.options.setServerMode("offline");
            }
        }

        if (currentDateForReset == danceOfJelliesForReset ||
            currentDateForReset == spiritsEveForReset && Config.endofdayTimeOut != 0)
        {
            if (Game1.timeOfDay is >= 2400 or 600)
            {
                timeOutTicksForReset += 1;
                if (timeOutTicksForReset >= (5040 + (Config.endofdayTimeOut * 60)))
                {
                    Game1.options.setServerMode("offline");
                }
            }
        }

        if (shippingMenuActive && Config.endofdayTimeOut != 0)
        {
            shippingMenuTimeoutTicks += 1;
            if (shippingMenuTimeoutTicks >= Config.endofdayTimeOut * 60)
            {
                Game1.options.setServerMode("offline");
            }
        }

        if (Game1.timeOfDay == 610)
        {
            shippingMenuActive = false;
            Game1.player.difficultyModifier = Config.profitmargin * .01f;

            Game1.options.setServerMode("online");
            timeOutTicksForReset = 0;
            shippingMenuTimeoutTicks = 0;
        }

        if (Game1.timeOfDay == 2600)
        {
            Game1.isTimePaused = false;
        }
    }

    /// <summary>Send a chat message.</summary>
    /// <param name="message">The message text.</param>
    private void SendChatMessage(string message)
    {
        Game1.chatBox.activate();
        Game1.chatBox.setText(message);
        Game1.chatBox.chatBox.RecieveCommandInput('\r');
    }

    /// <summary>Leave the current festival, if any.</summary>
    private void LeaveFestival()
    {
        Game1.netReady.SetLocalReady("festivalEnd", true);
        Game1.activeClickableMenu = new ReadyCheckDialog("festivalEnd", true, who =>
        {
            GetBedCoordinates();
            Game1.exitActiveMenu();
            Game1.warpFarmer("Farmhouse", bedX, bedY, false);
            Game1.timeOfDay = currentDate == spiritsEve ? 2400 : 2200;
            Game1.shouldTimePass();
        });
    }

    private void WarpToFarm()
    {
        Point farmhousePos = Game1.getFarm().GetMainFarmHouseEntry();
        Game1.warpFarmer("Farm", farmhousePos.X, farmhousePos.Y, false);
    }

    private bool IsInFestival()
    {
        return Game1.CurrentEvent is { isFestival: true };
    }
}