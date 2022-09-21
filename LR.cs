using System;
using System.Linq;
using System.Collections.Generic;
using System.Text.RegularExpressions;

using RBot;
using RBot.Options;
using System.Windows.Forms;
using System.Threading;

/// <changelog>
/// v.1.0   - Created base bot
/// v.1.1   - fixed misplaced fealty req qty
///         - fixed part where it wont reaccept fealty quest after a single run
///         - added fps changer to 10 when running. returns to 30 after exit
/// v.1.2   - used SafeMapJoin
///         - added complete quest if possible on start of fealties
///         - Cleaned MessageChats and added timestamp     
///         - Added death wait of 12 seconds
///         - changed death wait code to a handler that simply waits
///         - Fix death lag freeze
/// v.1.3   - Added other drops option
///         - Reworked Engine QuestBolt and added PacketRun
///         - Added a version checker
///         - Fixed fps counter closing on and off each run. 
///         - Reworked MainLoop and turned it into a thread so that if the 
///           player dies the bot will not reopen the Bot.Configure UI again.
///         - Added monster toggle for performance
///         - fixed ExitHandler not working (separated it to a different thread)
///         - Fixed Config Ui restarting after relog
/// v.1.4   - reworked QuestBolt and packetrun. though its packetrun still dead
///         - fixed skill not running after relog
///         - Fixed a little issue with LF3 not reaccepting
///         - added class picking shit
///         - removed prefarm mode. lol can't be arsed.
///         - fixed broken completetion


/// <note> Para sa mga may madaming pangangailangan.
///        Edit this only if you want to be specific at classes
///        Otherwise, DO NOT edit anything else. Just run the script.
///        It'll open the ScriptOption UI for you to choose the settings.
public class ClassSpecification {
   // Value:  "solo"      :soloing class
   //         "solo-dot"  :use DoT if DoT default enabled. solo class otherwise
   //         "farm"      :use farming class
   //         "farm-fb"   :use FB if FB default enabled. farm class otherwise
   //         "tethered"  :class for tethered farming

   // LF1 Classes
   public string Aeacus = "solo-dot";
   public string Tethered = "tethered";
   public string Darkened = "farm";
   public string Dracolich  = "farm";

   // LF2 Classes
   public string Grim = "farm";
   public string Ancient = "farm";
   public string Pirate = "farm";
   public string Battleon = "farm-fb";
   public string Mirror = "farm-fb";
   public string Darkblood = "farm";
   public string Vampire = "farm";
   public string Spirit = "farm";
   public string Dragon = "farm-fb";
   public string Doomwood = "farm-fb";

   // LF3 Classes
   public string DageFavor = "farm";
   public string Emblem = "farm";
   public string DarkToken = "farm-fb";

   // Legion Token
   public string DreadRock = "farm-fb";
   public string ShogunParagon = "farm";
   public string BrightParagon = "farm";
   
}

/// <summary>The Data structure for a skill.</summary>
public class DataSkill {
   public string Type;
   public int Index;
   public int sValue;

   public DataSkill(string Type, int Index=0, int sValue=0) {
      this.Type = Type;
      this.Index = Index-1;
      this.sValue = sValue;
   }
}

/// <summary>The Data structure for a quest/item run.</summary>
public class DataRun {
   public string RunType;
   public string ClassType;
   public string Map;
   public string Cell;
   public string Pad;
   public int QuestID;
   public string Item;
   public int Qty;
   public bool isTemp=false;
   public bool UseBoss=false;
   public string Monster="*";
   public string Packet="";
   public int MonsterMapID;

   /// <summary>Data structure for QuestBolt </summary>
   /// <param name="ClassType"> The ClassType to use </param>
   /// <param name="Map"> The Map to use </param>
   /// <param name="Cell"> The Cell to use </param>
   /// <param name="Pad"> The pad to use </param>
   /// <param name="QuestID"> 0 by default </param>
   /// <param name="Item"> Empty by default </param>
   /// <param name="Qty"> 1 by default </param>
   /// <param name="isTemp"> false by default </param>
   /// <param name="UseBoss"> 1 by default. Refers whether to use boss map number </param>
   /// <param name="Monster"> * by default </param>
   public DataRun(string ClassType, string Map, string Cell, string Pad, int QuestID=0, string Item="", int Qty=1, string Packet="", bool isTemp=false, bool UseBoss=false, string Monster="*", int MonsterMapID=-1) {
      this.ClassType = ClassType;
      this.Map = Map;
      this.Cell = Cell;
      this.Pad = Pad;
      this.QuestID = QuestID;
      this.Item = Item;
      this.Qty = Qty;
      this.isTemp = isTemp;
      this.UseBoss = UseBoss;
      this.Monster = Monster;
      this.Packet = Packet;
      this.MonsterMapID = MonsterMapID;

      if (Packet != "") {
         this.RunType = "packet";
         return;
      } 

      if (QuestID == 0) {
         this.RunType = "item";
      } 

      if (QuestID > 0){
         this.RunType = "quest";
      } 
      
   }
}

/// <summary>Bot Engine that contains essential functions</summary>
public class Engine {

   /// ================================================ ///
   ///            Essential Variable Section 
   /// ================================================ ///
   // System Variables
   public ScriptInterface bot => ScriptInterface.Instance;
   public bool DebugMode = false;
   public string ServerName;

   public Thread MainThread;
   public bool Relogged = false;
   public bool BotEnd = false;

   // Setup Variables
   public int MapNumber;
   public int MapNumberBoss;

   // Skill Variables
   public bool EnableSkillRun = true;
   public int SkillWait = 0;

   // Army Settings
   public bool ArmySync;
   public int ArmyCount;
   public int ArmyWait;

   /// ================================================ ///
   ///                  Function Section 
   /// ================================================ ///

   /// <summary>Equips an item.</summary>
   public void SafeEquip(string ItemName) {
      while (InvHas(ItemName) && !bot.Inventory.IsEquipped(ItemName)) {
         ExitCombat();
         bot.Player.EquipItem(ItemName);
      }
   }

   /// <summary>Converts Skill string input into an int List</summary>
   public void SkillConvert(IDictionary<string, List<DataSkill>> Skillset) {
      List<int> result = new List<int>();
      foreach(KeyValuePair<string, List<DataSkill>> kvp in Skillset) {
         foreach(string a in bot.Config.Get<string>(kvp.Key).Split(',')) {
            string raw = a.Trim();
            if (string.IsNullOrEmpty(raw)) continue;
            if (raw.Contains("w")) {
               kvp.Value.Add(new DataSkill("w", sValue:GetNumbers(raw)) );
               continue;
            }
            if (raw.Contains("h") && raw.Contains(">")) {
               result = SplitInt(raw, '>');
               kvp.Value.Add(new DataSkill("h>", Index:result[1], sValue:result[0]) );
               continue;
            }
            if (raw.Contains("h") && raw.Contains("<")) {
               result = SplitInt(raw, '<');
               kvp.Value.Add(new DataSkill("h<", Index:result[1], sValue:result[0]) );
               continue;
            }
            if (raw.Contains("m") && raw.Contains(">")) {
               result = SplitInt(raw, '>');
               kvp.Value.Add(new DataSkill("m>", Index:result[1], sValue:result[0]) );
               continue;
            }
            if (raw.Contains("m") && raw.Contains("<")) {
               result = SplitInt(raw,'<');
               kvp.Value.Add(new DataSkill("m<", Index:result[1], sValue:result[0]) );
               continue;
            }
            try {
               kvp.Value.Add(new DataSkill("s", Index:int.Parse(raw)));
            } catch { }
            continue;
         }
      }
   }

