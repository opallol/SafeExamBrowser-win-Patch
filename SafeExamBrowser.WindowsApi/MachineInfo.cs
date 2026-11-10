/*
 * Copyright (c) 2026 ETH Zürich, IT Services
 *
 * This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at http://mozilla.org/MPL/2.0/.
 */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Management;
using System.Text;

namespace SafeExamBrowser.WindowsApi
{
	/// <summary>
	/// Collects machine information (motherboard serial, running processes, services) for the proctoring patch.
	/// Ported from the 1.0.2 build (originally in <c>SafeExamBrowser.WindowsApi.MachineInfo</c>).
	/// </summary>
	public static class MachineInfo
	{
		private sealed class ProcessSnapshotInfo
		{
			public int Id { get; set; }
			public string Name { get; set; }
			public string MainWindowTitle { get; set; }
			public string StartTime { get; set; }
			public long WorkingSet { get; set; }
			public string FileName { get; set; }
		}

		private sealed class ServiceSnapshotInfo
		{
			public string Name { get; set; }
			public string DisplayName { get; set; }
			public string State { get; set; }
			public string StartMode { get; set; }
		}

		private const string UNKNOWN_SERIAL = "Unknown";
		private const int MAX_SERIAL_QUERY_ATTEMPTS = 3;

		private static readonly object serialLock = new object();
		private static volatile string cachedSerialNumber;
		private static int serialQueryAttempts;

		public static string GetMotherboardSerialNumber()
		{
			var cached = cachedSerialNumber;

			if (cached != null)
			{
				return cached;
			}

			lock (serialLock)
			{
				if (cachedSerialNumber != null)
				{
					return cachedSerialNumber;
				}

				var serial = QueryMotherboardSerialNumber();
				serialQueryAttempts++;

				if (serial != null)
				{
					cachedSerialNumber = serial;
				}
				else if (serialQueryAttempts >= MAX_SERIAL_QUERY_ATTEMPTS)
				{
					cachedSerialNumber = UNKNOWN_SERIAL;
				}

				return serial ?? UNKNOWN_SERIAL;
			}
		}

		private static string QueryMotherboardSerialNumber()
		{
			try
			{
				using (var searcher = new ManagementObjectSearcher("SELECT SerialNumber FROM Win32_BaseBoard"))
				{
					foreach (ManagementObject item in searcher.Get())
					{
						var serial = item["SerialNumber"]?.ToString();

						if (!string.IsNullOrWhiteSpace(serial))
						{
							return serial;
						}
					}
				}
			}
			catch
			{
			}

			return null;
		}

		public static string GetRunningProcessesAsJson()
		{
			try
			{
				var snapshots = GetRunningProcessSnapshots();
				var builder = new StringBuilder();

				builder.Append("{");
				builder.Append("\"Timestamp\":\"" + DateTime.UtcNow.ToString("o") + "\",");
				builder.Append("\"Count\":" + snapshots.Count + ",");
				builder.Append("\"Processes\":[");

				for (var i = 0; i < snapshots.Count; i++)
				{
					var p = snapshots[i];

					builder.Append("{");
					builder.Append("\"Id\":" + p.Id + ",");
					builder.Append("\"Name\":\"" + EscapeJson(p.Name) + "\",");
					builder.Append("\"MainWindowTitle\":\"" + EscapeJson(p.MainWindowTitle) + "\",");
					builder.Append("\"StartTime\":\"" + EscapeJson(p.StartTime) + "\",");
					builder.Append("\"WorkingSet\":" + p.WorkingSet + ",");
					builder.Append("\"FileName\":\"" + EscapeJson(p.FileName) + "\"");
					builder.Append("}");

					if (i < snapshots.Count - 1)
					{
						builder.Append(",");
					}
				}

				builder.Append("]}");

				return builder.ToString();
			}
			catch (Exception e)
			{
				return "{\"Error\":\"Failed to retrieve processes\",\"Message\":\"" + EscapeJson(e.Message) + "\",\"Timestamp\":\"" + DateTime.UtcNow.ToString("o") + "\"}";
			}
		}

		public static string GetProcessesForApi()
		{
			try
			{
				var snapshots = GetRunningProcessSnapshots();
				var builder = new StringBuilder();

				builder.Append("[");

				for (var i = 0; i < snapshots.Count; i++)
				{
					var p = snapshots[i];

					builder.Append("{");
					builder.Append("\"id\":" + p.Id + ",");
					builder.Append("\"name\":\"" + EscapeJson(p.Name) + "\",");
					builder.Append("\"title\":\"" + EscapeJson(p.MainWindowTitle) + "\"");
					builder.Append("}");

					if (i < snapshots.Count - 1)
					{
						builder.Append(",");
					}
				}

				builder.Append("]");

				return builder.ToString();
			}
			catch (Exception e)
			{
				return "{\"error\":\"Failed to retrieve processes\",\"message\":\"" + EscapeJson(e.Message) + "\"}";
			}
		}

