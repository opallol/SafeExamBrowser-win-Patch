/*
 * Copyright (c) 2026 ETH Zürich, IT Services
 *
 * This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at http://mozilla.org/MPL/2.0/.
 */

namespace SafeExamBrowser.SystemComponents.Contracts.Camera
{
	/// <summary>
	/// Describes a camera input device. Ported from the 1.0.2 proctoring patch.
	/// </summary>
	public class CameraDevice
	{
		/// <summary>
		/// The device identifier (DirectShow moniker string).
		/// </summary>
		public string DeviceId { get; set; }

		/// <summary>
		/// The human-readable device name.
		/// </summary>
		public string Name { get; set; }
	}
}
