using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Autodesk.Revit.UI;
using AutoPileCap.Services;

namespace AutoPileCap.UI
{
    public partial class LicenseWindow : Window
    {
        private UIApplication _uiapp;
        private DispatcherTimer? _copyFeedbackTimer;

        public LicenseWindow(UIApplication? uiapp = null)
        {
            InitializeComponent();
            _uiapp = uiapp!;
            TxtMachineId.Text = LicenseManager.GetMachineId();
        }

        private void BtnCopy_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Clipboard.SetText(TxtMachineId.Text);
                ShowStatus("Machine ID copied to clipboard!", isError: false);

                if (TxtCopyBtnLabel != null)
                {
                    TxtCopyBtnLabel.Text = "Copied!";
                }
                if (IconCopyDefault != null) IconCopyDefault.Visibility = Visibility.Collapsed;
                if (IconCopySuccess != null) IconCopySuccess.Visibility = Visibility.Visible;

                if (_copyFeedbackTimer == null)
                {
                    _copyFeedbackTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2.5) };
                    _copyFeedbackTimer.Tick += (s, ev) =>
                    {
                        if (TxtCopyBtnLabel != null)
                        {
                            TxtCopyBtnLabel.Text = "Copy ID";
                        }
                        if (IconCopyDefault != null) IconCopyDefault.Visibility = Visibility.Visible;
                        if (IconCopySuccess != null) IconCopySuccess.Visibility = Visibility.Collapsed;
                        _copyFeedbackTimer?.Stop();
                    };
                }
                _copyFeedbackTimer.Stop();
                _copyFeedbackTimer.Start();
            }
            catch (Exception ex)
            {
                ShowStatus("Unable to copy: " + ex.Message, isError: true);
            }
        }

        private void BtnPaste_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (Clipboard.ContainsText())
                {
                    string text = Clipboard.GetText()?.Trim() ?? string.Empty;
                    if (!string.IsNullOrEmpty(text))
                    {
                        TxtActivationCode.Text = text;
                        ShowStatus("Activation code pasted!", isError: false);
                    }
                    else
                    {
                        ShowStatus("Clipboard is empty or does not contain text.", isError: true);
                    }
                }
                else
                {
                    ShowStatus("No text found in clipboard.", isError: true);
                }
            }
            catch (Exception ex)
            {
                ShowStatus("Unable to paste: " + ex.Message, isError: true);
            }
        }

        private void TxtActivationCode_TextChanged(object sender, TextChangedEventArgs e)
        {
            // Clear error banner when user begins typing/editing
            if (StatusBorder != null && StatusBorder.Visibility == Visibility.Visible && StatusIconError.Visibility == Visibility.Visible)
            {
                StatusBorder.Visibility = Visibility.Collapsed;
                TxtError.Text = string.Empty;
            }
        }

        private void BtnActivate_Click(object sender, RoutedEventArgs e)
        {
            string code = TxtActivationCode.Text.Trim();
            if (string.IsNullOrEmpty(code))
            {
                ShowStatus("Please enter or paste your activation code.", isError: true);
                TxtActivationCode.Focus();
                return;
            }

            if (LicenseManager.ValidateActivationCode(TxtMachineId.Text, code))
            {
                LicenseManager.SaveLicenseCode(code);
                ShowStatus("Activation successful! Thank you for using BIM Plus.", isError: false);
                MessageBox.Show("Activation successful!\n\nThank you for choosing BIM Plus⁺ Suite.", "BIM Plus — Activated", MessageBoxButton.OK, MessageBoxImage.Information);
                this.DialogResult = true;
                this.Close();
            }
            else
            {
                ShowStatus("Invalid activation code. Please check and try again.", isError: true);
                TxtActivationCode.Focus();
                TxtActivationCode.SelectAll();
            }
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            this.Close();
        }

        private void ShowStatus(string message, bool isError)
        {
            if (string.IsNullOrEmpty(message))
            {
                if (StatusBorder != null) StatusBorder.Visibility = Visibility.Collapsed;
                return;
            }

            if (StatusBorder != null)
            {
                StatusBorder.Visibility = Visibility.Visible;
                if (isError)
                {
                    StatusBorder.Background = new SolidColorBrush(Color.FromRgb(254, 242, 242));
                    StatusBorder.BorderBrush = new SolidColorBrush(Color.FromRgb(254, 202, 202));
                    TxtError.Foreground = new SolidColorBrush(Color.FromRgb(220, 38, 38));
                    if (StatusIconError != null) StatusIconError.Visibility = Visibility.Visible;
                    if (StatusIconSuccess != null) StatusIconSuccess.Visibility = Visibility.Collapsed;
                }
                else
                {
                    StatusBorder.Background = new SolidColorBrush(Color.FromRgb(240, 253, 244));
                    StatusBorder.BorderBrush = new SolidColorBrush(Color.FromRgb(187, 247, 208));
                    TxtError.Foreground = new SolidColorBrush(Color.FromRgb(22, 163, 74));
                    if (StatusIconError != null) StatusIconError.Visibility = Visibility.Collapsed;
                    if (StatusIconSuccess != null) StatusIconSuccess.Visibility = Visibility.Visible;
                }
            }

            if (TxtError != null)
            {
                TxtError.Text = message;
            }
        }
    }
}
