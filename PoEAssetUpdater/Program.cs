using LibDat;
using LibGGPK;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;

namespace PoEAssetUpdater
{
	public class Program
	{
		#region Properties

		private static string ApplicationName => Path.GetFileNameWithoutExtension(Assembly.GetExecutingAssembly().Location);

		private const int TotalNumberOfStats = 6;

		private const ulong UndefinedValue = 18374403900871474942L;

		private static readonly char[] NewLineSplitter = "\r\n".ToCharArray();
		private static readonly char[] WhiteSpaceSplitter = "\t ".ToCharArray();

		private static readonly Regex StatDescriptionLangRegex = new Regex("^lang \"(.*)\"$");

		private static readonly string[] TradeAPIStatSuffixes = new string[] { " (Local)", " (Shields)", " (Maps)", " (Legacy)", " (Staves)" };

		private static readonly string[] SuppressedWarningLabels = new string[] { "pseudo", "delve", "monster", "veiled" };

		private static readonly Dictionary<string, string> ItemTradeDataCategoryIdToCategoryMapping = new Dictionary<string, string>()
		{
			["Currency"] = ItemCategory.Currency,
			["Cards"] = ItemCategory.Card,
			["Catalysts"] = ItemCategory.Currency,
			["DeliriumOrbs"] = ItemCategory.Currency,
			["DelveFossils"] = ItemCategory.CurrencyFossil,
			["DelveResonators"] = ItemCategory.CurrencyResonator,
			["Essences"] = ItemCategory.Currency,
			["Fragments"] = ItemCategory.MapFragment,
			["Incubators"] = ItemCategory.CurrencyIncubator,
			["Leaguestones"] = ItemCategory.Leaguestone,
			["MapsBlighted"] = ItemCategory.Map,
			["MapsTier1"] = ItemCategory.Map,
			["MapsTier2"] = ItemCategory.Map,
			["MapsTier3"] = ItemCategory.Map,
			["MapsTier4"] = ItemCategory.Map,
			["MapsTier5"] = ItemCategory.Map,
			["MapsTier6"] = ItemCategory.Map,
			["MapsTier7"] = ItemCategory.Map,
			["MapsTier8"] = ItemCategory.Map,
			["MapsTier9"] = ItemCategory.Map,
			["MapsTier10"] = ItemCategory.Map,
			["MapsTier11"] = ItemCategory.Map,
			["MapsTier12"] = ItemCategory.Map,
			["MapsTier13"] = ItemCategory.Map,
			["MapsTier14"] = ItemCategory.Map,
			["MapsTier15"] = ItemCategory.Map,
			["MapsTier16"] = ItemCategory.Map,
			["Nets"] = ItemCategory.Currency,
			["Oils"] = ItemCategory.Currency,
			["Prophecies"] = ItemCategory.Currency,
			["Scarabs"] = ItemCategory.MapScarab,
			["Vials"] = ItemCategory.Currency,
		};

