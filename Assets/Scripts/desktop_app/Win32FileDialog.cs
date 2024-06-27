// Copyright 2020 The Blocks Authors
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//      http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

#if UNITY_STANDALONE_WIN

using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace com.google.apps.peltzer.client.desktop_app {
  /// <summary>
  /// Wrapper to call the Win32 API's handy (but annoying) GetOpenFileName function.
  /// </summary>
  public static class Win32FileDialog {
    public enum FilterType {
      ALL_FILES,
      IMAGE_FILES
    }

    private const string ALL_FILES_FILTER = "All files (*.*)\0*.*\0\0";
    private const string IMAGE_FILES_FILTER = "Image files (*.jpg; *.png)\0*.jpg;*.png\0All files (*.*)\0*.*\0\0";

    /// <summary>
    /// Show a Win32 built-in "Open File" dialog.
    /// </summary>
    /// <param name="title">Title of the dialog box.</param>
    /// <param name="filter">The filter to use in the dialog box for selecting files. For example, use
    /// IMAGE_FILES_FILTER for JPG and PNG images.</param>
    /// <param name="selectedFilePath">(Out) the selected file path, if the user confirmed the
    /// dialog box, or null if they cancelled.</param>
    /// <returns>True if the user picked a file and confirmed the dialog box. False if the user cancelled.</returns>
    public static bool ShowWin32FileDialog(string title, FilterType filterType, out string selectedFilePath) {
      OpenFileName ofn = new OpenFileName();
      ofn.lStructSize = Marshal.SizeOf(ofn);
      if (filterType == FilterType.IMAGE_FILES) {
        ofn.lpstrFilter = IMAGE_FILES_FILTER;
      } else {
        ofn.lpstrFile = ALL_FILES_FILTER;
      }
      ofn.lpstrFile = new String(new char[512]);
      ofn.nMaxFile = ofn.lpstrFile.Length;
      ofn.lpstrFileTitle = new String(new char[512]);
      ofn.nMaxFileTitle = ofn.lpstrFileTitle.Length;
      ofn.lFlags = 0x00001000 /* OFN_FILEMUSTEXIST */ |
        0x00000800 /* OFN_PATHMUSTEXIST */ |
        0x00000008 /* OFN_NOCHANGEDIR */;
      ofn.lpstrTitle = title;
      if (GetOpenFileName(ofn)) {
        selectedFilePath = ofn.lpstrFile;
        return true;
      } else {
        selectedFilePath = null;
        return false;
      }
    }

    /*
    ORIGINAL WIN32 STRUCT:
    typedef struct tagOFN { 
      DWORD         lStructSize; 
      HWND          hwndOwner; 
      HINSTANCE     hInstance; 
      LPCTSTR       lpstrFilter; 
      LPTSTR        lpstrCustomFilter; 
      DWORD         nMaxCustFilter; 
      DWORD         nFilterIndex; 
      LPTSTR        lpstrFile; 
      DWORD         nMaxFile; 
      LPTSTR        lpstrFileTitle; 
      DWORD         nMaxFileTitle; 
      LPCTSTR       lpstrInitialDir; 
      LPCTSTR       lpstrTitle; 
      DWORD         Flags; 
      WORD          nFileOffset; 
      WORD          nFileExtension; 
      LPCTSTR       lpstrDefExt; 
      LPARAM        lCustData; 
      LPOFNHOOKPROC lpfnHook; 
      LPCTSTR       lpTemplateName; 
      void *        pvReserved;
      DWORD         dwReserved;
      DWORD         FlagsEx;
    } OPENFILENAME, *LPOPENFILENAME; 
    */
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private class OpenFileName {
      public int lStructSize;
      public IntPtr hwndOwner = IntPtr.Zero;
      public IntPtr hInstance = IntPtr.Zero;
      public string lpstrFilter = null;
      public string lpstrCustomFilter = null;
      public int nMaxCustFilter;
      public int nFilterIndex;
      public string lpstrFile = null;
      public int nMaxFile = 0;
      public string lpstrFileTitle = null;
      public int nMaxFileTitle = 0;
      public string lpstrInitialDir = null;
      public string lpstrTitle = null;
      public int lFlags;
      public ushort nFileOffset;
      public ushort nFileExtension;
      public string lpstrDefExt = null;
      public IntPtr lCustData = IntPtr.Zero;
      public IntPtr lpfHook = IntPtr.Zero;
      public int lpTemplateName;
      public IntPtr pvReserved = IntPtr.Zero;
      public int dwReserved;
      public int lFlagsEx;
    }

    [DllImport("Comdlg32.dll", CharSet = CharSet.Auto)]
    private static extern bool GetOpenFileName([In, Out] OpenFileName ofn);
  }
}

#endif  // #if UNITY_STANDALONE_WIN