   /// <summary>Starts attacking the target</summary>
   /// <param name="SkillSet">The skillset int list</param>
   public void SkillActivate(List<DataSkill> SkillSet, string MonsterTarget="*", int MonsterMapID=9999) {
      if (!EnableSkillRun) return;
      EnableSkillRun = false;

      if (bot.Monsters.Exists(MonsterTarget)) {
         bot.Player.Attack(MonsterTarget);
      } else if (MonsterMapID != 9999){
         bot.Player.Attack(MonsterMapID);
      } else {
         bot.Player.Attack("*");
      }
      foreach (DataSkill data in SkillSet) {
         switch(data.Type) {
            case "s":
               bot.Player.UseSkill(data.Index); 
               if (SkillWait  != 0) bot.Sleep(SkillWait); 
               continue;
            case "w":
               bot.Sleep(data.sValue);
               continue;
            case "h>":
               if (bot.Player.Health > data.sValue) {
                  bot.Player.UseSkill(data.Index); 
                  if (SkillWait  != 0) bot.Sleep(SkillWait); 
               }
               continue;
            case "h<":
               if (bot.Player.Health < data.sValue) {
                  bot.Player.UseSkill(data.Index); 
                  if (SkillWait  != 0) bot.Sleep(SkillWait); 
               }
               continue;
            case "m>":
               if (bot.Player.Mana > data.sValue) {
                  bot.Player.UseSkill(data.Index); 
                  if (SkillWait  != 0) bot.Sleep(SkillWait); 
               }
               continue;
            case "m<":
               if (bot.Player.Mana < data.sValue) {
                  bot.Player.UseSkill(data.Index); 
                  if (SkillWait  != 0) bot.Sleep(SkillWait); 
               }
               continue;
         }

      }
      EnableSkillRun = true;
   }

   /// <summary>Skill switch use.</summary>
   public virtual void SkillUse(string skill, string MonsterTarget="*", int MonsterMapID=9999) {}

   public void SafeMapJoin(string MapName, string Cell="Enter", string Pad="Spawn", bool UseBoss=false, int CustomMap=0, bool EnableArmyHere=false) {
      //Must have the following functions in your script:
      //ExitCombat
      string mapname = MapName.ToLower();
      int number = 1;
      if (CustomMap == 0) {
         switch(UseBoss) {
            case true: number = MapNumberBoss; break;
            case false: number = MapNumber; break;
         }
      } else { number = CustomMap; }
         
      while (bot.Map.Name != mapname) {
         ExitCombat();
         if (mapname == "tercessuinotlim") bot.Player.Jump("m22", "Center");
         bot.Player.Join($"{mapname}-{number}", Cell, Pad);
         bot.Wait.ForMapLoad(mapname);
         bot.Sleep(500);
      }


      if (ArmySync && EnableArmyHere) {
         ExitCombat();
         int time_left = 0;
         while (time_left < ArmyWait) {
            if (bot.Map.PlayerCount >= ArmyCount) break;
            time_left += 1;
            MessageChat($"Waiting for army. {time_left}s/{ArmyWait}s");
            bot.Sleep(1000);
         }
      }
      

      if (bot.Player.Cell != Cell) bot.Player.Jump(Cell, Pad);
      bot.Log($"[{DateTime.Now:HH:mm:ss}] > Joined map {mapname}-{number}, positioned at the {Pad} side of cell {Cell}.");

   }

   /// <summary>
   /// Attempts to complete the quest with the set amount of {TurnInAttempts}. If it fails to complete, logs out. If it successfully completes, re-accepts the quest and checks if it can be completed again.
   /// </summary>
   public void SafeQuestComplete(int QuestID, int ItemID=-1, int TurnInAttempts=4) {
      //Must have the following functions in your script:
      //ExitCombat

      ExitCombat();
      if (!bot.Quests.IsInProgress(QuestID)) bot.Quests.EnsureAccept(QuestID);

      bot.Quests.EnsureComplete(QuestID, ItemID, tries: TurnInAttempts);

      if (bot.Quests.IsInProgress(QuestID)) {
         bot.Log($"[{DateTime.Now:HH:mm:ss}] > Failed to turn in Quest {QuestID}. Logging out.");
         bot.Player.Logout();
      }
      bot.Log($"[{DateTime.Now:HH:mm:ss}] > Turned In Quest {QuestID} successfully.");
      // while (!bot.Quests.IsInProgress(QuestID)) bot.Quests.EnsureAccept(QuestID);
   }


   ///<summary>
   /// Checks if the current RBot version matches 
   /// which version of RBot you made this bot in.
   ///
   /// Do NOT forget to use the namespace `using System.Windows.Forms;`
   /// </summary>
   /// <param name="NativeRBotVersion">The RBot version you made this bot in.</param>
   public void VersionEvaluator(string NativeRBotVersion) {
      var VersionEval = Application.ProductVersion.CompareTo(NativeRBotVersion);

      switch (VersionEval) {
         case 0:
            bot.Log($"[System] Bot Native Ver. ({NativeRBotVersion}) == RBot Current Version ({Application.ProductVersion}). Good.");
            break;
         case 1:
            bot.Log($"[System] Bot Native Ver. ({NativeRBotVersion}) < RBot Current Version ({Application.ProductVersion}). Good.");
            break;
         case -1:
            bot.Log($"[System] Bot Native Ver. ({NativeRBotVersion}) > RBot Current Version ({Application.ProductVersion}). Get the latest version. This bot will not work unless you do.");
            // Eternally loops and stops bot from going if this current rbot version is outdated relative to the native bot's version.
            while (true) {
               MessageNotify($"This bot uses RBot ({NativeRBotVersion}) Your version is ({Application.ProductVersion}). Get the Latest RBot you dumbfuck.");
               bot.Sleep(2000);
            }
      }
   }

   public void SmartSaveState() {
      bot.SendPacket("%xt%zm%whisper%1%creating save state%" + bot.Player.Username + "%");
      bot.Log($"[{DateTime.Now:HH:mm:ss}] Successfully Saved State.");
   }

   /// <summary> Also Banks useless AC Items.</summary>
   public void AddToDrops(params string[] Items) {
      
      ExitCombat();

      // Declares AC items to bank
      string[] Whitelisted = {"Note", "Item", "Resource", "QuestItem", "ServerUse"};
      
      // Banks unneeded items that are included in the whitelisted
      foreach(var item in bot.Inventory.Items) {
         if (!Whitelisted.Contains(item.Category.ToString())) continue;
         if (item.Name != "Treasure Potion" && item.Coins && !Array.Exists(Items, x => x == item.Name)) bot.Inventory.ToBank(item.Name);
      }
      // Adds to drops
      foreach (string item in Items) bot.Drops.Add(item);
   }

   /// <summary> Pure Unbank</summary>
   public void Unbank(params string[] Items) {
      ExitCombat();

      bot.Player.LoadBank();
      bot.Sleep(500);

      // Unbanks the required items
      foreach (string item in Items) {
         if (bot.Bank.Contains(item)) {
            bot.Bank.ToInventory(item);
            MessageChat($"Unbanked: {item}");
         } 
      }

   }

   /// <summary> Banks items </summary>
   public void Bank(params string[] Items) {
      // Safe Loading
      ExitCombat();
      bot.Player.LoadBank();

      // Unbanks the required items
      foreach (string item in Items) {
         if (InvHas(item)) bot.Inventory.ToBank(item);
      }

   }


   /// <summary> Leaves Combat</summary>
   public void ExitCombat() {
      if (bot.Options.AggroMonsters != false) bot.Options.AggroMonsters = false;
      if (bot.Player.Cell != "Wait") bot.Player.Jump("Wait", "Spawn");
      while (bot.Player.State == 2) bot.Sleep(500);
      bot.Sleep(500);
   }

