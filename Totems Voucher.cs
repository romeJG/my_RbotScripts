using RBot;
using System.Linq;

public class Script {

	public void ScriptMain(ScriptInterface bot){
		bot.Options.SafeTimings = true;
		bot.Options.RestPackets = true;
		
		bot.Skills.StartTimer();
		
		
		bot.Bank.ToInventory("Essence of Nulgath");
		bot.Bank.ToInventory("Voucher of Nulgath (non-mem)");
		bot.Bank.ToInventory("Totem of Nulgath");
		//4778
		
		if(bot.Map.Name != "tercessuinotlim"){
	        bot.Player.Join("citadel", "m22", "Right");
            bot.Player.Join("tercessuinotlim", "m2", "Bottom");
        }
        
		while(!bot.ShouldExit() && !bot.Inventory.Contains("Totem of Nulgath", 30)){
			bot.Quests.EnsureAccept(4778);
			
			bot.Player.HuntForItem("Dark makai", "Essence of Nulgath", 60);
			
			bot.Player.Jump("Enter", "Spawn");
			bot.Quests.EnsureComplete(4778, 5357);
			
			bot.Wait.ForPickup("Totem of Nulgath");
			bot.Player.Pickup("Totem of Nulgath");
		}
	}
}
