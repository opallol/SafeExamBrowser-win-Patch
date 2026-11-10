/*
 * Copyright (c) 2026 ETH Zürich, IT Services
 *
 * This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at http://mozilla.org/MPL/2.0/.
 */

using CefSharp;
using SafeExamBrowser.Logging.Contracts;

namespace SafeExamBrowser.Browser.Handlers
{
	/// <summary>
	/// Permission handler for the hidden proctoring browser. Blocks direct camera access when streaming is enabled,
	/// otherwise auto-grants requested permissions. Ported from the 1.0.2 proctoring patch.
	/// </summary>
	internal class HiddenBrowserPermissionHandler : IPermissionHandler
	{
		private readonly ILogger logger;
		private readonly bool streamCameraEnabled;

		internal HiddenBrowserPermissionHandler(ILogger logger, bool streamCameraEnabled)
		{
			this.logger = logger;
			this.streamCameraEnabled = streamCameraEnabled;
		}

		public bool OnRequestMediaAccessPermission(IWebBrowser chromiumWebBrowser, IBrowser browser, IFrame frame, string requestingUrl, MediaAccessPermissionType requestedPermissions, IMediaAccessCallback callback)
		{
			if (streamCameraEnabled && (requestedPermissions & MediaAccessPermissionType.VideoCapture) == MediaAccessPermissionType.VideoCapture)
			{
				logger.Info($"Blocking media access permission for hidden browser because camera streaming is enabled: {requestedPermissions}");

				var remaining = requestedPermissions & ~MediaAccessPermissionType.VideoCapture;

				if (remaining != MediaAccessPermissionType.None)
				{
					callback.Continue(remaining);
				}
				else
				{
					callback.Cancel();
				}

				return true;
			}

			logger.Info($"Auto-granting media access permission for hidden browser: {requestedPermissions}");
			callback.Continue(requestedPermissions);

			return true;
		}

		public void OnDismissPermissionPrompt(IWebBrowser chromiumWebBrowser, IBrowser browser, ulong promptId, PermissionRequestResult result)
		{
		}

		public bool OnShowPermissionPrompt(IWebBrowser chromiumWebBrowser, IBrowser browser, ulong promptId, string requestingOrigin, PermissionRequestType requestedPermissions, IPermissionPromptCallback callback)
		{
			logger.Debug($"Auto-granting permission prompt for hidden browser: {requestedPermissions}");
			callback.Continue(PermissionRequestResult.Accept);

			return true;
		}
	}
}