		public static string GetServicesForApi()
		{
			try
			{
				var services = new List<ServiceSnapshotInfo>();

				using (var searcher = new ManagementObjectSearcher("SELECT Name, DisplayName, State, StartMode FROM Win32_Service"))
				{
					foreach (ManagementObject item in searcher.Get())
					{
						using (item)
						{
							services.Add(new ServiceSnapshotInfo
							{
								Name = item["Name"]?.ToString(),
								DisplayName = item["DisplayName"]?.ToString(),
								State = item["State"]?.ToString(),
								StartMode = item["StartMode"]?.ToString()
							});
						}
					}
				}

				var ordered = services.OrderBy(s => s.Name, StringComparer.OrdinalIgnoreCase).ToList();
				var builder = new StringBuilder();

				builder.Append("[");

				for (var i = 0; i < ordered.Count; i++)
				{
					var s = ordered[i];

					builder.Append("{");
					builder.Append("\"name\":\"" + EscapeJson(s.Name) + "\",");
					builder.Append("\"displayName\":\"" + EscapeJson(s.DisplayName) + "\",");
					builder.Append("\"state\":\"" + EscapeJson(s.State) + "\",");
					builder.Append("\"startMode\":\"" + EscapeJson(s.StartMode) + "\"");
					builder.Append("}");

					if (i < ordered.Count - 1)
					{
						builder.Append(",");
					}
				}

				builder.Append("]");

				return builder.ToString();
			}
			catch (Exception e)
			{
				return "{\"error\":\"Failed to retrieve services\",\"message\":\"" + EscapeJson(e.Message) + "\"}";
			}
		}

		private static List<ProcessSnapshotInfo> GetRunningProcessSnapshots()
		{
			var list = new List<ProcessSnapshotInfo>();

			using (var searcher = new ManagementObjectSearcher("SELECT ProcessId, Name, ExecutablePath FROM Win32_Process"))
			{
				foreach (ManagementObject item in searcher.Get())
				{
					using (item)
					{
						var processId = GetProcessId(item);

						if (!processId.HasValue)
						{
							continue;
						}

						var process = TryGetLiveProcess(processId.Value);

						if (process != null)
						{
							var name = GetProcessName(process, item);

							if (!string.IsNullOrWhiteSpace(name))
							{
								list.Add(new ProcessSnapshotInfo
								{
									Id = process.Id,
									Name = name,
									MainWindowTitle = GetSafeMainWindowTitle(process),
									StartTime = GetSafeStartTime(process),
									WorkingSet = GetSafeWorkingSet(process),
									FileName = GetSafeExecutablePath(item) ?? GetSafeFileName(process)
								});
							}
						}
					}
				}
			}

			return list
				.OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
				.ThenBy(p => p.Id)
				.ToList();
		}

		private static int? GetProcessId(ManagementObject process)
		{
			try
			{
				return Convert.ToInt32(process["ProcessId"]);
			}
			catch
			{
				return null;
			}
		}

		private static string GetProcessName(Process liveProcess, ManagementObject process)
		{
			try
			{
				return liveProcess.ProcessName;
			}
			catch
			{
				try
				{
					return Convert.ToString(process["Name"]);
				}
				catch
				{
					return string.Empty;
				}
			}
		}

		private static string GetSafeExecutablePath(ManagementObject process)
		{
			try
			{
				return Convert.ToString(process["ExecutablePath"]);
			}
			catch
			{
				return string.Empty;
			}
		}

		private static Process TryGetLiveProcess(int processId)
		{
			try
			{
				var process = Process.GetProcessById(processId);

				try
				{
					return process.HasExited ? null : process;
				}
				catch
				{
					return null;
				}
			}
			catch
			{
				return null;
			}
		}

		private static string GetSafeMainWindowTitle(Process process)
		{
			try
			{
				return process.MainWindowTitle ?? string.Empty;
			}
			catch
			{
				return string.Empty;
			}
		}

		private static string GetSafeStartTime(Process process)
		{
			try
			{
				return process.StartTime.ToString("o");
			}
			catch
			{
				return string.Empty;
			}
		}

		private static long GetSafeWorkingSet(Process process)
		{
			try
			{
				return process.WorkingSet64;
			}
			catch
			{
				return 0L;
			}
		}

		private static string GetSafeFileName(Process process)
		{
			try
			{
				return process.MainModule?.FileName ?? string.Empty;
			}
			catch
			{
				return string.Empty;
			}
		}

		private static string EscapeJson(string value)
		{
			if (string.IsNullOrEmpty(value))
			{
				return string.Empty;
			}

			return value
				.Replace("\\", "\\\\")
				.Replace("\"", "\\\"")
				.Replace("\n", "\\n")
				.Replace("\r", "\\r")
				.Replace("\t", "\\t");
		}
	}
}
