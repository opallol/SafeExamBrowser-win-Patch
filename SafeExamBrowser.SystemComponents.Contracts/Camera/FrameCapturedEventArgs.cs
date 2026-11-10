/*
 * Copyright (c) 2026 ETH Zürich, IT Services
 *
 * This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at http://mozilla.org/MPL/2.0/.
 */

using System;

namespace SafeExamBrowser.SystemComponents.Contracts.Camera
{
	/// <summary>
	/// Event arguments carrying a captured camera frame (JPEG-encoded). Ported from the 1.0.2 proctoring patch.
	/// </summary>
	public class FrameCapturedEventArgs : EventArgs
	{
		/// <summary>
		/// The JPEG-encoded frame data.
		/// </summary>
		public byte[] FrameData { get; set; }

		/// <summary>
		/// The frame width in pixels.
		/// </summary>
		public int Width { get; set; }

		/// <summary>
		/// The frame height in pixels.
		/// </summary>
		public int Height { get; set; }
	}
}
