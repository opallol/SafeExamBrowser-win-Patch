/*
 * Copyright (c) 2026 ETH Zürich, IT Services
 *
 * This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at http://mozilla.org/MPL/2.0/.
 */

using SafeExamBrowser.Client.Helpers;
using SafeExamBrowser.Client.Services;
using SafeExamBrowser.Core.Contracts.OperationModel;
using SafeExamBrowser.Core.Contracts.OperationModel.Events;
using SafeExamBrowser.Logging.Contracts;
using SafeExamBrowser.UserInterface.Contracts.MessageBox;
using SafeExamBrowser.UserInterface.Contracts.Windows;

namespace SafeExamBrowser.Client.Operations
{
	/// <summary>
	/// Starts the local asset server (serving process.json / link.json and static assets to the exam page) and the
	/// asset updater, aborting startup if the server port cannot be acquired. Ported from the 1.0.2 proctoring patch.
	/// </summary>
	internal class AssetServerOperation : ClientOperation
	{
		private readonly ILogger logger;
		private readonly IMessageBox messageBox;
		private readonly ISplashScreen splashScreen;

		private SimpleAssetLoader assetLoader;
		private LocalAssetServer assetServer;
		private AssetUpdaterService assetUpdater;

		public override event StatusChangedEventHandler StatusChanged;

		public AssetServerOperation(ClientContext context, ILogger logger, IMessageBox messageBox, ISplashScreen splashScreen) : base(context)
		{
			this.logger = logger;
			this.messageBox = messageBox;
			this.splashScreen = splashScreen;
		}

		public override OperationResult Perform()
		{
			logger.Info("Initializing asset infrastructure...");

			assetLoader = new SimpleAssetLoader(logger);
			assetLoader.ExtractAssets();

			assetServer = new LocalAssetServer(assetLoader.AssetDirectory, logger);

			if (!assetServer.Start())
			{
				logger.Error("The local asset server is not running, aborting startup!");
				messageBox.Show(
					"The local asset server could not be started because its network port is already in use. Please close any program using port 48080 and restart the exam.",
					"Asset Server Error",
					MessageBoxAction.Ok,
					MessageBoxIcon.Error,
					splashScreen);

				return OperationResult.Failed;
			}

			assetUpdater = new AssetUpdaterService(logger, assetLoader.AssetDirectory);
			assetUpdater.RegisterBrowserWindow(Context.Browser);
			assetUpdater.Start();

			logger.Info("Asset infrastructure initialized successfully.");

			return OperationResult.Success;
		}

		public override OperationResult Revert()
		{
			assetUpdater?.Stop();
			assetServer?.Stop();
			assetLoader?.Cleanup();

			return OperationResult.Success;
		}
	}
}
