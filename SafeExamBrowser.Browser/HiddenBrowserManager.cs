/*
 * Copyright (c) 2026 ETH Zürich, IT Services
 *
 * This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at http://mozilla.org/MPL/2.0/.
 */

using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using CefSharp;
using CefSharp.WinForms;
using SafeExamBrowser.Browser.Contracts;
using SafeExamBrowser.Browser.Handlers;
using SafeExamBrowser.Logging.Contracts;

namespace SafeExamBrowser.Browser
{
	/// <summary>
	/// Runs an off-screen CEF browser on its own STA thread to load a proctoring URL in the background. Ported from the 1.0.2 proctoring patch.
	/// </summary>
	public class HiddenBrowserManager : IHiddenBrowserManager
	{
		private const int WS_EX_NOACTIVATE = 0x08000000;
		private const int GWL_EXSTYLE = -20;

		private readonly ILogger logger;
		private readonly object lockObject = new object();

		private Form hiddenForm;
		private ChromiumWebBrowser hiddenBrowser;
		private Thread browserThread;
		private bool isInitialized;

		[DllImport("user32.dll")]
		private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

		[DllImport("user32.dll")]
		private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

		public HiddenBrowserManager(ILogger logger)
		{
			this.logger = logger;
		}

		public void Initialize(string url, bool streamCameraEnabled)
		{
			lock (lockObject)
			{
				if (isInitialized)
				{
					logger.Warn("Hidden browser already initialized");

					return;
				}

				if (string.IsNullOrWhiteSpace(url))
				{
					logger.Warn("Hidden browser URL is empty, skipping initialization");

					return;
				}

				logger.Info("Initializing hidden browser with URL: " + url);

				browserThread = new Thread(() => RunBrowserThread(url, streamCameraEnabled))
				{
					IsBackground = true,
					Name = "HiddenBrowserThread"
				};

				browserThread.SetApartmentState(ApartmentState.STA);
				browserThread.Start();

				isInitialized = true;
				logger.Info("Hidden browser thread started successfully");
			}
		}

		public void Terminate()
		{
			lock (lockObject)
			{
				if (!isInitialized)
				{
					return;
				}

				logger.Info("Terminating hidden browser");

				try
				{
					if (hiddenForm != null && !hiddenForm.IsDisposed)
					{
						if (hiddenBrowser != null && !hiddenBrowser.IsDisposed)
						{
							hiddenForm.Invoke((Action)(() => hiddenBrowser.Load("about:blank")));
							Thread.Sleep(500);
						}

						hiddenForm.Invoke((Action)(() => hiddenForm.Close()));
					}

					if (browserThread != null && browserThread.IsAlive)
					{
						browserThread.Join(2000);
					}

					logger.Info("Hidden browser terminated successfully");
				}
				catch (Exception e)
				{
					logger.Error("Error terminating hidden browser", e);
				}
				finally
				{
					isInitialized = false;
					hiddenForm = null;
					hiddenBrowser = null;
					browserThread = null;
				}
			}
		}

		private void RunBrowserThread(string url, bool streamCameraEnabled)
		{
			try
			{
				logger.Debug("Starting hidden browser message loop");

				hiddenForm = new Form
				{
					Width = 640,
					Height = 480,
					ShowInTaskbar = false,
					FormBorderStyle = FormBorderStyle.None,
					Opacity = 0.01,
					StartPosition = FormStartPosition.Manual,
					Location = new Point(-32000, -32000)
				};

				hiddenForm.HandleCreated += (sender, args) =>
				{
					try
					{
						var style = GetWindowLong(hiddenForm.Handle, GWL_EXSTYLE);
						SetWindowLong(hiddenForm.Handle, GWL_EXSTYLE, style | WS_EX_NOACTIVATE);
						logger.Debug("Applied WS_EX_NOACTIVATE style to hidden form");
					}
					catch (Exception e)
					{
						logger.Error("Failed to apply no-activate style", e);
					}
				};

				hiddenBrowser = new ChromiumWebBrowser(url)
				{
					Dock = DockStyle.Fill,
					BrowserSettings = new CefSharp.BrowserSettings
					{
						WindowlessFrameRate = 15,
						WebGl = CefState.Enabled,
						Javascript = CefState.Enabled,
						LocalStorage = CefState.Enabled
					},
					RequestHandler = new HiddenBrowserRequestHandler(logger, url),
					PermissionHandler = new HiddenBrowserPermissionHandler(logger, streamCameraEnabled),
					RenderProcessMessageHandler = new HiddenBrowserRenderProcessMessageHandler(logger, streamCameraEnabled)
				};

				logger.Debug("Attached request, permission, and render process handlers to hidden browser");

				hiddenForm.Controls.Add(hiddenBrowser);
				hiddenForm.FormClosing += (sender, args) => logger.Debug("Hidden form closing");
				hiddenForm.Show();

				logger.Debug("Hidden form created off-screen, entering message loop");
				Application.Run(hiddenForm);
				logger.Debug("Hidden browser message loop exited");
			}
			catch (Exception e)
			{
				logger.Error("Error in hidden browser thread", e);
			}
		}
	}
}