		private static readonly Dictionary<string, string> BaseItemTypeInheritsFromToCategoryMapping = new Dictionary<string, string>()
		{
			// Accessories
			["AbstractRing"] = ItemCategory.AccessoryRing,
			["AbstractAmulet"] = ItemCategory.AccessoryAmulet,
			["AbstractBelt"] = ItemCategory.AccessoryBelt,
			// Armors/Armours
			["AbstractShield"] = ItemCategory.ArmourShield,
			["AbstractHelmet"] = ItemCategory.ArmourHelmet,
			["AbstractBodyArmour"] = ItemCategory.ArmourChest,
			["AbstractBoots"] = ItemCategory.ArmourBoots,
			["AbstractGloves"] = ItemCategory.ArmourGloves,
			["AbstractQuiver"] = ItemCategory.ArmourQuiver,
			// Currencies
			["AbstractCurrency"] = ItemCategory.Currency,
			["StackableCurrency"] = ItemCategory.Currency,
			["DelveSocketableCurrency"] = ItemCategory.CurrencyResonator,
			["AbstractUniqueFragment"] = ItemCategory.CurrencyPiece,
			// Divination Cards
			["AbstractDivinationCard"] = ItemCategory.Card,
			// Flasks
			["AbstractLifeFlask"] = ItemCategory.Flask,
			["AbstractManaFlask"] = ItemCategory.Flask,
			["AbstractHybridFlask"] = ItemCategory.Flask,
			["CriticalUtilityFlask"] = ItemCategory.Flask,
			["AbstractUtilityFlask"] = ItemCategory.Flask,
			// Gems
			["ActiveSkillGem"] = ItemCategory.GemActivegem,
			["SupportSkillGem"] = ItemCategory.GemSupportGem,
			// Jewels
			["AbstractJewel"] = ItemCategory.Jewel,
			["AbstractAbyssJewel"] = ItemCategory.JewelAbyss,
			// Maps
			["AbstractMap"] = ItemCategory.Map,
			["AbstractMapFragment"] = ItemCategory.MapFragment,
			["AbstractMiscMapItem"] = ItemCategory.MapFragment,
			// Metamorph Samples
			["MetamorphosisDNA"] = ItemCategory.MonsterSample,
			// Watchstones
			["AtlasRegionUpgrade"] = ItemCategory.Watchstone,
			// Weapons
			["AbstractTwoHandSword"] = ItemCategory.WeaponTwoSword,
			["AbstractWand"] = ItemCategory.WeaponWand,
			["AbstractDagger"] = ItemCategory.WeaponDagger,
			["AbstractRuneDagger"] = ItemCategory.WeaponRunedagger,
			["AbstractClaw"] = ItemCategory.WeaponClaw,
			["AbstractOneHandAxe"] = ItemCategory.WeaponOneAxe,
			["AbstractOneHandSword"] = ItemCategory.WeaponOneSword,
			["AbstractOneHandSwordThrusting"] = ItemCategory.WeaponOneSword,
			["AbstractOneHandMace"] = ItemCategory.WeaponOneMace,
			["AbstractSceptre"] = ItemCategory.WeaponSceptre,
			["AbstractBow"] = ItemCategory.WeaponBow,
			["AbstractStaff"] = ItemCategory.WeaponStaff,
			["AbstractWarstaff"] = ItemCategory.WeaponWarstaff,
			["AbstractTwoHandAxe"] = ItemCategory.WeaponTwoAxe,
			["AbstractTwoHandSword"] = ItemCategory.WeaponTwoSword,
			["AbstractTwoHandMace"] = ItemCategory.WeaponTwoMace,
			["AbstractFishingRod"] = ItemCategory.WeaponRod,
			// Ignored (i.e. not exported as they're untradable items!)
			["AbstractMicrotransaction"] = null,
			["AbstractQuestItem"] = null,
			["AbstractLabyrinthItem"] = null,
			["AbstractHideoutDoodad"] = null,
			["LabyrinthTrinket"] = null,
			["AbstactPantheonSoul"] = null,
		};

		#endregion

		#region Public Methods

		public static void Main(string[] args)
		{
			// Validate args array size
			if(args.Length != 2)
			{
				Logger.WriteLine("Invalid number of arguments.");
				PrintUsage();
				return;
			}

			// Validate arguments
			string contentFilePath = args[0];
			if(!File.Exists(contentFilePath))
			{
				Logger.WriteLine($"File '{contentFilePath}' does not exist.");
				PrintUsage();
				return;
			}
			string assetOutputDir = args[1];
			if(!Directory.Exists(assetOutputDir))
			{
				Logger.WriteLine($"Directory '{assetOutputDir}' does not exist.");
				PrintUsage();
				return;
			}

			try
			{
				// Read the GGPKG file
				GrindingGearsPackageContainer container = new GrindingGearsPackageContainer();
				container.Read(contentFilePath, Logger.Write);

				/*ExportBaseItemTypeCategories(contentFilePath, assetOutputDir, container);
				ExportBaseItemTypes(contentFilePath, assetOutputDir, container);
				ExportClientStrings(contentFilePath, assetOutputDir, container);
				//maps.json -> Likely created/maintained manually.
				ExportMods(contentFilePath, assetOutputDir, container);*/
				ExportStats(contentFilePath, assetOutputDir, container);
				//stats-local.json -> Likely/maintained created manually.
				//ExportWords(contentFilePath, assetOutputDir, container);

				Console.WriteLine(string.Empty);
				Console.WriteLine("Press any key to exit...");
				Console.Read();
			}
			catch(Exception ex)
			{
				PrintError($"{ex.Message}\r\n{ex.StackTrace}");
			}
			finally
			{
				Logger.SaveLogs(Path.Combine(assetOutputDir, string.Concat(ApplicationName, ".log")));
			}
		}

