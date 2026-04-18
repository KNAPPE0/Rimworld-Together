using GameServer.Core;
using Shared;
using Shared.Files.Configs;
using Shared.Misc;

namespace GameServer.Commands
{
    public class CMD_Enforcement : CMD_Base
    {
        public CMD_Enforcement()
        {
            Prefix = "enforcement";
            Description = "View or toggle scenario/storyteller/difficulty enforcement. Usage: enforcement [scenario|storyteller|difficulty] [on|off]";
            ParameterCount = -1;
        }

        public override void Action()
        {
            if (CMD_Base.CommandParameters == null || CMD_Base.CommandParameters.Length == 0)
            {
                // Show status
                bool scen = Master.ScenarioValues?.IsEnforced ?? false;
                bool story = Master.StorytellerValues?.IsEnforced ?? false;
                bool diff = Master.DifficultyValues?.IsEnforced ?? false;
                bool modCfg = Master.ModConfig?.IsEnforced ?? false;

                Printer.Title("=== Enforcement Status ===");
                string scenStatus = scen ? "ENFORCED" : "Free";
                string storyStatus = story ? "ENFORCED" : "Free";
                string diffStatus = diff ? "ENFORCED" : "Free";
                string modStatus = modCfg ? "ENFORCED" : "Free";
                string scenName = Master.ScenarioValues?.Name ?? "none";
                string storyName = Master.StorytellerValues?.DefName ?? "none";
                Printer.Warning($"Scenario:    {scenStatus} ({scenName})");
                Printer.Warning($"Storyteller: {storyStatus} ({storyName})");
                Printer.Warning($"Difficulty:  {diffStatus}");
                Printer.Warning($"Mod Config:  {modStatus}");
                Printer.Title("Usage: enforcement [scenario|storyteller|difficulty] [on|off]");
                return;
            }

            string target = CMD_Base.CommandParameters[0].ToLower();
            string action = CMD_Base.CommandParameters.Length > 1 ? CMD_Base.CommandParameters[1].ToLower() : "";

            switch (target)
            {
                case "scenario":
                    if (Master.ScenarioValues == null) { Printer.Warning("No scenario config loaded."); return; }
                    if (action == "on") { Master.ScenarioValues.IsEnforced = true; ScenarioConfigFile.Save(ScenarioConfigFile.SavePath, Master.ScenarioValues); Printer.Title("Scenario enforcement ON"); }
                    else if (action == "off") { Master.ScenarioValues.IsEnforced = false; ScenarioConfigFile.Save(ScenarioConfigFile.SavePath, Master.ScenarioValues); Printer.Title("Scenario enforcement OFF"); }
                    else { string ss = Master.ScenarioValues.IsEnforced ? "ENFORCED" : "Free"; Printer.Warning($"Scenario: {ss} ({Master.ScenarioValues.Name})"); }
                    break;

                case "storyteller":
                    if (Master.StorytellerValues == null) { Printer.Warning("No storyteller config loaded."); return; }
                    if (action == "on") { Master.StorytellerValues.IsEnforced = true; StorytellerConfigFile.Save(StorytellerConfigFile.SavePath, Master.StorytellerValues); Printer.Title("Storyteller enforcement ON"); }
                    else if (action == "off") { Master.StorytellerValues.IsEnforced = false; StorytellerConfigFile.Save(StorytellerConfigFile.SavePath, Master.StorytellerValues); Printer.Title("Storyteller enforcement OFF"); }
                    else { string sts = Master.StorytellerValues.IsEnforced ? "ENFORCED" : "Free"; Printer.Warning($"Storyteller: {sts} ({Master.StorytellerValues.DefName})"); }
                    break;

                case "difficulty":
                    if (Master.DifficultyValues == null) { Printer.Warning("No difficulty config loaded."); return; }
                    if (action == "on") { Master.DifficultyValues.IsEnforced = true; DifficultyConfigFile.Save(DifficultyConfigFile.SavePath, Master.DifficultyValues); Printer.Title("Difficulty enforcement ON"); }
                    else if (action == "off") { Master.DifficultyValues.IsEnforced = false; DifficultyConfigFile.Save(DifficultyConfigFile.SavePath, Master.DifficultyValues); Printer.Title("Difficulty enforcement OFF"); }
                    else { string ds = Master.DifficultyValues.IsEnforced ? "ENFORCED" : "Free"; Printer.Warning($"Difficulty: {ds}"); }
                    break;

                default:
                    Printer.Warning($"Unknown target: {target}. Use: scenario, storyteller, difficulty");
                    break;
            }
        }
    }
}