   ///<summary>
   /// Uses the black message bar on top of aqw UI to send a message
   /// </summary>
   /// <param name="MessageChat">The text message</param>
   public void MessageNotify(string Message) {
      bot.CallGameFunction("MsgBox.notify", Message);
      MessageChat(Message, ChatType:"moderator");
   }

   /// <summary> Sends moderator message form</summary>
   public void MessageChat(string Message, bool SkipChat=false, string ChatType="event") {
      if (!SkipChat) bot.SendClientPacket($"%xt%chatm%0%{ChatType}~> {Message}%% %");
      bot.Log($"[{DateTime.Now:HH:mm:ss}] > {Message}.");
   }


   public void CleanChat() {
      foreach (int value in Enumerable.Range(1, 10)) {
         // bot.SendClientPacket($"%xt%chatm%0%zone~{MessageChat}%{Name}%");
         bot.SendClientPacket($"%xt%chatm%0%zone~ % %");
         bot.Log("");
         // bot.SendClientPacket($"%xt%chatm%0%zone~%%");
      }
   }

   public void DataRunner(List<DataRun> QuestRuns, bool EnableArmyHere=false) {
      foreach(DataRun data in QuestRuns) {
         switch(data.RunType) {
            case "item":
               if (!InvHas(data.Item, data.Qty, data.isTemp)) {
                  ItemRun(data.ClassType, data.Map, data.Cell, data.Pad, data.Item, data.Qty, data.isTemp, data.UseBoss, data.Monster, data.MonsterMapID, EnableArmyHere);
               }
               continue;
            case "quest":
               if (data.Item != "") {
                  while(!InvHas(data.Item, data.Qty, data.isTemp)) {
                     QuestRun(data.ClassType, data.Map, data.Cell, data.Pad, data.QuestID, data.UseBoss, data.Monster, data.MonsterMapID, EnableArmyHere);
                  }
                  continue;
               } else {
                  QuestRun(data.ClassType, data.Map, data.Cell, data.Pad, data.QuestID, data.UseBoss, data.Monster, data.MonsterMapID, EnableArmyHere);
                  continue;
               }
            case "packet":
               PacketRun(data.Map, data.Cell, data.Pad, data.QuestID, data.Item, data.Qty, data.isTemp, data.Packet, EnableArmyHere);
               continue;

         }
      }
   }

   public void QuestAccepts(params int[] QuestIDs) {
      ExitCombat();
      foreach(int ID in QuestIDs) {
         if (!bot.Quests.IsInProgress(ID)) bot.Quests.EnsureAccept(ID);
      }
   }

   public void QuestCompletes(params int[] QuestIDs) {
      ExitCombat();
      foreach(int ID in QuestIDs) {
         while (bot.Quests.CanComplete(ID)) {
            bot.Log($"{bot.Quests.CanComplete(ID)}");
            SafeQuestComplete(ID);
            bot.Sleep(400);
            bot.Log($"Completed: {ID}");
         }
      }
   }

   public void QuestRun(string ClassType, string Map, string Cell, string Pad, int QuestID, bool UseBoss=false, string Monster="*", int MonsterMapID=9999, bool EnableArmyHere=false) {
      MessageChat($"(Item Run) Quest: {QuestID}");
      if (bot.Map.Name != Map) SafeMapJoin(Map, Cell, Pad, EnableArmyHere:EnableArmyHere);
      
      bot.Quests.EnsureAccept(QuestID);
      
      while (!bot.Quests.CanComplete(QuestID)) {
         if (bot.Player.Cell != Cell) bot.Player.Jump(Cell, Pad);
         EnsureAgro();
         SkillUse(ClassType, Monster, MonsterMapID);
      }

      ExitCombat();
      SafeQuestComplete(QuestID);
   }

   public void QuestFarm(string ClassType, string Map, string Cell, string Pad, int[] QuestID, string Item, int Qty, bool isTemp=false, bool UseBoss=false, string Monster="*", int MonsterMapID=9999, bool EnableArmyHere=false) {
      MessageChat($"(Quest Farm) Quest: {QuestID[0]}");
      if (bot.Map.Name != Map) SafeMapJoin(Map, Cell, Pad, EnableArmyHere:EnableArmyHere);
      
      QuestAccepts(QuestID);

      while (!InvHas(Item, Qty, isTemp)) {
         if (bot.Player.Cell != Cell) bot.Player.Jump(Cell, Pad);
         EnsureAgro();
         SkillUse(ClassType, Monster, MonsterMapID);
      }

      ExitCombat();

      foreach(int ID in QuestID) {

         while (bot.Quests.CanComplete(ID)) {

            bot.Quests.Complete(ID);
            bot.Sleep(500);
            bot.Quests.EnsureAccept(ID);
            bot.Sleep(500);
            bot.Log($"Completed: {ID}");
         }
      }
   }


   public void QuestBolt(int QuestID, List<DataRun> QuestRuns, string Item="", int Qty=1,  bool isTemp=false, bool EnableArmyHere=false) {
      
      // if quest has an item
      if (Item != "") {
         while (!InvHas(Item, Qty, isTemp)) {
            if (QuestID != 0) ExitCombat();
            bot.Quests.EnsureAccept(QuestID);
            DataRunner(QuestRuns, EnableArmyHere);
            ExitCombat();
            if (QuestID != 0) SafeQuestComplete(QuestID);
         }

         return;
      }

      // if not
      // while (bot.Quests.CanComplete(QuestID)) {
      bot.Log("Can complete");
      ExitCombat();
      bot.Quests.EnsureAccept(QuestID);
      DataRunner(QuestRuns, EnableArmyHere);
      ExitCombat();
      SafeQuestComplete(QuestID);
      // }
   }


   public void PacketRun(string Map, string Cell, string Pad, int QuestID=0, string Item="", int Qty=1, bool istemp=false, string Packet="", bool EnableArmyHere=false) {
      
      if (bot.Map.Name != Map) SafeMapJoin(Map, Cell, Pad, EnableArmyHere:EnableArmyHere);
      ExitCombat();

      if (QuestID != 0) {
         MessageChat($"(Packet Run) Quest: {QuestID}");
         bot.Quests.EnsureAccept(QuestID);
         while (!bot.Quests.CanComplete(QuestID)) {
            if (bot.Player.Cell != "Wait") ExitCombat();
            bot.SendPacket(Packet);
            bot.Sleep(600);
         }
         ExitCombat();
         SafeQuestComplete(QuestID);
         return;
      } 

      if (Item != "") {
         MessageChat($"(Packet Run) Item: {Item} | Qty: {Qty} | IsTemp: {istemp}");
         while(!InvHas(Item, Qty, istemp)) {
            if (bot.Player.Cell != "Wait") ExitCombat();
            bot.SendPacket(Packet);
            bot.Sleep(600);
         }
      }


   }

   public void ItemRun(string ClassType, string Map, string Cell, string Pad, string Item, int Qty, bool isTemp=false, bool UseBoss=false, string Monster="*", int MonsterMapID=9999, bool EnableArmyHere=false) {
      MessageChat($"(Item Run) | Item: {Item} | Qty: {Qty}");
      if (bot.Map.Name != Map) SafeMapJoin(Map, Cell, Pad, UseBoss, EnableArmyHere:EnableArmyHere);

      while (!InvHas(Item, Qty, isTemp)) {
         if (bot.Player.Cell != Cell) bot.Player.Jump(Cell, Pad);
         EnsureAgro();
         SkillUse(ClassType, Monster, MonsterMapID);
      }

      ExitCombat();
   }