		#endregion

		#region Private Methods

		private static void PrintUsage()
		{
			Logger.WriteLine("Usage:");
			Logger.WriteLine($"{ApplicationName} <path-to-Content.ggpk> <asset-output-dir>");
			Console.Read();
		}

		private static void PrintError(string message)
		{
			Logger.WriteLine(string.Empty);
			Logger.WriteLine($"!!! ERROR !!! {message}");
			Logger.WriteLine(string.Empty);
		}

		private static void PrintWarning(string message)
		{
			Logger.WriteLine($"!! WARNING: {message}");
		}

		private static void ExportDataFile(GrindingGearsPackageContainer container, string contentFilePath, string exportFilePath, Action<string, DirectoryTreeNode, JsonWriter> writeData)
		{
			Logger.WriteLine($"Exporting {Path.GetFileName(exportFilePath)}...");

			var dataDir = container.DirectoryRoot.Children.Find(x => x.Name == "Data");

			using(var streamWriter = new StreamWriter(exportFilePath))
			{
				// Create a JSON writer with human-readable output.
				var jsonWriter = new JsonTextWriter(streamWriter)
				{
					Formatting = Formatting.Indented,
					Indentation = 1,
					IndentChar = '\t'
				};
				jsonWriter.WriteStartObject();

				writeData(contentFilePath, dataDir, jsonWriter);

				jsonWriter.WriteEndObject();
			}

			Logger.WriteLine($"Exported '{exportFilePath}'.");
		}

		private static void ExportLanguageDataFile(string contentFilePath, DirectoryTreeNode dataDir, JsonWriter jsonWriter, string datFileName, Action<int, RecordData, JsonWriter> writeRecordData)
		{
			foreach(var language in Language.All)
			{
				// Determine the directory to search for the given datFile. English is the base/main language and isn't located in a sub-folder.
				var searchDir = language == Language.English ? dataDir : dataDir.Children.FirstOrDefault(x => x.Name == language);
				if(searchDir == null)
				{
					Logger.WriteLine($"\t{language} Language folder not found.");
					continue;
				}

				// Find the given datFile.
				var datContainer = GetDatContainer(searchDir, contentFilePath, datFileName);
				if(datContainer == null)
				{
					// An error was already logged.
					continue;
				}

				Logger.WriteLine($"\tExporting {datFileName} in {language}.");

				// Create a node and write the data of each record in this node.
				jsonWriter.WritePropertyName(language);
				jsonWriter.WriteStartObject();

				for(int j = 0, recordsLength = datContainer.Records.Count; j < recordsLength; j++)
				{
					writeRecordData(j, datContainer.Records[j], jsonWriter);
				}

				jsonWriter.WriteEndObject();
			}
		}

		private static void ExportBaseItemTypes(string contentFilePath, string exportDir, GrindingGearsPackageContainer container)
		{
			ExportDataFile(container, contentFilePath, Path.Combine(exportDir, "base-item-types.json"), WriteRecords);

			void WriteRecords(string _, DirectoryTreeNode dataDir, JsonWriter jsonWriter)
			{
				ExportLanguageDataFile(contentFilePath, dataDir, jsonWriter, "BaseItemTypes.dat", WriteSlashSeparatedRecord);
				ExportLanguageDataFile(contentFilePath, dataDir, jsonWriter, "Prophecies.dat", WriteNormalRecord);
				ExportLanguageDataFile(contentFilePath, dataDir, jsonWriter, "MonsterVarieties.dat", WriteSlashSeparatedRecord);
			}

			void WriteSlashSeparatedRecord(int idx, RecordData recordData, JsonWriter jsonWriter)
			{
				string id = recordData.GetDataValueStringByFieldId("Id").Split('/').Last();
				string name = recordData.GetDataValueStringByFieldId("Name");
				jsonWriter.WritePropertyName(id);
				jsonWriter.WriteValue(name);
				jsonWriter.WritePropertyName(name);
				jsonWriter.WriteValue(id);
			}

			void WriteNormalRecord(int idx, RecordData recordData, JsonWriter jsonWriter)
			{
				string id = recordData.GetDataValueStringByFieldId("Id");
				string name = recordData.GetDataValueStringByFieldId("Name");
				jsonWriter.WritePropertyName(id);
				jsonWriter.WriteValue(name);
				jsonWriter.WritePropertyName(name);
				jsonWriter.WriteValue(id);
			}
		}

