/*
 * Copyright (c) 2026 ETH Zürich, IT Services
 *
 * This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at http://mozilla.org/MPL/2.0/.
 */

using CefSharp;
using SafeExamBrowser.Logging.Contracts;
using BrowserSettings = SafeExamBrowser.Settings.Browser.BrowserSettings;

namespace SafeExamBrowser.Browser.Handlers
{
	/// <summary>
	/// Denies direct page access to the camera when camera streaming is enabled, so the page only ever
	/// receives the SEB-provided stream (via the camera polyfill). Ported from the 1.0.2 proctoring patch.
	/// </summary>
	internal class PermissionHandler : IPermissionHandler
	{
		private readonly BrowserSettings settings;
		private readonly ILogger logger;

		internal PermissionHandler(BrowserSettings settings, ILogger logger)
		{
			this.settings = settings;
			this.logger = logger;
		}

		public bool OnRequestMediaAccessPermission(IWebBrowser chromiumWebBrowser, IBrowser browser, IFrame frame, string requestingUrl, MediaAccessPermissionType requestedPermissions, IMediaAccessCallback callback)
		{
			if (settings.StreamCameraEnabled && (requestedPermissions & MediaAccessPermissionType.VideoCapture) == MediaAccessPermissionType.VideoCapture)
			{
				logger.Info($"Denying direct camera access because camera streaming is enabled. Requested: {requestedPermissions} by {requestingUrl}");

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

			callback.Continue(requestedPermissions);

			return true;
		}

		public void OnDismissPermissionPrompt(IWebBrowser chromiumWebBrowser, IBrowser browser, ulong promptId, PermissionRequestResult result)
		{
		}

		public bool OnShowPermissionPrompt(IWebBrowser chromiumWebBrowser, IBrowser browser, ulong promptId, string requestingOrigin, PermissionRequestType requestedPermissions, IPermissionPromptCallback callback)
		{
			return false;
		}
	}
}