   public void BuyRun(string Item, int Qty, int Shop, string Map, string Cell, string Pad, int Gold){
      ExitCombat();
      Unbank(Item);
      if (InvHas(Item)) return;
      if (bot.Player.Gold < Gold) GetGold(Gold);

      SafeMapJoin(Map, Cell, Pad, false, 99999);
      bot.Sleep(2500);
      bot.Shops.Load(Shop);
      bot.Sleep(3000);
      bot.Shops.BuyItem(Item);
      bot.Sleep(2500);
   }

   public void GetGold(int Qty, string Cell="", string Pad="") {
      SafeMapJoin("icestormarena", "Enter", "Spawn", false, 1);
      // Under level
      if (bot.Player.Level < 50) {
         Cell = "r4";
         Pad = "Bottom";
      }
      // Mid level
      if (bot.Player.Level >= 50 && bot.Player.Level < 75) {
         Cell = "r3b";
         Pad = "Top";
      }
      // High level
      if (bot.Player.Level >= 75) {
         Cell = "r3c";
         Pad = "Top";
      }
      while (bot.Player.Gold < Qty) {
         if (bot.Player.Cell != Cell) bot.Player.Jump(Cell, Pad);
         if (Cell == "r4") bot.SendPacket("%xt%zm%aggroMon%134123%70%71%72%73%74%75%");
         SkillUse("farm");
      }
      return;

   }


   /// ================================================ ///
   ///                  Shortened Section 
   /// ================================================ ///
   public bool InvHas(string item, int Qty=1, bool IsTemp=false) {
      if (!IsTemp) {
         return bot.Inventory.Contains(item, Qty);
      } else {
         return bot.Inventory.ContainsTempItem(item, Qty);
      }
      
   }

   public bool BankHas(string item, int Qty=1) {
      return bot.Bank.Contains(item, Qty);
   }

   public void EnsureAgro() {
      if (bot.Options.AggroMonsters) return;
         bot.Options.AggroMonsters = true;
   }
 

   /// ================================================ ///
   ///                  Utility Section 
   /// ================================================ ///

   /// <summary>
   /// Cleans a string of non-numerics and returns an integer
   /// </summary>
   /// <param name="delim">string to extract ints from</param>
   public int GetNumbers(string input) {
      return int.Parse(Regex.Replace(input, @"[^\d]+", "\n").Trim());
   }

   /// <summary>
   /// Extracts only the integers in a string
   /// </summary>
   /// <param name="input">string to split and extract ints from</param>
   /// <param name="delim">the delimeter char</param>
   public List<int> SplitInt(string input, char delim) {
      List<int> result = new List<int>();
      foreach(string health_item in input.Split(delim)) {
         result.Add(GetNumbers(health_item));
      }
      if (result.Count != 2) {
         result.Add(0);
      }
      return result;
   }

   /// <summary>
   /// Sets the game FPS
   /// </summary>
   /// <param name="FPS"> Frames per second </param>
   public void SetFPS(int FPS) {
      bot.SetGameObject("stage.frameRate", FPS);
   }

   /// <summary>
   /// Hides the monsters for performance
   /// </summary>
   /// <param name="Value"> true -> hides monsters. false -> reveals them </param>
   public void HideMonsters(bool Value) {
      switch(Value) {
         case true:
            if (!bot.GetGameObject<bool>("ui.monsterIcon.redX.visible")) {
               bot.CallGameFunction("world.toggleMonsters");
            }
            return;
         case false:
            if (bot.GetGameObject<bool>("ui.monsterIcon.redX.visible")) {
               bot.CallGameFunction("world.toggleMonsters");
            }
            return;

      }

   }

   /// <summary>
   /// Shows FPS counter
   /// </summary>
   /// <param name="Value"> true -> hides fps. false -> reveals them </param>
   public void ShowFPSCounter(bool Value) {
      switch(Value) {
         case true:
            if (!bot.GetGameObject<bool>("ui.mcFPS.visible")) {
               bot.CallGameFunction("world.toggleFPS");
            }
            return;
         case false:
            if (bot.GetGameObject<bool>("ui.mcFPS.visible")) {
               bot.CallGameFunction("world.toggleFPS");
            }
            return;
      }


   }


   /// ================================================ ///
   ///                  Handlers Section 
   /// ================================================ ///
   public void LoadHandlers() {

      // Player Death
      bot.RegisterHandler(2, b => {
         if (bot.Player.State==0) {
            bot.Log("> You died.");
            bot.Player.SetSpawnPoint();
            ExitCombat();
            bot.Sleep(12000);
         }
      });

      // AutoRelogin
      bot.RegisterHandler(1, b => {
         if (bot.Player.Kicked) {
            MainThread.Abort();
            bot.Sleep(30000);
         }

         if (!bot.Player.LoggedIn) {
            bot.Log("> Your session exited. Relogging again....");
            MainThread.Abort();
         }
     });

   }

   public void ExitMainThread(bool DontAbort=false){

      if (!DontAbort) MainThread.Abort();
      

      MessageChat("Resetting FPS back to 30");

      bot.SetGameObject("stage.frameRate", 30);
      bot.Options.LagKiller = false;
      bot.Options.HidePlayers = false;
      HideMonsters(false);

      ExitCombat();
   }

   public void ExitHandlerThread(){
      while (!bot.ShouldExit()) continue;
      ExitMainThread();
   }

   public void ExitHandlerSimple() {
      bot.RegisterHandler(1, b => {
         if (bot.ShouldExit()) {
            MessageChat("Resetting FPS back to 30");

            bot.SetGameObject("stage.frameRate", 30);
            bot.Options.LagKiller = false;
            bot.Options.HidePlayers = false;
            HideMonsters(false);

            ExitCombat();
         }
      });
   }

   public void Relog() {
      bot.CallGameFunction("mcConnDetail.hideConn");
      Relogged = true;
      bot.Log("> Relogging in progress");
      bot.Player.Reconnect(ServerName);
      bot.Sleep(3000);
      while (!bot.Player.LoggedIn) {}
      bot.Sleep(3000);
      while (!bot.Player.Loaded) {}   
      
   }


}


public class Script: Engine {

   public bool MainLoopOn = true;
   public ClassSpecification Classes = new ClassSpecification();

   // Default variables
   public bool UsePotions;
   public bool DefaultFB;
   public bool DefaultDOT;

   // Class & Skill variables
   public string ClassFarm;
   public string ClassSolo;
   public string ClassTethered;
   public string ClassFrostval = "Frostval Barbarian";
   public string ClassDragonOfTime = "Dragon of Time";
   public List<DataSkill> SkillFarm = new List<DataSkill>();
   public List<DataSkill> SkillSolo = new List<DataSkill>();
   public List<DataSkill> SkillTethered = new List<DataSkill>();
   public List<DataSkill> SkillDragonOfTime = new List<DataSkill>();
   public List<DataSkill> SkillFrostval = new List<DataSkill>();

   // Booster variables
   public string BoostUndead;
   public string BoostMonster;

