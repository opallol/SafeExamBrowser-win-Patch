/*
 * Copyright (c) 2026 ETH Zürich, IT Services
 *
 * This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at http://mozilla.org/MPL/2.0/.
 */

using System;
using System.Collections.Specialized;
using System.IO;
using CefSharp;
using CefSharp.Handler;
using SafeExamBrowser.Logging.Contracts;
using SafeExamBrowser.SystemComponents.Contracts.Camera;
using SafeExamBrowser.WindowsApi;

namespace SafeExamBrowser.Browser.Handlers
{
	/// <summary>
	/// Resource handler for the hidden proctoring browser: serves the streamed camera snapshot and injects the
	/// machine identifier header for proctoring requests. Ported from the 1.0.2 proctoring patch.
	/// </summary>
	internal class HiddenBrowserResourceHandler : ResourceRequestHandler
	{
		private readonly ILogger logger;
		private readonly string proctoringUrl;

		/// <summary>
		/// Camera service used to serve the streamed camera snapshot. Set by the client's camera stream operation.
		/// </summary>
		internal static ICameraService CameraService { get; set; }

		internal HiddenBrowserResourceHandler(ILogger logger, string proctoringUrl)
		{
			this.logger = logger;
			this.proctoringUrl = proctoringUrl;
		}

		protected override IResourceHandler GetResourceHandler(IWebBrowser webBrowser, IBrowser browser, IFrame frame, IRequest request)
		{
			if (request.Url != null && request.Url.StartsWith("http://localhost:18923/snapshot", StringComparison.OrdinalIgnoreCase))
			{
				var frameData = CameraService?.GetCurrentFrame();

				if (frameData != null)
				{
					return CefSharp.ResourceHandler.FromStream(new MemoryStream(frameData), "image/jpeg");
				}

				return CefSharp.ResourceHandler.FromString("");
			}

			return base.GetResourceHandler(webBrowser, browser, frame, request);
		}

		protected override CefReturnValue OnBeforeResourceLoad(IWebBrowser webBrowser, IBrowser browser, IFrame frame, IRequest request, IRequestCallback callback)
		{
			AppendMachineIdHeader(webBrowser, request);

			return base.OnBeforeResourceLoad(webBrowser, browser, frame, request, callback);
		}

		private void AppendMachineIdHeader(IWebBrowser webBrowser, IRequest request)
		{
			try
			{
				Uri.TryCreate(webBrowser.Address, UriKind.Absolute, out var pageUrl);
				Uri.TryCreate(request.Url, UriKind.Absolute, out var requestUrl);
				Uri.TryCreate(proctoringUrl, UriKind.Absolute, out var configuredUrl);

				if (configuredUrl == null || requestUrl == null)
				{
					return;
				}

				var sameHostAsConfigured = requestUrl.Host.Equals(configuredUrl.Host, StringComparison.OrdinalIgnoreCase);
				var isMainFrame = request.ResourceType == ResourceType.MainFrame;
				var sameHostAsPage = pageUrl != null && (pageUrl.Host?.Equals(requestUrl.Host, StringComparison.OrdinalIgnoreCase)).GetValueOrDefault();

				if (isMainFrame || (sameHostAsConfigured && sameHostAsPage))
				{
					var headers = new NameValueCollection(request.Headers);
					var serial = MachineInfo.GetMotherboardSerialNumber();

					if (!string.IsNullOrWhiteSpace(serial))
					{
						headers["X-SafeExamBrowser-MachineSerialNumber"] = serial;
						request.Headers = headers;
						logger.Debug("Injected machine ID header for hidden browser request: " + requestUrl.Host);
					}
				}
			}
			catch (Exception e)
			{
				logger.Error("Failed to inject machine ID header in hidden browser", e);
			}
		}
	}
}
