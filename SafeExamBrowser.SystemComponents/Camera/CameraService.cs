/*
 * Copyright (c) 2026 ETH Zürich, IT Services
 *
 * This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at http://mozilla.org/MPL/2.0/.
 */

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using AForge.Video;
using AForge.Video.DirectShow;
using SafeExamBrowser.Logging.Contracts;
using SafeExamBrowser.SystemComponents.Contracts.Camera;

namespace SafeExamBrowser.SystemComponents.Camera
{
	/// <summary>
	/// Captures webcam frames via DirectShow (AForge) and exposes the latest JPEG frame. Ported from the 1.0.2 proctoring patch.
	/// </summary>
	public class CameraService : ICameraService
	{
		private readonly ILogger logger;
		private readonly object frameLock = new object();

		private VideoCaptureDevice videoSource;
		private byte[] currentFrame;
		private int targetFrameIntervalMs = 100;
		private int lastFrameTimestamp;

		private readonly ImageCodecInfo cachedJpegEncoder;
		private readonly EncoderParameters cachedEncoderParams;

		public bool IsCapturing => videoSource != null && videoSource.IsRunning;

		public event EventHandler<FrameCapturedEventArgs> FrameCaptured;

		public CameraService(ILogger logger)
		{
			this.logger = logger;
			this.cachedJpegEncoder = GetEncoder(ImageFormat.Jpeg);
			this.cachedEncoderParams = new EncoderParameters(1)
			{
				Param = { [0] = new EncoderParameter(Encoder.Quality, 40L) }
			};
		}

		public IList<CameraDevice> GetAvailableDevices()
		{
			var devices = new List<CameraDevice>();

			try
			{
				foreach (FilterInfo filter in new FilterInfoCollection(FilterCategory.VideoInputDevice))
				{
					devices.Add(new CameraDevice
					{
						DeviceId = filter.MonikerString,
						Name = filter.Name
					});
				}
			}
			catch (Exception e)
			{
				logger.Error("Failed to enumerate camera devices", e);
			}

			return devices;
		}

		public void StartCapture(string deviceId, int width, int height, int frameRate)
		{
			StopCapture();

			try
			{
				targetFrameIntervalMs = frameRate > 0 ? 1000 / frameRate : 100;
				logger.Info($"Starting camera capture. Device: {deviceId}, Resolution: {width}x{height}, FPS: {frameRate}");

				videoSource = new VideoCaptureDevice(deviceId);

				VideoCapabilities best = null;
				var bestDelta = int.MaxValue;

				foreach (var capability in videoSource.VideoCapabilities)
				{
					var delta = Math.Abs(capability.FrameSize.Width - width) + Math.Abs(capability.FrameSize.Height - height);

					if (delta < bestDelta)
					{
						bestDelta = delta;
						best = capability;
					}
				}

				if (best != null)
				{
					videoSource.VideoResolution = best;
				}

				videoSource.NewFrame += VideoSource_NewFrame;
				videoSource.Start();
			}
			catch (Exception e)
			{
				logger.Error("Failed to start camera capture for device " + deviceId, e);
			}
		}

		public void StopCapture()
		{
			if (videoSource == null)
			{
				return;
			}

			logger.Info("Stopping camera capture");

			try
			{
				videoSource.SignalToStop();
				videoSource.WaitForStop();
				videoSource.NewFrame -= VideoSource_NewFrame;
			}
			catch (Exception e)
			{
				logger.Error("Error stopping camera capture", e);
			}
			finally
			{
				videoSource = null;
			}
		}

		public byte[] GetCurrentFrame()
		{
			lock (frameLock)
			{
				return currentFrame;
			}
		}

		private void VideoSource_NewFrame(object sender, NewFrameEventArgs eventArgs)
		{
			var tickCount = Environment.TickCount;

			if (tickCount - lastFrameTimestamp < targetFrameIntervalMs)
			{
				return;
			}

			lastFrameTimestamp = tickCount;

			try
			{
				var frame = eventArgs.Frame;
				byte[] frameData;

				using (var stream = new MemoryStream())
				{
					frame.Save(stream, cachedJpegEncoder, cachedEncoderParams);
					frameData = stream.ToArray();
				}

				lock (frameLock)
				{
					currentFrame = frameData;
				}

				FrameCaptured?.Invoke(this, new FrameCapturedEventArgs
				{
					FrameData = frameData,
					Width = frame.Width,
					Height = frame.Height
				});
			}
			catch (Exception e)
			{
				logger.Debug("Error processing camera frame: " + e.Message);
			}
		}

		private ImageCodecInfo GetEncoder(ImageFormat format)
		{
			foreach (var codec in ImageCodecInfo.GetImageDecoders())
			{
				if (codec.FormatID == format.Guid)
				{
					return codec;
				}
			}

			return null;
		}
	}
}
