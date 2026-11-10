/*
 * Copyright (c) 2026 ETH Zürich, IT Services
 *
 * This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at http://mozilla.org/MPL/2.0/.
 */

using System;
using System.IO;
using System.Reflection;
using CefSharp;
using SafeExamBrowser.Browser.Content;
using SafeExamBrowser.Logging.Contracts;

namespace SafeExamBrowser.Browser.Handlers
{
	/// <summary>
	/// Injects the camera polyfill into the hidden proctoring browser when streaming is enabled. Ported from the 1.0.2 proctoring patch.
	/// </summary>
	internal class HiddenBrowserRenderProcessMessageHandler : IRenderProcessMessageHandler
	{
		private readonly ILogger logger;
		private readonly bool streamCameraEnabled;

		private string cameraPolyfillScript;

		internal HiddenBrowserRenderProcessMessageHandler(ILogger logger, bool streamCameraEnabled)
		{
			this.logger = logger;
			this.streamCameraEnabled = streamCameraEnabled;
		}

		public void OnContextCreated(IWebBrowser chromiumWebBrowser, IBrowser browser, IFrame frame)
		{
			if (!streamCameraEnabled)
			{
				return;
			}

			try
			{
				if (cameraPolyfillScript == null)
				{
					var assembly = Assembly.GetAssembly(typeof(ContentLoader));
					var path = $"{typeof(ContentLoader).Namespace}.CameraPolyfill.js";

					using (var stream = assembly.GetManifestResourceStream(path))
					using (var reader = new StreamReader(stream))
					{
						cameraPolyfillScript = reader.ReadToEnd();
					}
				}

				frame.ExecuteJavaScriptAsync(cameraPolyfillScript);
				logger.Debug("Injected CameraPolyfill.js into hidden browser frame");
			}
			catch (Exception e)
			{
				logger.Error("Failed to inject CameraPolyfill.js into hidden browser", e);
			}
		}

		public void OnContextReleased(IWebBrowser chromiumWebBrowser, IBrowser browser, IFrame frame)
		{
		}

		public void OnFocusedNodeChanged(IWebBrowser chromiumWebBrowser, IBrowser browser, IFrame frame, IDomNode node)
		{
		}

		public void OnUncaughtException(IWebBrowser chromiumWebBrowser, IBrowser browser, IFrame frame, JavascriptException exception)
		{
		}
	}
}
