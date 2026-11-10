/*
 * Copyright (c) 2026 ETH Zürich, IT Services
 *
 * This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at http://mozilla.org/MPL/2.0/.
 */

namespace SafeExamBrowser.Browser.Contracts
{
	/// <summary>
	/// Controls the hidden proctoring browser which loads a proctoring URL off-screen. Ported from the 1.0.2 proctoring patch.
	/// </summary>
	public interface IHiddenBrowserManager
	{
		/// <summary>
		/// Starts the hidden browser on its own STA thread, loading the given URL.
		/// </summary>
		void Initialize(string url, bool streamCameraEnabled);

		/// <summary>
		/// Closes the hidden browser and stops its thread.
		/// </summary>
		void Terminate();
	}
}
