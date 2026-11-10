/*
 * Copyright (c) 2026 ETH Zürich, IT Services
 *
 * This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at http://mozilla.org/MPL/2.0/.
 */

using System;
using System.Drawing;
using System.IO;
using System.Threading;
using SafeExamBrowser.Browser.Handlers;
using SafeExamBrowser.Core.Contracts.OperationModel;
using SafeExamBrowser.Core.Contracts.OperationModel.Events;
using SafeExamBrowser.Logging.Contracts;
using SafeExamBrowser.SystemComponents.Contracts.Camera;
using SafeExamBrowser.UserInterface.Contracts;
using SafeExamBrowser.UserInterface.Contracts.MessageBox;
using SafeExamBrowser.UserInterface.Contracts.Windows;

namespace SafeExamBrowser.Client.Operations
{
	/// <summary>
	/// Selects a camera, starts capture, validates the feed is live (not static) and wires it into the resource
	/// handler for streaming to the exam page. Ported from the 1.0.2 proctoring patch.
	/// </summary>
	internal class CameraStreamOperation : ClientOperation
	{
		private readonly ICameraService cameraService;
		private readonly ILogger logger;
		private readonly IMessageBox messageBox;
		private readonly ISplashScreen splashScreen;
		private readonly IUserInterfaceFactory uiFactory;

		public override event StatusChangedEventHandler StatusChanged;

		public CameraStreamOperation(
			ClientContext context,
			ILogger logger,
			ICameraService cameraService,
			IUserInterfaceFactory uiFactory,
			IMessageBox messageBox,
			ISplashScreen splashScreen) : base(context)
		{
			this.cameraService = cameraService;
			this.logger = logger;
			this.messageBox = messageBox;
			this.splashScreen = splashScreen;
			this.uiFactory = uiFactory;
		}

		public override OperationResult Perform()
		{
			if (!Context.Settings.Proctoring.StreamCameraEnabled)
			{
				return OperationResult.Success;
			}

			logger.Info("Starting camera stream operation...");

			var devices = cameraService.GetAvailableDevices();

			if (devices.Count == 0)
			{
				logger.Error("No camera devices found.");

				return OperationResult.Failed;
			}

			CameraDevice device;

			if (devices.Count == 1)
			{
				device = devices[0];
				logger.Info("Auto-selected the only available camera: " + device.Name);
			}
			else
			{
				device = uiFactory.CreateCameraSelectionDialog().Show(devices);
			}

			if (device == null)
			{
				logger.Warn("User cancelled camera selection.");

				return OperationResult.Aborted;
			}

			cameraService.StartCapture(device.DeviceId, 640, 480, 10);

			if (!ValidateLiveFeed())
			{
				logger.Error("Camera feed is completely static (no motion). Fake camera or placeholder detected.");
				messageBox.Show(
					"Camera feed is completely static or covered.\n\nPlease ensure you are using a real webcam, the lens is not covered, and you are not using a static virtual camera (like OBS).",
					"Camera Error",
					MessageBoxAction.Ok,
					MessageBoxIcon.Error,
					splashScreen);
				cameraService.StopCapture();

				return OperationResult.Failed;
			}

			ResourceHandler.CameraService = cameraService;
			Context.CameraService = cameraService;

			return OperationResult.Success;
		}

		public override OperationResult Revert()
		{
			cameraService?.StopCapture();
			ResourceHandler.CameraService = null;

			return OperationResult.Success;
		}

		private bool ValidateLiveFeed()
		{
			logger.Info("Validating camera stream...");

			Bitmap reference = null;

			try
			{
				for (var attempt = 0; attempt < 50; attempt++)
				{
					var frame = cameraService.GetCurrentFrame();

					if (frame != null && frame.Length > 1024)
					{
						try
						{
							using (var stream = new MemoryStream(frame))
							using (var bitmap = new Bitmap(stream))
							{
								var center = bitmap.GetPixel(bitmap.Width / 2, bitmap.Height / 2);
								var upperLeft = bitmap.GetPixel(bitmap.Width / 3, bitmap.Height / 3);
								var lowerLeft = bitmap.GetPixel(bitmap.Width / 4, bitmap.Height / 4 * 3);

								if (center.ToArgb() != upperLeft.ToArgb() || center.ToArgb() != lowerLeft.ToArgb())
								{
									if (reference == null)
									{
										reference = new Bitmap(bitmap);
										Thread.Sleep(500);

										continue;
									}

									if (HasMotion(reference, bitmap))
									{
										return true;
									}

									reference.Dispose();
									reference = new Bitmap(bitmap);
								}
							}
						}
						catch
						{
						}
					}

					Thread.Sleep(100);
				}
			}
			finally
			{
				reference?.Dispose();
			}

			return false;
		}

		private bool HasMotion(Bitmap reference, Bitmap current)
		{
			var difference = 0;
			var stepX = current.Width / 10;
			var stepY = current.Height / 10;

			for (var x = 0; x < current.Width; x += stepX)
			{
				for (var y = 0; y < current.Height; y += stepY)
				{
					var a = reference.GetPixel(x, y);
					var b = current.GetPixel(x, y);

					difference += Math.Abs(a.R - b.R) + Math.Abs(a.G - b.G) + Math.Abs(a.B - b.B);
				}
			}

			return difference > 10;
		}
	}
}