		private static void ExportClientStrings(string contentFilePath, string exportDir, GrindingGearsPackageContainer container)
		{
			ExportDataFile(container, contentFilePath, Path.Combine(exportDir, "client-strings.json"), WriteRecords);

			void WriteRecords(string _, DirectoryTreeNode dataDir, JsonWriter jsonWriter)
			{
				ExportLanguageDataFile(contentFilePath, dataDir, jsonWriter, "ClientStrings.dat", WriteClientStringRecord);
				ExportLanguageDataFile(contentFilePath, dataDir, jsonWriter, "AlternateQualityTypes.dat", WriteAlternateQualityTypesRecord);
			}

			void WriteClientStringRecord(int idx, RecordData recordData, JsonWriter jsonWriter)
			{
				string id = recordData.GetDataValueStringByFieldId("Id");
				string name = recordData.GetDataValueStringByFieldId("Text");
				jsonWriter.WritePropertyName(id);
				jsonWriter.WriteValue(name);
			}

			void WriteAlternateQualityTypesRecord(int idx, RecordData recordData, JsonWriter jsonWriter)
			{
				string id = string.Concat("Quality", idx.ToString(CultureInfo.InvariantCulture));
				string name = recordData.GetDataValueStringByFieldId("Name");
				jsonWriter.WritePropertyName(id);
				jsonWriter.WriteValue(name);
			}
		}

		private static void ExportWords(string contentFilePath, string exportDir, GrindingGearsPackageContainer container)
		{
			ExportDataFile(container, contentFilePath, Path.Combine(exportDir, "words.json"), WriteRecords);

			void WriteRecords(string _, DirectoryTreeNode dataDir, JsonWriter jsonWriter)
			{
				ExportLanguageDataFile(contentFilePath, dataDir, jsonWriter, "Words.dat", WriteRecord);
			}

			void WriteRecord(int idx, RecordData recordData, JsonWriter jsonWriter)
			{
				string id = idx.ToString(CultureInfo.InvariantCulture);
				string name = recordData.GetDataValueStringByFieldId("Text");
				jsonWriter.WritePropertyName(id);
				jsonWriter.WriteValue(name);
				jsonWriter.WritePropertyName(name);
				jsonWriter.WriteValue(id);
			}
		}

		private static DatContainer GetDatContainer(DirectoryTreeNode dataDir, string contentFilePath, string datFileName)
		{
			var dataFile = dataDir.Files.FirstOrDefault(x => x.Name == datFileName);
			if(dataFile == null)
			{
				Logger.WriteLine($"\t{datFileName} not found in '{dataDir.Name}'.");
				return null;
			}

#warning TODO: Optimize ReadFileContent by using a BinaryReader instead.
			var data = dataFile.ReadFileContent(contentFilePath);
			using(var dataStream = new MemoryStream(data))
			{
				// Read the MemoryStream and create a DatContainer
				return new DatContainer(dataStream, dataFile.Name);
			}
		}

