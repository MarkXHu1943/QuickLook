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

using QuickLook.Common.Helpers;
using QuickLook.Common.Plugin;
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;

namespace QuickLook.Plugin.ImageViewer.AnimatedImage.Providers;

internal class NativeProvider : AnimationProvider
{
    public NativeProvider(Uri path, MetaProvider meta, ContextObject contextObject) : base(path, meta, contextObject)
    {
        Animator = new Int32AnimationUsingKeyFrames();
        Animator.KeyFrames.Add(new DiscreteInt32KeyFrame(0,
            KeyTime.FromTimeSpan(TimeSpan.Zero)));
    }

    public override Task<BitmapSource> GetThumbnail(Size renderSize)
    {
        var fullSize = Meta.GetSize();
        var decodeWidth =
            (int)Math.Round(Math.Min(Meta.GetSize().Width, Math.Max(1d, Math.Floor(renderSize.Width))));
        var decodeHeight =
            (int)Math.Round(Math.Min(Meta.GetSize().Height, Math.Max(1d, Math.Floor(renderSize.Height))));
        var orientation = Meta.GetOrientation();
        var rotate = ShouldRotate(orientation);

        return new Task<BitmapSource>(() =>
        {
            try
            {
                var img = new BitmapImage();
                img.BeginInit();
                img.UriSource = Path;
                img.CacheOption = BitmapCacheOption.OnLoad;
                img.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
                // specific renderSize to avoid .net's double to int conversion
                img.DecodePixelWidth = rotate ? decodeHeight : decodeWidth;
                img.DecodePixelHeight = rotate ? decodeWidth : decodeHeight;
                img.EndInit();

                var scaled = rotate
                    ? new TransformedBitmap(img,
                        new ScaleTransform(fullSize.Height / img.PixelWidth, fullSize.Width / img.PixelHeight))
                    : new TransformedBitmap(img,
                        new ScaleTransform(fullSize.Width / img.PixelWidth, fullSize.Height / img.PixelHeight));

                var rotated = ApplyTransformFromExif(scaled, orientation);

                Helper.DpiHack(rotated);
                rotated.Freeze();

                return rotated;
            }
            // System.IO.IOException: Cannot read from the stream.
            // Inner Exception: COMException(0x88982F72): Failed to read from the stream.
            catch (IOException ioe) when (ioe.InnerException is COMException come)
            {
                // https://github.com/QL-Win/QuickLook/issues/1671
                // This error is caused by unsupported ColorContexts in Windows
                // Although can avoid color problems through `CreateOptions = BitmapCreateOptions.IgnoreColorProfile`
                // But it may cause color casts, so fallback to ImageMagick
                if (come.Message.Contains("0x88982F72"))
                {
                    var fallbackProvidor = new ImageMagickProvider(Path, Meta, ContextObject);
                    var task = fallbackProvidor.GetThumbnail(renderSize);

                    task.Start();
                    return task.Result;
                }
                ProcessHelper.WriteLog(ioe.ToString());
                return null;
            }
            catch (Exception e)
            {
                ProcessHelper.WriteLog(e.ToString());
                return null;
            }
        });
    }

    public override Task<BitmapSource> GetRenderedFrame(int index)
    {
        var fullSize = Meta.GetSize();
        var rotate = ShouldRotate(Meta.GetOrientation());

        return new Task<BitmapSource>(() =>
        {
            try
            {
                var img = new BitmapImage();
                img.BeginInit();
                img.UriSource = Path;
                img.CacheOption = BitmapCacheOption.OnLoad;
                img.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
                img.DecodePixelWidth = (int)(rotate ? fullSize.Height : fullSize.Width);
                img.DecodePixelHeight = (int)(rotate ? fullSize.Width : fullSize.Height);
                img.EndInit();

                var img2 = ApplyTransformFromExif(img, Meta.GetOrientation());

                Helper.DpiHack(img2);
                img2.Freeze();

                return img2;
            }
            // System.IO.IOException: Cannot read from the stream.
            // Inner Exception: COMException(0x88982F72): Failed to read from the stream.
            catch (IOException ioe) when (ioe.InnerException is COMException come)
            {
                // https://github.com/QL-Win/QuickLook/issues/1671
                // This error is caused by unsupported ColorContexts in Windows
                // Although can avoid color problems through `CreateOptions = BitmapCreateOptions.IgnoreColorProfile`
                // But it may cause color casts, so fallback to ImageMagick
                if (come.Message.Contains("0x88982F72"))
                {
                    var fallbackProvidor = new ImageMagickProvider(Path, Meta, ContextObject);
                    var task = fallbackProvidor.GetRenderedFrame(index);

                    task.Start();
                    return task.Result;
                }
                ProcessHelper.WriteLog(ioe.ToString());
                return null;
            }
            catch (Exception e)
            {
                ProcessHelper.WriteLog(e.ToString());
                return null;
            }
        });
    }

    private static bool ShouldRotate(Orientation orientation)
    {
        var rotate = false;
        switch (orientation)
        {
            case Orientation.LeftTop:
            case Orientation.RightTop:
            case Orientation.RightBottom:
            case Orientation.LeftBottom:
                rotate = true;
                break;
        }

        return rotate;
    }

    private static BitmapSource ApplyTransformFromExif(BitmapSource image, Orientation orientation)
    {
        switch (orientation)
        {
            case Orientation.Undefined:
            case Orientation.TopLeft:
                return image;

            case Orientation.TopRight:
                return new TransformedBitmap(image, new ScaleTransform(-1, 1, 0, 0));

            case Orientation.BottomRight:
                return new TransformedBitmap(image, new RotateTransform(180));

            case Orientation.BottomLeft:
                return new TransformedBitmap(image, new ScaleTransform(1, 1, 0, 0));

            case Orientation.LeftTop:
                return new TransformedBitmap(
                    new TransformedBitmap(image, new RotateTransform(90)),
                    new ScaleTransform(-1, 1, 0, 0));

            case Orientation.RightTop:
                return new TransformedBitmap(image, new RotateTransform(90));

            case Orientation.RightBottom:
                return new TransformedBitmap(
                    new TransformedBitmap(image, new RotateTransform(270)),
                    new ScaleTransform(-1, 1, 0, 0));

            case Orientation.LeftBottom:
                return new TransformedBitmap(image, new RotateTransform(270));
        }

        return image;
    }

    public override void Dispose()
    {
    }
}
