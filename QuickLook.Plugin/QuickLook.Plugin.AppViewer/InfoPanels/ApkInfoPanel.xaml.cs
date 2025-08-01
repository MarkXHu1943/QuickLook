﻿// Copyright © 2017-2025 QL-Win Contributors
//
// This file is part of QuickLook program.
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
//
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
//
// You should have received a copy of the GNU General Public License
// along with this program.  If not, see <http://www.gnu.org/licenses/>.

using QuickLook.Common.ExtensionMethods;
using QuickLook.Common.Helpers;
using QuickLook.Common.Plugin;
using QuickLook.Plugin.AppViewer.PackageParsers.Apk;
using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Media.Imaging;

namespace QuickLook.Plugin.AppViewer.InfoPanels;

public partial class ApkInfoPanel : UserControl, IAppInfoPanel
{
    private readonly ContextObject _context;

    public ApkInfoPanel(ContextObject context)
    {
        _context = context;

        DataContext = this;
        InitializeComponent();

        string translationFile = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "Translations.config");
        applicationNameTitle.Text = TranslationHelper.Get("APP_NAME", translationFile);
        versionNameTitle.Text = TranslationHelper.Get("APP_VERSION_NAME", translationFile);
        versionCodeTitle.Text = TranslationHelper.Get("APP_VERSION_CODE", translationFile);
        packageNameTitle.Text = TranslationHelper.Get("PACKAGE_NAME", translationFile);
        abisTitle.Text = TranslationHelper.Get("ABI", translationFile);
        minSdkVersionTitle.Text = TranslationHelper.Get("APP_MIN_SDK_VERSION", translationFile);
        targetSdkVersionTitle.Text = TranslationHelper.Get("APP_TARGET_SDK_VERSION", translationFile);
        totalSizeTitle.Text = TranslationHelper.Get("TOTAL_SIZE", translationFile);
        modDateTitle.Text = TranslationHelper.Get("LAST_MODIFIED", translationFile);
        permissionsGroupBox.Header = TranslationHelper.Get("PERMISSIONS", translationFile);
    }

    public void DisplayInfo(string path)
    {
        var name = Path.GetFileName(path);
        filename.Text = string.IsNullOrEmpty(name) ? path : name;

        _ = Task.Run(() =>
        {
            if (File.Exists(path))
            {
                var size = new FileInfo(path).Length;
                IApkInfo info = Path.GetExtension(Plugin.ConfirmPath(path)).ToLower() switch
                {
                    ".apk" => ApkParser.Parse(path),
                    ".aab" => AabParser.Parse(path),
                    _ => throw new NotSupportedException("Extension is not supported."),
                };
                var last = File.GetLastWriteTime(path);

                Dispatcher.Invoke(() =>
                {
                    applicationName.Text = info.Label;
                    versionName.Text = info.VersionName;
                    versionCode.Text = info.VersionCode;
                    abis.Text = string.Join(", ", info.ABIs ?? []);
                    packageName.Text = info.PackageName;
                    minSdkVersion.Text = info.MinSdkVersion;
                    targetSdkVersion.Text = info.TargetSdkVersion;
                    totalSize.Text = size.ToPrettySize(2);
                    modDate.Text = last.ToString(CultureInfo.CurrentCulture);
                    permissions.ItemsSource = info.Permissions;

                    if (info.HasIcon)
                    {
                        try
                        {
                            using var stream = new MemoryStream(info.Logo);
                            var icon = new BitmapImage();
                            icon.BeginInit();
                            icon.CacheOption = BitmapCacheOption.OnLoad;
                            icon.StreamSource = stream;
                            icon.EndInit();
                            icon.Freeze();
                            image.Source = icon;
                        }
                        catch (Exception e)
                        {
                            Debug.WriteLine(e);
                            image.Source = new BitmapImage(new Uri("pack://application:,,,/QuickLook.Plugin.AppViewer;component/Resources/android.png"));
                        }
                    }
                    else
                    {
                        image.Source = new BitmapImage(new Uri("pack://application:,,,/QuickLook.Plugin.AppViewer;component/Resources/android.png"));
                    }

                    _context.IsBusy = false;
                });
            }
        });
    }
}