		private static void ExportMods(string contentFilePath, string exportDir, GrindingGearsPackageContainer container)
		{
			ExportDataFile(container, contentFilePath, Path.Combine(exportDir, "mods.json"), WriteRecords);

			void WriteRecords(string _, DirectoryTreeNode dataDir, JsonWriter jsonWriter)
			{
				var modsDatContainer = GetDatContainer(dataDir, contentFilePath, "Mods.dat");
				var statsDatContainer = GetDatContainer(dataDir, contentFilePath, "Stats.dat");

				if(modsDatContainer == null || statsDatContainer == null)
				{
					return;
				}

				// Create the root node.
				jsonWriter.WritePropertyName("Default");
				jsonWriter.WriteStartObject();

				// Group mods
				var groupedRecords = modsDatContainer.Records.Select(RecordSelector).GroupBy(x => x.statNames);

				foreach(var recordGroup in groupedRecords)
				{
					// Write the stat names
					jsonWriter.WritePropertyName(recordGroup.Key);
					jsonWriter.WriteStartObject();
					int recordIdx = 0;
					foreach(var (recordData, statNames, lastValidStatNum) in recordGroup)
					{
						// Write the stat name excluding its group name
						jsonWriter.WritePropertyName(recordData.GetDataValueStringByFieldId("Id").Replace(recordData.GetDataValueStringByFieldId("CorrectGroup"), ""));
						jsonWriter.WriteStartArray();

						// Write all stats in the array
						for(int i = 1; i <= lastValidStatNum; i++)
						{
							WriteMinMaxValues(recordData, jsonWriter, i);
						}

						jsonWriter.WriteEndArray();
						recordIdx++;
					}
					jsonWriter.WriteEnd();
				}
				jsonWriter.WriteEndObject();

				(RecordData recordData, string statNames, int lastValidStatNum) RecordSelector(RecordData recordData)
				{
					List<string> statNames = new List<string>();
					int lastValidStatsKey = 0;
					for(int i = 1; i <= TotalNumberOfStats; i++)
					{
						ulong statsKey = ulong.Parse(recordData.GetDataValueStringByFieldId(string.Concat("StatsKey", i.ToString(CultureInfo.InvariantCulture))));

						if(statsKey != UndefinedValue)
						{
							statNames.Add(statsDatContainer.Records[(int)statsKey].GetDataValueStringByFieldId("Id"));
							lastValidStatsKey = i;
						}
					}
					return (recordData, string.Join(" ", statNames.Distinct().ToArray()), lastValidStatsKey);
				}
			}

			void WriteMinMaxValues(RecordData recordData, JsonWriter jsonWriter, int statNum)
			{
				string statPrefix = string.Concat("Stat", statNum.ToString(CultureInfo.InvariantCulture));
				int minValue = int.Parse(recordData.GetDataValueStringByFieldId(string.Concat(statPrefix, "Min")));
				int maxValue = int.Parse(recordData.GetDataValueStringByFieldId(string.Concat(statPrefix, "Max")));

				jsonWriter.WriteStartObject();
				jsonWriter.WritePropertyName("min");
				jsonWriter.WriteValue(minValue);
				jsonWriter.WritePropertyName("max");
				jsonWriter.WriteValue(maxValue);
				jsonWriter.WriteEndObject();
			}
		}

