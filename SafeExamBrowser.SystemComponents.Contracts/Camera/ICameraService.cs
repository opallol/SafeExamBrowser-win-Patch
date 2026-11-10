/*
 * Copyright (c) 2026 ETH Zürich, IT Services
 *
 * This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at http://mozilla.org/MPL/2.0/.
 */

using System;
using System.Collections.Generic;

namespace SafeExamBrowser.SystemComponents.Contracts.Camera
{
	/// <summary>
	/// Controls webcam capture and exposes the current frame for streaming. Ported from the 1.0.2 proctoring patch.
	/// </summary>
	public interface ICameraService
	{
		/// <summary>
		/// Indicates whether the service is currently capturing.
		/// </summary>
		bool IsCapturing { get; }

		/// <summary>
		/// Raised whenever a new frame has been captured and encoded.
		/// </summary>
		event EventHandler<FrameCapturedEventArgs> FrameCaptured;

		/// <summary>
		/// Enumerates the available camera input devices.
		/// </summary>
		IList<CameraDevice> GetAvailableDevices();

		/// <summary>
		/// Starts capturing from the given device at the requested resolution and frame rate.
		/// </summary>
		void StartCapture(string deviceId, int width, int height, int frameRate);

		/// <summary>
		/// Stops capturing.
		/// </summary>
		void StopCapture();

		/// <summary>
		/// Returns the most recently captured frame (JPEG-encoded), or <c>null</c> if none is available.
		/// </summary>
		byte[] GetCurrentFrame();
	}
}
