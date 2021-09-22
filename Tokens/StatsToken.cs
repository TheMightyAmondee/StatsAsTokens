﻿// Copyright (C) 2021 Vertigon
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
// 
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
// 
// You should have received a copy of the GNU General Public License
// along with this program.  If not, see https://www.gnu.org/licenses/.

using StardewModdingAPI;
using StardewValley;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace StatsAsTokens
{
	internal class StatsToken : BaseToken
	{
		/*********
		** Fields
		*********/

		/// <summary>The game stats as of the last context update.</summary>
		private readonly Dictionary<string, Stats> statsDict;
		/// <summary>Array of public fields in the type StardewValley.Stats.</summary>
		private readonly FieldInfo[] statFields;

		/*********
		** Constructor
		*********/

		public StatsToken()
		{
			statsDict = new(StringComparer.OrdinalIgnoreCase)
			{
				[host] = new Stats(),
				[loc] = new Stats()
			};

			foreach (KeyValuePair<string, Stats> pair in statsDict)
			{
				InitializeOtherStatFields(pair.Value);
			}

			statFields = typeof(Stats).GetFields();
		}

		/*********
		** Public methods
		*********/

		/****
		** Metadata
		****/

		public override bool TryValidateInput(string input, out string error)
		{
			error = "";
			string[] args = input.ToLower().Trim().Split('|');

			if (args.Count() == 2)
			{
				if (!args[0].Contains("player="))
				{
					error += "Named argument 'player' not provided. ";
				}
				else if (args[0].IndexOf('=') == args[0].Length - 1)
				{
					error += "Named argument 'player' not provided a value. Must be one of the following values: 'host', 'local'. ";
				}
				else
				{
					// accept hostplayer or host, localplayer or local
					string playerType = args[0].Substring(args[0].IndexOf('=') + 1).Trim().Replace("player", "");
					if (!(playerType.Equals("host") || playerType.Equals("local")))
					{
						error += "Named argument 'player' must be one of the following values: 'host', 'local'. ";
					}
				}

				if (!args[1].Contains("stat="))
				{
					error += "Named argument 'stat' not provided. ";
					return false;
				}
				else if (args[1].IndexOf('=') == args[1].Length - 1)
				{
					error += "Named argument 'stat' must be a string consisting of alphanumeric values. ";
				}
				else
				{
					string statArg = args[1].Substring(args[1].IndexOf('=') + 1);
					if (statArg.Any(ch => !char.IsLetterOrDigit(ch) && ch != ' '))
					{
						error += "Only alphanumeric values may be provided to 'stat' argument. ";
					}
				}
			}
			else
			{
				error += "Incorrect number of arguments provided. A 'player' argument and 'stat' argument should be provided. ";
			}

			return error.Equals("");
		}

		/****
		** State
		****/

		public override bool DidStatsChange()
		{
			bool hasChanged = false;

			string pType;

			// check cached local player stats against Game1's local player stats
			// only needs to happen if player is local and not master
			if (!Game1.IsMasterGame)
			{
				pType = loc;

				foreach (FieldInfo field in statFields)
				{
					if (field.FieldType.Equals(typeof(uint)))
					{
						if (!field.GetValue(Game1.stats).Equals(field.GetValue(statsDict[pType])))
						{
							hasChanged = true;
							field.SetValue(statsDict[pType], field.GetValue(Game1.stats));
						}
					}
					else if (field.FieldType.Equals(typeof(SerializableDictionary<string, uint>)))
					{
						SerializableDictionary<string, uint> otherStats = (SerializableDictionary<string, uint>)field.GetValue(Game1.stats);
						SerializableDictionary<string, uint> cachedOtherStats = statsDict[loc].stat_dictionary;

						foreach (KeyValuePair<string, uint> pair in otherStats)
						{
							if (!cachedOtherStats.ContainsKey(pair.Key))
							{
								hasChanged = true;
								cachedOtherStats[pair.Key] = pair.Value;
							}
							else if (!cachedOtherStats[pair.Key].Equals(pair.Value))
							{
								hasChanged = true;
								cachedOtherStats[pair.Key] = pair.Value;
							}
						}
					}
				}
			}

			pType = host;

			// check cached master player stats against Game1's master player stats
			// needs to happen whether player is host or local
			foreach (FieldInfo field in statFields)
			{
				if (field.FieldType.Equals(typeof(uint)))
				{
					if (!field.GetValue(Game1.MasterPlayer.stats).Equals(field.GetValue(statsDict[pType])))
					{
						hasChanged = true;
						field.SetValue(statsDict[pType], field.GetValue(Game1.MasterPlayer.stats));
					}
				}
				else if (field.FieldType.Equals(typeof(SerializableDictionary<string, uint>)))
				{
					SerializableDictionary<string, uint> otherStats = (SerializableDictionary<string, uint>)field.GetValue(Game1.MasterPlayer.stats);
					SerializableDictionary<string, uint> cachedOtherStats = statsDict[pType].stat_dictionary;

					foreach (KeyValuePair<string, uint> pair in otherStats)
					{
						if (!cachedOtherStats.ContainsKey(pair.Key))
						{
							hasChanged = true;
							cachedOtherStats[pair.Key] = pair.Value;
						}
						else if (!cachedOtherStats[pair.Key].Equals(pair.Value))
						{
							hasChanged = true;
							cachedOtherStats[pair.Key] = pair.Value;
						}
					}
				}
			}

			return hasChanged;
		}

		public override IEnumerable<string> GetValues(string input)
		{
			List<string> output = new();

			string[] args = input.Split('|');

			string playerType = args[0].Substring(args[0].IndexOf('=') + 1).Trim().ToLower().Replace("player", "").Replace(" ", "");
			string stat = args[1].Substring(args[1].IndexOf('=') + 1).Trim().ToLower().Replace(" ", "");

			if (playerType.Equals("host"))
			{
				bool found = TryGetField(stat, host, out string hostStat);

				if (found)
				{
					output.Add(hostStat);
				}
			}
			else if (playerType.Equals("local"))
			{
				bool found = TryGetField(stat, loc, out string hostStat);

				if (found)
				{
					output.Add(hostStat);
				}
			}

			return output;
		}

		/*********
		** Private methods
		*********/

		/// <summary>
		/// Initializes stat fields for internal stat dictionary. These stats are not fields in the <c>Stats</c> object and so do not show up normally until they have been incremented at least once.
		/// </summary>
		/// <param name="stats">The <c>Stats</c> object to initialize the internal stat dictionary of.</param>
		private void InitializeOtherStatFields(Stats stats)
		{
			stats.stat_dictionary = new SerializableDictionary<string, uint>()
			{
				["timesEnchanted"] = 0,
				["beachFarmSpawns"] = 0,
				["childrenTurnedToDoves"] = 0,
				["boatRidesToIsland"] = 0,
				["hardModeMonstersKilled"] = 0,
				["trashCansChecked"] = 0
			};
		}

		/// <summary>
		/// Attempts to find the specified stat field for the specified player type, and if located, passes the value out via <c>foundStat</c>.
		/// </summary>
		/// <param name="statField">The stat to look for</param>
		/// <param name="playerType">The player type to check - host or local</param>
		/// <param name="foundStat">The string to pass the value to if located.</param>
		/// <returns><c>True</c> if located, <c>False</c> otherwise.</returns>
		private bool TryGetField(string statField, string playerType, out string foundStat)
		{
			bool found = false;
			foundStat = "";

			if (playerType.Equals(loc) && Game1.IsMasterGame)
			{
				playerType = host;
			}

			foreach (FieldInfo field in statFields)
			{
				if (field.Name.ToLower().Equals(statField))
				{
					found = true;
					foundStat = field.GetValue(statsDict[playerType]).ToString();
				}
			}

			if (!found)
			{
				foreach (string key in statsDict[playerType].stat_dictionary.Keys)
				{
					if (key.ToLower().Replace(" ", "").Equals(statField))
					{
						found = true;
						foundStat = statsDict[playerType].stat_dictionary[key].ToString();
					}
				}
			}

			return found;
		}
	}
}