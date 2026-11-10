/*
 * Copyright (c) 2026 ETH Zürich, IT Services
 *
 * This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at http://mozilla.org/MPL/2.0/.
 */

using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using SafeExamBrowser.SystemComponents.Contracts.Camera;
using SafeExamBrowser.UserInterface.Contracts.Windows;

namespace SafeExamBrowser.UserInterface.Desktop.Windows
{
	public partial class CameraSelectionDialog : Window, ICameraSelectionDialog
	{
		private CameraDevice selectedDevice;

		public CameraSelectionDialog()
		{
			InitializeComponent();
		}

		public CameraDevice Show(IList<CameraDevice> devices)
		{
			CameraListBox.ItemsSource = devices;

			if (devices.Count > 0)
			{
				CameraListBox.SelectedIndex = 0;
			}

			OkButton.IsEnabled = devices.Count > 0;

			ShowDialog();

			return selectedDevice;
		}

		private void CameraListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			OkButton.IsEnabled = CameraListBox.SelectedItem != null;
		}

		private void OkButton_Click(object sender, RoutedEventArgs e)
		{
			selectedDevice = CameraListBox.SelectedItem as CameraDevice;
			DialogResult = true;
			Close();
		}

		private void CancelButton_Click(object sender, RoutedEventArgs e)
		{
			selectedDevice = null;
			DialogResult = false;
			Close();
		}
	}
}