		private static void ExportStats(string contentFilePath, string exportDir, GrindingGearsPackageContainer container)
		{
			ExportDataFile(container, contentFilePath, Path.Combine(exportDir, "stats.json"), WriteRecords);

			void WriteRecords(string _, DirectoryTreeNode dataDir, JsonWriter jsonWriter)
			{
				var modsDatContainer = GetDatContainer(dataDir, contentFilePath, "Mods.dat");
				var statsDatContainer = GetDatContainer(dataDir, contentFilePath, "Stats.dat");

				DirectoryTreeNode statDescriptionsDir = container.DirectoryRoot.Children.FirstOrDefault(x => x.Name == "Metadata")?.Children.FirstOrDefault(x => x.Name == "StatDescriptions");
				string[] statDescriptionsText = GetStatDescriptions("stat_descriptions.txt");
				string[] mapStatDescriptionsText = GetStatDescriptions("stat_descriptions.txt");
				string[] atlasStatDescriptionsText = GetStatDescriptions("atlas_stat_descriptions.txt");

				if(modsDatContainer == null || statsDatContainer == null || statDescriptionsDir == null || statDescriptionsText == null || atlasStatDescriptionsText == null)
				{
					return;
				}

				Logger.WriteLine($"Parsing Stat Description Files...");

				// Create a list of all stat descriptions
				List<StatDescription> statDescriptions = new List<StatDescription>();
				string[] lines = statDescriptionsText.Concat(mapStatDescriptionsText).Concat(atlasStatDescriptionsText).ToArray();
				for(int lineIdx = 0, lastLineIdx = lines.Length - 1; lineIdx <= lastLineIdx; lineIdx++)
				{
					string line = lines[lineIdx];
					// Description found => read id(s)
					if(line.StartsWith("description"))
					{
						line = lines[++lineIdx];
						string[] ids = line.Split(WhiteSpaceSplitter, StringSplitOptions.RemoveEmptyEntries);
						int statCount = int.Parse(ids[0]);

						// Strip the number indicating how many stats are present from the IDs
						StatDescription statDescription = new StatDescription(ids.Skip(1).ToArray());

						// Initial (first) language is always english
						string language = Language.English;
						while(true)
						{
							// Read the next line as it contains how many mods are added.
							line = lines[++lineIdx];
							int textCount = int.Parse(line);
							for(int i = 0; i < textCount; i++)
							{
								statDescription.ParseAndAddStatLine(language, lines[++lineIdx]);
							}
							if(lineIdx < lastLineIdx)
							{
								// Take a peek at the next line to check if it's a new language, or something else
								line = lines[lineIdx + 1];
								Match match = StatDescriptionLangRegex.Match(line);
								if(match.Success)
								{
									lineIdx++;
									language = match.Groups[1].Value.Replace(" ", "");
								}
								else
								{
									break;
								}
							}
							else
							{
								break;
							}
						}

						statDescriptions.Add(statDescription);
					}
				}

				Logger.WriteLine("Downloading PoE Trade API Stats...");

				// Download the PoE Trade Stats json
				JObject poeTradeStats;
				using(WebClient wc = new WebClient())
				{
					poeTradeStats = JObject.Parse(wc.DownloadString("https://www.pathofexile.com/api/trade/data/stats"));
				}

				Logger.WriteLine("Parsing PoE Trade API Stats...");

				// Parse the PoE Trade Stats
				foreach(var result in poeTradeStats["result"])
				{
					var label = ((string)result["label"]).ToLowerInvariant();
					jsonWriter.WritePropertyName(label);
					jsonWriter.WriteStartObject();
					foreach(var entry in result["entries"])
					{
						string tradeId = ((string)entry["id"]).Substring(label.Length + 1);
						string text = (string)entry["text"];

						// Strip the "local" stat indication from the trade site text
						foreach(var tradeAPIStatSuffix in TradeAPIStatSuffixes)
						{
							if(text.EndsWith(tradeAPIStatSuffix))
							{
								text = text.Substring(0, text.LastIndexOf(tradeAPIStatSuffix));
							}
						}

						StatDescription statDescription = statDescriptions.Find(x => x.HasMatchingStatLine(text));

						if(statDescription == null)
						{
							if(!SuppressedWarningLabels.Contains(label))
							{
								PrintWarning($"Missing {nameof(StatDescription)} for Label '{label}' TradeID '{tradeId}', Desc: '{text}'");
							}
							continue;
						}

						jsonWriter.WritePropertyName(tradeId);
						jsonWriter.WriteStartObject();
						jsonWriter.WritePropertyName("id");
						jsonWriter.WriteValue(statDescription.FullIdentifier);
						if(statsDatContainer.Records.Exists(x => statDescription.HasMatchingIdentifier(x.GetDataValueStringByFieldId("Id")) && bool.Parse(x.GetDataValueStringByFieldId("IsLocal"))))
						{
							jsonWriter.WritePropertyName("mod");
							jsonWriter.WriteValue("local");
						}
						jsonWriter.WritePropertyName("negated");
						jsonWriter.WriteValue(statDescription.Negated);
						jsonWriter.WritePropertyName("text");
						jsonWriter.WriteStartObject();
						for(int i = 0; i < Language.All.Length; i++)
						{
							jsonWriter.WritePropertyName((i + 1).ToString(CultureInfo.InvariantCulture));
							jsonWriter.WriteStartObject();
							foreach(var statLine in statDescription.GetStatLines(Language.All[i]))
							{
								jsonWriter.WritePropertyName(statLine.NumberPart);
								jsonWriter.WriteValue(statLine.StatDescription);
							}
							jsonWriter.WriteEndObject();
						}
						jsonWriter.WriteEndObject();
						jsonWriter.WriteEndObject();
					}
					jsonWriter.WriteEndObject();
				}

				string[] GetStatDescriptions(string fileName)
				{
					var statDescriptionsFile = statDescriptionsDir.Files.FirstOrDefault(x => x.Name == fileName);

					if(statDescriptionsFile == null)
					{
						Logger.WriteLine($"\t{fileName} not found in '{statDescriptionsDir.Name}'.");
						return null;
					}

					Logger.WriteLine($"Reading {statDescriptionsFile.Name}...");
					string content = Encoding.Unicode.GetString(statDescriptionsFile.ReadFileContent(contentFilePath));
					return content
						.Split(NewLineSplitter, StringSplitOptions.RemoveEmptyEntries)
						.Select(x => x.Trim())
						.Where(x => x.Length > 0).ToArray();
				}
			}
		}

