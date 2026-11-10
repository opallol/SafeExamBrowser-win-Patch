/*
 * Copyright (c) 2026 ETH Zürich, IT Services
 *
 * This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at http://mozilla.org/MPL/2.0/.
 */

using SafeExamBrowser.Core.Contracts.OperationModel;
using SafeExamBrowser.Core.Contracts.OperationModel.Events;
using SafeExamBrowser.I18n.Contracts;
using SafeExamBrowser.Logging.Contracts;

namespace SafeExamBrowser.Client.Operations
{
	/// <summary>
	/// Starts and stops the hidden proctoring browser according to the session settings. Ported from the 1.0.2 proctoring patch.
	/// </summary>
	internal class HiddenBrowserOperation : ClientOperation
	{
		private readonly ILogger logger;

		public override event StatusChangedEventHandler StatusChanged;

		public HiddenBrowserOperation(ClientContext context, ILogger logger) : base(context)
		{
			this.logger = logger;
		}

		public override OperationResult Perform()
		{
			var enabled = (Context.Settings?.Proctoring?.HiddenBrowserEnabled).GetValueOrDefault();
			var url = Context.Settings?.Proctoring?.HiddenBrowserUrl;

			if (!enabled)
			{
				logger.Info("Hidden proctoring browser is disabled");

				return OperationResult.Success;
			}

			logger.Info("Initializing hidden proctoring browser...");
			StatusChanged?.Invoke(TextKey.OperationStatus_InitializeProctoring);

			if (string.IsNullOrWhiteSpace(url))
			{
				logger.Warn("Hidden browser enabled but URL is not configured");

				return OperationResult.Success;
			}

			if (Context.HiddenBrowser == null)
			{
				logger.Error("HiddenBrowser manager is not initialized in context");

				return OperationResult.Failed;
			}

			var streamCameraEnabled = (Context.Settings?.Proctoring?.StreamCameraEnabled).GetValueOrDefault();

			Context.HiddenBrowser.Initialize(url, streamCameraEnabled);
			logger.Info("Hidden proctoring browser initialized with URL: " + url);

			return OperationResult.Success;
		}

		public override OperationResult Revert()
		{
			if (Context.HiddenBrowser != null)
			{
				logger.Info("Terminating hidden proctoring browser...");
				Context.HiddenBrowser.Terminate();
				logger.Info("Hidden proctoring browser terminated");
			}

			return OperationResult.Success;
		}
	}
}
