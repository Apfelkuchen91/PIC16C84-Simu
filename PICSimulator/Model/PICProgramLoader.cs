﻿using PICSimulator.Model.Commands;
using System;
using System.Collections.Generic;
using System.IO;

namespace PICSimulator.Model
{
	static class PICProgramLoader
	{
		public static List<PICCommand> LoadFromFile(string file)
		{
			string[] lines = File.ReadAllLines(file);

			List<PICCommand> result = new List<PICCommand>();

			foreach (string line in lines)
			{
				if (String.IsNullOrWhiteSpace(line))
					continue;

				var v = splitLine(line);

				if (v == null)
					return null;

				uint? pos = ParseHex(v.Item1);
				uint? cmd = ParseHex(v.Item2);
				uint? scpos = ParseHex(v.Item3);

				string txt = v.Item4;

				if (pos == null || cmd == null || scpos == null)
					continue;

				PICCommand pic_cmd = PICComandHelper.CreateCommand(txt, scpos.Value, pos.Value, cmd.Value);

				if (pic_cmd == null)
					return null;

				result.Add(pic_cmd);
			}

			return result;
		}

		private static Tuple<string, string, string, string> splitLine(string line)
		{
			if (line.Length < 27)
			{
				return null;
			}

			string a = line.Substring(00, 05);
			string b = line.Substring(05, 15);
			string c = line.Substring(20, 05);
			string d = line.Substring(25, line.Length - 25);

			return Tuple.Create(a, b, c, d);
		}

		private static uint? ParseHex(string v)
		{
			v = v.Trim();
			v = v.TrimStart('0');

			try
			{
				return Convert.ToUInt32(v, 16);
			}
			catch (FormatException)
			{
				return null;
			}
			catch (ArgumentException)
			{
				return null;
			}
		}
	}
}