   // Option variables
   public string OptionsStorage = "Legion Revenant";
   public bool DontPreconfigure = true;
   public List<IOption> Options = new List<IOption>() {
      new Option<string>("‎space0", "『 Metadata 』", "Bot info.\n(This is just a section header).", ""),
      new Option<string>("author", "Author", "The creator of this bot is bloom.", "Bloom", true),
      new Option<string>("version", "Version", "The bot version.", "v.4 (Beta)", true),
      new Option<string>("findus", "Find Us", "Join us on discord if you have any question. You can find more bots in the portal link.", "https://auqw.tk/", true),
      new Option<string>("‎space1", " ", "", ""),

      new Option<string>("‎space2", "『 Setting 』", "The Setting Section.\n(This is just a section header).", ""),
      new Option<int>("map", "Map", "Map number to join into.", 9999),
      new Option<int>("mapBoss", "Boss Map", "The Boss Map number to join into.", 1),
      new Option<bool>("usePotions", "Use Potions", "Whether to buy and use potions.\nTrue -> Will use. False -> Will not use.", true),
      new Option<bool>("defaultFB", "Use FB by default", "If you have Frostval barbarian, will use it by default.\nTrue -> Will use. | False -> Will not use.", true),
      new Option<bool>("defaultDOT", "Use DoT by default", "If you have Dragon of Time, will use it by default.\nTrue -> Will use. | False -> Will not use.", false),
      new Option<GoldFarmEnum>("goldFarm", "Gold Farm Area", "Set to private or public.", GoldFarmEnum.Public),
      new Option<string>("otherDrops", "Other Drops", "List of drops you want the bot to pick up other than the default. Will automatically unbank.\nExample -> Empowered Essence, Treasure Chest, etc...", ""),
      new Option<string>("‎space3", " ", "", ""),

      new Option<string>("‎space4", "『 Boosters 』", "The Item Booster Section.\n(This is just a section header).", ""),
      new Option<string>("‎boostUndead", "Undead Boosters", "List of items to boost undead damage. Separate by comma.\nExample -> Blinding Light of Destiny, Sepulchure's Armor, etc...", "Blinding Light of Destiny"),
      new Option<string>("‎boostMonster", "Monster Boosters", "List of items to boost all monster damage. Separate by comma.\nExample -> Necrotic Sword of Doom, Sepulchure's Armor, etc...", "Necrotic Sword of Doom"),
      new Option<string>("‎space5", " ", "", ""),

      new Option<string>("‎space6", "『 Classes 』", "The Class Section.\n(This is just a section header).", ""),
      new Option<int>("skillWait", "Skill Wait", "Inherent wait between skills in ms. Example -> 500", 0), 
      new Option<string>("classFarm", "Farming Class", "Class for Farming.\nExample -> Legion Revenant", "Legion Revenant"),
      new Option<string>("skillFarming", "Farming Skills", "Farming Skillset. Use -> [1, 2, 3, 4, 5, 6] just like the aqw UI skill keys. Add wNumber to delay before executing a skill. Time is in ms, minimum of 10ms. Example -> 5,4,w800,1", "1,2,3,4,5"),
      new Option<string>("classSolo", "Soloing Class", "Class for Soloing.\nExample -> Vindicator of Gay", "Legion Revenant"),
      new Option<string>("skillSoloing", "Soloing Skills", "Soloing Skillset. Use -> [1, 2, 3, 4, 5, 6] just like the aqw UI skill keys. Add wNumber to delay before executing a skill. Time is in ms, minimum of 10ms. Example -> 5,4,w800,1", "1,2,3,4,5"),
      new Option<string>("classTethered", "Tethered Class", "Class for Tethered Souls Farming.\nExample -> Vindicator of Gay", "Legion Revenant"),
      new Option<string>("skillTethered", "Tethered Skills", "Tethered Skillset. Use -> [1, 2, 3, 4, 5, 6] just like the aqw UI skill keys. Add wNumber to delay before executing a skill. Time is in ms, minimum of 10ms. Example -> 5,4,w800,1", "1,2,3,4,5"),
      new Option<string>("‎space7", " ", "", ""),

      new Option<string>("‎space8", "『 Default Classes 』", "The Default Class Section. This will be used if you have it.\n(This is just a section header).", ""),
      new Option<string>("skillFrosval", "Frostval Barbarian Skills", "FB Skillset. Use -> [1, 2, 3, 4, 5, 6] just like the aqw UI skill keys. Add wNumber to delay before executing a skill. Time is in ms, minimum of 10ms. Example -> 5,4,w800,1", "1,5"),
      new Option<string>("skillDragonOfTime", "Dragon of Time Skills", "Dragon of Time Skillset. Use -> [1, 2, 3, 4, 5, 6] just like the aqw UI skill keys. Add wNumber to delay before executing a skill. Time is in ms, minimum of 10ms. Example -> 5,4,w800,1", "1,2,3,4,5"),
      new Option<string>("‎space9", " ", "", ""),

      new Option<string>("‎space10", "『 Army 』", "The Army Section.\n(This is just a section header).", ""),
      new Option<bool>("armySync", "Army Sync", "The Army Sychronizer. Will wait a certain amount of time after each map join.\n True -> enable | False -> disable", false),
      new Option<int>("armyCount", "Army Count", "The amount of character (including the main player) to wait before the bot proceeds. If these amount are not met after n amount of time, bot will proceed.", 3),
      new Option<int>("armyWait", "Army Wait", "Amount of time to wait for the characters to gather. If after this amount of time (in secs) passed by and the characters haven't gathered, bot will move on.", 30),
   };
 

   /// <summary> Main body of the bot </summary>
   public void ScriptMain(ScriptInterface bot) {
      bot.Log("[System] > Started once");
      VersionEvaluator("3.6.0.0");
      bot.Config.Configure();

      // Ext Handler
      Thread ExitThread = new Thread(new ThreadStart(ExitHandlerThread));
      ExitThread.Start();

      // Handlers
      LoadHandlers();
      
      // Server Name
      ServerName = bot.Player.ServerIP.Split('.')[0];

      // Loop Controller
      while (true) {
         EnableSkillRun = true;
         MainThread = new Thread(new ThreadStart(MainLoop));
         MainThread.Start();
         MainThread.Join();

         if (BotEnd) break;

         bot.Sleep(2000);
         Relog();
      }
      ExitMainThread();
      SafeMapJoin("freakitiki", "Enter", "Spawn", false, 99999);
      bot.Player.Jump("Enter", "Spawn");
      MessageChat("Legion Revenant Bot Complete Run!", ChatType: "moderator");
      MessageChat("Now suck Bloom's PP", ChatType: "moderator");
   }


   public void MainLoop() {
      while (!bot.Player.Loaded) {}

      // Chat
      CleanChat();
      MessageChat("Starting Legion Revenant Master bot by Bloom", ChatType: "moderator");
      
      // Setups bot
      MessageChat("Applying Configurations");
      ExitCombat();
      Configurations();
      LoadReqs();
      LoadVars();
      LoadDrops();

      // Main Unbank
      MessageChat("Unbanking main Items");
      Unbank(ClassFarm, ClassSolo, ClassTethered, ClassFrostval, ClassDragonOfTime, 
            "Revenant's Spellscroll", "Conquest Wreath", "Exalted Crown");

      /// Main Check
      Manager();
      BotEnd = true;
      ExitMainThread();
   }


   public void Manager() {
      ExitCombat();
      MessageChat("Checking your progress");
      UpdateProgress();

      if (bot.Player.GetFactionRank("Evil") < 10) GetReputationEvil();
      if (!InvHas("Revenant's Spellscroll", 20)) QuestLegionFealty1(20);
      if (!InvHas("Conquest Wreath", 6)) QuestLegionFealty2(6);
      if (!InvHas("Exalted Crown", 10)) QuestLegionFealty3(10);

      bot.Sleep(1500);

      /// <remark> Complete </remark>
      SafeMapJoin("yulgar", "Enter", "Spawn", false, 99999);
      ExitCombat();
      bot.Player.LoadBank();
      foreach (string item in AllLegionItems) {
         if (InvHas(item) && !FealtyRewards.Contains(item)) bot.Inventory.ToBank(item);
      }

      // Complete
      bot.Sleep(1500);
      bot.Quests.EnsureComplete(6900);
      bot.Sleep(2000);
      SafeEquip("Legion Revenant");
      bot.Sleep(2000);
      return;
   }

