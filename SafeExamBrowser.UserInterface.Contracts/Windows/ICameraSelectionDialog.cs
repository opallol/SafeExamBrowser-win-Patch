/*
 * Copyright (c) 2026 ETH Zürich, IT Services
 *
 * This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at http://mozilla.org/MPL/2.0/.
 */

using System.Collections.Generic;
using SafeExamBrowser.SystemComponents.Contracts.Camera;

namespace SafeExamBrowser.UserInterface.Contracts.Windows
{
	/// <summary>
	/// The dialog which lets the user select a camera device for streaming. Ported from the 1.0.2 proctoring patch.
	/// </summary>
	public interface ICameraSelectionDialog
	{
		/// <summary>
		/// Shows the dialog and returns the selected device, or <c>null</c> if the user cancelled.
		/// </summary>
		CameraDevice Show(IList<CameraDevice> devices);
	}
}