		private static void ExportBaseItemTypeCategories(string contentFilePath, string exportDir, GrindingGearsPackageContainer container)
		{
			ExportDataFile(container, contentFilePath, Path.Combine(exportDir, "base-item-type-categories.json"), WriteRecords);

			void WriteRecords(string _, DirectoryTreeNode dataDir, JsonWriter jsonWriter)
			{
				var baseItemTypesDatContainer = GetDatContainer(dataDir, contentFilePath, "BaseItemTypes.dat");
				var propheciesDatContainer = GetDatContainer(dataDir, contentFilePath, "Prophecies.dat");
				var monsterVarietiesDatContainer = GetDatContainer(dataDir, contentFilePath, "MonsterVarieties.dat");
				var itemTradeDataDatContainer = GetDatContainer(dataDir, contentFilePath, "ItemTradeData.dat");

				if(baseItemTypesDatContainer == null || itemTradeDataDatContainer == null)
				{
					return;
				}

				// Parse the Item Trade Data
				Dictionary<string, string> itemTradeDataCategories = new Dictionary<string, string>();
				foreach(var itemTradeData in itemTradeDataDatContainer.Records)
				{
					var categoryId = itemTradeData.GetDataValueStringByFieldId("CategoryId");
					if(!ItemTradeDataCategoryIdToCategoryMapping.TryGetValue(categoryId, out string category))
					{
						PrintError($"Missing {nameof(ItemTradeDataCategoryIdToCategoryMapping)} for '{categoryId}'");
						continue;
					}
					var baseItemTypes = ParseList(itemTradeData.GetDataValueStringByFieldId("Keys0"));
					baseItemTypes.ForEach(x =>
					{
						if(!itemTradeDataCategories.TryGetValue(x, out string existingCategory) || category == existingCategory)
						{
							itemTradeDataCategories[x] = category;
						}
						else
						{
							PrintError($"BaseItemType {x} belongs to two different categories '{existingCategory}' and '{category}'");
						}
					});
				}

				// Create the root node.
				jsonWriter.WritePropertyName("Default");
				jsonWriter.WriteStartObject();

				// Write the Base Item Types
				for(int i = 0, recordCount = baseItemTypesDatContainer.Records.Count; i < recordCount; i++)
				{
					var baseItemType = baseItemTypesDatContainer.Records[i];
					string id = baseItemType.GetDataValueStringByFieldId("Id").Split('/').Last();
					string inheritsFrom = baseItemType.GetDataValueStringByFieldId("InheritsFrom").Split('/').Last();

					// First try to find a specialised trade curreny category; If non exist, check the inheritance mapping for a matching category.
					if(!itemTradeDataCategories.TryGetValue(i.ToString(CultureInfo.InvariantCulture), out string category) &&
						!BaseItemTypeInheritsFromToCategoryMapping.TryGetValue(inheritsFrom, out category))
					{
						PrintError($"Missing {Path.GetFileNameWithoutExtension(baseItemTypesDatContainer.DatName)} Category for '{id}' (InheritsFrom '{inheritsFrom}') at row {i}");
						continue;
					}

					// Special case for Awakened Support Gems
					if(category == ItemCategory.GemSupportGem && id.EndsWith("Plus"))
					{
						category = ItemCategory.GemSupportGemplus;
					}

					// Special case for Cluster Jewels
					if(category == ItemCategory.Jewel && id.StartsWith("JewelPassiveTreeExpansion"))
					{
						category = ItemCategory.JewelCluster;
					}

					// Only write to the json if an appropriate category was found.
					if(category != null)
					{
						jsonWriter.WritePropertyName(id);
						jsonWriter.WriteValue(category);
					}
				}

				// Write the Prophecies
				foreach(var prophecy in propheciesDatContainer.Records)
				{
					jsonWriter.WritePropertyName(prophecy.GetDataValueStringByFieldId("Id"));
					jsonWriter.WriteValue(ItemCategory.Prophecy);
				}

				// Write the Monster Varieties
				foreach(var prophecy in monsterVarietiesDatContainer.Records)
				{
					jsonWriter.WritePropertyName(prophecy.GetDataValueStringByFieldId("Id").Split('/').Last());
					jsonWriter.WriteValue(ItemCategory.MonsterBeast);
				}

				jsonWriter.WriteEndObject();

				List<string> ParseList(string stringifiedList)
				{
					return stringifiedList
						.Substring(1, stringifiedList.Length - 2)// Remove the brackets
						.Split(',')//Split the list
						.Select(x => x.Trim())//Trim any spaces
						.ToList();
				}
			}
		}

		#endregion
	}
}
