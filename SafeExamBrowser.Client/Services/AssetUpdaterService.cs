/*
 * Copyright (c) 2026 ETH Zürich, IT Services
 *
 * This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at http://mozilla.org/MPL/2.0/.
 */

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using SafeExamBrowser.Browser.Contracts;
using SafeExamBrowser.Logging.Contracts;
using SafeExamBrowser.WindowsApi;

namespace SafeExamBrowser.Client.Services
{
	/// <summary>
	/// Periodically writes the running processes (process.json) and visited URLs (link.json) into the asset
	/// directory served by the local asset server. Ported from the 1.0.2 proctoring patch.
	/// </summary>
	internal class AssetUpdaterService
	{
		private const int UPDATE_INTERVAL_MS = 600000;

		private readonly ILogger logger;
		private readonly string assetDirectory;
		private readonly object lockObject = new object();
		private readonly HashSet<string> visitedUrls = new HashSet<string>();

		private Timer updateTimer;

		public AssetUpdaterService(ILogger logger, string assetDirectory)
		{
			this.logger = logger;
			this.assetDirectory = assetDirectory;
		}

		public void Start()
		{
			if (updateTimer == null)
			{
				updateTimer = new Timer(UpdateCallback, null, 0, UPDATE_INTERVAL_MS);
				logger.Info("AssetUpdaterService started");
			}
		}

		public void Stop()
		{
			if (updateTimer != null)
			{
				updateTimer.Dispose();
				updateTimer = null;
				logger.Info("AssetUpdaterService stopped");
			}
		}

		public void RegisterBrowserWindow(IBrowserApplication browserApplication)
		{
			// ponytail: no-op registration. The 1.0.2 handler for window changes was itself empty, so navigation-driven
			// link tracking never populated link.json there either; process.json (the real signal) is written by the timer.
			// Wire up IBrowserApplication navigation events here and call TrackUrl(...) if URL tracking is ever needed.
			if (browserApplication == null)
			{
				return;
			}

			logger.Debug("Registered browser application for link tracking (no-op)");
		}

		public void TrackUrl(string url)
		{
			if (string.IsNullOrWhiteSpace(url) || url.StartsWith("about:") || url.StartsWith("chrome:"))
			{
				return;
			}

			lock (lockObject)
			{
				if (!visitedUrls.Contains(url))
				{
					visitedUrls.Add(url);
					logger.Debug("Tracked URL: " + url);
					UpdateLinkJson();
				}
			}
		}

		private void UpdateCallback(object state)
		{
			try
			{
				UpdateProcessJson();
				UpdateLinkJson();
			}
			catch (Exception e)
			{
				logger.Error("Failed to update asset files", e);
			}
		}

		private void UpdateProcessJson()
		{
			try
			{
				File.WriteAllText(Path.Combine(assetDirectory, "process.json"), MachineInfo.GetRunningProcessesAsJson());
				logger.Debug("Updated process.json");
			}
			catch (Exception e)
			{
				logger.Error("Failed to update process.json", e);
			}
		}

		private void UpdateLinkJson()
		{
			try
			{
				lock (lockObject)
				{
					var timestamp = DateTime.UtcNow.ToString("o");
					var builder = new StringBuilder();

					builder.Append("{");
					builder.Append("\"Timestamp\":\"" + timestamp + "\",");
					builder.Append("\"Count\":" + visitedUrls.Count + ",");
					builder.Append("\"Links\":[");

					var links = visitedUrls.ToList();

					for (var i = 0; i < links.Count; i++)
					{
						builder.Append("{");
						builder.Append("\"Index\":" + (i + 1) + ",");
						builder.Append("\"Url\":\"" + EscapeJson(links[i]) + "\",");
						builder.Append("\"Visited\":\"" + timestamp + "\"");
						builder.Append("}");

						if (i < links.Count - 1)
						{
							builder.Append(",");
						}
					}

					builder.Append("]}");

					File.WriteAllText(Path.Combine(assetDirectory, "link.json"), builder.ToString());
					logger.Debug("Updated link.json");
				}
			}
			catch (Exception e)
			{
				logger.Error("Failed to update link.json", e);
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