   /// <summary>Bot Options</summary>
   public void Configurations() {
   	if (bot.Map.Name == "battleon") SafeMapJoin("yulgar", "Enter", "Spawn", false, 9999);
      
      MessageChat("Setting FPS to 10.");

      SetFPS(10);
      ShowFPSCounter(true);
      HideMonsters(true);

      MessageChat("Applying Configurations");
      bot.Options.SafeTimings = true;
      bot.Options.RestPackets = true;
      bot.Options.PrivateRooms = true;
      bot.Options.InfiniteRange = true;
      bot.Options.PrivateRooms = false;
      bot.Options.SkipCutscenes = true;
      bot.Options.DisableFX = true;
      bot.Options.AggroMonsters = false;
      bot.Options.AutoRelogin = false;
      bot.Options.HidePlayers = true;
      bot.Lite.DisableSkillAnimations = true;
      bot.Lite.CustomDropsUI = false;
   }

   /// <summary>Loads Banks and quests</summary>
   public void LoadReqs() {
      // Loads Quests
      MessageChat("Loading Quests.");
      bot.Quests.Load(6897, 6898, 6899, 6900, 4743, 4742, 364, 6248, 6249);
      bot.Sleep(1500);
   }


   /// <summary>Gets variables and assigns them</summary>
   public void LoadVars() {
      if (Relogged) return;
      MessageChat("Loading Variables.");
      // System Setups

      // Var setups
      MapNumber = bot.Config.Get<int>("map");
      MapNumberBoss = bot.Config.Get<int>("mapBoss");
      
      UsePotions  = bot.Config.Get<bool>("usePotions");
      DefaultFB  = bot.Config.Get<bool>("defaultFB");
      DefaultDOT  = bot.Config.Get<bool>("defaultDOT");

      // Army Settings
      ArmySync = bot.Config.Get<bool>("armySync");
      ArmyCount = bot.Config.Get<int>("armyCount");
      ArmyWait = bot.Config.Get<int>("armyWait");

      MessageChat("Setting up skills.");
      // Class setups
      ClassFarm = bot.Config.Get<string>("classFarm");
      ClassSolo = bot.Config.Get<string>("classSolo");
      ClassTethered = bot.Config.Get<string>("classTethered");

      /// Class Checks Defaults
      if (DefaultFB) {
         if (!InvHas(ClassFrostval)) DefaultFB = false;
      }
      if (DefaultDOT) {
         if (!InvHas(ClassDragonOfTime)) DefaultDOT = false;
      }

      // Skill setups
		SkillConvert(new Dictionary<string, List<DataSkill>>(){
         {"skillFarming", SkillFarm},
         {"skillSoloing", SkillSolo},
         {"skillTethered", SkillTethered},
         {"skillFrosval", SkillFrostval},
         {"skillDragonOfTime", SkillDragonOfTime},
      });
      SkillWait = bot.Config.Get<int>("skillWait");

      // Booster setups
      BoostMonster = bot.Config.Get<string>("‎boostMonster");
      BoostUndead = bot.Config.Get<string>("‎boostUndead");
      bot.Player.LoadBank();
      // Getting other drops
      if (bot.Config.Get<string>("otherDrops").Trim() != "") {
         List<string> OtherDrops = AllLegionItems.ToList();
         foreach (string item in bot.Config.Get<string>("otherDrops").Split(',')) {
            string item_ = item.Trim();
            OtherDrops.Add(item_);
            if (bot.Bank.Contains(item_)) bot.Bank.ToInventory(item_);

         }
         AllLegionItems = OtherDrops.ToArray();
      }

   }

      
         
            
   public void LoadDrops() {
   	MessageChat("Adding all needed items to drop list and banking unncessary AC items.");
      AddToDrops(AllLegionItems);
      bot.Lite.CustomDropsUI = false;
      bot.Drops.RejectElse = true;
      bot.Drops.Start();

   }



   /// <summary>Chooses which skill to use</summary>
   /// <param name="skill">The skill name</param>
   public override void SkillUse(string skill, string MonsterTarget="*", int MonsterMapID=9999) {
      if (skill == "farm-fb" && DefaultFB) {
         SafeEquip(ClassFrostval);
         SkillActivate(SkillFrostval);
         return;
      } else if (skill == "solo-dot" && DefaultDOT){
         SafeEquip(ClassDragonOfTime);
         SkillActivate(SkillDragonOfTime);
         return;
      } 

      if (EnableSkillRun == false) return;
      switch(skill) {
         case "farm":
         case "farm-fb":
            SafeEquip(ClassFarm);
            SkillActivate(SkillFarm);
            return;
         case "solo":
         case "solo-dot":
            SafeEquip(ClassSolo);
            SkillActivate(SkillSolo);
            return;
         case "tethered":
            SafeEquip(ClassTethered);
            SkillActivate(SkillTethered);
            return;
         case "frostval":
            SafeEquip(ClassFrostval);
            SkillActivate(SkillFrostval);
            return;
         case "dragonoftime":
            SafeEquip(ClassDragonOfTime);
            SkillActivate(SkillDragonOfTime);
            return;
      }
   }


   public void UpdateProgress() {
   	int rev = bot.Inventory.GetQuantity("Revenant's Spellscroll");
   	int con = bot.Inventory.GetQuantity("Conquest Wreath");
   	int exa = bot.Inventory.GetQuantity("Exalted Crown");
   	double percent = ((rev+con+exa)/36.0)*100;
   	MessageChat($"Your Revenant progress: {percent:.##}/100", ChatType: "moderator");
   }



   /// ================================================ ///
   ///                  Prereqs Section 
   /// ================================================ ///
   public class Prereqs {}

   public void GetReputationEvil() {
      while ((bot.Player.GetFactionRank("Evil") < 10)) {
         QuestRun("farm", "newbie", "r2", "Left", 364);
      }

   }
   public void StoryWorldSoul() {}

   public void StoryNecrodungeon() {}

   public void StoryCruxShip() {}

   /// ================================================ ///
   ///                  Quests Section 
   /// ================================================ ///
   public class Quests {}


   public void QuestLegionFealty1(int Qty) {
      MessageChat("(Quest) 1/3 - Starting Legion Fealty 1", ChatType: "moderator");
      // Safespace
      ExitCombat();

      // Unbanks
      MessageChat("(Quest) 1/3 - Unbanking items");
      Unbank(UnbankLegionFeality1);

      // Banks
      MessageChat("(Quest) 1/3 - Banking unndeeed items");
      Bank(BankLegionFealty1);

      // Quest Loop
      while(!InvHas("Revenant's Spellscroll", Qty)) {
         // Accepts
         ExitCombat();
         bot.Quests.EnsureAccept(6897);
         if (bot.Quests.CanComplete(6897)) SafeQuestComplete(6897);
         
         // Checks
         if (!InvHas("Aeacus Empowered", 50)) {
            SafeEquip(BoostUndead);
            ItemRun(Classes.Aeacus, "judgement", "r10a", "Left", "Aeacus Empowered", 50, false, true, EnableArmyHere:true);
         }
         if ((!InvHas("Tethered Soul", 300))) {
            SafeEquip(BoostMonster);
            GetDarkCasterClass();
            ItemRun(Classes.Tethered, "revenant", "r2", "Left", "Tethered Soul", 300, false, true, EnableArmyHere:true);
         }
         if (!InvHas("Darkened Essence", 500)) {
            SafeEquip(BoostMonster);
            ItemRun(Classes.Darkened, "shadowrealmpast", "Enter", "Spawn", "Darkened Essence", 500, false, true, EnableArmyHere:true);
         }
         if (!InvHas("Dracolich Contract", 1000)) {
            SafeEquip(BoostUndead);
            ItemRun(Classes.Dracolich, "necrodungeon", "r22", "Down", "Dracolich Contract", 1000, false, true, EnableArmyHere:true);
         }

         // Complete
         bot.Sleep(2000);
         SafeQuestComplete(6897);
         bot.Sleep(2000);
         UpdateProgress();
      }


   }

