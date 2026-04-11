using ExileCore;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace MapNotify_3_28
{
    public partial class MapNotify_3_28 : BaseSettingsPlugin<MapNotifySettings>
    {

        public void ResetConfigs()
        {
            LogMessage("Deleting existing config files...");
            string[] files = { "GoodMods.txt", "BadMods.txt", "ExpectedNodes.txt", "ExpectedBonusNodes.txt" };
            foreach (var file in files)
            {
                var path = Path.Combine(ConfigDirectory, file);
                if (File.Exists(path)) File.Delete(path);
            }
            ExpectedNodes = LoadExpectedNodes();
            ExpectedBonusNodes = LoadExpectedBonusNodes();
            GoodModsDictionary = LoadConfigGoodMod();
            BadModsDictionary = LoadConfigBadMod();

        }

        private HashSet<string> LoadExpectedNodes()
        {
            var path = Path.Combine(ConfigDirectory, "ExpectedNodes.txt");
            var templatePath = Path.Combine(DirectoryFullName, "data", "ExpectedNodes.txt");

            if (!File.Exists(path))
            {
                // Fallback: If running from Temp (Source-compilation), look in the Source folder for templates.
                if (!File.Exists(templatePath) && DirectoryFullName.Contains(@"\Plugins\Temp\"))
                {
                    var sourceTemplatePath = templatePath.Replace(@"\Plugins\Temp\", @"\Plugins\Source\");
                    if (File.Exists(sourceTemplatePath)) templatePath = sourceTemplatePath;
                }

                if (File.Exists(templatePath))
                {
                    LogMessage($"Copying ExpectedNodes template from {templatePath}", 5);
                    File.Copy(templatePath, path);
                }
                else
                {
                    LogError($"Template not found at {templatePath}. Using fallback defaults.", 10);
                }
            }

            if (!File.Exists(path))
            {
                // Fallback: If template is missing, pre-fill with SpecialNodeMapping names
                var defaults = SpecialNodeMapping.Keys.Concat(SpecialNodeMapping.Values).Distinct();
                var content = "# Whitelist for all Atlas nodes (Names or IDs)\r\n" + string.Join("\r\n", defaults);
                File.WriteAllText(path, content);
            }

            return File.ReadAllLines(path)
                .Select(line => line.Trim())
                .Where(line => !string.IsNullOrWhiteSpace(line) && !line.StartsWith("#"))
                .ToHashSet(System.StringComparer.OrdinalIgnoreCase);
        }

        private HashSet<string> LoadExpectedBonusNodes()
        {
            var path = Path.Combine(ConfigDirectory, "ExpectedBonusNodes.txt");
            var templatePath = Path.Combine(DirectoryFullName, "data", "ExpectedBonusNodes.txt");

            if (!File.Exists(path))
            {
                // Fallback: If running from Temp (Source-compilation), look in the Source folder for templates.
                if (!File.Exists(templatePath) && DirectoryFullName.Contains(@"\Plugins\Temp\"))
                {
                    var sourceTemplatePath = templatePath.Replace(@"\Plugins\Temp\", @"\Plugins\Source\");
                    if (File.Exists(sourceTemplatePath)) templatePath = sourceTemplatePath;
                }

                if (File.Exists(templatePath))
                {
                    LogMessage($"Copying ExpectedBonusNodes template from {templatePath}", 5);
                    File.Copy(templatePath, path);
                }
                else
                {
                    LogError($"Template not found at {templatePath}. Using fallback defaults.", 10);
                }
            }

            if (!File.Exists(path))
            {
                // Fallback: Pre-fill with a few known standards if template is missing
                var defaults = new[] { "Mesa", "City Square", "Atoll", "Shore", "Promenade" };
                var content = "# Maps that possess a Bonus Objective (Exclude Unique Maps)\r\n" + string.Join("\r\n", defaults);
                File.WriteAllText(path, content);
            }

            return File.ReadAllLines(path)
                .Select(line => line.Trim())
                .Where(line => !string.IsNullOrWhiteSpace(line) && !line.StartsWith("#"))
                .ToHashSet(System.StringComparer.OrdinalIgnoreCase);
        }

        private Dictionary<string, StyledText> LoadConfigGoodMod()
        {
            var FullDict = new Dictionary<string, StyledText>();
            
            // Load Good Mods. Using indexer assignment prevents crashes if keys overlap between files.
            foreach (var mod in LoadConfigGood(Path.Combine(ConfigDirectory, "GoodMods.txt")))
                FullDict[mod.Key] = mod.Value;
            
            LogMessage("Loaded config files...");
            return FullDict;
        }


        private Dictionary<string, StyledText> LoadConfigBadMod()
        {
            LogMessage("Loading Bad Mods ..");
            return LoadConfigBad(Path.Combine(ConfigDirectory, "BadMods.txt"));
        }


        public Dictionary<string, StyledText> LoadConfigGood(string path)
        {
            if (!File.Exists(path))
                if (path.Contains("GoodMods"))
                    CreateModConfig(path);

            return SafeLoadDictionary(path);
        }

        public Dictionary<string, StyledText> LoadConfigBad(string path)
        {
            var CreateDefaultPath1 = Path.Combine(ConfigDirectory, "BadMods.txt");

            if (!File.Exists(CreateDefaultPath1))
                if (CreateDefaultPath1.Contains("BadMods"))
                    CreateBadModConfig(CreateDefaultPath1);

            return SafeLoadDictionary(path);
        }

        private Dictionary<string, StyledText> SafeLoadDictionary(string path)
        {
            var dict = new Dictionary<string, StyledText>();
            foreach (var line in GenDictionary(path))
            {
                var bricking = false;
                if (line.Length > 3)
                    bool.TryParse(line[3] ?? null, out bricking);

                dict[line[0]] = new StyledText { Text = line[1], Color = SharpToNu(HexToSDXVector4(line[2])), Bricking = bricking };
            }
            return dict;
        }

        public IEnumerable<string[]> GenDictionary(string path)
        {
            return File.ReadAllLines(path).Where(line => !string.IsNullOrWhiteSpace(line) &&
                line.IndexOf(';') >= 0 && !line.StartsWith("#"))
                .Select(line => line.Split('#')[0].Split(';')
                .Select(parts => parts.Trim()).ToArray());
        }

        public void CreateModConfig(string path)
        {
            #region Create Default Map ModConfig
            new FileInfo(path).Directory.Create();
            var outFile =
@"#Contains;Name in tooltip;RGBA colour code;Bricked (true/false, optional, default false)
# GOOD MODS
MapBeyondLeague;Beyond;00FF00FF
MapMonsterIncreasedItemQuantityAndRarity;Increased Quantity and Rarity;00FF00FF";
            File.WriteAllText(path, outFile);
            LogMessage("Created GoodMods.txt...");
            #endregion
        }

        public void CreateBadModConfig(string path)
        {
            #region Create Default Map Bad Config
            new FileInfo(path).Directory.Create();
            var outFile =
@"#Contains;Name in tooltip;RGBA colour code;Bricked (true/false, optional, default false)
# BAD MODS
ElementalReflect;Elemental Reflect;FF0000FF
PhysicalReflect;Physical Reflect;FF0000FF
MapMonsterLifeLeechImmunity;Cannot Leech Life or Mana;FF0000FF
NoLifeESRegen;No Regen;FF007FFF
MapPlayerReducedRegen;60%% Less Regen;FF007FFF
MapPlayerMaxResists;Max Res Down;FF007FFF
MapPlayerCurseEnfeeble;Enfeeble;FF00FFFF
MapPlayerCurseElementalWeak;Elemental Weakness;FF00FFFF
MapPlayerCurseVuln;Vulnerability;FF00FFFF
MapPlayerCurseTemp;Temporal Chains;FF00FFFF
MapPlayerElementalEquilibrium;Elemental Equilibrium;FF00FFFF
MapTwoBosses;Twinned;00FF00FF
MapDangerousBoss;Boss Damage & Speed;00FF00FF
MapMassiveBoss;Boss AoE & Life;00FF00FF
MapMonsterColdDamage;Extra Phys as Cold;FF007FFF
MapMonsterFireDamage;Extra Phys as Fire;FF007FFF
MapMonsterLightningDamage;Extra Phys as Lightning;FF007FFF
MapMonsterLife;More Monster Life;FF007FFF
MapMonsterFast;Monster Speed;FF007FFF
MapBloodlinesModOnMagicsMapWorld;Bloodlines;FF7F00FF
MapNemesis;Nemesis;FF7F00FF
MapDesecratedGround;Desecrated Ground;CCCC00FF
MapShockedGround;Shocked Ground;CCCC00FF
MapChilledGround;Chilled Ground;CCCC00FF
MapBurningGround;Burning Ground;CCCC00FF";
            File.WriteAllText(path, outFile);
            LogMessage("Created BadMods.txt...");
            #endregion
        }
    }
}
