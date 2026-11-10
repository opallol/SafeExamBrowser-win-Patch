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
using System.Reflection;
using SafeExamBrowser.Logging.Contracts;

namespace SafeExamBrowser.Client.Helpers
{
	/// <summary>
	/// Extracts embedded proctoring asset files (resources named <c>SafeExamBrowser.Client.Assets.*</c>) to a temporary
	/// directory served by the <see cref="LocalAssetServer"/>. Ported from the 1.0.2 proctoring patch.
	/// </summary>
	internal class SimpleAssetLoader
	{
		private const string ASSET_PREFIX = "SafeExamBrowser.Client.Assets.";

		private readonly ILogger logger;
		private readonly string targetDirectory;

		public string AssetDirectory => targetDirectory;

		public SimpleAssetLoader(ILogger logger)
		{
			this.logger = logger;
			this.targetDirectory = Path.Combine(Path.GetTempPath(), "SEB_Assets");
		}

		public void ExtractAssets()
		{
			try
			{
				if (!Directory.Exists(targetDirectory))
				{
					Directory.CreateDirectory(targetDirectory);
					logger.Info("Created asset directory: " + targetDirectory);
				}

				var assembly = Assembly.GetExecutingAssembly();
				var resources = assembly.GetManifestResourceNames().Where(name => name.StartsWith(ASSET_PREFIX)).ToList();

				if (resources.Count == 0)
				{
					logger.Info("No embedded assets found to extract");

					return;
				}

				logger.Info($"Found {resources.Count} embedded assets to extract");

				foreach (var resource in resources)
				{
					ExtractResource(assembly, resource);
				}

				logger.Info("Asset extraction completed");
			}
			catch (Exception e)
			{
				logger.Error("Failed to extract assets", e);
			}
		}

		public void Cleanup()
		{
			try
			{
				if (Directory.Exists(targetDirectory))
				{
					Directory.Delete(targetDirectory, true);
					logger.Info("Cleaned up asset directory");
				}
			}
			catch (Exception e)
			{
				logger.Error("Failed to cleanup asset directory", e);
			}
		}

		private void ExtractResource(Assembly assembly, string resourceName)
		{
			try
			{
				var relative = resourceName.Substring(ASSET_PREFIX.Length);
				var segments = relative.Split('.');

				if (segments.Length < 2)
				{
					logger.Warn("Skipping malformed resource name: " + resourceName);

					return;
				}

				var extension = segments[segments.Length - 1];
				var fileName = segments[segments.Length - 2];
				var folders = segments.Take(segments.Length - 2);
				var directory = folders.Any()
					? Path.Combine(targetDirectory, string.Join(Path.DirectorySeparatorChar.ToString(), folders))
					: targetDirectory;

				if (!Directory.Exists(directory))
				{
					Directory.CreateDirectory(directory);
				}

				var destinationPath = Path.Combine(directory, fileName + "." + extension);

				using (var stream = assembly.GetManifestResourceStream(resourceName))
				using (var destination = new FileStream(destinationPath, FileMode.Create, FileAccess.Write))
				{
					stream.CopyTo(destination);
				}

				logger.Debug("Extracted asset: " + relative + " -> " + destinationPath);
			}
			catch (Exception e)
			{
				logger.Error("Failed to extract resource: " + resourceName, e);
			}
		}
	}
}
