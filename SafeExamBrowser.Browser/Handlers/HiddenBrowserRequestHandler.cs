/*
 * Copyright (c) 2026 ETH Zürich, IT Services
 *
 * This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at http://mozilla.org/MPL/2.0/.
 */

using CefSharp;
using CefSharp.Handler;
using SafeExamBrowser.Logging.Contracts;

namespace SafeExamBrowser.Browser.Handlers
{
	/// <summary>
	/// Request handler for the hidden proctoring browser. Ported from the 1.0.2 proctoring patch.
	/// </summary>
	internal class HiddenBrowserRequestHandler : CefSharp.Handler.RequestHandler
	{
		private readonly HiddenBrowserResourceHandler resourceHandler;

		internal HiddenBrowserRequestHandler(ILogger logger, string proctoringUrl)
		{
			resourceHandler = new HiddenBrowserResourceHandler(logger, proctoringUrl);
		}

		protected override IResourceRequestHandler GetResourceRequestHandler(IWebBrowser webBrowser, IBrowser browser, IFrame frame, IRequest request, bool isNavigation, bool isDownload, string requestInitiator, ref bool disableDefaultHandling)
		{
			return resourceHandler;
		}

		protected override bool OnCertificateError(IWebBrowser chromiumWebBrowser, IBrowser browser, CefErrorCode errorCode, string requestUrl, ISslInfo sslInfo, IRequestCallback callback)
		{
			callback.Continue(true);

			return true;
		}
	}
}