   public void QuestLegionFealty2(int Qty) {
   	MessageChat("(Quest) 2/3 - Starting Legion Fealty 2", ChatType: "moderator");

      // Safespace
      ExitCombat();

      // Banks
      MessageChat("(Quest) 2/3 - Unbanking items");
      Bank(BankLegionFealty2);

      // Unbanks
      MessageChat("(Quest) 2/3 - Banking unndeeed items");
      Unbank(UnbankLegionFeality2);
      
      SafeEquip(BoostUndead);

      // Quest Loop
      while(!InvHas("Conquest Wreath", Qty)) {
         // Accepts
         ExitCombat();
         bot.Quests.EnsureAccept(6898);
         if (bot.Quests.CanComplete(6898)) SafeQuestComplete(6898);

         // Checks
         if (!InvHas("Grim Cohort Conquered", 500)) {
            ItemRun(Classes.Grim, "doomvault", "r1", "Left", "Grim Cohort Conquered", 500, false, false, EnableArmyHere:true);
         }
         if (!InvHas("Ancient Cohort Conquered", 500)) {
            ItemRun(Classes.Ancient, "mummies", "Enter", "Spawn", "Ancient Cohort Conquered", 500, false, false, EnableArmyHere:true);
         }
         if (!InvHas("Pirate Cohort Conquered", 500)) {
            ItemRun(Classes.Pirate, "wrath", "r4", "Left", "Pirate Cohort Conquered", 500, false, false, EnableArmyHere:true);
         }
         if (!InvHas("Battleon Cohort Conquered", 500)) {
            ItemRun(Classes.Battleon, "doomwar", "r11", "Left", "Battleon Cohort Conquered", 500, false, false, EnableArmyHere:true);
         }
         if (!InvHas("Mirror Cohort Conquered", 500)) {
            ItemRun(Classes.Mirror, "overworld", "r2", "Down", "Mirror Cohort Conquered", 500, false, false, EnableArmyHere:true);
         }
         if (!InvHas("Darkblood Cohort Conquered", 500)) {
            ItemRun(Classes.Darkblood, "deathpits", "r1", "Left", "Darkblood Cohort Conquered", 500, false, false, EnableArmyHere:true);
         }
         if (!InvHas("Vampire Cohort Conquered", 500)) {
            ItemRun(Classes.Vampire, "maxius", "r2", "Left", "Vampire Cohort Conquered", 500, false, false, EnableArmyHere:true);
         }
         if (!InvHas("Spirit Cohort Conquered", 500)) {
            ItemRun(Classes.Spirit, "curseshore", "Enter", "Spawn", "Spirit Cohort Conquered", 500, false, false, EnableArmyHere:true);
         }
         if (!InvHas("Dragon Cohort Conquered", 500)) {
            ItemRun(Classes.Dragon, "dragonbone", "Enter", "CenterA", "Dragon Cohort Conquered", 500, false, false, EnableArmyHere:true);
         }
         if (!InvHas("Doomwood Cohort Conquered", 500)) {
            ItemRun(Classes.Doomwood, "doomwood", "r6", "Right", "Doomwood Cohort Conquered", 500, false, false, EnableArmyHere:true);
         }

         // Complete
         bot.Sleep(2000);
         SafeQuestComplete(6898);
         bot.Sleep(2000);
         UpdateProgress();
      }

   }


   public void QuestLegionFealty3(int Qty) {
   	MessageChat("(Quest) 3/3 - Starting Legion Fealty 3", ChatType: "moderator");

      // Safespace
      ExitCombat();
  
      // Unbanks
      MessageChat("(Quest) 3/3 - Unbanking items");
      Unbank(UnbankLegionFeality3);

      // Banks
      MessageChat("(Quest) 3/3 - Banking unndeeed items");
      Bank(BankLegionFealty3);

      // Accepts
      bot.Quests.EnsureAccept(6899);

      SafeEquip(BoostMonster);

      // Quest Loop
      while(!InvHas("Exalted Crown", Qty)) {
         // Accepts

         ExitCombat();
         bot.Quests.EnsureAccept(6899);
         if (bot.Quests.CanComplete(6899)) SafeQuestComplete(6899);


         // Checks
         while (!InvHas("Dark Token", 100)) {
            if (bot.Map.Name != "seraphicwardage") SafeMapJoin("seraphicwardage", "Enter", "Spawn", EnableArmyHere:true);
            
            QuestAccepts(new int[] {6248, 6249});

            while (!InvHas("Mega Seraphic Medals", 10, true)) {
               if (bot.Player.Cell != "Enter") bot.Player.Jump("Enter", "Spawn");
               EnsureAgro();
               SkillUse(Classes.DarkToken, "*");
            }

            ExitCombat();
            while (bot.Inventory.ContainsTempItem("Mega Seraphic Medals", 3)) {
               bot.Quests.Complete(6249);
               bot.Sleep(500);
            }

            while (bot.Inventory.ContainsTempItem("Seraphic Medals", 5)) {
               bot.Quests.Complete(6248);
               bot.Sleep(500);
            }

         }
         if (!InvHas("Hooded Legion Cowl", 1)) {
            BuyRun("Hooded Legion Cowl", 1, 216, "underworld", "s1", "Left", 500000);
         }
         
         if (!InvHas("Dage's Favor", 300)) {
            ItemRun(Classes.DageFavor, "evilwardage", "r8", "Left", "Dage's Favor", 303, false, false);
         }
         while (!InvHas("Emblem of Dage", 1)) {
            QuestRun(Classes.Emblem, "shadowblast", "r10", "Left", 4742,  false);
         }
         if (!InvHas("Diamond Token of Dage", 30)) {
		      QuestBolt(QuestID:4743, Item:"Diamond Token of Dage", Qty:30, EnableArmyHere:true, QuestRuns: new List<DataRun>(){
					new DataRun("farm-fb", "tercessuinotlim", "m2", "Spawn", Item:"Defeated Makai", Qty:25),
					new DataRun("solo", "aqlesson", "Frame9", "Right", Item:"Carnax Eye", isTemp:true, UseBoss:true),
					new DataRun("solo", "deepchaos", "Frame4", "Left", Item:"Kathool Tentacle", isTemp:true, UseBoss:true),
					new DataRun("solo", "lair", "End", "Right", Item:"Red Dragon's Fang", isTemp:true, UseBoss:true),
					new DataRun("solo", "dflesson", "r12", "Right", Item:"Fluffy's Bones", isTemp:true, UseBoss:true),
					new DataRun("solo", "bloodtitan", "Enter", "Spawn", Item:"Blood Titan's Blade", isTemp:true, UseBoss:true),
		      });
         }
         if (!InvHas("Legion Token", 4000)) {
            GetLegionToken(4000);
         }  


         // Complete
         bot.Sleep(2000);
         SafeQuestComplete(6899);
         bot.Sleep(2000);
         UpdateProgress();
      }

   }

   /// ================================================ ///
   ///                  Farm Section 
   /// ================================================ ///
   public class Farms {}
   public void GetLegionToken(int Qty) {

      if (!InvHas("Legion Token")) Unbank("Legion Token");
      if (InvHas("Legion Token", Qty)) return;
      if (bot.Bank.Contains("Shogun Paragon Pet")) Unbank("Shogun Paragon Pet");

      // Pet Firsts
      if (InvHas("Shogun Paragon Pet")) {
         while (!InvHas("Legion Token", Qty)) QuestRun(Classes.ShogunParagon, "fotia", "r5", "Left", 5755,  false);
         return;
      } 

      if (bot.Bank.Contains("Shogun Dage Pet")) {
         Unbank("Shogun Dage Pet");
         while (!InvHas("Legion Token", Qty)) QuestRun(Classes.BrightParagon, "fotia", "r5", "Left", 5756,  false);
         return;
      }
      

      // Do dreadrock
      GetUndeadChampion();
      while (!InvHas("Legion Token", Qty)) QuestRun(Classes.DreadRock, "dreadrock", "r4", "Right", 4849, false);
      return;
   }




   /// ================================================ ///
   ///                  Items Section 
   /// ================================================ ///
   public class Items {}
   public void GetPotion(string PotionName, int Qty, int Buy=30) {
      ExitCombat();
      SafeMapJoin("alchemyacademy", "Enter", "Spawn", false, 99999);
      bot.Sleep(1500);
      bot.Shops.Load(2036);
      bot.Sleep(2000);

      if (bot.Player.Gold < 225000) Buy = bot.Player.Gold/7500;

      foreach (int value in Enumerable.Range(1, Buy)) {
         bot.Shops.BuyItem("Gold Voucher 7.5k");
         bot.Sleep(500);
         bot.Shops.BuyItem(PotionName);
         bot.Sleep(500);
      }

   }


   public void GetUndeadChampion() {
      Unbank("Undead Champion");
      if (InvHas("Undead Champion")) return;
      if (bot.Player.Gold < 50000) GetGold(50000);

      SafeMapJoin("underworld", "s1", "Left", false, 99999);
      bot.Sleep(2500);
      bot.Shops.Load(216);
      bot.Sleep(3000);
      bot.Shops.BuyItem("Undead Champion");
      bot.Sleep(2500);
      return;
   }

   public void GetDarkCasterClass() {
      string[] DarkCasters = {
         "Infinite Legion Dark Caster",
         "Dark Caster", 
         "Immortal Dark Caster",
         "Arcane Dark Caster",
         "Mystical Dark Caster",
         "Timeless Dark Caster",
         "Evolved Dark Caster",
         "Infinite Dark Caster",
         "Legion Evolved Dark Caster",
      };

      ExitCombat();

      bot.Player.LoadBank();
      foreach (string darkclass in DarkCasters) {
         if (InvHas(darkclass)) {
            return;
         }
         if (bot.Bank.Contains(darkclass)) {
            bot.Bank.ToInventory(darkclass);
            return;
         }
      }
      GetLegionToken(2000);

      SafeMapJoin("underworld", "s1", "Left", false, 99999);
      bot.Sleep(2500);
      bot.Shops.Load(238);
      bot.Sleep(3000);
      bot.Shops.BuyItem("Infinite Legion Dark Caster");
      bot.Sleep(2500);
      return;


   }


   /// ================================================ ///
   ///                  Items Section 
   /// ================================================ ///
   public class Constants {}
   public string[] AllLegionItems = {
		"Legion Revenant",

      "Undead Champion",
      "Felicitous Philtre",
      "Endurance Draught",
      "Potion of Evasion",
      "Infinite Legion Dark Caster",

      "Revenant's Spellscroll",
      "Aeacus Empowered",
      "Tethered Soul",
      "Darkened Essence",
      "Dracolich Contract",

      "Conquest Wreath",
      "Grim Cohort Conquered",
      "Ancient Cohort Conquered",
      "Pirate Cohort Conquered",
      "Battleon Cohort Conquered",
      "Mirror Cohort Conquered",
      "Darkblood Cohort Conquered",
      "Vampire Cohort Conquered",
      "Spirit Cohort Conquered",
      "Dragon Cohort Conquered",
      "Doomwood Cohort Conquered",

      "Exalted Crown",
      "Hooded Legion Cowl",
      "Legion Token",
      "Dage's Favor",
      "Emblem of Dage",
      "Diamond Token of Dage",
      "Dark Token",
      "Defeated Makai",
      "Legion Seal",
      "Gem of Mastery"
   };

   public string[] BankLegionFealty1 = {
      "Grim Cohort Conquered",
      "Ancient Cohort Conquered",
      "Pirate Cohort Conquered",
      "Battleon Cohort Conquered",
      "Mirror Cohort Conquered",
      "Darkblood Cohort Conquered",
      "Vampire Cohort Conquered",
      "Spirit Cohort Conquered",
      "Dragon Cohort Conquered",
      "Doomwood Cohort Conquered",

      "Hooded Legion Cowl",
      "Legion Token",
      "Dage's Favor",
      "Emblem of Dage",
      "Diamond Token of Dage",
      "Dark Token",
      "Defeated Makai",
      "Legion Seal",
      "Gem of Mastery",

   };

   public string[] BankLegionFealty2 = {
      // "Revenant's Spellscroll",
      "Aeacus Empowered",
      "Tethered Soul",
      "Darkened Essence",
      "Dracolich Contract",

      "Hooded Legion Cowl",
      "Legion Token",
      "Dage's Favor",
      "Emblem of Dage",
      "Diamond Token of Dage",
      "Dark Token",
      "Defeated Makai",
      "Legion Seal",
      "Gem of Mastery",
   };

   public string[] BankLegionFealty3 = {
      "Grim Cohort Conquered",
      "Ancient Cohort Conquered",
      "Pirate Cohort Conquered",
      "Battleon Cohort Conquered",
      "Mirror Cohort Conquered",
      "Darkblood Cohort Conquered",
      "Vampire Cohort Conquered",
      "Spirit Cohort Conquered",
      "Dragon Cohort Conquered",
      "Doomwood Cohort Conquered",

      // "Revenant's Spellscroll",
      "Aeacus Empowered",
      "Tethered Soul",
      "Darkened Essence",
      "Dracolich Contract",
   };

   // Unbank lists
   public string[] UnbankLegionFeality1 = {
      "Revenant's Spellscroll",
      "Aeacus Empowered",
      "Tethered Soul",
      "Darkened Essence",
      "Dracolich Contract",
   };

   public string[] UnbankLegionFeality2 = {
      "Grim Cohort Conquered",
      "Ancient Cohort Conquered",
      "Pirate Cohort Conquered",
      "Battleon Cohort Conquered",
      "Mirror Cohort Conquered",
      "Darkblood Cohort Conquered",
      "Vampire Cohort Conquered",
      "Spirit Cohort Conquered",
      "Dragon Cohort Conquered",
      "Doomwood Cohort Conquered",
   };

   public string[] UnbankLegionFeality3 = {
      "Exalted Crown",
      "Hooded Legion Cowl",
      "Legion Token",
      "Dage's Favor",
      "Emblem of Dage",
      "Diamond Token of Dage",
      "Dark Token",
      "Defeated Makai",
      "Legion Seal",
      "Gem of Mastery",
   };

   public string[] FealtyRewards = {
      "Revenant's Spellscroll",
      "Conquest Wreath",
      "Exalted Crown",
      "Legion Revenant"
   };

   public enum GoldFarmEnum {
      Public,
      Private
   }




   // public void ExitHandler() {
   //    bot.RegisterHandler(2, b => {
   //       if (bot.ShouldExit()) {
   //          bot.SetGameObject("stage.frameRate", 30);
   //          bot.Options.LagKiller = false;
   //       }
   //   });
   // }


}